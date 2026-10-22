namespace LastEpochLAN.Mod;

internal sealed partial class AutoPlayRuntime
{
    internal int DisconnectRefreshes;
    // A room owns neither the local Start/Stop choice nor its character profile.
    // Refresh native references after teardown without arming a stopped Assist.
    internal void RoomDisconnected()
    {
        try {ReleaseHeld();}
        catch(Exception ex) {MelonLoader.MelonLogger.Warning("Assist disconnect release: "+ex.GetBaseException().Message);}
        try {InputBlock(null,false);}
        catch(Exception ex) {blockedInput=null;MelonLoader.MelonLogger.Warning("Assist disconnect input: "+ex.GetBaseException().Message);}
        localReady=false;enemies.Clear();nearest=rareBoss=null;
        nextRead=nextScan=nextBindings=nextTargets=nextBuffs=0;
        recovery.Reset();pauseReason="";Error="";DisconnectRefreshes++;
        Status=Open?"Paused - F1 open":Armed?"Resuming local Assist after room disconnect":"Stopped - F1 to configure";
        LastRunStatus=Status;
    }
}
