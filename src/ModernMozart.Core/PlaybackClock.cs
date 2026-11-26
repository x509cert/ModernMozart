using System.Diagnostics;

namespace ModernMozart.Core;

/// <summary>
/// Smooth playback position for animation. Audio devices report how much has been heard in
/// bursts (every buffer, often 10–40 ms and uneven); drawing those raw positions makes a
/// playhead stutter. This clock runs on the wall clock between reports and slews gently
/// toward each report, snapping only on a real discontinuity. Output never moves backwards.
/// Thread-safe: report from the audio thread, read from the UI thread.
/// </summary>
public sealed class PlaybackClock(Func<double> secondsNow)
{
    private const double SnapThreshold = 0.08;   // s — beyond this, trust the device outright
    private const double Slew          = 0.15;   // fraction of the error corrected per report

    private readonly Lock _gate = new();
    private double _anchorPos, _anchorAt, _shown;
    private bool _anchored;

    public PlaybackClock() : this(Stopwatch.GetTimestamp, Stopwatch.Frequency) { }

    private PlaybackClock(Func<long> ticks, long frequency) : this(() => ticks() / (double)frequency) { }

    public void Reset()
    {
        lock (_gate) { _anchored = false; _shown = 0; }
    }

    /// <summary>Position the device says has actually been heard.</summary>
    public void Report(TimeSpan heard)
    {
        double h = heard.TotalSeconds, t = secondsNow();
        lock (_gate)
        {
            if (!_anchored)
            {
                if (h <= 0) return;                    // device still filling its buffer
                (_anchorPos, _anchorAt, _anchored) = (h, t, true);
                return;
            }

            double predicted = _anchorPos + (t - _anchorAt);
            double error = h - predicted;
            _anchorPos = Math.Abs(error) > SnapThreshold ? h : predicted + error * Slew;
            _anchorAt  = t;
        }
    }

    /// <summary>Best estimate of what is being heard right now.</summary>
    public TimeSpan Now()
    {
        lock (_gate)
        {
            if (!_anchored) return TimeSpan.Zero;
            _shown = Math.Max(_shown, _anchorPos + (secondsNow() - _anchorAt));
            return TimeSpan.FromSeconds(_shown);
        }
    }
}
