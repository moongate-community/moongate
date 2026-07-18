using Moongate.Ultima.Imaging;

namespace Moongate.Http.Plugin.Services.Mobiles;

/// <summary>
/// Saves a bitmap PNG to a temporary sibling and moves it into place: a reader must never see a
/// half-written file, and a same-directory move is atomic on Linux and Windows alike. The temporary
/// name keeps the .png suffix because <see cref="UltimaBitmap.Save" /> picks the encoder from it.
/// </summary>
internal static class PngWriter
{
    public static void WriteAtomically(string path, UltimaBitmap bitmap)
    {
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp.png";

        try
        {
            bitmap.Save(temporary, false);
            File.Move(temporary, path, true);
        }
        catch
        {
            File.Delete(temporary);

            throw;
        }
    }
}
