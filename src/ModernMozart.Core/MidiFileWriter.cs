using System.Buffers.Binary;

namespace ModernMozart.Core;

/// <summary>
/// Standard MIDI File (Format 1) writer. Pure BCL.
///   Track 0: tempo + 3/4 time signature
///   Track 1: right hand (channel 0)
///   Track 2: left hand  (channel 1)
/// </summary>
public static class MidiFileWriter
{
    public static byte[] Build(PerformancePlan plan)
    {
        using var ms = new MemoryStream();
        Write(plan, ms);
        return ms.ToArray();
    }

    public static void Write(PerformancePlan plan, Stream output)
    {
        byte[][] tracks =
        [
            TempoTrack(60_000_000 / plan.Bpm, plan.TotalTicks),
            NoteTrack(plan, PerformancePlan.RightHand, "Right hand"),
            NoteTrack(plan, PerformancePlan.LeftHand,  "Left hand"),
        ];

        Span<byte> header = stackalloc byte[14];
        "MThd"u8.CopyTo(header);
        BinaryPrimitives.WriteInt32BigEndian (header[4..],  6);
        BinaryPrimitives.WriteUInt16BigEndian(header[8..],  1);                       // format 1
        BinaryPrimitives.WriteUInt16BigEndian(header[10..], (ushort)tracks.Length);
        BinaryPrimitives.WriteUInt16BigEndian(header[12..], PerformancePlan.TicksPerQuarter);
        output.Write(header);

        foreach (var t in tracks)
            output.Write(t);
    }

    private static byte[] TempoTrack(int microsPerQuarter, long endTick)
    {
        var t = new List<byte>(64);
        Meta(t, 0, 0x03, "Musikalisches Würfelspiel K. 516f"u8);
        Meta(t, 0, 0x58, [0x03, 0x02, 0x18, 0x08]);                                     // 3/4
        Meta(t, 0, 0x59, [0x01, 0x00]);                                                 // G major
        Meta(t, 0, 0x51, [(byte)(microsPerQuarter >> 16), (byte)(microsPerQuarter >> 8), (byte)microsPerQuarter]);
        Meta(t, endTick, 0x2F, []);
        return Chunk(t);
    }

    private static byte[] NoteTrack(PerformancePlan plan, byte channel, string name)
    {
        var t = new List<byte>(plan.Events.Length * 2);
        Meta(t, 0, 0x03, System.Text.Encoding.UTF8.GetBytes(name));
        Vlq(t, 0); t.AddRange([(byte)(0xC0 | channel), 0x00]);              // Acoustic Grand Piano
        Vlq(t, 0); t.AddRange([(byte)(0xB0 | channel), 0x07, 100]);          // volume
        Vlq(t, 0); t.AddRange([(byte)(0xB0 | channel), 0x5B, 40]);           // reverb send

        long prev = 0;
        foreach (var e in plan.Events)
        {
            if (e.Channel != channel) continue;
            Vlq(t, e.Tick - prev);
            t.AddRange([e.Status, e.Data1, e.Data2]);
            prev = e.Tick;
        }

        Meta(t, Math.Max(0, plan.TotalTicks - prev), 0x2F, []);
        return Chunk(t);
    }

    private static void Meta(List<byte> t, long delta, byte type, ReadOnlySpan<byte> data)
    {
        Vlq(t, delta);
        t.Add(0xFF); t.Add(type);
        Vlq(t, data.Length);
        t.AddRange(data);
    }

    private static byte[] Chunk(List<byte> body)
    {
        var chunk = new byte[8 + body.Count];
        "MTrk"u8.CopyTo(chunk);
        BinaryPrimitives.WriteInt32BigEndian(chunk.AsSpan(4), body.Count);
        body.CopyTo(chunk, 8);
        return chunk;
    }

    private static void Vlq(List<byte> buf, long value)
    {
        if (value < 0) value = 0;
        Span<byte> tmp = stackalloc byte[5];
        int n = 0;
        do { tmp[n++] = (byte)(value & 0x7F); value >>= 7; } while (value > 0);
        for (int i = n - 1; i > 0; i--) buf.Add((byte)(tmp[i] | 0x80));
        buf.Add(tmp[0]);
    }
}
