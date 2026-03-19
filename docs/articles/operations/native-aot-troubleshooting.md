# NativeAOT Troubleshooting

Operational runbook for common NativeAOT issues in Moongate v2.

## Build and Run Commands

Build AOT (project script):

```bash
./scripts/run_aot.sh --root-directory ~/moongate --uo-directory ~/uo
```

Run stress smoke:

```bash
dotnet run --project tools/Moongate.Stress -- --clients 10 --duration 15
```

## Known Failure Patterns

## 1) Reflection/DI incompatible with AOT

Typical symptom:

- `MethodInfo.MakeGenericMethod() is not compatible with AOT`
- DryIoc stack trace during dynamic conversion or resolution

Common causes:

- runtime generic reflection-based resolution
- ambiguous or non-explicit DI registrations
- code that uses non-source-generated dynamic factory selection

Checklist:

1. avoid runtime reflection in bootstrap paths
2. prefer source generation already present in the project
3. use explicit DI registrations and unambiguous constructors
4. verify that service collections are not built dynamically

## 2) Serializer incompatible with AOT

Typical symptom:

- crash or deserialization failure during snapshot load
- regressions in AOT publish after changes to entities or snapshots

Project decision:

- use `MessagePack-CSharp` with source generation for snapshot and journal data
- avoid serializers that require runtime metadata that is not preserved

Checklist:

1. after changing snapshot entities, regenerate and rebuild
2. test startup with an existing snapshot
3. validate serialize/deserialize roundtrips in tests

## 3) Crash immediately after login in the AOT binary

Typical symptom:

- crash during handshake/login or immediately after `AccountLoginPacket`

Quick checklist:

1. run with `Debug` logging
2. verify parser and network session state
3. verify persistence serializer initialization (`LoadAsync`)
4. try a clean snapshot to isolate data corruption

## 4) Linker/trimmer errors

If you use external libraries with reflection:

1. add descriptors to `ILLink.Descriptors.xml`
2. preserve the required types
3. rerun a full AOT publish

## Pre-flight AOT Checklist

Before declaring AOT stable:

1. `dotnet test`
2. local AOT publish
3. start the AOT server with real data
4. log in with a real client
5. smoke-test critical paths:
   - movement
   - item double click script
   - gump callbacks
   - command execution
   - persistence save/load

## Practical Debug Tips

- use the full stack trace, not just the first frame
- always compare JIT and AOT runs on the same dataset
- when the bug is intermittent, reduce the workload (1 client, no stress)
- when the bug is deterministic, create a minimal test before the fix

## References

- [Installation](../getting-started/installation.md)
- [Stress Test](stress-test.md)
- [.NET NativeAOT compatibility guidance](https://aka.ms/nativeaot-compatibility)
