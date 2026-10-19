using System.Numerics;

namespace LastEpochLAN.Core;

public readonly record struct MotionSample(double Time, Vector3 Position, float Yaw);

// Presentation only. Damage, death and authoritative world state do not wait
// for this clock. Different host/client clock origins are supported.
public sealed class SnapshotPresentationClock
{
    private double offset = double.NaN, latest, rendered = double.NegativeInfinity;
    private double lastArrival=double.NaN,lastRenderAt=double.NaN,jitter;
    private readonly bool adaptive;
    public SnapshotPresentationClock(bool adaptive=false)=>this.adaptive=adaptive;
    public double Delay { get; private set; } = 0.1;
    public bool Observe(double sampleTime, double arrivalTime)
    {
        if (!double.IsFinite(sampleTime) || !double.IsFinite(arrivalTime) || sampleTime < 0 || arrivalTime < 0 ||
            (!double.IsNaN(offset) && sampleTime <= latest)) return false;
        double candidate = arrivalTime - sampleTime;
        if(adaptive && !double.IsNaN(lastArrival))
        {
            jitter=Math.Max(jitter*0.98,Math.Max(0,(arrivalTime-lastArrival)-(sampleTime-latest)));
            Delay=Math.Clamp(0.1+jitter,0.1,0.25);
        }
        // Slow adjustment to a lower transport delay prevents jitter from
        // repeatedly moving the presentation clock backwards/forwards.
        offset = double.IsNaN(offset) ? candidate : offset + Math.Min(0, candidate - offset) * 0.1;
        latest = sampleTime;
        lastArrival=arrivalTime;
        return true;
    }
    public double RenderTime(double now)
    {
        if (double.IsNaN(offset)) return 0;
        double target=now-offset-Delay;
        if(adaptive && double.IsFinite(rendered) && double.IsFinite(lastRenderAt) && now-lastRenderAt is >0 and <0.5)
        {
            double delta=now-lastRenderAt;
            rendered+=Math.Clamp(target-rendered,delta*0.85,delta*1.15);
        }
        else rendered = Math.Max(rendered,target);
        lastRenderAt=now;
        return rendered;
    }
    public void Clear() { offset = double.NaN; latest = 0; rendered = double.NegativeInfinity;
        lastArrival=lastRenderAt=double.NaN;jitter=0;Delay=0.1; }
}

public sealed class BufferedMotion
{
    private readonly List<MotionSample> samples = new(8);
    public int Count => samples.Count;
    public bool Push(MotionSample sample)
    {
        if (!double.IsFinite(sample.Time) || sample.Time < 0 || !float.IsFinite(sample.Position.X) ||
            !float.IsFinite(sample.Position.Y) || !float.IsFinite(sample.Position.Z) || !float.IsFinite(sample.Yaw)) return false;
        if (samples.Count > 0 && sample.Time <= samples[^1].Time) return false;
        if (samples.Count == 8) samples.RemoveAt(0);
        samples.Add(sample); return true;
    }
    public MotionSample Sample(double time)
    {
        if (samples.Count == 0) throw new InvalidOperationException("No motion baseline.");
        if (time <= samples[0].Time) return samples[0];
        for (int i = 1; i < samples.Count; i++)
        {
            if (time > samples[i].Time) continue;
            var a = samples[i - 1]; var b = samples[i];
            float fraction = (float)((time - a.Time) / (b.Time - a.Time));
            return new MotionSample(time, Vector3.Lerp(a.Position, b.Position, fraction), LerpYaw(a.Yaw, b.Yaw, fraction));
        }
        var last = samples[^1];
        if (samples.Count == 1) return last;
        var previous = samples[^2];
        double gap = last.Time - previous.Time;
        // Bridge a short late packet, then hold. Never predict indefinitely
        // during a disconnect, native map load or stopped stream.
        float ahead = (float)(Math.Clamp(time - last.Time, 0, 0.08) / gap);
        return new MotionSample(time, last.Position + (last.Position - previous.Position) * ahead, last.Yaw);
    }
    private static float LerpYaw(float a, float b, float fraction)
    {
        float delta = ((b - a) % 360 + 540) % 360 - 180;
        return a + delta * fraction;
    }
    public void Clear() => samples.Clear();
}
