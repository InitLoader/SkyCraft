using System;
using SulfurCraft.Link;

namespace SulfurCraft.Game
{
    internal struct MotionPose
    {
        public double X, Y, Z, Eye, BobPhase, BobAmount;
    }

    // Continue Minecraft's render phase within its already collision-resolved tick.
    internal sealed class MotionInterpolator
    {
        private readonly double qpcPerMs;
        public MotionInterpolator(long frequency) { qpcPerMs = frequency / 1000.0; }

        public MotionPose Sample(McState state, long now)
        {
            double tickMs = state.TickMs > 0 && !float.IsInfinity(state.TickMs) ? state.TickMs : 50;
            double partial = state.FrameQpc > 0 && !float.IsNaN(state.FramePartial) && !float.IsInfinity(state.FramePartial)
                ? state.FramePartial + Math.Max(0, now - state.FrameQpc) / (tickMs * qpcPerMs)
                : (now - state.TickQpc) / (tickMs * qpcPerMs);
            partial = Math.Max(0, Math.Min(1, partial));
            return new MotionPose
            {
                X = Lerp(state.PrevX, state.CurX, partial), Y = Lerp(state.PrevY, state.CurY, partial), Z = Lerp(state.PrevZ, state.CurZ, partial),
                Eye = Lerp(state.PreviousEye, state.CurrentEye, partial),
                BobPhase = -(state.CurrentWalk + (state.CurrentWalk - state.PreviousWalk) * partial),
                BobAmount = Lerp(state.PreviousBob, state.CurrentBob, partial)
            };
        }
        private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    }
}
