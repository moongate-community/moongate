using Moongate.Ultima.Data;

namespace Moongate.Ultima.Interfaces;

/// <summary>
/// Renders body animation frames from the client animation files. Requires the client
/// directory to be set via <c>Files.SetDirectory</c> before use.
/// </summary>
public interface IBodyRenderer
{
    /// <summary>One PNG-encoded animation frame, or null when body/action/frame is missing.</summary>
    Stream? GetBodyImage(int body, int action = 0, int direction = 4, int frame = 0, ushort hue = 0);

    /// <summary>Every frame of an action/direction as PNG, empty when the animation is missing.</summary>
    IReadOnlyList<BodyFrame> GetBodyFrames(int body, int action, int direction, ushort hue = 0);
}
