using System.Text;

namespace ModernMozart.Core;

/// <summary>
/// Reads a SoundFont 2 file's display name (the INFO/INAM bank name) from the start of the
/// file, without loading any samples. Works on forward-only streams such as Android assets.
/// </summary>
public static class SoundFontHeader
{
    /// <summary>The bank name, or null if the stream isn't a SoundFont 2 file or has no name.</summary>
    public static string? ReadName(Stream stream)
    {
        try
        {
            using var r = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
            if (Id(r) != "RIFF") return null;
            r.ReadUInt32();
            if (Id(r) != "sfbk") return null;

            // The INFO list is the first chunk of an sfbk file.
            if (Id(r) != "LIST") return null;
            long infoLeft = r.ReadUInt32();
            if (Id(r) != "INFO") return null;
            infoLeft -= 4;

            while (infoLeft >= 8)
            {
                string id = Id(r);
                int size = checked((int)r.ReadUInt32());
                int padded = size + (size & 1);
                byte[] data = r.ReadBytes(padded);
                if (data.Length < padded) return null;
                infoLeft -= 8 + padded;

                if (id == "INAM")
                {
                    string name = Encoding.ASCII.GetString(data, 0, size).TrimEnd('\0').Trim();
                    return name.Length > 0 ? name : null;
                }
            }
            return null;
        }
        catch (Exception ex) when (ex is EndOfStreamException or OverflowException or IOException)
        {
            return null;
        }
    }

    private static string Id(BinaryReader r) => Encoding.ASCII.GetString(r.ReadBytes(4));
}
