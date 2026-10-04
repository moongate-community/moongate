namespace Moongate.Server.Ultima.Types.Mobiles;

/// <summary>
///     The actions of a monster body, by their number in the client's animation files (UOFiddler's names): what <c>mobile.animate</c> plays on a skeleton, an orc or a dragon.
/// </summary>
public enum MonsterAnimationType
{
    Walk = 0,
    Stand = 1,
    Die1 = 2,
    Die2 = 3,
    Attack1 = 4,
    Attack2 = 5,
    Attack3 = 6,
    AttackBow = 7,
    AttackCrossbow = 8,
    AttackThrow = 9,
    GetHit = 10,
    Pillage = 11,
    Stomp = 12,
    Cast2 = 13,
    Cast3 = 14,
    BlockRight = 15,
    BlockLeft = 16,
    Fidget1 = 17,
    Fidget2 = 18,
    Fly = 19,
    TakeOff = 20,
    GetHitInAir = 21
}
