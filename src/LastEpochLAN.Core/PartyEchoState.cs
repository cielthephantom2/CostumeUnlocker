namespace LastEpochLAN.Core;

// Only the Host publishes this state. RunId distinguishes consecutive Echoes
// even when their scene name and native EchoId are reused.
public sealed record PartyEchoState
{
    public long Revision {get;init;}
    public string RunId {get;init;}="";
    public string Scene {get;init;}="";
    public string HostScene {get;init;}="";
    public bool Active {get;init;}
    public byte[] Run {get;init;}=Array.Empty<byte>();
    public byte[] Metadata {get;init;}=Array.Empty<byte>();
    public bool Completed {get;init;}
    public EchoCompletion? Reward {get;init;}
    public string[] RewardCharacters {get;init;}=Array.Empty<string>();
    public uint RandomState {get;init;}
    public int ObjectiveSeed {get;init;}
    public int ObjectiveType {get;init;}
    public int PortalTarget {get;init;}
    public int BonusStability {get;init;}
    public int MaxBonusStability {get;init;}
    public EchoObjective[] Objectives {get;init;}=Array.Empty<EchoObjective>();
    public bool IsValid(bool requireContext=true)=>Revision>0 && Scene is {Length:<=128} && HostScene is {Length:<=128} &&
        RunId is {Length:<=32} && (!Active || Guid.TryParseExact(RunId,"N",out _) && Scene.Length>0 && HostScene.Length>0) &&
        Run is {Length:<=262144} && Metadata is {Length:<=8192} &&
        (!Active || Run.Length>0 && Metadata.Length>0 || !requireContext && Run.Length==0 && Metadata.Length==0) &&
        PortalTarget is >=0 and <=2 && ObjectiveType is >=0 and <=255 && BonusStability>=0 && MaxBonusStability>=0 &&
        RewardCharacters is {Length:<=4} && RewardCharacters.All(c=>c is {Length:>0 and <=128}) && RewardCharacters.Distinct().Count()==RewardCharacters.Length &&
        (Reward==null || Completed && Reward.IsValid()) && Objectives is {Length:<=32} && Objectives.All(o=>o!=null && o.IsValid());
    public PartyEchoState Copy()=>this with {Run=Run.ToArray(),Metadata=Metadata.ToArray(),Objectives=Objectives.ToArray(),RewardCharacters=RewardCharacters.ToArray()};
}

public sealed record EchoCompletion(float EnemyScore,int BonusStability,int Chests,bool HasReward,int RewardIndex,
    bool HasGuestReward,int GuestIndex1,int GuestIndex2,float CountModifier)
{
    public bool IsValid()=>float.IsFinite(EnemyScore) && EnemyScore>=0 && BonusStability>=0 && Chests is >=0 and <=1024 &&
        RewardIndex is >=-1 and <=1000000 && GuestIndex1 is >=-1 and <=1000000 && GuestIndex2 is >=-1 and <=1000000 &&
        float.IsFinite(CountModifier) && CountModifier is >=0 and <=100000;
}

public sealed record EchoObjective(int GameMode,byte Type,int Message,int Data,byte Count,int Category,bool Completed)
{
    public bool IsValid()=>GameMode is >=0 and <=255 && Message is >=0 and <=65535 && Category is >=0 and <=255;
}

public static class PartyEchoRules
{
    // Seeing the Host's Echo does not mean the Guest has borrowed that run.
    // A party-only Guest must keep advertising its own loaded local character.
    public static bool LocalGameplayReady(bool guestSession,bool borrowing,bool busy,bool borrowedContextMatches)=>
        !busy && (!borrowing || guestSession && borrowedContextMatches);
    public static bool SharedWorldReady(bool localReady,bool guestSession,bool hostEchoActive,bool borrowing)=>
        localReady && (!guestSession || !hostEchoActive || borrowing);
    public static bool NeedsEntry(PartyEchoState state,string joinedRun,string currentScene)=>
        state.Active && string.Equals(state.HostScene,state.Scene,StringComparison.OrdinalIgnoreCase) &&
        (state.RunId!=joinedRun || !string.Equals(currentScene,state.Scene,StringComparison.OrdinalIgnoreCase));
    public static bool Matches(PartyEchoState? state,string run,string scene)=>
        state is {Active:true} && state.RunId==run && string.Equals(state.Scene,scene,StringComparison.OrdinalIgnoreCase);
}
