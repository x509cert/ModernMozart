using Android.Media;
using MeltySynth;
using ModernMozart.Core;
using AudioEncoding = Android.Media.Encoding;

namespace ModernMozart.App.Audio;

/// <summary>
/// Highest-quality path: MeltySynth renders the piano from a SoundFont on a dedicated
/// thread and streams 32-bit float PCM to an <see cref="AudioTrack"/>.
/// UI position comes from the track's playback head, so the highlighted bar matches
/// what is coming out of the speaker, not what has merely been rendered.
/// </summary>
public sealed class SoundFontPlayer(SoundFont soundFont, string soundFontName) : IWaltzPlayer
{
    private const int SampleRate   = 44_100;
    private const int BlockFrames  = 1_024;     // ~23 ms per write
    private const float MasterGain = 1.5f;      // renderer's soft limiter catches peaks

    public string Name => $"SoundFont · {soundFontName}";

    public Task PlayAsync(PerformancePlan plan, Action<TimeSpan> onPosition, CancellationToken ct)
        => Task.Factory.StartNew(
            () => Run(plan, onPosition, ct),
            ct, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    private void Run(PerformancePlan plan, Action<TimeSpan> onPosition, CancellationToken ct)
    {
        Android.OS.Process.SetThreadPriority(Android.OS.ThreadPriority.Audio);

        var settings = new SynthesizerSettings(SampleRate) { EnableReverbAndChorus = true };
        var renderer = new WaltzRenderer(new Synthesizer(soundFont, settings), plan, MasterGain);

        int minBytes  = AudioTrack.GetMinBufferSize(SampleRate, ChannelOut.Stereo, AudioEncoding.PcmFloat);
        int bufBytes  = Math.Max(minBytes, BlockFrames * 2 * sizeof(float)) * 4;

        using var track = new AudioTrack.Builder()
            .SetAudioAttributes(new AudioAttributes.Builder()
                .SetUsage(AudioUsageKind.Media)!
                .SetContentType(AudioContentType.Music)!
                .Build()!)!
            .SetAudioFormat(new AudioFormat.Builder()
                .SetEncoding(AudioEncoding.PcmFloat)!
                .SetSampleRate(SampleRate)!
                .SetChannelMask(ChannelOut.Stereo)!
                .Build()!)!
            .SetBufferSizeInBytes(bufBytes)!
            .SetTransferMode(AudioTrackMode.Stream)!
            .Build()!;

        var block = new float[BlockFrames * 2];
        long framesWritten = 0;

        try
        {
            track.Play();

            // Stream until the renderer (performance + release tail) is exhausted.
            while (!ct.IsCancellationRequested)
            {
                int frames = renderer.Render(block);
                if (frames == 0) break;

                int written = track.Write(block, 0, frames * 2, WriteMode.Blocking);
                if (written < 0) throw new InvalidOperationException($"AudioTrack.Write failed ({written}).");
                framesWritten += written / 2;

                onPosition(Heard(track));
            }

            // Let the device buffer drain so the final chord isn't cut off.
            while (!ct.IsCancellationRequested && track.PlaybackHeadPosition < framesWritten)
            {
                onPosition(Heard(track));
                Thread.Sleep(20);
            }
        }
        finally
        {
            if (ct.IsCancellationRequested)
            {
                track.Pause();
                track.Flush();
            }
            track.Stop();
            track.Release();
        }
    }

    private static TimeSpan Heard(AudioTrack track)
        => TimeSpan.FromSeconds((double)(uint)track.PlaybackHeadPosition / SampleRate);
}
