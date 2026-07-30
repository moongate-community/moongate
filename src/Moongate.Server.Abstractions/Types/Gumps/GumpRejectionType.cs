namespace Moongate.Server.Abstractions.Types.Gumps;

/// <summary>Why a gump response was refused, or <see cref="None" /> when it was accepted.</summary>
public enum GumpRejectionType : byte
{
    /// <summary>The response matched what the server drew.</summary>
    None = 0,

    /// <summary>No gump with that serial and type is open for this session.</summary>
    NotOpen,

    /// <summary>The button id was never drawn, and is not the client's own close button.</summary>
    UnknownButton,

    /// <summary>A ticked switch id was never drawn.</summary>
    UnknownSwitch,

    /// <summary>A returned text entry id was never drawn.</summary>
    UnknownTextEntry,

    /// <summary>A text entry exceeded the 239 characters the client's own field allows.</summary>
    TextTooLong
}
