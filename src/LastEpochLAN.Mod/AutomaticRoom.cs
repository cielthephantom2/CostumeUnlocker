namespace LastEpochLAN.Mod;

// Character login owns room identity; no editable connection configuration.
internal sealed class AutomaticRoom
{
    internal const string Address="27.70.239.115";
    internal const int Port=7777;
    internal const double RetrySeconds=30;
    internal string CharacterId {get;private set;}="";
    internal string Name {get;private set;}="";
    internal bool IsHost=>string.Equals(Name,"Doomsday",StringComparison.Ordinal);
    internal bool Paused {get;private set;}
    internal double RetryAt {get;private set;}
    internal bool Observe(string id,string name)
    {
        id??="";name??="";
        if(id==CharacterId && name==Name)return false;
        CharacterId=id;Name=name;Paused=false;RetryAt=0;return true;
    }
    internal bool Due(double now,bool connected,bool joining)=>!Paused && CharacterId.Length>0 &&
        !string.IsNullOrWhiteSpace(Name) && !connected && !joining && now>=RetryAt;
    internal void Attempt(double now)=>RetryAt=now+RetrySeconds;
    internal void Failed(double now)=>RetryAt=now+RetrySeconds;
    internal void Pause()=>Paused=true;
    internal void Resume(){Paused=false;RetryAt=0;}
}
