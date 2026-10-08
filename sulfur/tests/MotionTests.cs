using System;
using SulfurCraft.Game;
using SulfurCraft.Link;

internal static class MotionTests
{
    private static int checks;
    private static McState Tick(int number, int jitter = 0) => new McState
    {
        TickQpc = (1000 + number * 50 + jitter) * 1000L, TickMs = 50,
        PrevX = number, CurX = number + 1, PrevY = 128, CurY = 128, PrevZ = 0, CurZ = 0,
        PreviousEye = 1.5f, CurrentEye = 1.7f, PreviousWalk = number, CurrentWalk = number + 1,
        PreviousBob = .02f, CurrentBob = .04f
    };
    private static void Main()
    {
        var motion = new MotionInterpolator(1000000);
        MotionPose midpoint = motion.Sample(Tick(0), 1035000);
        Check(Math.Abs(midpoint.X - .5) < .00001, "Position uses the 10 ms history delay");
        Check(Math.Abs(midpoint.Eye - 1.6) < .00001, "Eye height follows the same interpolation");
        Check(Math.Abs(midpoint.BobAmount - .03) < .00001 && Math.Abs(midpoint.BobPhase + 1.5) < .00001, "Walk bob follows the same interpolation");
        motion.Reset();
        McState actual = Tick(0);
        double previous = 0, maximumStep = 0;
        int oldHolds = 0, newHolds = 0;
        for (int ms = 1000; ms < 11000; ms += 5)
        {
            int number = (ms - 1000) / 50;
            int jitter = number % 2 == 0 ? 0 : 6;
            if (ms >= 1000 + number * 50 + jitter) actual = Tick(number, jitter);
            MotionPose pose = motion.Sample(actual, ms * 1000L);
            if (ms > 5000)
            {
                double step = pose.X - previous;
                Check(step >= -.0001, "Alternating tick jitter must not move the camera backward");
                maximumStep = Math.Max(maximumStep, step);
                if (step < .0001) newHolds++;
                if (ms * 1000L - actual.TickQpc >= 50000) oldHolds++;
            }
            previous = pose.X;
        }
        Check(oldHolds > 0 && newHolds == 0, "History removes repeated end-of-tick holds caused by 6 ms jitter");
        Check(maximumStep < .12, "Jitter does not produce catch-up jumps");
        motion.Reset();
        Check(Math.Abs(motion.Sample(Tick(0), 1035000).X - .5) < .00001, "Teleport reset discards the previous world history");
        motion.Sample(Tick(20), 2035000);
        Check(Math.Abs(motion.Sample(Tick(0), 1035000).X - .5) < .00001, "Backward QPC after client restart resets the history");
        motion.Reset();
        MotionPose stopped = motion.Sample(Tick(0), 2000000);
        Check(stopped.X == 1, "A long client stall does not extrapolate through walls");
        Console.WriteLine($"PASS: {checks} motion checks; old holds={oldHolds}, new holds={newHolds}, max step={maximumStep:F4}");
    }
    private static void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
        checks++;
    }
}
