using LastEpochLAN.Core;
using UnityEngine;
using Il2CppLE.AssetBundles;
using UObject=UnityEngine.Object;

namespace LastEpochLAN.Mod;

internal sealed class NativeSummonReplicas : IDisposable
{
    private sealed class Peer
    {
        internal string Epoch="";
        internal readonly SummonPresentation Timeline=new();
        internal readonly Dictionary<int,Model> Models=new();
    }
    private sealed class Model : IDisposable
    {
        internal readonly CosmeticPrefab Prefab;
        internal GameObject Root;
        private readonly Il2Cpp.AnimationManagerWithAnimator driver;
        private readonly NativePlayerAnimationReplica motion;
        private readonly Animator animator;
        private SummonState previous;
        private long sequence,playedAttack;
        private IntPtr plainController;
        private long plainSequence=-1;
        private bool started;
        internal Model(SummonState state,SoftRef<Il2Cpp.ActorVisuals> prefab,UnityEngine.SceneManagement.Scene scene)
        {
            Prefab=state.Prefab;Root=new GameObject("LAN remote summon "+state.Id+" "+state.Template);Root.SetActive(false);
            try
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(Root,scene);
                var visual=SoftRefExtensions.Instantiate(prefab,Root.transform,false,"LastEpochLAN.SummonVisual",0);
                if(visual==null)throw new InvalidOperationException("Summon visual prefab returned null.");
                visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=Quaternion.identity;visual.transform.localScale=Vector3.one;
                foreach(var script in Root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    var animation=script.TryCast<Il2Cpp.AnimationManagerWithAnimator>();
                    if(animation!=null){animation.enabled=false;driver??=animation;continue;}
                    if(script.TryCast<LoadRefComponent>()!=null)continue;
                    if(script.TryCast<Il2CppAnimationLoader.AnimationLoader>()!=null){script.enabled=true;continue;}
                    if(NativeVfxPresentation.Keep(script)){script.enabled=true;continue;}
                    UObject.DestroyImmediate(script);
                }
                foreach(var collider in Root.GetComponentsInChildren<Collider>(true))collider.enabled=false;
                foreach(var body in Root.GetComponentsInChildren<Rigidbody>(true))UObject.DestroyImmediate(body);
                foreach(var agent in Root.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true))UObject.DestroyImmediate(agent);
                foreach(var value in Root.GetComponentsInChildren<Animator>(true))
                {value.applyRootMotion=false;value.cullingMode=AnimatorCullingMode.AlwaysAnimate;value.enabled=true;}
                foreach(var renderer in Root.GetComponentsInChildren<Renderer>(true))renderer.enabled=true;
                if(Root.GetComponentsInChildren<Il2Cpp.Actor>(true).Length!=0 || Root.GetComponentsInChildren<Il2Cpp.ActorSync>(true).Length!=0 ||
                    Root.GetComponentsInChildren<Il2Cpp.ActorVisuals>(true).Length!=0)
                    throw new InvalidOperationException("Summon presentation retained a gameplay Actor.");
                animator=Root.GetComponentsInChildren<Animator>(true).FirstOrDefault();
                if(driver!=null){driver.animator??=animator;motion=new(Root);}
                visual.gameObject.SetActive(true);
            }
            catch {Dispose();throw;}
        }
        internal void Render(SummonFrame frame)
        {
            var state=frame.State;
            var point=new Vector3(frame.Pose.Position.X,frame.Pose.Position.Y,frame.Pose.Position.Z);
            bool moving=(Root.transform.position-point).sqrMagnitude>0.000001f;
            Root.transform.SetPositionAndRotation(point,Quaternion.Euler(0,frame.Pose.Yaw,0));
            Root.transform.localScale=NativeSkillVisualCapture.Vector(state.Scale);
            if(Root.activeSelf!=state.Visible)Root.SetActive(state.Visible);
            if(!ReferenceEquals(previous,state)){previous=state;sequence++;}
            if(driver!=null)
            {
                if(!started && driver.animator?.runtimeAnimatorController!=null && driver.animator.layerCount>0)
                {driver.Start();driver.stopAllAnimations();started=true;}
                if(state.Motion!=null || state.Animation==null)motion.Apply(state.Motion,moving,false);
                if(state.AttackSequence>0 && playedAttack!=state.AttackSequence &&
                    motion.ReplayEnemyAttack(state.AttackAnimation,state.AttackSpeed,state.Motion))playedAttack=state.AttackSequence;
                motion.ApplyCastSnapshot(state.Animation,sequence);
                motion.TickPresentation();
            }
            else if(animator?.runtimeAnimatorController!=null && (plainSequence!=sequence || plainController!=animator.runtimeAnimatorController.Pointer))
            {
                NativeAnimation.Apply(animator,state.Animation);
                plainSequence=sequence;plainController=animator.runtimeAnimatorController.Pointer;
            }
        }
        public void Dispose(){if(Root!=null){Root.SetActive(false);UObject.Destroy(Root);}Root=null;}
    }
    private readonly Dictionary<string,Peer> peers=new(StringComparer.Ordinal);
    private readonly Dictionary<CosmeticPrefab,LoadRef<Il2Cpp.ActorVisuals>> assets=new();
    private readonly Dictionary<CosmeticPrefab,float> retryAt=new();
    private string scene="";
    private PlayerState[] lastPlayers;
    internal string Error="";
    internal int Count=>peers.Values.Sum(p=>p.Models.Count);
    internal void Apply(PlayerState[] players,string zone)
    {
        if(scene!=zone){Dispose();scene=zone;}
        if(ReferenceEquals(lastPlayers,players))return;
        lastPlayers=players;
        var ids=players.Where(p=>p.Scene==zone && p.Available && p.SummonEpoch!="").Select(p=>p.Id).ToHashSet();
        foreach(var id in peers.Keys.Where(id=>!ids.Contains(id)).ToArray())
        {foreach(var model in peers[id].Models.Values)model.Dispose();peers.Remove(id);}
        foreach(var player in players.Where(p=>ids.Contains(p.Id)))
        {
            if(!peers.TryGetValue(player.Id,out var peer))peers.Add(player.Id,peer=new());
            if(!peer.Timeline.Receive(player.SummonEpoch,player.SummonSampleTime,player.Summons,LanSession.MonotonicSeconds))continue;
            if(peer.Epoch!=player.SummonEpoch){foreach(var model in peer.Models.Values)model.Dispose();peer.Models.Clear();peer.Epoch=player.SummonEpoch;}
            var wanted=player.Summons.ToDictionary(s=>s.Id);
            foreach(var id in peer.Models.Keys.Where(id=>!wanted.TryGetValue(id,out var state) || state.Prefab!=peer.Models[id].Prefab).ToArray())
            {peer.Models[id].Dispose();peer.Models.Remove(id);}
        }
        var used=peers.Values.SelectMany(p=>p.Timeline.States).Select(s=>s.Prefab).ToHashSet();
        foreach(var key in assets.Keys.Where(k=>!used.Contains(k)).ToArray()){assets[key].Dispose();assets.Remove(key);retryAt.Remove(key);}
        foreach(var key in retryAt.Keys.Where(k=>!used.Contains(k)).ToArray())retryAt.Remove(key);
    }
    internal void Tick()
    {
        var local=Il2Cpp.PlayerFinder.getPlayerActor();
        if(scene=="" || local==null || !local.HasMovedToPlayerSpawn)return;
        int spawns=2,loads=4;double now=LanSession.MonotonicSeconds;
        foreach(var peer in peers.Values)
        {
            double cursor=peer.Timeline.RenderTime(now);
            foreach(var state in peer.Timeline.States)
            {
                try
                {
                    if(!peer.Models.TryGetValue(state.Id,out var model))
                    {
                        if(spawns<=0 || Time.unscaledTime<retryAt.GetValueOrDefault(state.Prefab))continue;
                        var prefab=new SoftRef<Il2Cpp.ActorVisuals>(new Il2CppLE.AssetBundles.Guid(state.Prefab.P0,state.Prefab.P1),state.Prefab.SubPath);
                        if(!assets.TryGetValue(state.Prefab,out var asset))
                        {
                            if(loads<=0 || assets.Count>=128)continue;
                            loads--;assets[state.Prefab]=asset=SoftRefExtensions.CreateLoadRef<Il2Cpp.ActorVisuals>(prefab,"LastEpochLAN.SummonLoad",0);
                        }
                        if(asset.Status==AssetStatus.Loading)continue;
                        if(asset.AssetOrNull==null)throw new InvalidOperationException("Summon visual asset failed to load: "+state.Template);
                        spawns--;model=new(state,prefab,local.gameObject.scene);peer.Models.Add(state.Id,model);
                    }
                    if(peer.Timeline.TrySample(state.Id,cursor,out var frame))model.Render(frame);
                }
                catch(Exception ex)
                {
                    Error="Summon visual: "+ex.GetBaseException().Message;
                    if(peer.Models.Remove(state.Id,out var broken))broken.Dispose();
                    if(assets.Remove(state.Prefab,out var failed))failed.Dispose();
                    retryAt[state.Prefab]=Time.unscaledTime+5;
                }
            }
        }
    }
    public void Dispose()
    {
        foreach(var peer in peers.Values)foreach(var model in peer.Models.Values)model.Dispose();peers.Clear();
        foreach(var asset in assets.Values)asset.Dispose();assets.Clear();retryAt.Clear();scene="";lastPlayers=null;
    }
}
