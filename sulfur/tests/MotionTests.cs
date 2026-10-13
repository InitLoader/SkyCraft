using System;
using SulfurCraft.Game;
using SulfurCraft.Link;

internal static class MotionTests
{
    private static int checks;
    private static McState Frame(int number, int elapsed, int jitter = 0) => new McState
    {
        TickQpc = (1000 + number * 50 + jitter) * 1000L, TickMs = 50,
        FrameQpc = (1000 + number * 50 + elapsed) * 1000L, FramePartial = elapsed / 50f,
        PrevX = number, CurX = number + 1, PrevY = 128, CurY = 128, PrevZ = 0, CurZ = 0,
        PreviousEye = 1.5f, CurrentEye = 1.7f, PreviousWalk = number, CurrentWalk = number + 1,
        PreviousBob = .02f, CurrentBob = .04f
    };
    private static void Main()
    {
        var motion = new MotionInterpolator(1000000);
        MotionPose midpoint = motion.Sample(Frame(0, 25), 1025000);
        Check(Math.Abs(midpoint.X - .5) < .00001, "Host matches Minecraft's current rendered position without a history delay");
        Check(Math.Abs(midpoint.Eye - 1.6) < .00001, "Eye height uses Minecraft's render phase");
        Check(Math.Abs(midpoint.BobAmount - .03) < .00001 && Math.Abs(midpoint.BobPhase + 1.5) < .00001, "Walking animation and position use the same phase");
        Check(Math.Abs(motion.Sample(Frame(0, 25), 1030000).X - .6) < .00001, "Transport time advances within the known collision-resolved tick");
        Check(motion.Sample(Frame(0, 25), 1010000).X == .5, "A future frame stamp does not rewind the sampled phase");
        double previous = 0, maximumStep = 0;
        for (int ms = 1000; ms < 11000; ms += 5)
        {
            int number = (ms - 1000) / 50, elapsed = (ms - 1000) % 50;
            MotionPose pose = motion.Sample(Frame(number, elapsed, number % 2 == 0 ? 0 : 6), ms * 1000L);
            double expected = (ms - 1000) / 50.0;
            Check(Math.Abs(pose.X - expected) < .00001, "No phase lag with jitter in the older physics stamp");
            Check(pose.X >= previous, "Render-frame updates remain monotonic");
            maximumStep = Math.Max(maximumStep, pose.X - previous); previous = pose.X;
        }
        Check(maximumStep < .101, "No catch-up jumps at tick boundaries");
        McState stopping = Frame(1, 20); stopping.CurX = stopping.PrevX;
        Check(motion.Sample(stopping, 1075000).X == 1, "A stopped tick has no residual host velocity");
        Check(motion.Sample(Frame(0, 25), 2000000).X == 1, "A client stall never extrapolates beyond the collision-resolved endpoint");
        Check(motion.Sample(Frame(0, 25), 1025000).X == .5, "A teleport or client restart requires no stale history reset");
        McState legacy = Frame(0, 25); legacy.FrameQpc = 0;
        Check(motion.Sample(legacy, 1025000).X == .5, "An older client falls back to its undelayed physics clock");
        McState invalid = Frame(0, 25); invalid.TickMs = float.NaN;
        Check(motion.Sample(invalid, 1030000).X == .6, "Invalid tick duration uses the normal tick period");
        Console.WriteLine($"PASS: {checks} motion checks; maximum step={maximumStep:F4}");
    }
    private static void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
        checks++;
    }
}
