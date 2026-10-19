using System.Numerics;
using LastEpochLAN.Core;

internal static class MotionTests
{
    internal static object Metrics { get; private set; } = new { };
    internal static void Run(Action<bool, string> check)
    {
        var buffer = new BufferedMotion();
        buffer.Push(new MotionSample(10, Vector3.Zero, 350));
        buffer.Push(new MotionSample(10.1, Vector3.UnitX, 10));
        var half = buffer.Sample(10.05);
        check(Math.Abs(half.Position.X - 0.5f) < 0.0001f && Math.Abs(half.Yaw - 360) < 0.01f, "buffer interpolates position and shortest yaw arc");
        check(!buffer.Push(new MotionSample(10, Vector3.One, 0)) && !buffer.Push(new MotionSample(double.NaN, Vector3.Zero, 0)), "duplicate backwards and invalid motion times rejected");
        check(buffer.Sample(100).Position.X <= 1.801f, "late-stream extrapolation is bounded to 80ms");
        buffer.Clear(); buffer.Push(new MotionSample(20, Vector3.UnitX * 20, 0));
        check(buffer.Sample(19).Position.X == 20 && buffer.Count == 1, "teleport clears obsolete interpolation samples");

        var clock = new SnapshotPresentationClock();
        var stream = new BufferedMotion();
        double[] jitter = { 0.02, 0.075, 0.025, 0.085, 0.04, 0.06 };
        int packet = 0, pauses = 0;
        bool monotonic = true;
        float previous = 0, maximumStep = 0;
        for (int frame = 0; frame <= 240; frame++)
        {
            double now = frame / 60d;
            while (packet <= 40 && packet * 0.1 + jitter[packet % jitter.Length] <= now)
            {
                double sample = 100 + packet * 0.1;
                double arrival = 1000 + packet * 0.1 + jitter[packet % jitter.Length];
                clock.Observe(sample, arrival);
                stream.Push(new MotionSample(sample, Vector3.UnitX * (packet * 0.1f), 0)); packet++;
            }
            if (stream.Count == 0) continue;
            float current = stream.Sample(clock.RenderTime(1000 + now)).Position.X;
            if (frame > 30 && frame < 230)
            {
                float step = current - previous;
                if (step < 0.001f) pauses++;
                maximumStep = Math.Max(maximumStep, step);
                monotonic &= step >= -0.00001f;
            }
            previous = current;
        }
        check(monotonic, "jittered presentation never moves backwards");
        check(pauses == 0 && maximumStep < 0.03f, "10Hz snapshots with 20–85ms arrival jitter render continuously at 60fps");
        Metrics = new { snapshotHz = 10, renderHz = 60, simulatedArrivalJitterMs = jitter.Select(j => j * 1000).ToArray(),
            bufferDelayMs = clock.Delay * 1000, pausesAfterWarmup = pauses, maximumStepAtOneUnitPerSecond = maximumStep,
            scope = "synthetic deterministic presentation stream; no claim of actual game FPS or network RTT" };
        check(!clock.Observe(1, 2000), "presentation clock rejects stale host time");
        check(!new WorldState { SampleTime = double.PositiveInfinity }.IsValid(), "non-finite host sample timestamp rejected");
        var adaptive=new SnapshotPresentationClock(true);
        adaptive.Observe(100,1000);double previousCursor=adaptive.RenderTime(1000);
        adaptive.Observe(100.1,1000.25);
        check(adaptive.Delay==0.25,"NPC presentation delay expands to bounded 250ms under a large arrival gap");
        bool paced=true;
        for(int frame=1;frame<=60;frame++)
        {
            double cursor=adaptive.RenderTime(1000+frame/60d);double step=cursor-previousCursor;
            paced &= step>0 && step<=1.15/60+1e-8;previousCursor=cursor;
        }
        check(paced,"adaptive NPC clock slews forward without a backwards seek or clock freeze when delay increases");
        for(int i=2;i<100;i++) adaptive.Observe(100+i*0.1,1000+i*0.1+0.15);
        check(adaptive.Delay>=0.1 && adaptive.Delay<0.15,"stable packet cadence gradually reduces extra NPC jitter buffering");
        adaptive.Clear();check(adaptive.Delay==0.1 && adaptive.RenderTime(2000)==0,"new instance resets adaptive NPC clock and jitter history");
    }
}
