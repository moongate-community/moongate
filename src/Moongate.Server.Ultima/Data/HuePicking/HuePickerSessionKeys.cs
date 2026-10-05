using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Internal.HuePicking;

namespace Moongate.Server.Ultima.Data.HuePicking;

/// <summary>
///     The session values of the hue picker.
/// </summary>
public static class HuePickerSessionKeys
{
    public static readonly SessionKey<HuePickerState?> State = new("HuePickerState");
}
