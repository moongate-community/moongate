namespace Moongate.Server.Ultima.Types.Mobiles;

/// <summary>
///     The actions of a human body, by their number in the client's animation files (UOFiddler's names): what <c>mobile.animate</c> plays on a player or a human NPC.
/// </summary>
public enum HumanAnimationType
{
    Walk = 0,
    WalkArmed = 1,
    Run = 2,
    RunArmed = 3,
    Stand = 4,
    Fidget1 = 5,
    Fidget2 = 6,
    CombatStand1H = 7,
    CombatStand2H = 8,
    AttackSlash1H = 9,
    AttackPierce1H = 10,
    AttackBash1H = 11,
    AttackBash2H = 12,
    AttackSlash2H = 13,
    AttackPierce2H = 14,
    CombatAdvance = 15,
    Spell1 = 16,
    Spell2 = 17,
    AttackBow = 18,
    AttackCrossbow = 19,
    GetHit = 20,
    DieForward = 21,
    DieBack = 22,
    MountedWalk = 23,
    MountedRun = 24,
    MountedStand = 25,
    MountedAttack1H = 26,
    MountedAttackBow = 27,
    MountedAttackCrossbow = 28,
    MountedAttack2H = 29,
    BlockShield = 30,
    Punch = 31,
    Bow = 32,
    Salute = 33,
    Eat = 34
}
