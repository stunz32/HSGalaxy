using System;
using System.Drawing;
using System.Threading;
using HSGalaxy.Diagnostics;

namespace HSGalaxy.UI.Validation
{
    /// <summary>
    /// Simple state machine to gate overlay depending on mirror-view validation.
    /// Attempts validation with exponential backoff until success or explicit failure.
    /// </summary>
    public sealed class MirrorViewGate
    {
        public MirrorViewState State { get; private set; } = MirrorViewState.NotStarted;
        private int _attempt;
        private DateTime _nextAttemptAt = DateTime.MinValue;

        /// <summary>
        /// Run a validation tick if the backoff has elapsed. Uses the provided capture lambda.
        /// Returns true if state changed.
        /// </summary>
        public bool Tick(Func<Rectangle> getRect, Func<Rectangle, System.Drawing.Bitmap> capture, System.Drawing.Color expectedOverlayColor)
        {
            var now = DateTime.UtcNow;
            if (now < _nextAttemptAt) return false;
            if (State is MirrorViewState.Passed or MirrorViewState.Failed) return false;

            State = MirrorViewState.Validating;
            bool ok = false;
            try
            {
                var rect = getRect();
                using var bmp = capture(rect);
                // Invert semantics: PASS = no overlay pixels found for 3 frames.
                ok = MirrorViewValidator.ValidateNoOverlayInCapture(rect, expectedOverlayColor, requiredCleanFrames: 3, sampleStep: 6);
            }
            catch (Exception ex)
            {
                OverlayLogger.Log("MirrorView.Tick.Error", ex.Message);
                ok = false;
            }

            if (ok)
            {
                State = MirrorViewState.Passed;
                OverlayLogger.Log("MirrorView.State", "Passed");
                return true;
            }

            // Schedule retry with exponential backoff, capped at 2s
            _attempt++;
            int delayMs = Math.Min(2000, 100 * (1 << Math.Min(5, _attempt))); // 100,200,400,800,1600,2000...
            _nextAttemptAt = now.AddMilliseconds(delayMs);
            OverlayLogger.Log("MirrorView.Retry", $"Attempt={_attempt}; NextDelayMs={delayMs}");
            return true;
        }
    }

    public enum MirrorViewState
    {
        NotStarted,
        Validating,
        Passed,
        Failed
    }
}

