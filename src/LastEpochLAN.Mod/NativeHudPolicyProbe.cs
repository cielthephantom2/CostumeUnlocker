using System.Reflection;
using LastEpochLAN.Core;
using UnityEngine;

namespace LastEpochLAN.Mod;

internal sealed class NativeHudPolicyProbe
{
    private Type policy;
    private LanSession host, guest;
    private Task<LanSession> joining;
    private int stage;
    private float deadline;
    internal object Result { get; private set; }
    internal bool Finished => Result != null;
    internal void Tick()
    {
        if (Finished) return;
        try
        {
            policy ??= AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "LastEpoch_Hud")?.GetType("LastEpoch_Hud.Scripts.LanPolicy");
            if (policy == null || !(bool)policy.GetProperty("Ready", BindingFlags.Public | BindingFlags.Static).GetValue(null)) return;
            if (host == null)
            {
                host = LanSession.Host("HudHost", "HudQA", "HudQAOnly", 39036, System.Net.IPAddress.Loopback);
                joining = LanSession.JoinAsync("127.0.0.1", 39036, "HudGuest", "HudQA", "HudQAOnly");
                deadline = Time.unscaledTime + 30; return;
            }
            if (guest == null) { if (!joining.IsCompleted) return; guest = joining.GetAwaiter().GetResult(); }
            if (Time.unscaledTime > deadline) throw new TimeoutException("Native HUD authority stage " + stage);
            var qa = policy.GetMethod("NativeQa", BindingFlags.Public | BindingFlags.Static);
            if (stage == 0) { qa.Invoke(null, new object[] { host, guest, 0 }); stage = 1; return; }
            if (guest.HudRevision != host.HudRevision || guest.HudRevision == 0) return;
            Result = qa.Invoke(null, new object[] { host, guest, 1 });
            if (Result != null) { HudAuthority.Session = null; guest.Dispose(); host.Dispose(); }
        }
        catch (Exception ex) { Result = new { passed = false, stage, error = (ex.InnerException ?? ex).ToString() }; HudAuthority.Session = null; guest?.Dispose(); host?.Dispose(); }
    }
}
