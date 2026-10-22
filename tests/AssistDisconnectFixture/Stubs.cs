namespace MelonLoader{public static class MelonLogger{public static List<string> Messages=new();public static void Warning(string text)=>Messages.Add(text);}}
namespace LastEpochLAN.Mod
{
internal sealed partial class AutoPlayRuntime
{
    internal bool Open,Armed,localReady=true,ThrowRelease,ThrowInput;
    internal int Releases,InputReleases;
    internal object blockedInput=new(),nearest=new(),rareBoss=new(),Settings=new();
    internal List<object> enemies=new(){new()};
    internal float nextRead=10,nextScan=10,nextBindings=10,nextTargets=10,nextBuffs=10;
    internal LastEpochLAN.Core.AutoPlayRecovery recovery=new();
    internal string pauseReason="Mod panel / Shared Stash open",Error="stale data",Status="",LastRunStatus="",character="saved-character",profilePath="saved-profile";
    private void ReleaseHeld(){Releases++;if(ThrowRelease)throw new InvalidOperationException("destroyed native channel");}
    private void InputBlock(object input,bool enabled){InputReleases++;if(ThrowInput)throw new InvalidOperationException("destroyed input");blockedInput=null;}
}
}
