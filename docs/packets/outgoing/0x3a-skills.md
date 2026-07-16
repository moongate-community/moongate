# 0x3A — Skills

<span class="mg-dir mg-dir-out">Server → Client</span>

Skill list (0x3A), in the absolute-with-caps form (type 0x02): the client's whole skill list in one go. Variable length: `6 + 9*Skills.Count`. Skills the mobile never trained are still sent, at zero, otherwise they go missing from the client's list.

- **Class:** [`SkillsPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/SkillsPacket.cs)
- **Size:** Variable

## Fields

| Field | Type |
|---|---|
| `Skills` | `IReadOnlyList<SkillEntry>` |
