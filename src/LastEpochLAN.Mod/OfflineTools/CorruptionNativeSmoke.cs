using Il2Cpp;

namespace LastEpochLAN.Mod.OfflineTools;

// QA-only: runs the actual patched native calculator on loaded timeline data.
// It never completes an echo, mutates a web or writes character progress.
internal static class CorruptionNativeSmoke
{
    internal static object Run()
    {
        var observations = new List<object>();
        bool uncapped = false, capped = false;
        foreach (var timeline in TimelineList.get().timelines)
        {
            if (timeline?.difficulties == null) continue;
            for (int index = 0; index < timeline.difficulties.Count; index++)
            {
                var difficulty = timeline.difficulties[index];
                if (difficulty == null) continue;
                if (!difficulty.hasMaxCorruption && !uncapped)
                {
                    int current = Math.Max(100, difficulty.minimumCorruption);
                    int total = EchoWeb.NewCorruptionForShadeEchoOfTier(timeline.timelineID,index,1,current,0,current,0,out int catchup,out int gaze);
                    int again = EchoWeb.NewCorruptionForShadeEchoOfTier(timeline.timelineID,index,1,current,0,current,0,out _,out _);
                    int withGaze = EchoWeb.NewCorruptionForShadeEchoOfTier(timeline.timelineID,index,1,current,2,current,0,out int catchup2,out int gaze2);
                    if (total != current+30 || again != total || catchup != 0 || gaze != 0 ||
                        withGaze != current+150 || catchup2 != 0 || gaze2 != 120)
                        throw new InvalidOperationException("Native Shade x5 calculator or Gaze breakdown mismatch.");
                    observations.Add(new { timeline=timeline.timelineID.ToString(),difficulty=index,current,total,withGaze,gaze2,repeatedPreview=again });
                    uncapped = true;
                }
                if (difficulty.hasMaxCorruption && difficulty.maximumCorruption > difficulty.minimumCorruption && !capped)
                {
                    int current = difficulty.maximumCorruption-1;
                    int total = EchoWeb.NewCorruptionForShadeEchoOfTier(timeline.timelineID,index,1,current,0,current,0,out _,out _);
                    if (total != difficulty.maximumCorruption) throw new InvalidOperationException("Native Shade timeline cap bypassed.");
                    observations.Add(new { timeline=timeline.timelineID.ToString(),difficulty=index,current,total,cap=difficulty.maximumCorruption });
                    capped = true;
                }
            }
            if (capped && uncapped) break;
        }
        if (!capped || !uncapped) throw new InvalidOperationException("Native Shade QA requires capped and uncapped timeline data.");
        return new { passed=true,observations,scope="Patched native calculator only; no boss kill or saved progression tested" };
    }
}
