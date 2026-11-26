using MeltySynth;

namespace ModernMozart.Core;

/// <summary>
/// Renders a <see cref="PerformancePlan"/> to PCM through a SoundFont synthesizer.
/// Events are dispatched at exact sample offsets (the render call is split at each
/// event), so timing is independent of the audio sink's buffer size.
/// Platform-neutral: the caller owns the audio device.
/// </summary>
public sealed class WaltzRenderer
{
    private static readonly TimeSpan ReleaseTail = TimeSpan.FromSeconds(2.5);

    private readonly Synthesizer     _synth;
    private readonly PerformancePlan _plan;
    private readonly double          _samplesPerTick;
    private int  _nextEvent;
    private long _frame;

    /// <param name="masterVolume">
    /// MeltySynth defaults to 0.5, which is quiet on a phone speaker. Output passes
    /// through a soft-knee limiter, so a hotter setting cannot hard-clip.
    /// </param>
    public WaltzRenderer(Synthesizer synth, PerformancePlan plan, float masterVolume = 1.0f)
    {
        _synth          = synth;
        _plan           = plan;
        _samplesPerTick = plan.SecondsPerTick * synth.SampleRate;
        TotalFrames     = (long)Math.Ceiling((plan.Length + ReleaseTail).TotalSeconds * synth.SampleRate);

        _synth.Reset();
        _synth.MasterVolume = masterVolume;
        foreach (byte ch in (byte[])[PerformancePlan.RightHand, PerformancePlan.LeftHand])
        {
            _synth.ProcessMidiMessage(ch, 0xC0, 0, 0);     // Acoustic Grand Piano
            _synth.ProcessMidiMessage(ch, 0xB0, 7, 100);   // channel volume
            _synth.ProcessMidiMessage(ch, 0xB0, 91, 40);   // reverb send — a small hall
            _synth.ProcessMidiMessage(ch, 0xB0, 93, 0);    // no chorus on piano
        }
    }

    public int SampleRate => _synth.SampleRate;
    public long TotalFrames { get; }
    public long FramesRendered => _frame;
    public bool IsFinished => _frame >= TotalFrames;

    /// <summary>
    /// Fills <paramref name="interleaved"/> (L,R,L,R…) and returns the number of frames written.
    /// Returns 0 once the performance (plus release tail) is complete.
    /// </summary>
    public int Render(Span<float> interleaved)
    {
        int framesWanted = (int)Math.Min(interleaved.Length / 2, TotalFrames - _frame);
        int done = 0;

        while (done < framesWanted)
        {
            DispatchDueEvents();

            long nextAt = _nextEvent < _plan.Events.Length
                ? FrameOf(_plan.Events[_nextEvent].Tick)
                : long.MaxValue;

            int chunk = (int)Math.Min(framesWanted - done, Math.Max(1, nextAt - _frame));
            _synth.RenderInterleaved(interleaved.Slice(done * 2, chunk * 2));
            done   += chunk;
            _frame += chunk;
        }

        Limit(interleaved[..(done * 2)]);
        return done;
    }

    /// <summary>Transparent below ±0.9; above that, a tanh knee approaching ±1.0.</summary>
    private static void Limit(Span<float> samples)
    {
        const float knee = 0.9f, range = 1.0f - knee;
        foreach (ref float s in samples)
        {
            float a = MathF.Abs(s);
            if (a > knee)
                s = MathF.CopySign(knee + range * MathF.Tanh((a - knee) / range), s);
        }
    }

    /// <summary>Silence everything immediately (used when playback is stopped early).</summary>
    public void Stop() => _synth.NoteOffAll(immediate: true);

    private void DispatchDueEvents()
    {
        var events = _plan.Events;
        while (_nextEvent < events.Length && FrameOf(events[_nextEvent].Tick) <= _frame)
        {
            var e = events[_nextEvent++];
            _synth.ProcessMidiMessage(e.Channel, e.Command, e.Data1, e.Data2);
        }
    }

    private long FrameOf(long tick) => (long)Math.Round(tick * _samplesPerTick);
}
