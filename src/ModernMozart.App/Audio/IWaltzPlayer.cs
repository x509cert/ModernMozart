using ModernMozart.Core;

namespace ModernMozart.App.Audio;

/// <summary>A way of turning a <see cref="PerformancePlan"/> into sound on the device.</summary>
public interface IWaltzPlayer
{
    /// <summary>Shown in the UI, e.g. "SoundFont · Salamander Grand".</summary>
    string Name { get; }

    /// <summary>
    /// Plays the performance to the end or until cancelled. <paramref name="onPosition"/> is
    /// called from a background thread with the time actually heard (not merely rendered).
    /// Completes normally on cancellation.
    /// </summary>
    Task PlayAsync(PerformancePlan plan, Action<TimeSpan> onPosition, CancellationToken ct);
}
