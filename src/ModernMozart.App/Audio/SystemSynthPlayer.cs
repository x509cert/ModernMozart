using Android.Media;
using ModernMozart.Core;

namespace ModernMozart.App.Audio;

/// <summary>
/// Zero-setup fallback: writes a Standard MIDI File and plays it through Android's
/// built-in Sonivox EAS General MIDI synthesizer. Works everywhere, sounds dated.
/// </summary>
public sealed class SystemSynthPlayer : IWaltzPlayer
{
    public string Name => "Android built-in synth";

    public async Task PlayAsync(PerformancePlan plan, Action<TimeSpan> onPosition, CancellationToken ct)
    {
        string path = Path.Combine(FileSystem.CacheDirectory, "performance.mid");
        await File.WriteAllBytesAsync(path, MidiFileWriter.Build(plan), ct);

        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var player = new MediaPlayer();
        player.SetAudioAttributes(new AudioAttributes.Builder()
            .SetUsage(AudioUsageKind.Media)!
            .SetContentType(AudioContentType.Music)!
            .Build()!);
        player.SetDataSource(path);
        player.Completion += (_, _) => done.TrySetResult();
        player.Error      += (_, e) => done.TrySetException(new InvalidOperationException($"MediaPlayer error {e.What}/{e.Extra}"));
        await Task.Run(player.Prepare, ct);

        using var reg = ct.Register(() => done.TrySetResult());
        player.Start();
        try
        {
            while (!done.Task.IsCompleted)
            {
                onPosition(TimeSpan.FromMilliseconds(player.CurrentPosition));
                await Task.WhenAny(done.Task, Task.Delay(33, CancellationToken.None));
            }
            await done.Task;   // surface playback errors
        }
        finally
        {
            if (player.IsPlaying) player.Stop();
            player.Release();
        }
    }
}
