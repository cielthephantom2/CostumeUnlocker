namespace LastEpochLAN.Core;

public sealed class PartyParticipation
{
    public bool Enabled {get;private set;}
    public void TeleportRequested()=>Enabled=true;
    public void Disconnect()=>Enabled=false;
    public bool CanShareWorld(bool host,string localScene,string hostScene,bool borrowing,bool busy)=>
        host || Enabled && !busy && localScene!="" && localScene==hostScene;
}
