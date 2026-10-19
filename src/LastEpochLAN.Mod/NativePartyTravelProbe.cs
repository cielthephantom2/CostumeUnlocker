using LastEpochLAN.Core;
using UnityEngine;

namespace LastEpochLAN.Mod;

internal sealed class NativePartyTravelProbe : IDisposable
{
    private readonly LanPartyTravel travel = new();
    private readonly NativePartyReplica party = new();
    private LanSession host, guest;
    private Task<LanSession> joining;
    private string originalScene, town;
    private Vector3 before, targetPoint;
    private int stage;
    private float deadline, settle;
    private bool sameMap;
    internal object Result { get; private set; }
    internal bool Finished => Result != null;
    internal void Tick()
    {
        if (Finished) return;
        try
        {
            var actor = Il2Cpp.PlayerFinder.getPlayerActor();
            if (actor == null || !actor.HasMovedToPlayerSpawn) return;
            var scene = Il2Cpp.SceneList.GetCurrentSceneDetails()?.Name ?? "";
            if (host == null)
            {
                originalScene = scene;
                host = LanSession.Host("PortalQAHost", "PortalQA", "PortalQAOnly", 39033, System.Net.IPAddress.Loopback);
                joining = LanSession.JoinAsync("127.0.0.1", 39033, "PortalQAPeer", "PortalQA", "PortalQAOnly");
                deadline = Time.unscaledTime + 65; return;
            }
            if (guest == null) { if (!joining.IsCompleted) return; guest = joining.GetAwaiter().GetResult(); }
            var p = actor.position();
            host.Publish(new PlayerState { Available = true, Scene = scene, X = p.x, Y = p.y, Z = p.z });
            if (stage == 0) targetPoint = p + new Vector3(7, 0, 0);
            string peerScene = stage < 2 ? originalScene : town;
            // In the second scene, put the software peer at the actual native
            // entrance after loading, before the adapter completes its placement.
            if (stage == 2 && scene == town) targetPoint = p;
            guest.Publish(new PlayerState { Available = true, Scene = peerScene, X = targetPoint.x, Y = targetPoint.y, Z = targetPoint.z,
                GiftIdentity = guest.LocalId, CharacterId = "portal-peer" });
            if (stage != 2 || scene != town || host.PlayerPositions.Any(s => s.Id == guest.LocalId && s.Scene == town &&
                Vector3.Distance(new Vector3(s.X, s.Y, s.Z), targetPoint) < 0.5f)) travel.Tick(host);
            if (Time.unscaledTime > deadline) throw new TimeoutException("Party travel QA stage " + stage + ": " + travel.Message);
            if (travel.Error != "") throw new InvalidOperationException(travel.Error);
            switch (stage)
            {
                case 0:
                    if (!host.PlayerPositions.Any(s => s.Id == guest.LocalId && s.Available)) return;
                    before = p;
                    party.Tick(host);
                    var member = Resources.FindObjectsOfTypeAll<Il2CppLE.UI.Party.UnityUI.PartyOverviewMemberPanel>()
                        .FirstOrDefault(m => m.IsActive && m.partyMember?.Identity?.MasterAccountID == guest.LocalId);
                    if (member?.requestPortalButton == null || !member.requestPortalButton.gameObject.activeSelf || !member.requestPortalButton.interactable)
                        throw new InvalidOperationException("Native party portal button not enabled.");
                    member.requestPortalButton.onClick.Invoke(); stage = 1; settle = Time.unscaledTime + 4; break;
                case 1:
                    if (Time.unscaledTime < settle) return;
                    sameMap = travel.Completed == 1 && Vector3.Distance(actor.position(), targetPoint) <= 5 && Vector3.Distance(actor.position(), before) > 1;
                    if (!sameMap) throw new InvalidOperationException("Native same-map placement did not move actual actor beside peer.");
                    foreach (var details in Il2Cpp.SceneList.get().sceneDetailsCollection)
                        if (details.sceneType == Il2Cpp.SceneType.Town && details.Name != scene) { town = details.Name; break; }
                    if (town == null) throw new InvalidOperationException("QA town destination unavailable.");
                    stage = 2; settle = Time.unscaledTime + 0.3f; break;
                case 2:
                    if (!travel.Busy && travel.Completed == 1 && Time.unscaledTime >= settle &&
                        host.PlayerPositions.Any(s => s.Id == guest.LocalId && s.Scene == town)) travel.Request(guest.LocalId);
                    if (travel.Completed < 2) return;
                    Result = new { passed = sameMap && scene == town && Vector3.Distance(actor.position(), targetPoint) <= 5,
                        sameMapNativeButton = sameMap, crossMapNativeTransition = scene == town, originalScene, town, travel.Completed,
                        scope = "actual native party Button, actor placement and campaign/town transition with a software TCP peer; two physical PCs and special instances unverified" };
                    Dispose(); break;
            }
        }
        catch (Exception ex) { Result = new { passed = false, stage, error = ex.ToString(), travel.Message }; Dispose(); }
    }
    public void Dispose() { party.Reset(); travel.Disconnect(); guest?.Dispose(); host?.Dispose(); }
}
