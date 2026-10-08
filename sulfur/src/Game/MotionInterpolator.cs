using System;
using System.Collections.Generic;
using SulfurCraft.Link;

namespace SulfurCraft.Game
{
    internal struct MotionPose
    {
        public double X, Y, Z, Eye, BobPhase, BobAmount;
    }

    // Port of SkyCraft's host tick history: a small adaptive delay absorbs tick arrival jitter.
    internal sealed class MotionInterpolator
    {
        private struct Tick { public McState State; public long At; public int Slots; }
        private readonly List<Tick> history = new List<Tick>(8);
        private readonly double[] due = new double[40];
        private readonly double qpcPerMs;
        private long lastTick, lastFrame;
        private int outliers, dueIndex;
        private bool dueInitialized;
        private double delay = 10;
        public double DelayMs => delay;

        public MotionInterpolator(long frequency) { qpcPerMs = frequency / 1000.0; }

        public void Reset()
        {
            history.Clear(); lastTick = lastFrame = 0; outliers = dueIndex = 0; delay = 10;
            dueInitialized = false;
            Array.Clear(due, 0, due.Length);
        }

        public MotionPose Sample(McState state, long now)
        {
            double tickMs = state.TickMs > 0 && !float.IsInfinity(state.TickMs) ? state.TickMs : 50;
            long period = Math.Max(1, (long)Math.Round(tickMs * qpcPerMs));
            if (state.TickQpc != lastTick || history.Count == 0)
            {
                if (state.TickQpc < lastTick) Reset();
                var tick = new Tick { State = state, At = state.TickQpc, Slots = 1 };
                if (history.Count > 0)
                {
                    int index = history.Count - 1;
                    Tick previous = history[index];
                    int slots = (int)Math.Round((tick.At - previous.At) / (double)period);
                    long error = tick.At - (previous.At + slots * period);
                    if (slots == 0 && previous.Slots >= 2)
                    {
                        previous.At -= period; previous.Slots--; history[index] = previous;
                        tick.At = previous.At + period;
                    }
                    else if (slots >= 1 && slots <= 10 && Math.Abs(error) < period * .3)
                    {
                        tick.At = previous.At + slots * period + error / 16;
                        tick.Slots = slots; outliers = 0;
                    }
                    else if (slots <= 10 && ++outliers < 3)
                    {
                        tick.Slots = Math.Max(slots, 1); tick.At = previous.At + tick.Slots * period;
                    }
                    else outliers = 0;
                }
                if (lastFrame != 0)
                {
                    if (!dueInitialized) { for (int i = 0; i < due.Length; i++) due[i] = delay - 1; dueInitialized = true; }
                    double lateness = (lastFrame - tick.At) / qpcPerMs;
                    if (lateness < 30) due[dueIndex++ % due.Length] = lateness;
                }
                if (history.Count == 8) history.RemoveAt(0);
                history.Add(tick); lastTick = state.TickQpc;
            }
            double frameMs = lastFrame == 0 ? 0 : Math.Max(0, (now - lastFrame) / qpcPerMs);
            lastFrame = now;
            double latest = double.MinValue;
            foreach (double value in due) latest = Math.Max(latest, value);
            double target = Math.Max(4, Math.Min(30, latest + 1)), dt = Math.Min(frameMs, 100) / 1000;
            if (dueInitialized) delay = target > delay ? Math.Min(target, delay + 20 * dt) : Math.Max(target, delay - 2 * dt);
            long renderAt = now - (long)(delay * qpcPerMs);
            int selected = 0;
            for (int i = history.Count - 1; i >= 0; i--) if (history[i].At <= renderAt) { selected = i; break; }
            Tick current = history[selected]; McState s = current.State;
            double partial = Clamp((renderAt - current.At) / (double)period);
            var pose = new MotionPose
            {
                X = Lerp(s.PrevX, s.CurX, partial), Y = Lerp(s.PrevY, s.CurY, partial), Z = Lerp(s.PrevZ, s.CurZ, partial),
                Eye = Lerp(s.PreviousEye, s.CurrentEye, partial),
                BobPhase = -(s.CurrentWalk + (s.CurrentWalk - s.PreviousWalk) * partial),
                BobAmount = Lerp(s.PreviousBob, s.CurrentBob, partial)
            };
            if (renderAt > current.At + period && selected + 1 < history.Count)
            {
                Tick next = history[selected + 1]; long gap = next.At - (current.At + period);
                double blend = gap > 0 ? Clamp((renderAt - (current.At + period)) / (double)gap) : 1;
                pose.X = Lerp(s.CurX, next.State.PrevX, blend); pose.Y = Lerp(s.CurY, next.State.PrevY, blend); pose.Z = Lerp(s.CurZ, next.State.PrevZ, blend);
                pose.Eye = Lerp(s.CurrentEye, next.State.PreviousEye, blend);
                pose.BobPhase = Lerp(-(s.CurrentWalk * 2 - s.PreviousWalk), -next.State.CurrentWalk, blend);
                pose.BobAmount = Lerp(s.CurrentBob, next.State.PreviousBob, blend);
            }
            return pose;
        }
        private static double Clamp(double value) => Math.Max(0, Math.Min(1, value));
        private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    }
}
