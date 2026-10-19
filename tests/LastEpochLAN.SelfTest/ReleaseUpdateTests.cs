using System.Net;
using System.Net.Http;
using LastEpochLAN.Core;
using LastEpochLAN.Mod;

internal static class ReleaseUpdateTests
{
    private static string Manifest(string version,string product="LastEpochLAN")=>
        $$"""{"schema":2,"product":"{{product}}","version":"{{version}}"}""";
    private sealed class Handler(Func<CancellationToken,Task<HttpResponseMessage>> reply):HttpMessageHandler
    {
        internal int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {Interlocked.Increment(ref Calls);return reply(token);}
    }
    private static Handler Reply(string body)=>new(_=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent(body)}));
    internal static async Task Run(Action<bool,string> check)
    {
        ReleaseVersion.TryParse("1.0.9-dev.9",out var dev9);ReleaseVersion.TryParse("1.0.9-dev.10",out var dev10);
        ReleaseVersion.TryParse("1.0.9",out var stable);ReleaseVersion.TryParse("1.0.10-dev.1",out var next);
        check(dev10.CompareTo(dev9)>0 && stable.CompareTo(dev10)>0 && next.CompareTo(stable)>0,
            "update versions compare numeric development builds and stable releases correctly");
        check(!ReleaseVersion.TryParse("1.0.9-dev.-1",out _) && !ReleaseVersion.TryParse("1.0.9-dev.999999999999",out _) &&
            !ReleaseVersion.TryParse("1.0.9-unknown",out _),"update checker rejects malformed and unsupported release labels");
        var same=Reply(Manifest("1.0.9-dev.13"));
        using(var checker=new ReleaseUpdateChecker(new HttpClient(same)))
        {
            checker.Check("1.0.9-dev.13");await checker.Work;
            check(checker.Status=="Latest version: 1.0.9-dev.13 (up to date)","F4 update status reports the installed published version as current");
            checker.Check("1.0.9-dev.13");await checker.Work;
            check(same.Calls==1,"repeated F4 draws cannot repeatedly download the release manifest");
        }
        using(var checker=new ReleaseUpdateChecker(new HttpClient(Reply(Manifest("1.0.9-dev.14")))))
        {checker.Check("1.0.9-dev.13");await checker.Work;check(checker.Status=="New update available: 1.0.9-dev.14","F4 update status detects a newer published build");}
        using(var checker=new ReleaseUpdateChecker(new HttpClient(Reply(Manifest("1.0.9-dev.12")))))
        {checker.Check("1.0.9-dev.13");await checker.Work;check(checker.Status.Contains("local build is newer"),"local development build is not misreported as an available downgrade");}
        using(var checker=new ReleaseUpdateChecker(new HttpClient(Reply(Manifest("1.0.9-dev.14","OtherProduct")))))
        {checker.Check("1.0.9-dev.13");await checker.Work;check(checker.Status.Contains("unavailable"),"foreign manifest cannot produce a false update notification");}
        using(var checker=new ReleaseUpdateChecker(new HttpClient(Reply(new string('x',17000)))))
        {checker.Check("1.0.9-dev.13");await checker.Work;check(checker.Status.Contains("unavailable"),"oversized release manifest is rejected without blocking the UI");}
        using(var checker=new ReleaseUpdateChecker(new HttpClient(new Handler(_=>throw new HttpRequestException("offline")))))
        {checker.Check("1.0.9-dev.13");await checker.Work;check(checker.Status.Contains("unavailable"),"network failure leaves an explicit update status instead of failing game callbacks");}
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using(var checker=new ReleaseUpdateChecker(new HttpClient(new Handler(async token=>
        {entered.SetResult();await Task.Delay(Timeout.Infinite,token);return new(HttpStatusCode.OK);}))))
        {
            checker.Check("1.0.9-dev.13");await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            checker.Dispose();await checker.Work.WaitAsync(TimeSpan.FromSeconds(3));
            check(checker.Status.Contains("unavailable"),"shutting down the mod cancels an in-flight release check");
        }
    }
}
