using Moongate.Ultima.Animation;
using Moongate.Ultima.Catalog;
using Moongate.Ultima.Data;
using Moongate.Ultima.Interfaces;

namespace Moongate.Ultima.Rendering;

/// <summary>Stateless facade over <see cref="Animations"/>.</summary>
public sealed class BodyRenderer : IBodyRenderer
{
    public Stream? GetBodyImage(int body, int action = 0, int direction = 4, int frame = 0, ushort hue = 0)
    {
        ValidateArguments(direction, frame);

        var frames = Load(body, action, direction, hue, frame == 0);

        if (frames is null || frame >= frames.Length || frames[frame]?.Bitmap is null)
        {
            return null;
        }

        return ItemCatalog.EncodePng(frames[frame].Bitmap);
    }

    public IReadOnlyList<BodyFrame> GetBodyFrames(int body, int action, int direction, ushort hue = 0)
    {
        ValidateArguments(direction, 0);

        var frames = Load(body, action, direction, hue, false);

        if (frames is null)
        {
            return [];
        }

        var result = new List<BodyFrame>(frames.Length);

        foreach (var frame in frames)
        {
            if (frame?.Bitmap is null)
            {
                continue;
            }

            result.Add(
                new BodyFrame
                {
                    Png = ItemCatalog.EncodePng(frame.Bitmap),
                    CenterX = frame.Center.X,
                    CenterY = frame.Center.Y,
                    Width = frame.Bitmap.Width,
                    Height = frame.Bitmap.Height
                }
            );
        }

        return result;
    }

    private static AnimationFrame[]? Load(int body, int action, int direction, ushort hue, bool firstFrame)
    {
        var hueValue = (int)hue;

        return Animations.GetAnimation(body, action, direction, ref hueValue, false, firstFrame);
    }

    private static void ValidateArguments(int direction, int frame)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        ArgumentOutOfRangeException.ThrowIfNegative(direction);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(direction, 7);
    }
}
