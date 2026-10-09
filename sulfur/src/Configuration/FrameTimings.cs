using System.Diagnostics;

namespace SulfurCraft.Configuration
{
    internal sealed class FrameTimings
    {
        private readonly double[] totals = new double[5];
        private readonly double[] maximum = new double[5];
        private long started;
        private int frames;
        private long lastReport = Stopwatch.GetTimestamp();
        public void Begin() { started = Stopwatch.GetTimestamp(); frames++; }
        public void Mark(int stage)
        {
            long now = Stopwatch.GetTimestamp();
            double ms = (now - started) * 1000.0 / Stopwatch.Frequency;
            totals[stage] += ms; maximum[stage] = System.Math.Max(maximum[stage], ms); started = now;
        }
        public string Report()
        {
            string[] names = { "input", "collision", "render", "actors/items", "overlay" };
            long now = Stopwatch.GetTimestamp();
            double elapsed = (now - lastReport) / (double)Stopwatch.Frequency;
            string text = $"Bridge frames/s={frames / System.Math.Max(.001, elapsed):F1}; CPU ms avg/max:";
            lastReport = now;
            for (int i = 0; i < names.Length; i++) { text += $" {names[i]}={totals[i] / System.Math.Max(1, frames):F2}/{maximum[i]:F2}"; totals[i] = maximum[i] = 0; }
            frames = 0; return text;
        }
    }
}
