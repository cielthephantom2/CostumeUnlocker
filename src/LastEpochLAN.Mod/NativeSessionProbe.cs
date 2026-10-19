using System.Collections.Concurrent;
using System.Security.Cryptography;
using HarmonyLib;
using Il2CppLE.Networking;
using Il2CppLE.Networking.Core;
using Il2CppLE.Networking.Core.Networking;
using Il2CppLE.Networking.Generated;
using Il2CppLE.Networking.Messaging;
using Il2CppLE.Networking.Replication;
using Il2CppLidgren.Network;
using UnityEngine;

namespace LastEpochLAN.Mod;

// The policy only applies to explicitly registered test-provider instances.
// Native authentication for every other provider is left untouched.
internal static class NativeLanAuthentication
{
    private static readonly ConcurrentDictionary<IntPtr, string> providers = new();
    internal static long Accepted;
    internal static long Rejected;
    internal static long HailCalls;
    internal static long VerifyCalls;
    internal static string LastAuthError = "";
    internal static void Register(UserAuthenticationProvider provider, string token) => providers[provider.Pointer] = token;
    internal static void Remove(UserAuthenticationProvider provider) => providers.TryRemove(provider.Pointer, out _);
    internal static bool TryGet(UserAuthenticationProvider provider, out string token) => providers.TryGetValue(provider.Pointer, out token);
}

[HarmonyPatch(typeof(UserAuthenticationProvider), nameof(UserAuthenticationProvider.PopulateHailMessage))]
internal static class NativeLanHailPatch
{
    private static bool Prefix(UserAuthenticationProvider __instance, NetOutgoingMessage hailMessage)
    {
        Interlocked.Increment(ref NativeLanAuthentication.HailCalls);
        if (!NativeLanAuthentication.TryGet(__instance, out string token)) return true;
        hailMessage.Write("LELAN-native-v1"); hailMessage.Write(token);
        return false;
    }
}

internal static class NativeLanVerifyPatch
{
    internal static bool Handle(ServerNetworkService __instance, NetIncomingMessage msg)
    {
        Interlocked.Increment(ref NativeLanAuthentication.VerifyCalls);
        var provider = __instance.AuthenticationProvider.TryCast<UserAuthenticationProvider>();
        if (provider == null) { NativeLanAuthentication.LastAuthError = "Provider cast failed"; return true; }
        if (!NativeLanAuthentication.TryGet(provider, out string token)) { NativeLanAuthentication.LastAuthError = "Provider was not registered"; return true; }
        var connection = msg.SenderConnection;
        try
        {
            if (connection.RemoteEndPoint.Address.ToString() != "127.0.0.1" || msg.LengthBytes > 256 ||
                msg.ReadString() != "LELAN-native-v1" || msg.ReadString() != token)
                throw new InvalidDataException("Invalid private native LAN handshake.");
            var context = new AuthenticationContext(connection);
            context._State_k__BackingField = AuthenticationState.AcceptedClient;
            context.continuation = (Func<NetPlayer>)(() =>
                new NetPlayer(connection) { _ClientSessionId_k__BackingField = "LAN-native-session" });
            if (!__instance.pendingAuthContexts.TryAdd(connection.RemoteUniqueIdentifier, context))
                throw new InvalidDataException("Duplicate native connection request.");
            // The native client reads its assigned connection ID from the server hail
            // before registering the server player and resetting replication state.
            var responseHail = __instance.Server.CreateMessage();
            responseHail.Write(connection.RemoteUniqueIdentifier.ToString(System.Globalization.CultureInfo.InvariantCulture));
            connection.m_localHailMessage = responseHail;
            connection.SendConnectResponse(NetTime.Now, false);
            Interlocked.Increment(ref NativeLanAuthentication.Accepted);
        }
        catch (Exception ex)
        {
            NativeLanAuthentication.LastAuthError = ex.Message;
            connection.Deny("Invalid private native LAN handshake.");
            Interlocked.Increment(ref NativeLanAuthentication.Rejected);
        }
        return false;
    }
}

// The native compiler inlines ReceiveConnectionRequestMessage into ReceiveCallback;
// patch the callback itself so connection approvals actually reach our local policy.
[HarmonyPatch(typeof(ServerNetworkService), nameof(ServerNetworkService.ReceiveCallback))]
internal static class NativeLanServerReceivePatch
{
    private static bool Prefix(ServerNetworkService __instance)
    {
        var provider = __instance.AuthenticationProvider.TryCast<UserAuthenticationProvider>();
        if (provider == null || !NativeLanAuthentication.TryGet(provider, out _)) return true;
        NetIncomingMessage msg;
        while ((msg = __instance.Server.ReadMessage()) != null)
        {
            try
            {
                switch (msg.MessageType)
                {
                    case NetIncomingMessageType.ConnectionApproval: NativeLanVerifyPatch.Handle(__instance, msg); break;
                    case NetIncomingMessageType.StatusChanged: __instance.ReceiveStatusChangedMessage(msg); break;
                    case NetIncomingMessageType.Data: __instance.ReceiveDataMessage(msg); break;
                    case NetIncomingMessageType.WarningMessage: BaseNetworkService.ReceiveWarningMessage(msg); break;
                    case NetIncomingMessageType.ErrorMessage: BaseNetworkService.ReceiveErrorMessage(msg); break;
                    case NetIncomingMessageType.DebugMessage: BaseNetworkService.ReceiveDebugMessage(msg); break;
                }
            }
            catch (Exception ex) { NativeLanAuthentication.LastAuthError = ex.ToString(); }
            finally { __instance.Server.Recycle(msg); }
        }
        return false;
    }
}

internal sealed class NativeSessionProbe : IDisposable
{
    private const int ObjectCount = 64;
    private readonly UserAuthenticationProvider serverAuth = new();
    private UserAuthenticationProvider clientAuth;
    private readonly NetObjectRegistry serverRegistry = new();
    private NetObjectRegistry clientRegistry;
    private ServerNetworkService server;
    private ClientNetworkService client;
    private readonly string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
    private readonly List<(NetObjectKey Key, ActorSyncNetObject Object)> actors = new();
    private readonly NativeReplicationBootstrap bootstrap = new();
    private float deadline;
    private float settleAt;
    private Stage stage;
    private long rejectedAtStart;
    private long acceptedAtStart;
    private bool rejectedWrongToken;
    private bool created;
    private bool updated;
    private bool lateJoined;
    private bool disconnected;
    private bool destroyed;
    private Il2Cpp.ActorSync hostReceiver;
    private Il2Cpp.ActorSync guestReceiver;
    private Il2Cpp.ActorSync guestUntargetedReceiver;
    private readonly List<GameObject> receiverRoots = new();
    private bool nativeHostToGuestRpc;
    private bool nativeGuestToHostRpc;
    private bool rpcTargetIsolation;
    private bool reconnectUpdates;
    private readonly bool withAssets;
    private NativeClassAnimator guestVisual;
    private bool nativeMovementRpc;
    private bool nativeAttackRpc;
    private string nativeAttackClip = "";
    private enum Stage { Reject, Connect, Create, HostRpc, MovementRpc, AttackRpc, StopAnimationRpc, GuestRpc, Update, RemoveHalf, Disconnect, Reconnect, Baseline, ReconnectUpdate, Destroy, Settle, Cleanup }
    internal object Result { get; private set; }
    internal bool Finished => Result != null;

    internal NativeSessionProbe(bool withAssets = false)
    {
        this.withAssets = withAssets;
        NativeLanAuthentication.LastAuthError = "";
        rejectedAtStart = NativeLanAuthentication.Rejected;
        acceptedAtStart = NativeLanAuthentication.Accepted;
        NativeLanAuthentication.Register(serverAuth, token);
        try
        {
            server = new ServerNetworkService(new GeneratedMessageRouter().Cast<IMessageRouter>(), serverRegistry.Cast<INetObjectRegistry>(),
                serverAuth.Cast<IAuthenticationProvider>(), new NetworkOptions());
            server.ReplicationSystem = new ReplicationSystem(server, true);
            NativeReplicationLifecycle.Track(server.ReplicationSystem);
            serverRegistry.ReplicationSystem = server.ReplicationSystem;
            server.Start("127.0.0.1", 0, "LAN-Native-Host-Probe");
            if (!NetObjectTypeRegistry.TryGetTypeConstructor(ActorSyncNetObject.TypeInfo.TypeID, out _))
                ActorSyncNetObject.RegisterNetObjectType();
            // Objects exist before any guest connects: initial state must arrive through native baselines.
            for (int i = 0; i < ObjectCount; i++)
            {
                var actor = new ActorSyncNetObject { CreatePrefabIndex = -1, MasterAccountId = "LAN-Probe-" + i,
                    position = InitialPosition(i), rotation = Quaternion.identity, healthRatio = 200, Enabled = false, ScaleX1000 = 1000 };
                var key = serverRegistry.TakeNextDynamicNetObjectKey();
                serverRegistry.Register(key, actor.Cast<INetObject>());
                actors.Add((key, actor));
            }
            StartClient("deliberately-wrong-probe-token");
            Advance(Stage.Reject);
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void StartClient(string clientToken)
    {
        clientAuth = new UserAuthenticationProvider();
        NativeLanAuthentication.Register(clientAuth, clientToken);
        clientRegistry = new NetObjectRegistry();
        client = new ClientNetworkService(new GeneratedMessageRouter().Cast<IMessageRouter>(), clientRegistry.Cast<INetObjectRegistry>(),
            clientAuth.Cast<IAuthenticationProvider>(), new NetworkOptions());
        client.ReplicationSystem = new ReplicationSystem(client, false);
        NativeReplicationLifecycle.Track(client.ReplicationSystem);
        clientRegistry.ReplicationSystem = client.ReplicationSystem;
        client.Start("127.0.0.1", 0, "LAN-Native-Guest-Probe");
        client.Connect(server.EndPoint);
    }

    private void StopClient()
    {
        if (client != null)
        {
            if (client.Running) client.Stop();
            NativeReplicationLifecycle.Untrack(client.ReplicationSystem);
            client.Dispose(); client = null;
        }
        if (clientAuth != null) { NativeLanAuthentication.Remove(clientAuth); clientAuth = null; }
        clientRegistry?.Clear();
    }

    private void Advance(Stage next)
    {
        stage = next; deadline = Time.unscaledTime + 12;
        MelonLoader.MelonLogger.Msg("Native replication QA: " + next);
    }

    internal void Tick()
    {
        if (Finished) return;
        try
        {
            // Native client callbacks also flush acknowledgements. An extra flush is safe;
            // the authoritative system has no acknowledgement queue and must not be flushed.
            if (server != null) { bootstrap.Tick(server); server.ReplicationSystem.Replicate(); }
            client?.ReplicationSystem.Flush();
            bool connected = client != null && client.CurrentConnectionStatus == NetConnectionStatus.Connected &&
                client.ConnectionsCount == 1 && server.ConnectionsCount == 1;
            switch (stage)
            {
                case Stage.Reject:
                    if (client.CurrentConnectionStatus == NetConnectionStatus.Connected || server.ConnectionsCount != 0)
                        throw new InvalidOperationException("A client with the wrong private token was admitted.");
                    if (NativeLanAuthentication.Rejected > rejectedAtStart && client.CurrentConnectionStatus == NetConnectionStatus.Disconnected)
                    {
                        if (clientRegistry.entries.Count != 0) throw new InvalidOperationException("Rejected guest received native objects.");
                        rejectedWrongToken = true;
                        StopClient(); StartClient(token); Advance(Stage.Connect);
                    }
                    break;
                case Stage.Connect:
                    if (connected) Advance(Stage.Create);
                    break;
                case Stage.Create:
                    if (Matches(false, false))
                    {
                        created = true;
                        hostReceiver = CreateReceiver(server, serverRegistry, actors[0].Object, "native host receiver");
                        clientRegistry.TryGetObject(actors[0].Key, out var guestObject);
                        guestReceiver = CreateReceiver(client, clientRegistry, guestObject.Cast<ActorSyncNetObject>(), "native guest receiver");
                        clientRegistry.TryGetObject(actors[2].Key, out var untargetedObject);
                        guestUntargetedReceiver = CreateReceiver(client, clientRegistry, untargetedObject.Cast<ActorSyncNetObject>(), "native untargeted receiver");
                        if (withAssets)
                        {
                            guestVisual = new NativeClassAnimator(1);
                            var visuals = guestReceiver.gameObject.AddComponent<Il2Cpp.ActorVisuals>();
                            visuals.animationManager = guestVisual.Driver; visuals.actorSync = guestReceiver;
                            guestReceiver.actorVisuals = visuals;
                            guestVisual.Animator.Rebind(); guestVisual.Animator.Update(0);
                            guestVisual.Driver.scaleMoveSpeed(1);
                        }
                        server.Send(new Il2CppLE.Networking.Generated.ActorSync.MessageSyncScale(actors[0].Key, 1750),
                            NetDeliveryMethod.ReliableOrdered, MessageReceiver.All, false, 0);
                        server.Send(new Il2CppLE.Networking.Generated.ActorSync.MessageSyncPositionSyncType(actors[0].Key, 1),
                            NetDeliveryMethod.ReliableOrdered, MessageReceiver.All, false, 0);
                        Advance(Stage.HostRpc);
                    }
                    break;
                case Stage.HostRpc:
                    if (guestReceiver.transform.localScale == Vector3.one * 1.75f &&
                        guestReceiver.positionSyncType == Il2Cpp.AbilitySync.PositionSyncType.RelativeToCreator)
                    {
                        if (hostReceiver.transform.localScale != Vector3.one || guestUntargetedReceiver.transform.localScale != Vector3.one)
                            throw new InvalidOperationException("Native RPC changed an untargeted Actor.");
                        nativeHostToGuestRpc = true; rpcTargetIsolation = true;
                        if (withAssets)
                        {
                            server.Send(new Il2CppLE.Networking.Generated.ActorSync.MessageStartMoving(actors[0].Key),
                                NetDeliveryMethod.ReliableOrdered, MessageReceiver.All, false, 0);
                            Advance(Stage.MovementRpc);
                        }
                        else BeginGuestRpc();
                    }
                    break;
                case Stage.MovementRpc:
                    if (guestVisual.Animator.GetBool(Il2Cpp.AnimationManagerWithAnimator.animationHashMove))
                    {
                        nativeMovementRpc = true;
                        server.Send(new Il2CppLE.Networking.Generated.ActorSync.MessageStopMoving(actors[0].Key), NetDeliveryMethod.ReliableOrdered, MessageReceiver.All, false, 0);
                        server.Send(new Il2CppLE.Networking.Generated.ActorSync.MessageStartAttacking(actors[0].Key, 0, 1), NetDeliveryMethod.ReliableOrdered, MessageReceiver.All, false, 0);
                        Advance(Stage.AttackRpc);
                    }
                    break;
                case Stage.AttackRpc:
                    if (guestVisual.Driver.Cast<Il2Cpp.ComplexAnimationManager>().IsAttacking)
                    {
                        if (guestVisual.Animator.GetBool(Il2Cpp.AnimationManagerWithAnimator.animationHashMove))
                            throw new InvalidOperationException("Native stop movement RPC failed.");
                        for (int i = 0; i < 3; i++) guestVisual.Animator.Update(0.1f);
                        nativeAttackClip = NativeClassAnimator.DominantClip(guestVisual.Animator);
                        if (nativeAttackClip == "") throw new InvalidOperationException("Native attack RPC produced no animation clip.");
                        nativeAttackRpc = true;
                        server.Send(new Il2CppLE.Networking.Generated.ActorSync.MessageStopAttacking(actors[0].Key), NetDeliveryMethod.ReliableOrdered, MessageReceiver.All, false, 0);
                        Advance(Stage.StopAnimationRpc);
                    }
                    break;
                case Stage.StopAnimationRpc:
                    if (!guestVisual.Driver.Cast<Il2Cpp.ComplexAnimationManager>().IsAttacking) BeginGuestRpc();
                    break;
                case Stage.GuestRpc:
                    if (hostReceiver.transform.localScale == Vector3.one * 2.25f)
                    {
                        if (guestReceiver.transform.localScale != Vector3.one * 1.75f)
                            throw new InvalidOperationException("Guest RPC changed its local receiver instead of the host.");
                        nativeGuestToHostRpc = true;
                        for (int i = 0; i < actors.Count; i++)
                        { actors[i].Object.position = UpdatedPosition(i); actors[i].Object.healthRatio = UpdatedHealth(i); }
                        Advance(Stage.Update);
                    }
                    break;
                case Stage.Update:
                    if (Matches(true, false))
                    {
                        updated = true;
                        for (int i = 1; i < actors.Count; i += 2) serverRegistry.Destroy(actors[i].Key);
                        Advance(Stage.RemoveHalf);
                    }
                    break;
                case Stage.RemoveHalf:
                    if (Matches(true, true)) { StopClient(); Advance(Stage.Disconnect); }
                    break;
                case Stage.Disconnect:
                    if (server.ConnectionsCount == 0)
                    { disconnected = true; StartClient(token); Advance(Stage.Reconnect); }
                    break;
                case Stage.Reconnect:
                    if (connected) Advance(Stage.Baseline);
                    break;
                case Stage.Baseline:
                    if (Matches(true, true))
                    {
                        lateJoined = true;
                        for (int i = 0; i < actors.Count; i += 2)
                        { actors[i].Object.position = UpdatedPosition(i) + Vector3.one * 7; actors[i].Object.healthRatio = 180; }
                        Advance(Stage.ReconnectUpdate);
                    }
                    break;
                case Stage.ReconnectUpdate:
                    if (Matches(true, true, true))
                    {
                        reconnectUpdates = true;
                        for (int i = 0; i < actors.Count; i += 2) serverRegistry.Destroy(actors[i].Key);
                        Advance(Stage.Destroy);
                    }
                    break;
                case Stage.Destroy:
                    if (clientRegistry.entries.Count == 0)
                    { destroyed = true; settleAt = Time.unscaledTime + 2; Advance(Stage.Settle); }
                    break;
                case Stage.Settle:
                    if (clientRegistry.entries.Count != 0) throw new InvalidOperationException("Destroyed native object reappeared.");
                    if (!connected) throw new InvalidOperationException("Native connection dropped during teardown settling.");
                    if (Time.unscaledTime >= settleAt)
                    { Dispose(); settleAt = Time.unscaledTime + 1; Advance(Stage.Cleanup); }
                    break;
                case Stage.Cleanup:
                    if (Time.unscaledTime >= settleAt)
                        Result = new { passed = true, rejectedWrongToken, authenticatedSessions = NativeLanAuthentication.Accepted - acceptedAtStart,
                            objectCount = ObjectCount, nativeObjectCreated = created, nativeObjectUpdated = updated,
                            nativeObjectDestroyed = destroyed, disconnectObservedByHost = disconnected,
                            reconnectBaselineCorrect = lateJoined, destroyedObjectsNotResurrected = lateJoined,
                            reconnectUpdates, nativeHostToGuestRpc, nativeGuestToHostRpc, rpcTargetIsolation,
                            nativeMovementRpc, nativeAttackRpc, nativeAttackClip, animationAssetsTested = withAssets,
                            scope = "Native UDP, 64 object lifecycles/reconnect, generated ActorSync RPCs and optional class animation; no campaign Actor, skill VFX or combat validation" };
                    break;
            }
            if (!Finished && Time.unscaledTime >= deadline)
                Fail(new TimeoutException($"Native probe stage {stage}: client {client?.CurrentConnectionStatus}, server connections {server?.ConnectionsCount}, guest objects {clientRegistry?.entries.Count}; {NativeLanAuthentication.LastAuthError}"));
        }
        catch (Exception ex) { Fail(ex); }
    }

    private Il2Cpp.ActorSync CreateReceiver(BaseNetworkService network, NetObjectRegistry registry, ActorSyncNetObject obj, string name)
    {
        var root = new GameObject("LAN QA " + name); root.SetActive(false); receiverRoots.Add(root);
        var receiver = root.AddComponent<Il2Cpp.ActorSync>(); receiver.CreateVisuals = false;
        NativeActorBinding.Attach(network, registry, obj, receiver);
        return receiver;
    }

    private void BeginGuestRpc()
    {
        client.Send(new Il2CppLE.Networking.Generated.ActorSync.MessageSyncScale(actors[0].Key, 2250), NetDeliveryMethod.ReliableOrdered, MessageReceiver.All, false, 0);
        Advance(Stage.GuestRpc);
    }

    private bool Matches(bool updatedState, bool halfRemoved, bool afterReconnect = false)
    {
        if (clientRegistry.Pointer == serverRegistry.Pointer) throw new InvalidOperationException("Guest registry aliases host.");
        if (clientRegistry.entries.Count != (halfRemoved ? ObjectCount / 2 : ObjectCount)) return false;
        for (int i = 0; i < actors.Count; i++)
        {
            bool present = clientRegistry.TryGetObject(actors[i].Key, out var received);
            if (halfRemoved && i % 2 == 1) { if (present) return false; continue; }
            if (!present) return false;
            var replica = received.Cast<ActorSyncNetObject>();
            if (replica.Pointer == actors[i].Object.Pointer) throw new InvalidOperationException("Guest object aliases host.");
            replica.SnapInterpolations();
            if (replica.position != (afterReconnect ? UpdatedPosition(i) + Vector3.one * 7 : updatedState ? UpdatedPosition(i) : InitialPosition(i)) ||
                replica.healthRatio != (afterReconnect ? (byte)180 : updatedState ? UpdatedHealth(i) : (byte)200) ||
                replica.MasterAccountId != "LAN-Probe-" + i) return false;
        }
        return true;
    }
    private static Vector3 InitialPosition(int i) => new(i * 2, 3, i * -3);
    private static Vector3 UpdatedPosition(int i) => new(i * -4, 9, i * 5);
    private static byte UpdatedHealth(int i) => (byte)(80 + i);
    private void Fail(Exception ex)
    {
        Result = new { passed = false, error = ex.ToString(), stage = stage.ToString(), scope = "native LAN replication lifecycle probe" };
        try { Dispose(); } catch (Exception cleanup) { MelonLoader.MelonLogger.Error(cleanup.ToString()); }
    }
    public void Dispose()
    {
        StopClient();
        if (server != null) { if (server.Running) server.Stop(); NativeReplicationLifecycle.Untrack(server.ReplicationSystem); server.Dispose(); server = null; }
        NativeLanAuthentication.Remove(serverAuth);
        foreach (var root in receiverRoots) if (root != null) UnityEngine.Object.Destroy(root);
        receiverRoots.Clear();
        guestVisual?.Dispose(); guestVisual = null;
    }
}
