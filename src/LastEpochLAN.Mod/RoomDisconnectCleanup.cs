namespace LastEpochLAN.Mod;

internal static class RoomDisconnectCleanup
{
    internal static void Run(Action cleanup,Action<Exception> report)
    {
        try {cleanup();}
        catch(Exception ex) {report(ex);}
    }
}
