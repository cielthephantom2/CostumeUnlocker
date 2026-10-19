using System.Diagnostics;
using System.Text.Json;

namespace LastEpochLAN.Core;

public readonly record struct PlayerLocation(string Id,string Scene,bool Available,bool Dead,float X,float Y,float Z);

public sealed record TimingSummary(int Samples,double AverageMs,double P95Ms,double P99Ms,double MaxMs,long AverageManagedBytes,long MaxManagedBytes);
public sealed record NetworkSummary(long SentBytes,long ReceivedBytes,long SentFrames,long ReceivedFrames,double AverageSendMs,double MaxSendMs,int PendingOutgoingCombat,int PendingIncomingCombat);

// Fixed-size main-thread sampler. Sorting/allocation happens only when a report
// is requested, never for each measured frame.
public sealed class TimingWindow
{
    private readonly double[] milliseconds;
    private readonly long[] allocations;
    private int cursor,count;
    public TimingWindow(int capacity=600)
    {if(capacity<1 || capacity>100000) throw new ArgumentOutOfRangeException(nameof(capacity));milliseconds=new double[capacity];allocations=new long[capacity];}
    public void Add(long started,long allocatedBefore)
        =>Record((Stopwatch.GetTimestamp()-started)*1000d/Stopwatch.Frequency,Math.Max(0,GC.GetAllocatedBytesForCurrentThread()-allocatedBefore));
    public void Record(double ms,long bytes)
    {
        if(!double.IsFinite(ms) || ms<0 || bytes<0) return;
        milliseconds[cursor]=ms;allocations[cursor]=bytes;cursor=(cursor+1)%milliseconds.Length;count=Math.Min(count+1,milliseconds.Length);
    }
    public TimingSummary Read()
    {
        if(count==0) return new(0,0,0,0,0,0,0);
        var sorted=new double[count];Array.Copy(milliseconds,sorted,count);Array.Sort(sorted);
        long total=0,max=0;for(int i=0;i<count;i++){total+=allocations[i];max=Math.Max(max,allocations[i]);}
        return new(count,sorted.Average(),sorted[(int)Math.Ceiling(count*0.95)-1],sorted[(int)Math.Ceiling(count*0.99)-1],sorted[^1],total/count,max);
    }
}

// Latest-value coalescing: the Unity thread captures data, this worker alone
// serializes and writes it. At most one pending report, not an unbounded backlog.
public sealed class LatestReportWriter : IDisposable
{
    private readonly object gate=new();
    private (string Path,object Report)? pending;
    private Task worker=Task.CompletedTask;
    private bool running,disposed;
    private string error="";
    public string Error {get {lock(gate) return error;}}
    public void Publish(string path,object report)
    {
        lock(gate)
        {
            if(disposed) return;
            pending=(path,report);
            if(!running) {running=true;worker=Task.Run(Drain);}
        }
    }
    private void Drain()
    {
        while(true)
        {
            (string Path,object Report) value;
            lock(gate)
            {if(pending==null){running=false;return;}value=pending.Value;pending=null;}
            string temporary=value.Path+".pending";
            try
            {
                File.WriteAllText(temporary,JsonSerializer.Serialize(value.Report,new JsonSerializerOptions {WriteIndented=true}));
                File.Move(temporary,value.Path,true);
                lock(gate) error="";
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or InvalidOperationException or ArgumentException)
            {lock(gate) error=ex.Message;try{File.Delete(temporary);}catch(IOException){}catch(UnauthorizedAccessException){}}
        }
    }
    public async Task FlushAsync()
    {
        while(true)
        {
            Task current;lock(gate){if(!running)return;current=worker;}
            await current.ConfigureAwait(false);
        }
    }
    // Already-captured data may finish on the worker; disposal never waits on
    // disk from a gameplay callback.
    public void Dispose() {lock(gate) disposed=true;}
}
