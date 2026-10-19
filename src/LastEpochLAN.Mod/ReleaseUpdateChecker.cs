#nullable enable
using System.Net.Http;
using System.Text.Json;
using LastEpochLAN.Core;

namespace LastEpochLAN.Mod;

internal sealed class ReleaseUpdateChecker : IDisposable
{
    private readonly HttpClient http;
    private readonly CancellationTokenSource stop=new();
    private readonly object sync=new();
    private DateTime nextCheck;
    private bool busy,disposed;
    private string status="Update status: not checked";
    internal string Status=>Volatile.Read(ref status);
    internal Task Work {get;private set;}=Task.CompletedTask;
    internal ReleaseUpdateChecker(HttpClient? client=null)
    {http=client??new HttpClient();http.Timeout=TimeSpan.FromSeconds(6);}
    internal void Check(string installed)
    {
        lock(sync)
        {
            if(disposed || busy || DateTime.UtcNow<nextCheck) return;
            busy=true;nextCheck=DateTime.UtcNow.AddMinutes(5);
            Volatile.Write(ref status,"Checking for updates...");
            Work=Task.Run(()=>CheckAsync(installed));
        }
    }
    private async Task CheckAsync(string installed)
    {
        try
        {
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
            deadline.CancelAfter(TimeSpan.FromSeconds(6));
            var token=deadline.Token;
            using var request=new HttpRequestMessage(HttpMethod.Get,"https://raw.githubusercontent.com/cielthephantom2/CostumeUnlocker/main/latest.json");
            request.Headers.CacheControl=new() {NoCache=true};
            using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if(response.Content.Headers.ContentLength>16384) throw new InvalidDataException("Manifest too large.");
            using var body=await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using var bytes=new MemoryStream();
            var chunk=new byte[1024];
            while(true)
            {
                int count=await body.ReadAsync(chunk.AsMemory(),token).ConfigureAwait(false);
                if(count==0) break;
                if(bytes.Length+count>16384) throw new InvalidDataException("Manifest too large.");
                bytes.Write(chunk,0,count);
            }
            using var document=JsonDocument.Parse(bytes.ToArray());
            var root=document.RootElement;
            string? latest=root.GetProperty("version").GetString();
            if(root.GetProperty("schema").GetInt32()!=2 || root.GetProperty("product").GetString()!="LastEpochLAN" ||
                !ReleaseVersion.TryParse(latest,out var release) || !ReleaseVersion.TryParse(installed,out var current))
                throw new InvalidDataException("Invalid release manifest.");
            int comparison=release.CompareTo(current);
            Volatile.Write(ref status,comparison>0 ? "New update available: "+latest : comparison==0 ?
                "Latest version: "+latest+" (up to date)" : "Latest published: "+latest+" (local build is newer)");
        }
        catch(Exception)
        {Volatile.Write(ref status,"Update check unavailable | installed: "+installed);}
        finally {lock(sync) busy=false;}
    }
    public void Dispose()
    {
        lock(sync)
        {
            if(disposed) return;
            disposed=true;stop.Cancel();http.Dispose();
        }
    }
}
