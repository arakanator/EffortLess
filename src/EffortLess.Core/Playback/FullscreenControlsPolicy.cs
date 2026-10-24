using System;

namespace EffortLess.Core.Playback;

public sealed class FullscreenControlsPolicy
{
    private bool _fullscreen;
    private bool _blocked;
    private double _delay;
    private bool _hasState;
    private TimeSpan _lastActivity;
    private bool _visible = true;

    public bool Visible => _visible;

    public void Update(bool fullscreen, bool playing, bool blocked, double delay, TimeSpan now)
    {
        delay = EffortLess.Core.Library.PlayerSettings.NormalizeFullscreenControlsHideDelay(delay);
        if (!_hasState)
        {
            SetState(fullscreen, playing, blocked, delay);
            _hasState = true;
            _lastActivity = now;
            _visible = true;
            return;
        }

        // Playback/EOF/keyboard changes must never count as pointer activity.
        if (_fullscreen != fullscreen) _visible = !fullscreen;
        if (blocked && !_blocked) _visible = true;
        SetState(fullscreen, playing, blocked, delay);
    }

    public void Activity(TimeSpan now)
    {
        _lastActivity = now;
        _visible = true;
    }

    public bool TryHide(TimeSpan now)
    {
        if (_visible && IsEligible())
        {
            TimeSpan elapsed = ElapsedSinceActivity(now);
            if (elapsed >= TimeSpan.FromSeconds(_delay))
            {
                _visible = false;
            }
        }

        return !_visible;
    }

    public TimeSpan? GetRemainingDelay(TimeSpan now)
    {
        if (!IsEligible())
        {
            return null;
        }

        if (!_visible)
        {
            return null;
        }

        TimeSpan delay = TimeSpan.FromSeconds(_delay);
        TimeSpan remaining = delay - ElapsedSinceActivity(now);
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private bool IsEligible() => _fullscreen && !_blocked;

    private TimeSpan ElapsedSinceActivity(TimeSpan now)
    {
        TimeSpan elapsed = now - _lastActivity;
        return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
    }

    private void SetState(bool fullscreen, bool playing, bool blocked, double delay)
    {
        _fullscreen = fullscreen;
        _blocked = blocked;
        _delay = delay;
    }
}
