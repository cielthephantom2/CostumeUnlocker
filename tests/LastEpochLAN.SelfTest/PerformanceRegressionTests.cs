using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using LastEpochLAN.Core;

internal static class PerformanceRegressionTests
{
    internal static async Task Run(Action<bool,string> check)
    {
        var window=new TimingWindow(100);
        for(int i=1;i<=200;i++) window.Record(i,i*10);
        var timing=window.Read();
        check(timing.Samples==100 && timing.P95Ms==195 && timing.P99Ms==199 && timing.AverageManagedBytes==1505,
            "fixed timing window reports rolling p95/p99 and managed allocations rather than lifetime averages");
        var idle=new EnemyState {Id=1,Template="validation",MaxHealth=10,Health=10};
        idle.IsValid();long beforeValidation=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<1000;i++) if(!idle.IsValid()) throw new InvalidOperationException("Valid idle target rejected.");
        check(GC.GetAllocatedBytesForCurrentThread()-beforeValidation<1024,
            "idle enemy validation does not create temporary PlayerState records or empty distinct/hash sets");
        check(!(idle with {SpawnX=float.NaN}).IsValid() && !(idle with {X=100001}).IsValid() && !(idle with {Yaw=float.PositiveInfinity}).IsValid(),
            "allocation-free coordinate checks retain finite and world-bound validation");
        var directory=Path.Combine(Path.GetTempPath(),"lelan-report-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        string path=Path.Combine(directory,"report.json");
        using(var writer=new LatestReportWriter())
        {
            for(int i=0;i<500;i++) writer.Publish(path,new {sequence=i});
            await writer.FlushAsync();
            check(JsonDocument.Parse(await File.ReadAllTextAsync(path)).RootElement.GetProperty("sequence").GetInt32()==499 && writer.Error=="",
                "background report writer coalesces a burst and commits the newest complete JSON atomically");
        }
        File.Delete(path);Directory.Delete(directory);
        var large=new Packet {Kind="pool-proof",Error=new string('x',200000)};
        using(var stream=new MemoryStream())
        {
            int bytes=await Wire.WriteAsync(stream,large,CancellationToken.None);stream.Position=0;
            var decoded=await Wire.ReadAsync(stream,CancellationToken.None);
            // Reuse and overwrite the pool after reading. The deserialized
            // record must own all data after the rented frame is returned.
            for(int i=0;i<4;i++){using var other=new MemoryStream();await Wire.WriteAsync(other,large with {Error=new string('y',200000)},CancellationToken.None);}
            check(bytes==stream.Length && decoded.Error==large.Error && decoded.ReceivedBytes==bytes-4,
                "pooled framed JSON survives buffer growth, roundtrip and pool reuse without exposing borrowed bytes");
        }
        bool rejected=false;
        try {using var stream=new MemoryStream();await Wire.WriteAsync(stream,large with {Error=new string('x',Wire.MaximumFrameBytes+1)},CancellationToken.None);}
        catch(InvalidDataException){rejected=true;}
        check(rejected,"pooled frame writer retains the original maximum payload limit");

        using var host=LanSession.Host("PerfHost","perf-fixture","private-fixture",0,IPAddress.Loopback);
        host.Publish(new PlayerState {Available=true,Scene="load-map"});
        host.PublishWorld(new() {Epoch=1,Revision=1,Scene="load-map",Enemies=Enumerable.Range(1,4000).Select(id=>new EnemyState
            {Id=id,Template="crowd",Health=100,MaxHealth=100}).ToArray()});
        using var stalled=new TcpClient {ReceiveBufferSize=1024,NoDelay=true};
        await stalled.ConnectAsync(IPAddress.Loopback,host.Port);
        await Wire.WriteAsync(stalled.GetStream(),new Packet {Kind="hello",Build="perf-fixture",Code="private-fixture",Name="StalledReader"},CancellationToken.None);
        var welcome=await Wire.ReadAsync(stalled.GetStream(),CancellationToken.None);
        check(welcome.Kind=="welcome","slow-peer load fixture authenticates a real socket before withholding reads");
        using var healthy=await LanSession.JoinAsync("127.0.0.1",host.Port,"HealthyReader","perf-fixture","private-fixture");
        long end=Environment.TickCount64+10000;
        while(healthy.WorldInfo.EnemyCount!=4000 && Environment.TickCount64<end) await Task.Delay(10);
        check(healthy.WorldInfo.EnemyCount==4000,"healthy peer receives complete baseline while another authenticated receiver stops reading");
        var sequences=new HashSet<long>();end=Environment.TickCount64+700;
        int sequence=0;
        while(Environment.TickCount64<end)
        {
            host.Publish(new PlayerState {Available=true,Scene="load-map",X=++sequence});
            var player=healthy.PlayerPositions.FirstOrDefault(p=>p.Id==host.LocalId);
            if(player!=null) sequences.Add(player.Sequence);
            await Task.Delay(20);
        }
        check(sequences.Count>=4,"backpressured peer does not stall healthy player snapshots in the shared broadcast loop");
        check(host.TryGetPlayerLocation(host.LocalId,out var location) && location.X==sequence && location.Available,
            "combat reads current scalar player location without cloning avatar payloads");
        check(host.NetworkStats.SentBytes>0 && healthy.NetworkStats.ReceivedFrames>0 && healthy.NetworkStats.ReceivedBytes>0,
            "transport reports actual framed byte and packet counters");
        using var pressureHost=LanSession.Host("PressureHost","perf-fixture","private-fixture",0,IPAddress.Loopback);
        pressureHost.PublishWorld(new() {Epoch=1,Revision=1,Scene="pressure-map",Enemies=new[]{idle}});
        using var producer=new TcpClient {NoDelay=true};await producer.ConnectAsync(IPAddress.Loopback,pressureHost.Port);
        await Wire.WriteAsync(producer.GetStream(),new Packet {Kind="hello",Build="perf-fixture",Code="private-fixture",Name="PressureGuest"},CancellationToken.None);
        await Wire.ReadAsync(producer.GetStream(),CancellationToken.None);
        for(int batch=0;batch<66;batch++)
            await Wire.WriteAsync(producer.GetStream(),new Packet {Kind="state",State=new PlayerState {Sequence=batch+1,Available=true,Scene="pressure-map"},
                Hits=Enumerable.Range(batch*32+1,32).Select(id=>new HitRequest {Sequence=id,Epoch=1,EnemyId=1,Damage=1}).ToArray()},CancellationToken.None);
        end=Environment.TickCount64+3000;
        while(pressureHost.NetworkStats.PendingIncomingCombat<2048 && Environment.TickCount64<end) await Task.Delay(5);
        check(pressureHost.NetworkStats.PendingIncomingCombat==2048,"combat receive queue remains bounded under a real socket burst");
        var commands=new List<long>();end=Environment.TickCount64+3000;
        while(commands.Count<2112 && Environment.TickCount64<end)
        {while(pressureHost.TryTakeCombat(out var item))commands.Add(item!.Sequence);await Task.Delay(5);}
        check(commands.SequenceEqual(Enumerable.Range(1,2112).Select(i=>(long)i)),
            "full combat queue backpressures and later delivers every valid damage command in order without silent loss");
    }
}
