import assert from 'node:assert/strict';
import test from 'node:test';
import { mergePackets } from '../scripts/build-packets.mjs';

const field = { offset: '0', name: 'Opcode', type: 'byte', size: '1', description: 'Packet identifier.' };
const override = (category = 'Login') => ({ category, tags: [category], fields: [field], notes: 'Wire format note.', handler: 'Handled by the login host.' });
const packet = (id, name, direction, sizing = 'fixed') => ({
  id, name, direction, sizing, fixedLength: sizing === 'fixed' ? 3 : null,
  minimumLength: 3, description: '', source: `src/${name}.cs`,
});

test('merge orders hexadecimal opcodes and subcommands, preserving separate directions', () => {
  const dump = [
    packet('0xBF/0x08', 'MapChangePacket', 'outgoing', 'variable'),
    packet('0xBD', 'ClientVersionRequestPacket', 'outgoing'),
    packet('0x02', 'MoveRequestPacket', 'incoming'),
    packet('0xBD', 'ClientVersionPacket', 'incoming', 'variable'),
    packet('0x0A', 'TenPacket', 'both'),
  ];
  const overrides = Object.fromEntries(dump.map(item => [`${item.id}:${item.direction}`, override()]));
  const result = mergePackets(dump, overrides);
  assert.deepEqual(result.map(item => `${item.id}:${item.direction}`), [
    '0x02:incoming', '0x0A:both', '0xBD:incoming', '0xBD:outgoing', '0xBF/0x08:outgoing',
  ]);
  assert.deepEqual(result[0].fields, [field]);
  assert.deepEqual(result[2].size, { kind: 'dynamic', minimum: 3 });
  assert.deepEqual(result[3].size, { kind: 'fixed', bytes: 3 });
});

test('merge rejects missing and unknown overrides', () => {
  const dump = [packet('0x02', 'MoveRequestPacket', 'incoming')];
  assert.throws(() => mergePackets(dump, {}), /Missing override.*0x02:incoming/);
  assert.throws(() => mergePackets(dump, {
    '0x02:incoming': override(), '0x03:incoming': override(),
  }), /Unknown override.*0x03:incoming/);
});

test('merge rejects conflicting packet identities and invalid override fields', () => {
  const item = packet('0x02', 'MoveRequestPacket', 'incoming');
  assert.throws(() => mergePackets([item, { ...item, name: 'OtherPacket' }], {
    '0x02:incoming': override(),
  }), /Conflicting packet.*0x02:incoming/);
  assert.throws(() => mergePackets([item], {
    '0x02:incoming': { ...override(), fields: [{ ...field, type: 'mystery' }] },
  }), /Invalid field type.*mystery/);
});

test('merge rejects overlapping fields and fields outside a fixed frame', () => {
  const item = packet('0x02', 'MoveRequestPacket', 'incoming');
  assert.throws(() => mergePackets([item], {
    '0x02:incoming': { ...override(), fields: [field, { ...field, offset: '0', name: 'Duplicate byte' }] },
  }), /Overlapping fields.*0x02:incoming/);
  assert.throws(() => mergePackets([item], {
    '0x02:incoming': { ...override(), fields: [field, { ...field, offset: '3', name: 'Beyond frame' }] },
  }), /Field outside fixed frame.*0x02:incoming/);
});
