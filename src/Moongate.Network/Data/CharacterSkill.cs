namespace Moongate.Network.Data;

/// <summary>A starting skill choice from character creation (0xF8): a skill id and its starting value.</summary>
public readonly record struct CharacterSkill(byte SkillId, byte Value);
