#nullable enable annotations
#nullable disable warnings
// DPS overlay adapted from the user-supplied Mods/Mod.dll, Mod.Features.DamageMeterOverlay.
// Combat capture is offline-only and does not load the reference DLL or its other features.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Il2Cpp;
using UnityEngine;
using UnityEngine.SceneManagement;
using LastEpochLAN.Core;

namespace LastEpochLAN.Mod.OfflineTools.Dps;

internal static class DamageMeterOverlay
{
	private readonly struct RecentHit(float time, float damage)
	{
		public float Time { get; } = time;

		public float Damage { get; } = damage;
	}

	private sealed class SkillTotal
	{
		public string Label { get; set; } = string.Empty;

		public string AbilityId { get; set; } = string.Empty;

		public double Damage { get; set; }

		public int Hits { get; set; }

		public int Crits { get; set; }

		public int Kills { get; set; }

		public AT Tags { get; set; }

		public HitEvents HitEvents { get; set; }

		public DamageType? PrimaryDamageType { get; set; }

		public int[] DamageTypeCounts { get; } = new int[7];
	}

	private readonly struct StatCard(string label, string value, Color accent, Color valueColor)
	{
		public string Label { get; } = label;

		public string Value { get; } = value;

		public Color Accent { get; } = accent;

		public Color ValueColor { get; } = valueColor;
	}

    private const float PanelWidth = 340f;

	private const float PanelGap = 8f;

	private const float DefaultTopLeftX = 24f;

	private const float DefaultTopLeftY = 84f;

	private const float PanelPadding = 14f;

	private const float HeaderHeight = 42f;

	private const float StatCardHeight = 52f;

	private const float StatCardGap = 8f;

	private const float ListHeaderHeight = 16f;

	private const float SkillRowHeight = 34f;

	private const float SkillRowGap = 6f;

	private const float FooterPadding = 14f;

	private const float ResizeGripSize = 18f;

	private const float MapPollIntervalSeconds = 0.35f;

	private const float FixedRecentWindowSeconds = 5f;

	private const int MaxSkillRows = 8;

	private static readonly Regex s_bracketNoiseRegex = new Regex("\\[[^\\]]*\\]|\\([^\\)]*\\)", RegexOptions.Compiled);

	private static readonly Regex s_identifierSplitRegex = new Regex("(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])|[_\\-]+", RegexOptions.Compiled);

	private static readonly Regex s_whitespaceRegex = new Regex("\\s{2,}", RegexOptions.Compiled);

	private static readonly string[] s_genericLabels = new string[10] { "Unknown", "Attack", "Effect", "Triggered", "Skill", "Ability", "Hit", "Damage", "Projectile", "Source" };

	private static readonly string[] s_redundantSuffixes = new string[15]
	{
		"Clone", "Instance", "Triggered", "Trigger", "Holder", "Controller", "Wrapper", "Visual", "Visuals", "Object",
		"Attack", "Effect", "Effects", "Skill", "Skill Effect"
	};

	private static GUIStyle? s_titleStyle;

	private static GUIStyle? s_headerMetaStyle;

	private static GUIStyle? s_badgeStyle;

	private static GUIStyle? s_statLabelStyle;

	private static GUIStyle? s_statValueStyle;

	private static GUIStyle? s_listHeaderStyle;

	private static GUIStyle? s_skillNameStyle;

	private static GUIStyle? s_skillNamePrimaryStyle;

	private static GUIStyle? s_skillMetaStyle;

	private static GUIStyle? s_skillPercentStyle;

	private static GUIStyle? s_skillValueStyle;

	private static GUIStyle? s_emptyStateStyle;

	private static GUIStyle? s_tooltipTitleStyle;

	private static GUIStyle? s_tooltipBodyStyle;

	private static GUIStyle? s_tooltipMetaStyle;

	private static readonly GUIContent s_sharedContent = new GUIContent();

	private static readonly Queue<RecentHit> s_recentHits = new Queue<RecentHit>();

	private static readonly Dictionary<string, SkillTotal> s_skillTotals = new Dictionary<string, SkillTotal>();

	private static readonly List<SkillTotal> s_cachedTopSkills = new List<SkillTotal>(8);

	private static string? s_currentMapKey;

	private static string? s_currentMapLabel;

	private static PropertyInfo? s_zoneStatsAreaLevelProperty;

	private static PropertyInfo? s_tmpTextProperty;

	private static Type? s_cachedTmpTextType;

	private static float s_nextMapPollTime;

	private static float s_sessionStartTime;

	private static double s_totalDamage;

	private static double s_biggestHit;

	private static int s_hitsCaptured;

	private static bool s_isPaused;

	private static float s_pauseStartedTime;

	private static float s_attachedSectionHeight;

	private static int s_skillCacheVersion;

	private static int s_lastSkillCacheVersion = -1;

	private static int s_lastSkillCacheLimit = -1;

	private static bool s_isDragging;

	private static bool s_isResizing;

	private static bool s_shouldCaptureGameMouseInput;

	private static Vector2 s_dragOffset;

	private static Vector2 s_resizeStartMouse;

	private static float s_resizeStartScale;

	public static bool IsPaused => s_isPaused;

	internal static bool ShouldCaptureGameMouseInput => s_shouldCaptureGameMouseInput;
	internal static bool IsManipulating => s_isDragging || s_isResizing;
	internal static Rect LastDrawnBounds;
    internal static bool IsShown => DpsSettings.damageMeterEnabled && DpsSettings.damageMeterVisible;
    private static void HidePresentation()
    {LastDrawnBounds=default;s_shouldCaptureGameMouseInput=false;s_isDragging=false;s_isResizing=false;}
	internal static (double Total,int Hits,int Skills) Readout => (s_totalDamage,s_hitsCaptured,s_skillTotals.Count);
	private static List<StatCard>? cachedCards;
	private static float nextCardsAt;
	private static readonly Dictionary<(string,string,string,string,string),string> labelCache = new();

	public static void SetAttachedSectionHeight(float height)
	{
		s_attachedSectionHeight = Mathf.Max(0f, height);
	}

	public static bool TryGetAnchorRect(out Rect rect)
	{
		if (!CanProvideAnchorRect())
		{
			rect = default(Rect);
			return false;
		}
		rect = ResolvePanelRect(GetBasePanelHeight(BuildStatCards().Count, GetVisibleSkillRowCount()));
		return true;
	}

	public static bool TryGetRenderedAnchorRect(out Rect rect)
	{
		if (!CanProvideAnchorRect())
		{
			rect = default(Rect);
			return false;
		}
		Rect rect2 = ResolvePanelRect(GetBasePanelHeight(BuildStatCards().Count, GetVisibleSkillRowCount()));
		float num = Mathf.Clamp(DpsSettings.damageMeterScale, 0.5f, 2f);
		rect = new Rect(rect2.x, rect2.y, rect2.width * num, rect2.height * num);
		return true;
	}

	public static float GetAttachedSectionGap()
	{
		return 6f;
	}

	public static int GetTrackedSkillCount()
	{
		return s_skillTotals.Count;
	}

	public static int GetHitCount()
	{
		return s_hitsCaptured;
	}

	public static float GetStackBaseTopY()
	{
        return 16f + DpsSettings.TopOffset;
	}

	public static void OnSceneChanged()
	{
		s_currentMapKey = null;
		s_currentMapLabel = null;
		s_nextMapPollTime = 0f;
		s_isPaused = false;
		s_pauseStartedTime = 0f;
		s_attachedSectionHeight = 0f;
		s_isDragging = false;
		s_isResizing = false;
		s_shouldCaptureGameMouseInput = false;
		ResetCombatData();
		RefreshMapContext(force: true);
	}

	public static void OnUpdate()
	{
        if (!IsShown)
		{
            HidePresentation();
			return;
		}
		HandleHotkeys();
		if (!s_isPaused && (DpsSettings.damageMeterShowWhenIdle || s_hitsCaptured > 0 || s_recentHits.Count != 0))
		{
			RefreshMapContext(force: false);
			PruneRecentHits(Time.unscaledTime);
		}
	}

	public static void OnGUI()
	{
		Event current = Event.current;
        if (!IsShown)
		{
            HidePresentation();
		}
		else
		{
			if (!DpsSettings.damageMeterShowWhenIdle && s_hitsCaptured <= 0)
			{
				return;
			}
			EnsureStyles();
			if (DpsDrawing.lineTex == null || s_titleStyle == null || s_headerMetaStyle == null || s_badgeStyle == null || s_statLabelStyle == null || s_statValueStyle == null || s_listHeaderStyle == null || s_skillNameStyle == null || s_skillNamePrimaryStyle == null || s_skillMetaStyle == null || s_skillPercentStyle == null || s_skillValueStyle == null || s_emptyStateStyle == null)
			{
				return;
			}
			List<StatCard> list = BuildStatCards();
			List<SkillTotal> topSkills = GetTopSkills(DpsSettings.damageMeterSkillRows);
			int visibleSkillRowCount = GetVisibleSkillRowCount(topSkills.Count);
			float opacity = Mathf.Clamp(DpsSettings.damageMeterOpacity, 0.35f, 1f);
			float num = Mathf.Clamp(DpsSettings.damageMeterScale, 0.5f, 2f);
			if (s_isResizing && current.type != EventType.Layout)
			{
				float num2 = Mathf.Max(current.mousePosition.x - s_resizeStartMouse.x, current.mousePosition.y - s_resizeStartMouse.y);
				num = Mathf.Clamp(s_resizeStartScale + num2 / 320f, 0.5f, 2f);
			}
			Rect rect = ResolvePanelRect(GetBasePanelHeight(list.Count, visibleSkillRowCount), num);
			LastDrawnBounds = new Rect(rect.x, rect.y, rect.width * num, rect.height * num);
			Color accent = WithOpacity(new Color(0.35f, 0.78f, 0.48f, 1f), opacity);
			Vector2 vector = ResolveScaledMousePosition(rect.position, num);
            Rect rect2 = new Rect(rect.x + 14f, rect.y + 8f, rect.width - 28f, 26f);
			Rect resizeGripRect = new Rect(rect.xMax - 18f - 6f, rect.yMax - 18f - 6f, 18f, 18f);
			bool flag = resizeGripRect.Contains(vector);
			if (current.isMouse)
			{
				bool flag2 = rect.Contains(vector);
				s_shouldCaptureGameMouseInput = (s_isDragging || s_isResizing) | flag2;
			}
			switch (current.type)
			{
			case EventType.MouseDown:
				if ((current.button == 0) & flag)
				{
					s_isResizing = true;
					s_resizeStartMouse = current.mousePosition;
					s_resizeStartScale = DpsSettings.damageMeterScale;
					current.Use();
				}
				else if (current.button == 0 && rect2.Contains(vector))
				{
					s_isDragging = true;
					s_dragOffset = current.mousePosition - rect.position;
					current.Use();
				}
				break;
			case EventType.MouseDrag:
				if (current.button == 0 && s_isDragging)
				{
					float value = current.mousePosition.x - s_dragOffset.x;
					float value2 = current.mousePosition.y - s_dragOffset.y;
					float max = Mathf.Max(6f, (float)Screen.width - rect.width * num - 6f);
					float max2 = Mathf.Max(6f, (float)Screen.height - (rect.height * num + s_attachedSectionHeight) - 6f);
					value = Mathf.Clamp(value, 6f, max);
					value2 = Mathf.Clamp(value2, 6f, max2);
                    DpsSettings.damageMeterOffsetX = value - 16f;
                    DpsSettings.TopOffset = value2 - 16f;
					current.Use();
				}
				else if (current.button == 0 && s_isResizing)
				{
					float num3 = Mathf.Max(current.mousePosition.x - s_resizeStartMouse.x, current.mousePosition.y - s_resizeStartMouse.y);
					DpsSettings.damageMeterScale = Mathf.Clamp(s_resizeStartScale + num3 / 320f, 0.5f, 2f);
					current.Use();
				}
				break;
			case EventType.MouseUp:
				if (current.button == 0)
				{
					s_isDragging = false;
					s_isResizing = false;
				}
				break;
			}
			Matrix4x4 matrix = GUI.matrix;
			if (!Mathf.Approximately(num, 1f))
			{
				GUIUtility.ScaleAroundPivot(new Vector2(num, num), rect.position);
			}
			DrawStatCards(rect, opacity, list);
			Vector2 mousePosition = (DpsSettings.damageMeterShowRowTooltips ? ResolveScaledMousePosition(rect.position, num) : new Vector2(float.MinValue, float.MinValue));
			DrawSkillTable(rect, accent, opacity, mousePosition, list, topSkills, visibleSkillRowCount);
			DrawResizeGrip(resizeGripRect, flag, opacity);
			GUI.matrix = matrix;
		}
	}

	public static void NotifyHit(float damage, DamageMeterHitInfo hitInfo)
	{
		if (!s_isPaused && !(damage <= 0f) && !float.IsNaN(damage) && !float.IsInfinity(damage))
		{
			float unscaledTime = Time.unscaledTime;
            cachedCards=null;
			s_totalDamage += damage;
			s_biggestHit = Math.Max(s_biggestHit, damage);
			s_hitsCaptured++;
			s_recentHits.Enqueue(new RecentHit(unscaledTime, damage));
			PruneRecentHits(unscaledTime);
			var labelKey=(hitInfo.SourceName,hitInfo.AbilityName,hitInfo.PrimaryAbilityName,hitInfo.OriginalAbilityName,hitInfo.AbilityId);
			if(!labelCache.TryGetValue(labelKey,out string? label))
			{
				if(labelCache.Count>=512) labelCache.Clear();
				label=BuildSkillLabel(hitInfo);labelCache[labelKey]=label;
			}
			string key = BuildSkillKey(label, hitInfo.AbilityId);
			if (!s_skillTotals.TryGetValue(key, out SkillTotal value))
			{
				value = new SkillTotal
				{
					Label = label,
					AbilityId = (hitInfo.AbilityId ?? string.Empty)
				};
				s_skillTotals[key] = value;
			}
			value.Damage += damage;
			value.Hits++;
			AccumulateSkillMetadata(value, hitInfo);
			MarkSkillCacheDirty();
		}
	}

	public static void ResetCombatData()
	{
        HidePresentation();
		cachedCards=null;nextCardsAt=0;labelCache.Clear();
		s_sessionStartTime = Time.unscaledTime;
		s_totalDamage = 0.0;
		s_biggestHit = 0.0;
		s_hitsCaptured = 0;
		s_recentHits.Clear();
		s_skillTotals.Clear();
		MarkSkillCacheDirty();
	}

	public static void TogglePause()
	{
		if (!s_isPaused)
		{
			s_isPaused = true;
			s_pauseStartedTime = Time.unscaledTime;
			return;
		}
		float num = Mathf.Max(0f, Time.unscaledTime - s_pauseStartedTime);
		s_isPaused = false;
		s_pauseStartedTime = 0f;
		if (num <= 0f)
		{
			return;
		}
		s_sessionStartTime += num;
		s_nextMapPollTime += num;
		if (s_recentHits.Count != 0)
		{
			Queue<RecentHit> queue = new Queue<RecentHit>(s_recentHits.Count);
			while (s_recentHits.Count > 0)
			{
				RecentHit recentHit = s_recentHits.Dequeue();
				queue.Enqueue(new RecentHit(recentHit.Time + num, recentHit.Damage));
			}
			while (queue.Count > 0)
			{
				s_recentHits.Enqueue(queue.Dequeue());
			}
		}
	}

	private static void HandleHotkeys() { }

	private static void RefreshMapContext(bool force)
	{
		float unscaledTime = Time.unscaledTime;
		if (!force && unscaledTime < s_nextMapPollTime)
		{
			return;
		}
		s_nextMapPollTime = unscaledTime + 0.35f;
		string name = SceneManager.GetActiveScene().name;
		string text = ResolveMapLabel(name);
		string b = name + "|" + text;
		if (string.IsNullOrWhiteSpace(s_currentMapKey))
		{
			s_currentMapKey = b;
			s_currentMapLabel = text;
			return;
		}
		if (string.Equals(s_currentMapKey, b, StringComparison.Ordinal))
		{
			s_currentMapLabel = text;
			return;
		}
		s_currentMapKey = b;
		s_currentMapLabel = text;
		if (DpsSettings.damageMeterResetPerMap)
		{
			ResetCombatData();
		}
	}

	private static string ResolveMapLabel(string sceneName)
	{
		try
		{
			ZoneStatsUI instance = ZoneStatsUI.instance;
			string text = null;
			if (instance != null)
			{
				object tmpTextObject = ResolveZoneStatsTmpText(instance);
				text = ResolveTmpTextValue(tmpTextObject);
			}
			if (!string.IsNullOrWhiteSpace(text))
			{
				return text.Trim();
			}
		}
		catch
		{
		}
		return string.IsNullOrWhiteSpace(sceneName) ? "Unknown Area" : sceneName;
	}

	private static object? ResolveZoneStatsTmpText(ZoneStatsUI zoneStats)
	{
		if ((object)s_zoneStatsAreaLevelProperty == null)
		{
			s_zoneStatsAreaLevelProperty = ((object)zoneStats).GetType().GetProperty("areaLevelTMP", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		}
		return s_zoneStatsAreaLevelProperty?.GetValue(zoneStats);
	}

	private static string? ResolveTmpTextValue(object? tmpTextObject)
	{
		if (tmpTextObject == null)
		{
			return null;
		}
		Type type = tmpTextObject.GetType();
		if (s_cachedTmpTextType != type)
		{
			s_cachedTmpTextType = type;
			s_tmpTextProperty = type.GetProperty("text", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		}
		return s_tmpTextProperty?.GetValue(tmpTextObject) as string;
	}

	private static string GetMapLabel()
	{
		return string.IsNullOrWhiteSpace(s_currentMapLabel) ? "Unknown Area" : s_currentMapLabel;
	}

	private static List<SkillTotal> GetTopSkills(int maxRows)
	{
		maxRows = Math.Clamp(maxRows, 1, 8);
		if (s_lastSkillCacheVersion == s_skillCacheVersion && s_lastSkillCacheLimit == maxRows)
		{
			return s_cachedTopSkills;
		}
		s_cachedTopSkills.Clear();
		if (s_skillTotals.Count > 0)
		{
			foreach (SkillTotal item in s_skillTotals.Values.OrderByDescending((SkillTotal skill) => skill.Damage).ThenBy<SkillTotal, string>((SkillTotal skill) => skill.Label, StringComparer.OrdinalIgnoreCase).Take(maxRows))
			{
				s_cachedTopSkills.Add(item);
			}
		}
		s_lastSkillCacheVersion = s_skillCacheVersion;
		s_lastSkillCacheLimit = maxRows;
		return s_cachedTopSkills;
	}

	private static void EnsureStyles()
	{
		if (s_titleStyle == null)
		{
			Font font = DpsDrawing.StringStyle?.font;
			s_titleStyle = BuildStyle(13, FontStyle.Bold, TextAnchor.MiddleLeft);
			s_headerMetaStyle = BuildStyle(8, FontStyle.Bold, TextAnchor.MiddleLeft);
			s_badgeStyle = BuildStyle(8, FontStyle.Bold, TextAnchor.MiddleCenter);
			s_statLabelStyle = BuildStyle(8, FontStyle.Bold, TextAnchor.MiddleLeft);
			s_statValueStyle = BuildStyle(16, FontStyle.Bold, TextAnchor.MiddleLeft);
			s_listHeaderStyle = BuildStyle(8, FontStyle.Bold, TextAnchor.MiddleLeft);
			s_skillNameStyle = BuildStyle(10, FontStyle.Bold, TextAnchor.MiddleLeft);
			s_skillNamePrimaryStyle = BuildStyle(10, FontStyle.Bold, TextAnchor.MiddleLeft);
			s_skillMetaStyle = BuildStyle(8, FontStyle.Normal, TextAnchor.MiddleLeft);
			s_skillPercentStyle = BuildStyle(8, FontStyle.Bold, TextAnchor.UpperRight);
			s_skillValueStyle = BuildStyle(11, FontStyle.Bold, TextAnchor.LowerRight);
			s_emptyStateStyle = BuildStyle(9, FontStyle.Normal, TextAnchor.MiddleCenter);
			s_tooltipTitleStyle = BuildStyle(10, FontStyle.Bold, TextAnchor.UpperLeft);
			s_tooltipBodyStyle = BuildStyle(9, FontStyle.Normal, TextAnchor.UpperLeft);
			s_tooltipMetaStyle = BuildStyle(8, FontStyle.Bold, TextAnchor.UpperLeft);
			s_tooltipBodyStyle.wordWrap = true;
			if (!(font == null))
			{
				s_titleStyle.font = font;
				s_headerMetaStyle.font = font;
				s_badgeStyle.font = font;
				s_statLabelStyle.font = font;
				s_statValueStyle.font = font;
				s_listHeaderStyle.font = font;
				s_skillNameStyle.font = font;
				s_skillNamePrimaryStyle.font = font;
				s_skillMetaStyle.font = font;
				s_skillPercentStyle.font = font;
				s_skillValueStyle.font = font;
				s_emptyStateStyle.font = font;
				s_tooltipTitleStyle.font = font;
				s_tooltipBodyStyle.font = font;
				s_tooltipMetaStyle.font = font;
			}
		}
	}

	private static GUIStyle BuildStyle(int fontSize, FontStyle fontStyle, TextAnchor alignment)
	{
		return new GUIStyle(GUI.skin.label)
		{
			fontSize = fontSize,
			fontStyle = fontStyle,
			alignment = alignment,
			wordWrap = false,
			clipping = TextClipping.Clip,
			padding = new RectOffset(0, 0, 0, 0),
			margin = new RectOffset(0, 0, 0, 0)
		};
	}

	private static bool CanProvideAnchorRect()
	{
        return IsShown;
	}

	private static Rect ResolvePanelRect(float baseHeight)
	{
		float scale = Mathf.Clamp(DpsSettings.damageMeterScale, 0.5f, 2f);
		return ResolvePanelRect(baseHeight, scale);
	}

	private static Rect ResolvePanelRect(float baseHeight, float scale)
	{
        float num = PanelWidth * scale;
		float num2 = baseHeight * scale;
		float num3 = num2 + s_attachedSectionHeight;
        float x = 16f + Mathf.Clamp(DpsSettings.damageMeterOffsetX, -4000f, 4000f);
        float stackBaseTopY = 16f + Mathf.Clamp(DpsSettings.TopOffset, -4000f, 4000f);
        Rect result = new Rect(x, stackBaseTopY, PanelWidth, baseHeight);
		if (DpsMenu.ShouldCaptureGameInput)
		{
			Rect windowRect = DpsMenu.windowRect;
			if (new Rect(result.x, result.y, num, num3).Overlaps(windowRect))
			{
				result.y = windowRect.yMax + 10f;
			}
		}
		float max = Mathf.Max(6f, (float)Screen.width - num - 6f);
		float max2 = Mathf.Max(6f, (float)Screen.height - num3 - 6f);
		result.x = Mathf.Clamp(result.x, 6f, max);
		result.y = Mathf.Clamp(result.y, 6f, max2);
		return result;
	}

	private static float GetBasePanelHeight(int statCardCount, int skillRows)
	{
        return 8f + statCardCount*22f + 12f + 16f + 6f + skillRows*34f + Math.Max(0,skillRows-1)*6f + 12f;
	}

	private static int GetVisibleSkillRowCount()
	{
		return GetVisibleSkillRowCount(GetTopSkills(DpsSettings.damageMeterSkillRows).Count);
	}

	private static int GetVisibleSkillRowCount(int populatedSkillRows)
	{
		if (!DpsSettings.damageMeterShowSkillBreakdown)
		{
			return 1;
		}
		int value = Math.Max(1, populatedSkillRows);
		return Mathf.Clamp(value, 1, 8);
	}

	private static List<StatCard> BuildStatCards()
	{
		if(cachedCards!=null && Time.unscaledTime<nextCardsAt) return cachedCards;
		nextCardsAt=Time.unscaledTime+0.1f;
		List<StatCard> list = new List<StatCard>(5)
		{
            new StatCard(s_isPaused?"DPS (paused)":"DPS", FormatRollingDps(), new Color(0.96f, 0.52f, 0.17f, 1f), new Color(0.98f, 0.98f, 0.99f, 1f))
		};
		if (DpsSettings.damageMeterShowRecentFiveSecond)
		{
			list.Add(new StatCard("5S TOTAL", FormatCompactNumber(GetDamageWithinWindow(5f)), new Color(0.4f, 0.78f, 0.93f, 1f), new Color(0.9f, 0.97f, 0.99f, 1f)));
		}
        list.Add(new StatCard("TOTAL", FormatTotalDamage(), new Color(0.88f, 0.71f, 0.34f, 1f), new Color(0.98f, 0.96f, 0.89f, 1f)));
        if (DpsSettings.damageMeterShowBiggestHit)
        {
			list.Add(new StatCard("BIGGEST", (s_biggestHit <= 0.0) ? "--" : FormatCompactNumber(s_biggestHit), new Color(0.94f, 0.44f, 0.31f, 1f), new Color(1f, 0.93f, 0.9f, 1f)));
		}
		if (DpsSettings.damageMeterShowHitCount)
		{
			list.Add(new StatCard("HITS", FormatHits(), new Color(0.46f, 0.82f, 0.68f, 1f), new Color(0.91f, 0.98f, 0.95f, 1f)));
		}
		cachedCards=list;return list;
	}

	private static void DrawStatCards(Rect panelRect, float opacity, List<StatCard> cards)
    {
        for(int i=0;i<cards.Count;i++)DrawStatCard(new Rect(panelRect.x+14,panelRect.y+8+i*22,panelRect.width-28,22),cards[i],opacity);
    }

	private static void DrawStatCard(Rect rect, StatCard card, float opacity)
    {
        DrawTextWithShadow(new Rect(rect.x,rect.y,95,20),card.Label,s_statLabelStyle,WithOpacity(new Color(0.8f,0.84f,0.86f),opacity),0.5f*opacity);
        DrawText(new Rect(rect.x+100,rect.y,rect.width-100,20),card.Value,s_statValueStyle,WithOpacity(new Color(0.94f,0.96f,0.94f),opacity),TextAnchor.MiddleRight);
    }

	private static void DrawSkillTable(Rect panelRect, Color accent, float opacity, Vector2 mousePosition, List<StatCard> cards, List<SkillTotal> topSkills, int visibleRows)
	{
        float num2 = panelRect.y + 8f + cards.Count*22f + 12f;
		float num3 = panelRect.x + 14f;
		float num4 = panelRect.width - 28f;
		DrawText(new Rect(num3 + num4 - 106f, num2, 46f, 16f), "SHARE", s_listHeaderStyle, WithOpacity(new Color(0.58f, 0.63f, 0.69f, 0.78f), opacity), TextAnchor.UpperRight);
		DrawText(new Rect(num3 + num4 - 58f, num2, 58f, 16f), "TOTAL", s_listHeaderStyle, WithOpacity(new Color(0.58f, 0.63f, 0.69f, 0.78f), opacity), TextAnchor.UpperRight);
		if (!DpsSettings.damageMeterShowSkillBreakdown)
		{
			DrawText(new Rect(num3, num2 + 26f, num4, 34f), "Skill breakdown hidden in settings.", s_emptyStateStyle, WithOpacity(new Color(0.61f, 0.66f, 0.72f, 0.78f), opacity));
			return;
		}
		float num5 = num2 + 16f + 6f;
		double totalDamage = Math.Max(1.0, s_totalDamage);
		SkillTotal skillTotal = null;
		Rect anchorRect = default(Rect);
		for (int i = 0; i < visibleRows; i++)
		{
			Rect rect = new Rect(num3, num5 + (float)i * 40f, num4, 34f);
			if (i < topSkills.Count)
			{
				if (DrawSkillRow(rect, i, topSkills[i], totalDamage, accent, opacity, mousePosition))
				{
					skillTotal = topSkills[i];
					anchorRect = rect;
				}
			}
			else
			{
				DrawEmptyRow(rect, i, opacity);
			}
		}
		if (topSkills.Count == 0)
		{
			float height = (float)visibleRows * 34f + (float)Math.Max(0, visibleRows - 1) * 6f;
			DrawText(new Rect(num3 + 8f, num5, num4 - 16f, height), "No player-owned combat captured on this map yet.", s_emptyStateStyle, WithOpacity(new Color(0.6f, 0.65f, 0.71f, 0.82f), opacity));
		}
		if (DpsSettings.damageMeterShowRowTooltips && skillTotal != null)
		{
			DrawSkillTooltip(anchorRect, skillTotal, totalDamage, accent, opacity);
		}
	}

	private static bool DrawSkillRow(Rect rect, int index, SkillTotal skill, double totalDamage, Color accent, float opacity, Vector2 mousePosition)
	{
		double num = Math.Clamp(skill.Damage / totalDamage, 0.0, 1.0);
		float width = Mathf.Max(2f, (float)((double)rect.width * num));
		bool flag = index == 0;
		bool flag2 = rect.Contains(mousePosition);
		Color color = (flag2 ? new Color(1f, 1f, 1f, 0.048f) : ((index % 2 == 0) ? new Color(1f, 1f, 1f, 0.032f) : new Color(1f, 1f, 1f, 0.022f)));
		Color color2 = (flag ? new Color(accent.r, accent.g, accent.b, flag2 ? 0.26f : 0.2f) : new Color(accent.r, accent.g, accent.b, flag2 ? 0.16f : 0.12f));
		Color color3 = (flag ? new Color(0.98f, 0.98f, 0.99f, 0.99f) : new Color(0.91f, 0.94f, 0.97f, 0.95f));
		Color color4 = new Color(0.63f, 0.68f, 0.74f, 0.8f);
		Color color5 = new Color(0.92f,0.96f,0.93f,0.98f);
		DrawFilledRect(rect, WithOpacity(color, opacity));

		DrawFilledRect(new Rect(rect.x, rect.y, width, rect.height), WithOpacity(color2, opacity));
		DrawOutline(rect, WithOpacity(flag2 ? new Color(accent.r, accent.g, accent.b, 0.18f) : new Color(1f, 1f, 1f, 0.035f), opacity));
		if (flag)
		{
			DrawFilledRect(new Rect(rect.x, rect.y, 2f, rect.height), WithOpacity(new Color(accent.r, accent.g, accent.b, 0.95f), opacity));
		}
		float x = rect.x + 10f;
		float num2 = rect.width - 130f;
		float num3 = 44f;
		float num4 = 68f;
		string text = FitTextWithEllipsis(skill.Label, flag ? s_skillNamePrimaryStyle : s_skillNameStyle, num2);
		string text2 = FitTextWithEllipsis(BuildSkillMeta(skill), s_skillMetaStyle, num2);
		DrawTextWithShadow(new Rect(x, rect.y + 6f, num2, 11f), text, flag ? s_skillNamePrimaryStyle : s_skillNameStyle, WithOpacity(color3, opacity), 0.18f * opacity);
		DrawText(new Rect(x, rect.y + 19f, num2, 9f), text2, s_skillMetaStyle, WithOpacity(color4, opacity));
		DrawText(new Rect(rect.x + rect.width - num4 - num3 - 10f, rect.y + 6f, num3, 10f), FormatPercent(num), s_skillPercentStyle, WithOpacity(flag ? accent : new Color(accent.r, accent.g, accent.b, 0.8f), opacity), TextAnchor.UpperRight);
		DrawText(new Rect(rect.x + rect.width - num4 - 10f, rect.y + 13f, num4, 12f), FormatCompactNumber(skill.Damage), s_skillValueStyle, WithOpacity(color5, opacity), TextAnchor.LowerRight);
		return flag2;
	}

	private static void MarkSkillCacheDirty()
	{
		s_skillCacheVersion++;
	}

	private static void DrawEmptyRow(Rect rect, int index, float opacity)
	{
		Color color = ((index % 2 == 0) ? new Color(1f, 1f, 1f, 0.018f) : new Color(1f, 1f, 1f, 0.012f));
		DrawFilledRect(rect, WithOpacity(color, opacity));
		DrawOutline(rect, WithOpacity(new Color(1f, 1f, 1f, 0.02f), opacity));
	}

	private static double GetDamageWithinWindow(float seconds)
	{
		if (s_recentHits.Count == 0)
		{
			return 0.0;
		}
		float unscaledTime = Time.unscaledTime;
		double num = 0.0;
		foreach (RecentHit s_recentHit in s_recentHits)
		{
			if (unscaledTime - s_recentHit.Time <= seconds)
			{
				num += (double)s_recentHit.Damage;
			}
		}
		return num;
	}

	private static string FormatRollingDps()
	{
		if (s_hitsCaptured <= 0)
		{
			return "--";
		}
		double num = Math.Max(2.0, DpsSettings.damageMeterRollingWindowSeconds);
		double value = GetDamageWithinWindow((float)num) / num;
		return FormatCompactNumber(value);
	}

	private static string FormatTotalDamage()
	{
		return (s_hitsCaptured <= 0) ? "--" : FormatCompactNumber(s_totalDamage);
	}

	private static string FormatHits()
	{
		return (s_hitsCaptured <= 0) ? "--" : s_hitsCaptured.ToString("N0", CultureInfo.InvariantCulture);
	}

	private static string FormatCompactNumber(double value)
	{
		double num = Math.Abs(value);
		if (num >= 1000000000.0)
		{
			return (value / 1000000000.0).ToString("0.#", CultureInfo.InvariantCulture) + "B";
		}
		if (num >= 1000000.0)
		{
			return (value / 1000000.0).ToString("0.#", CultureInfo.InvariantCulture) + "M";
		}
		if (num >= 1000.0)
		{
			return (value / 1000.0).ToString("0.#", CultureInfo.InvariantCulture) + "K";
		}
		return Math.Round(value, 0).ToString("N0", CultureInfo.InvariantCulture);
	}

	private static string FormatPercent(double value)
	{
		double num = value * 100.0;
		return (num >= 10.0) ? (num.ToString("0.#", CultureInfo.InvariantCulture) + "%") : (num.ToString("0.0", CultureInfo.InvariantCulture) + "%");
	}

	private static string FormatDuration(float seconds)
	{
		TimeSpan timeSpan = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
		return (timeSpan.TotalMinutes >= 1.0) ? timeSpan.ToString("m\\:ss", CultureInfo.InvariantCulture) : timeSpan.ToString("s\\.s\\s", CultureInfo.InvariantCulture);
	}

	private static void AccumulateSkillMetadata(SkillTotal total, DamageMeterHitInfo hitInfo)
	{
		total.Tags |= hitInfo.Tags;
		total.HitEvents |= hitInfo.HitEvents;
		if ((hitInfo.HitEvents & (HitEvents.Crit | HitEvents.SuperCrit)) != HitEvents.None)
		{
			total.Crits++;
		}
		if ((hitInfo.HitEvents & HitEvents.Kill) != HitEvents.None)
		{
			total.Kills++;
		}
		if (hitInfo.PrimaryDamageType.HasValue)
		{
			int value = (int)hitInfo.PrimaryDamageType.Value;
			if (value >= 0 && value < total.DamageTypeCounts.Length)
			{
				total.DamageTypeCounts[value]++;
				total.PrimaryDamageType = ResolveDominantDamageType(total);
			}
		}
		else if (!total.PrimaryDamageType.HasValue)
		{
			total.PrimaryDamageType = ResolveDamageTypeFromTags(total.Tags);
		}
	}

	private static string BuildSkillKey(string label, string? abilityId)
	{
		if (!string.IsNullOrWhiteSpace(abilityId))
		{
			return "id:" + abilityId.Trim();
		}
		return "label:" + label;
	}

	private static string BuildSkillLabel(DamageMeterHitInfo hitInfo)
	{
		string text = CleanDisplayName(hitInfo.PrimaryAbilityName);
		string text2 = CleanDisplayName(hitInfo.OriginalAbilityName);
		string text3 = CleanDisplayName(hitInfo.AbilityName);
		string text4 = CleanDisplayName(hitInfo.SourceName);
		string text5 = CleanDisplayName(hitInfo.AbilityId);
		if (!string.IsNullOrWhiteSpace(text) && !IsGenericSkillLabel(text))
		{
			return text;
		}
		if (!string.IsNullOrWhiteSpace(text2) && !IsGenericSkillLabel(text2))
		{
			return text2;
		}
		if (!string.IsNullOrWhiteSpace(text3) && !IsGenericSkillLabel(text3))
		{
			if (!string.IsNullOrWhiteSpace(text4) && !IsGenericSkillLabel(text4) && !string.Equals(text3, text4, StringComparison.OrdinalIgnoreCase) && text4.Length > text3.Length && text4.Contains(text3, StringComparison.OrdinalIgnoreCase))
			{
				return text4;
			}
			return text3;
		}
		if (!string.IsNullOrWhiteSpace(text4) && !IsGenericSkillLabel(text4))
		{
			return text4;
		}
		if (!string.IsNullOrWhiteSpace(text3))
		{
			return text3;
		}
		if (!string.IsNullOrWhiteSpace(text4))
		{
			return text4;
		}
		if (!string.IsNullOrWhiteSpace(text5))
		{
			return text5;
		}
		return "Unknown Skill";
	}

	private static DamageType? ResolveDominantDamageType(SkillTotal total)
	{
		int num = 0;
		int num2 = -1;
		for (int i = 0; i < total.DamageTypeCounts.Length; i++)
		{
			if (total.DamageTypeCounts[i] > num)
			{
				num = total.DamageTypeCounts[i];
				num2 = i;
			}
		}
		return (num2 >= 0) ? new DamageType?((DamageType)num2) : ResolveDamageTypeFromTags(total.Tags);
	}

	private static DamageType? ResolveDamageTypeFromTags(AT tags)
	{
		if ((tags & AT.Fire) != AT.None)
		{
			return DamageType.FIRE;
		}
		if ((tags & AT.Cold) != AT.None)
		{
			return DamageType.COLD;
		}
		if ((tags & AT.Lightning) != AT.None)
		{
			return DamageType.LIGHTNING;
		}
		if ((tags & AT.Necrotic) != AT.None)
		{
			return DamageType.NECROTIC;
		}
		if ((tags & AT.Void) != AT.None)
		{
			return DamageType.VOID;
		}
		if ((tags & AT.Poison) != AT.None)
		{
			return DamageType.POISON;
		}
		if ((tags & AT.Physical) != AT.None)
		{
			return DamageType.PHYSICAL;
		}
		return null;
	}

	private static string CleanDisplayName(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
		{
			return string.Empty;
		}
		string text = raw.Trim();
		int num = text.IndexOfAny(new char[2] { '[', '(' });
		if (num > 0)
		{
			text = text.Substring(0, num);
		}
		text = s_bracketNoiseRegex.Replace(text, " ");
		text = text.Replace('/', ' ');
		text = s_identifierSplitRegex.Replace(text, " ");
		text = s_whitespaceRegex.Replace(text, " ").Trim();
		text = StripRedundantSuffixes(text);
		text = CollapseRepeatedWords(text);
		if (string.IsNullOrWhiteSpace(text))
		{
			return string.Empty;
		}
		return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text.ToLowerInvariant());
	}

	private static string StripRedundantSuffixes(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}
		string text = value.Trim();
		bool flag;
		do
		{
			flag = false;
			for (int i = 0; i < s_redundantSuffixes.Length; i++)
			{
				string text2 = s_redundantSuffixes[i];
				if (text.EndsWith(text2, StringComparison.OrdinalIgnoreCase))
				{
					string text3 = text;
					int length = text2.Length;
					string text4 = text3.Substring(0, text3.Length - length).TrimEnd();
					if (text4.Length >= 3)
					{
						text = text4;
						flag = true;
						break;
					}
				}
			}
		}
		while (flag);
		return text;
	}

	private static string CollapseRepeatedWords(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}
		string[] array = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (array.Length <= 1)
		{
			return value;
		}
		List<string> list = new List<string>(array.Length);
		for (int i = 0; i < array.Length; i++)
		{
			if (i <= 0 || !string.Equals(array[i], array[i - 1], StringComparison.OrdinalIgnoreCase))
			{
				list.Add(array[i]);
			}
		}
		return string.Join(" ", list);
	}

	private static bool IsGenericSkillLabel(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return true;
		}
		for (int i = 0; i < s_genericLabels.Length; i++)
		{
			if (string.Equals(value, s_genericLabels[i], StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	private static string BuildSkillMeta(SkillTotal skill)
	{
		List<string> list = new List<string>(6);
		string text = DescribeDamageType(skill.PrimaryDamageType);
		if (DpsSettings.damageMeterShowDamageTypeMeta && !string.IsNullOrEmpty(text))
		{
			list.Add(text);
		}
		if (DpsSettings.damageMeterShowSkillTagMeta)
		{
			AppendTagSummary(list, skill.Tags, skill.HitEvents);
		}
		if (DpsSettings.damageMeterShowCritMeta && skill.Crits > 0)
		{
			list.Add((skill.Crits == skill.Hits) ? "Crit" : (skill.Crits.ToString("N0", CultureInfo.InvariantCulture) + " crits"));
		}
		if (DpsSettings.damageMeterShowKillMeta && skill.Kills > 0)
		{
			list.Add((skill.Kills == 1) ? "1 kill" : (skill.Kills.ToString("N0", CultureInfo.InvariantCulture) + " kills"));
		}
		list.Add((skill.Hits == 1) ? "1 hit" : (skill.Hits.ToString("N0", CultureInfo.InvariantCulture) + " hits"));
		return string.Join(" Â· ", list);
	}

	private static void DrawSkillTooltip(Rect anchorRect, SkillTotal skill, double totalDamage, Color accent, float opacity)
	{
		if (s_tooltipTitleStyle != null && s_tooltipBodyStyle != null && s_tooltipMetaStyle != null)
		{
			string text = BuildSkillTooltipBody(skill, totalDamage);
			float num = 236f;
			float num2 = 14f;
			float num3 = Mathf.Max(34f, s_tooltipBodyStyle.CalcHeight(new GUIContent(text), num - 24f));
			Rect rect = ResolveTooltipRect(anchorRect, num, 16f + num2 + num3 + 18f);
			DrawShadow(rect, 2f, 0.18f * opacity);
			DrawShadow(rect, 6f, 0.08f * opacity);
			DrawFilledRect(rect, WithOpacity(new Color(0.04f, 0.05f, 0.06f, 0.96f), opacity));
			DrawVerticalGradient(new Rect(rect.x, rect.y, rect.width, 54f), WithOpacity(new Color(accent.r, accent.g, accent.b, 0.08f), opacity), WithOpacity(new Color(accent.r, accent.g, accent.b, 0f), opacity), 8);
			DrawOutline(rect, WithOpacity(new Color(1f, 1f, 1f, 0.06f), opacity));
			DrawFilledRect(new Rect(rect.x, rect.y, rect.width, 1.5f), WithOpacity(accent, opacity));
			float x = rect.x + 12f;
			float num4 = rect.y + 10f;
			DrawText(new Rect(x, num4 - 1f, 70f, 10f), "DETAILS", s_tooltipMetaStyle, WithOpacity(accent, opacity));
			DrawTextWithShadow(new Rect(x, num4 + 10f, rect.width - 24f, num2), FitTextWithEllipsis(skill.Label, s_tooltipTitleStyle, rect.width - 24f), s_tooltipTitleStyle, WithOpacity(new Color(0.97f, 0.98f, 0.99f, 0.98f), opacity), 0.22f * opacity);
			DrawText(new Rect(x, num4 + 26f, rect.width - 24f, num3), text, s_tooltipBodyStyle, WithOpacity(new Color(0.76f, 0.81f, 0.86f, 0.92f), opacity));
		}
	}

	private static Rect ResolveTooltipRect(Rect anchorRect, float width, float height)
	{
		float num = anchorRect.xMax + 10f;
		if (num + width > (float)Screen.width - 6f)
		{
			num = anchorRect.xMax - width;
		}
		float num2 = anchorRect.y;
		if (num2 + height > (float)Screen.height - 6f)
		{
			num2 = Mathf.Max(6f, (float)Screen.height - height - 6f);
		}
		return new Rect(Mathf.Max(6f, num), num2, width, height);
	}

	private static string BuildSkillTooltipBody(SkillTotal skill, double totalDamage)
	{
		List<string> list = new List<string>(7)
		{
			"Total Damage: " + FormatCompactNumber(skill.Damage),
			"Share: " + FormatPercent(Math.Clamp(skill.Damage / Math.Max(1.0, totalDamage), 0.0, 1.0)),
			"Hits: " + skill.Hits.ToString("N0", CultureInfo.InvariantCulture)
		};
		if (DpsSettings.damageMeterShowDamageTypeMeta)
		{
			string text = DescribeDamageType(skill.PrimaryDamageType);
			if (!string.IsNullOrEmpty(text))
			{
				list.Add("Dominant Type: " + text);
			}
		}
		if (DpsSettings.damageMeterShowSkillTagMeta)
		{
			string text2 = BuildSkillTagText(skill.Tags, skill.HitEvents);
			if (!string.IsNullOrEmpty(text2))
			{
				list.Add("Tags: " + text2);
			}
		}
		if (DpsSettings.damageMeterShowCritMeta && skill.Crits > 0)
		{
			list.Add("Crits: " + skill.Crits.ToString("N0", CultureInfo.InvariantCulture));
		}
		if (DpsSettings.damageMeterShowKillMeta && skill.Kills > 0)
		{
			list.Add("Kills: " + skill.Kills.ToString("N0", CultureInfo.InvariantCulture));
		}
		if (!string.IsNullOrWhiteSpace(skill.AbilityId))
		{
			list.Add("Ability ID: " + skill.AbilityId);
		}
		return string.Join("\n", list);
	}

	private static string BuildSkillTagText(AT tags, HitEvents hitEvents)
	{
		List<string> list = new List<string>(5);
		AppendTagSummary(list, tags, hitEvents);
		return string.Join(", ", list);
	}

	private static void AppendTagSummary(List<string> parts, AT tags, HitEvents hitEvents)
	{
		if ((tags & AT.Spell) != AT.None)
		{
			parts.Add("Spell");
		}
		else if ((tags & AT.Melee) != AT.None || (hitEvents & HitEvents.MeleeHit) != HitEvents.None)
		{
			parts.Add("Melee");
		}
		else if ((tags & AT.Throwing) != AT.None)
		{
			parts.Add("Throwing");
		}
		else if ((tags & AT.Bow) != AT.None)
		{
			parts.Add("Bow");
		}
		if ((tags & AT.DoT) != AT.None)
		{
			parts.Add("DoT");
		}
		else if ((tags & AT.Hit) != AT.None)
		{
			parts.Add("Hit");
		}
		if ((tags & AT.Minion) != AT.None)
		{
			parts.Add("Minion");
		}
		else if ((tags & AT.Totem) != AT.None)
		{
			parts.Add("Totem");
		}
	}

	private static string DescribeDamageType(DamageType? damageType)
	{
		if (!damageType.HasValue)
		{
			return string.Empty;
		}
		DamageType value = damageType.Value;
		if (1 == 0)
		{
		}
		string result = value switch
		{
			DamageType.PHYSICAL => "Physical",
			DamageType.FIRE => "Fire",
			DamageType.COLD => "Cold",
			DamageType.LIGHTNING => "Lightning",
			DamageType.NECROTIC => "Necrotic",
			DamageType.VOID => "Void",
			DamageType.POISON => "Poison",
			_ => string.Empty,
		};
		if (1 == 0)
		{
		}
		return result;
	}

	private static Vector2 ResolveScaledMousePosition(Vector2 pivot, float scale)
	{
		Vector2 mousePosition = Event.current.mousePosition;
		if (Mathf.Approximately(scale, 1f))
		{
			return mousePosition;
		}
		return pivot + (mousePosition - pivot) / scale;
	}

	private static string FitTextWithEllipsis(string value, GUIStyle style, float maxWidth)
	{
		if (string.IsNullOrEmpty(value))
		{
			return string.Empty;
		}
		s_sharedContent.text = value;
		if (style.CalcSize(s_sharedContent).x <= maxWidth)
		{
			return value;
		}
		string text = value;
		while (text.Length > 1)
		{
			string text2 = text;
			text = text2.Substring(0, text2.Length - 1);
			s_sharedContent.text = text + "...";
			if (style.CalcSize(s_sharedContent).x <= maxWidth)
			{
				return text + "...";
			}
		}
		return "...";
	}

	private static void DrawResizeGrip(Rect resizeGripRect, bool hovered, float opacity)
	{
		Color color = (hovered ? WithOpacity(new Color(0.65f, 0.75f, 0.68f, 0.7f), opacity) : WithOpacity(new Color(0.4f, 0.46f, 0.52f, 0.55f), opacity));
		float num = resizeGripRect.xMax - 3f;
		float num2 = resizeGripRect.yMax - 3f;
		for (int i = 0; i < 3; i++)
		{
			float num3 = (float)i * 4f;
			DrawFilledRect(new Rect(num - 8f - num3, num2 - 1f - num3, 6f, 1f), color);
			DrawFilledRect(new Rect(num - 1f - num3, num2 - 8f - num3, 1f, 6f), color);
		}
	}

	private static void PruneRecentHits(float now)
	{
		float num = Mathf.Max(5f, DpsSettings.damageMeterRollingWindowSeconds);
		while (s_recentHits.Count > 0 && !(now - s_recentHits.Peek().Time <= num))
		{
			s_recentHits.Dequeue();
		}
	}

	private static Color WithOpacity(Color color, float opacity)
	{
		color.a *= opacity;
		return color;
	}

	private static void DrawText(Rect rect, string text, GUIStyle style, Color color, TextAnchor? alignmentOverride = null)
	{
		TextAnchor alignment = style.alignment;
		Color textColor = style.normal.textColor;
		if (alignmentOverride.HasValue)
		{
			style.alignment = alignmentOverride.Value;
		}
		style.normal.textColor = color;
		s_sharedContent.text = text;
		GUI.Label(rect, s_sharedContent, style);
		style.normal.textColor = textColor;
		style.alignment = alignment;
	}

	private static void DrawTextWithShadow(Rect rect, string text, GUIStyle style, Color color, float shadowAlpha)
	{
		DrawText(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, style, new Color(0f, 0f, 0f, shadowAlpha), style.alignment);
		DrawText(rect, text, style, color, style.alignment);
	}

	private static void DrawFilledRect(Rect rect, Color color)
	{
		if (!(rect.width <= 0f) && !(rect.height <= 0f) && !(DpsDrawing.lineTex == null))
		{
			Color color2 = GUI.color;
			GUI.color = color;
			GUI.DrawTexture(rect, DpsDrawing.lineTex);
			GUI.color = color2;
		}
	}

	private static void DrawVerticalGradient(Rect rect, Color topColor, Color bottomColor, int steps)
	{
		if (steps <= 1 || rect.height <= 1f)
		{
			DrawFilledRect(rect, topColor);
			return;
		}
		float num = rect.height / (float)steps;
		for (int i = 0; i < steps; i++)
		{
			float t = (float)i / (float)(steps - 1);
			Color color = Color.Lerp(topColor, bottomColor, t);
			DrawFilledRect(new Rect(rect.x, rect.y + num * (float)i, rect.width, num + 1f), color);
		}
	}

	private static void DrawOutline(Rect rect, Color color)
	{
		DrawFilledRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
		DrawFilledRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
		DrawFilledRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
		DrawFilledRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
	}

	private static void DrawShadow(Rect rect, float spread, float alpha)
	{
		DrawFilledRect(new Rect(rect.x - spread, rect.y - spread, rect.width + spread * 2f, rect.height + spread * 2f), new Color(0f, 0f, 0f, alpha));
	}
}
