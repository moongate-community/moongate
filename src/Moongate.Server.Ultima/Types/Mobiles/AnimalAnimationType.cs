namespace Moongate.Server.Ultima.Types.Mobiles;

/// <summary>
///     The actions of an animal body, by their number in the client's animation files (UOFiddler's names): what <c>mobile.animate</c> plays on a cat, a horse or a bird.
/// </summary>
public enum AnimalAnimationType
{
    Walk = 0,
    Run = 1,
    Stand = 2,
    Eat = 3,
    Alert = 4,
    Attack1 = 5,
    Attack2 = 6,
    GetHit = 7,
    Die1 = 8,
    Fidget1 = 9,
    Fidget2 = 10,
    LieDown = 11,
    Die2 = 12
}
