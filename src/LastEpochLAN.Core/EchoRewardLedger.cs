using System.Security.Cryptography;
using System.Text;

namespace LastEpochLAN.Core;

// Write-ahead claim: a native grant is never replayed after a crash/partial save.
// A failed grant remains claimed for investigation instead of risking duplicates.
public sealed class EchoRewardLedger
{
    private readonly string directory;
    public EchoRewardLedger(string directory)=>this.directory=directory;
    private string PathFor(string run,string character,string suffix)
    {
        if(!Guid.TryParseExact(run,"N",out _) || string.IsNullOrWhiteSpace(character) || character.Length>128)
            throw new ArgumentException("Invalid Echo reward identity.");
        return Path.Combine(directory,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(run+"|"+character)))+suffix);
    }
    private static bool Create(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try {using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);stream.WriteByte(1);stream.Flush(true);return true;}
        catch(IOException) when(File.Exists(path)){return false;}
    }
    public void Participate(string run,string character)=>Create(PathFor(run,character,".entered"));
    public bool TryClaim(string run,string character)=>File.Exists(PathFor(run,character,".entered")) && Create(PathFor(run,character,".claimed"));
}
