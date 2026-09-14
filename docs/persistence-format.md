# Moongate binary persistence format, version 1

Each collection owns `<name>.snapshot.bin`, `<name>.journal.bin`, and a separately
held `<name>.lock` file in its configured directory. Collection names contain
1–64 lowercase ASCII letters, digits, `_`, or `-`. The lock file is opened with
exclusive sharing and remains open across checkpoint publication. Its contents
are unused. Collection IDs are nonzero UInt32 `Serial.Value` values supplied by
the caller; the engine never allocates IDs.

All integer metadata is unsigned and little-endian. Payloads are opaque bytes to
the raw engine; the typed facade supplies MemoryPack bytes. No padding or record
alignment is added. Reserved bytes must be zero. Unsupported versions and invalid
committed data raise an exception naming the affected path.

## Checksums

Checksums use CRC-32/ISO-HDLC (the IEEE CRC-32 used by ZIP): reflected polynomial
`0xEDB88320`, initial register `0xFFFFFFFF`, final XOR `0xFFFFFFFF`. The checksum
of ASCII `123456789` is `0xCBF43926`; the empty payload checksum is zero. Stored
checksum integers are little-endian. Checksums detect accidental corruption;
they are not authentication.

## File header: 100 bytes

Both committed files begin with this fixed header.

| Offset | Size | Field |
| --- | --- | --- |
| 0 | 8 | ASCII `MGSTORE1` |
| 8 | 2 | Format version, currently `1` |
| 10 | 1 | Kind: `1` snapshot, `2` journal |
| 11 | 1 | Reserved, zero |
| 12 | 4 | Collection name length, 1–64 |
| 16 | 8 | Snapshot sequence or journal base sequence |
| 24 | 8 | Snapshot entity count; zero for a journal |
| 32 | 64 | ASCII collection name, then zero bytes to fill 64 bytes |
| 96 | 4 | CRC of exactly bytes 0–95 |

The entire header and its CRC are checked before using its metadata. The name
must match the configured collection exactly, including zero padding. A snapshot
at sequence zero is initially empty. A journal base cannot exceed its paired
snapshot sequence.

For a journal named `items` at base `0x0807060504030201` with zero count, the file
header CRC is `0xABB65675`.

## Record header: 32 bytes, followed by the payload

Snapshots and journals share record framing.

| Offset | Size | Field |
| --- | --- | --- |
| 0 | 4 | ASCII `MGR1` |
| 4 | 1 | Operation: `1` upsert, `2` delete |
| 5 | 3 | Reserved, zero |
| 8 | 8 | Sequence |
| 16 | 4 | Nonzero entity `Serial.Value` |
| 20 | 4 | Payload length N |
| 24 | 4 | CRC of exactly the N payload bytes |
| 28 | 4 | CRC of exactly record-header bytes 0–27 |
| 32 | N | Payload bytes |

The header CRC is verified before trusting N. Upserts require `1 <= N <=
MaxPayloadBytes`; deletes require N = 0 and an empty-payload CRC of zero. The
default payload limit is 16 MiB. Configuration accepts positive limits up to
.NET's `Array.MaxLength`. Readers check that bytes remain before allocating the
payload. Unknown operations, zero IDs, nonzero reserved bytes, and invalid lengths
fail recovery even when the payload would extend beyond EOF.

An upsert at sequence 1 for ID `0x12345678`, payload `04 02`, has payload CRC
`0xCBBBB6D7` and record-header CRC `0x27172C3E`.

## Snapshot body

The header's entity count is followed by exactly that many upsert records. Every
record has the snapshot sequence S. IDs must be distinct. Record order is
unspecified. The reader bounds the count against the remaining file size before
iterating. A truncated record, incorrect record sequence, duplicate ID, delete
record, checksum error, or trailing bytes invalidates the snapshot. Snapshots
never receive tail repair.

## Journal body and recovery

Records begin at offset 100. The first complete record has sequence `base + 1`;
every later complete record advances by exactly one. Overflow, duplicate,
regressing, and gapped sequences fail recovery. After reading the snapshot at S,
the engine validates every journal record, including the prefix at or below S,
and applies only records above S. An old journal must reach S; a newly compacted
journal can have base S and no records. These rules accept the old journal after
snapshot publication and the new journal after journal publication.

An EOF strictly inside the final 32-byte record header or its validated payload
is an incomplete final frame. Recovery retains the complete prefix and truncates
the file to that prefix, flushing the repair while holding the collection lock.
A warning identifies the file and truncation offset. A complete invalid header
or payload CRC is corruption, not a recoverable tail. An incomplete file header
is always corruption. Uncommitted temporary files are ignored.

If neither committed file exists, initialization creates an empty pair. If
exactly one exists, initialization fails, including at sequence zero. This also
reports an interrupted first initialization instead of assuming missing history
was empty. Initialization errors release all handles.

## Mutation, cancellation, and checkpoint protocol

The store copies and validates each upsert payload before its first asynchronous
wait. One collection gate serializes commits, consistent reads, captures, and
checkpoints. Once append starts, a mutation writes the complete record and calls
`FileStream.Flush(flushToDisk: true)` without cancellation. Only successful flush
publishes the new committed buffer and advances the sequence. Cancellation is
observed before append and while waiting for the gate. Overflow is rejected before
I/O. An absent delete writes nothing. Returned raw buffers remain unchanged by
later commits and are internal immutable views; callers must not mutate them.

The default automatic checkpoint threshold is 64 MiB of journal file length,
including its header. Once reached, checkpointing runs before the next actual
mutation. A checkpoint failure cannot turn an already acknowledged write into a
reported failed write. Checkpointing writes a unique temporary snapshot in the
same directory, flushes and closes it, then atomically replaces the snapshot. It
then writes and flushes a new temporary journal header based at S, closes the old
journal, replaces it, and opens the new journal for append. The separate lock
stays held throughout. Closing the journal before replacement also avoids relying
on Windows allowing replacement of an open file.

Any uncertain mutation or checkpoint I/O outcome faults the store. Further reads,
queries, mutations, and checkpoints reject until a new store instance recovers
from disk. A failed append may still have reached disk; recovery may include it.
Faulted or aborted disposal releases handles without checkpointing. Healthy
disposal rejects new work immediately, drains in-flight operations, checkpoints,
and releases handles even if checkpointing fails. Queued work that has not begun
its operation rejects after shutdown starts. Dispose failure is propagated;
repeated disposal observes the same completion or failure.

Version 1 requests durable file flushes and same-filesystem atomic replacement.
The integration suite exercises the implementation on Linux. It does not claim
hardware power-loss guarantees or durable directory-entry publication across all
filesystems; directory metadata is not explicitly fsynced. This is a recovery
journal that may be compacted, not a permanent audit trail or backup system.
