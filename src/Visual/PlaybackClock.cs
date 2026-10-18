namespace WaveFunctionCollapse.Visual;

/// <summary>Converts real frame time to fixed simulation ticks without changing the timestep.</summary>
public sealed class PlaybackClock
{
    private double accumulated;
    public double Speed { get; private set; } = 1;
    public bool Paused { get; private set; }

    public void SetPaused(bool paused) { Paused = paused; accumulated = 0; }
    public void SetSpeed(double speed)
    {
        if (!double.IsFinite(speed) || speed < 0.25 || speed > 32) throw new ArgumentOutOfRangeException(nameof(speed));
        Speed = speed;
        accumulated = 0;
    }

    public int Advance(double frameSeconds, double fixedDeltaSeconds)
    {
        if (!double.IsFinite(frameSeconds) || frameSeconds < 0) throw new ArgumentOutOfRangeException(nameof(frameSeconds));
        if (!double.IsFinite(fixedDeltaSeconds) || fixedDeltaSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(fixedDeltaSeconds));
        if (Paused) return 0;
        // Long OS stalls do not produce an unbounded catch-up loop on the UI thread.
        accumulated = Math.Min(accumulated + Math.Min(frameSeconds, 0.25) * Speed, fixedDeltaSeconds * 128);
        int ticks = Math.Min(128, (int)Math.Floor(accumulated / fixedDeltaSeconds + 1e-10));
        accumulated = Math.Max(0, accumulated - ticks * fixedDeltaSeconds);
        return ticks;
    }
}
