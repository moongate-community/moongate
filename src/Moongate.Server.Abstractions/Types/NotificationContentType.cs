namespace Moongate.Server.Abstractions.Types;

/// <summary>How a rendered notification body should be interpreted by the channel delivering it.</summary>
public enum NotificationContentType
{
    /// <summary>Plain text. The default, and what every channel can handle.</summary>
    Text,

    /// <summary>An HTML document fragment. Channels that cannot render it should fall back to sending it as text.</summary>
    Html
}
