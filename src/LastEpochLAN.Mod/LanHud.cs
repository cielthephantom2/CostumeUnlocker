using LastEpochLAN.Core;
using UnityEngine;

namespace LastEpochLAN.Mod;

internal static class LanHud
{
    private static readonly RecentEnemyDamage damage = new();
    private static WorldState cachedWorld;
    internal static void Reset() { damage.Reset(); cachedWorld = null; }
    internal static void Draw(LanSession session, string scene, WorldState shared = null)
    {
        if (session == null || session.IsHost || !session.Connected || scene == "" || Event.current.type != EventType.Repaint) return;
        var color = GUI.color;
        var camera = Camera.main;
        if (camera == null) return;
        var info = session.WorldInfo;
        if (shared != null) cachedWorld = shared;
        else if (cachedWorld == null || cachedWorld.Epoch != info.Epoch || cachedWorld.Revision != info.Revision) cachedWorld = session.World;
        var world = cachedWorld;
        damage.Observe(world, Time.unscaledTime);
        if (!session.IsHost && world.Scene == scene)
            foreach (var enemy in world.Enemies.Where(enemy => !enemy.Dead && damage.IsVisible(enemy.Id, Time.unscaledTime)).Take(128))
            {
                var point = camera.WorldToScreenPoint(new Vector3(enemy.X, enemy.Y + 2.3f, enemy.Z));
                if (point.z <= 0 || point.x < 0 || point.x > Screen.width || point.y < 0 || point.y > Screen.height) continue;
                var rect = new Rect(point.x - 35, Screen.height - point.y, 70, 6);
                GUI.color = Color.black; GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = new Color(0.8f, 0.08f, 0.05f, 1);
                GUI.DrawTexture(new Rect(rect.x + 1, rect.y + 1, 68 * Mathf.Clamp01(enemy.Health / enemy.MaxHealth), 4), Texture2D.whiteTexture);
            }
        GUI.color = color;
        GUI.color = color;
    }
}
