using System.Diagnostics;
using LastEpochLAN.Core;
using UnityEngine;

namespace LastEpochLAN.Mod;

internal enum PerformanceArea
{
    Toolkit, AutoAssist, AutoTargets, PlayerPublish, WorldSync, RemotePresentation,
    VisualCapture, VisualRead, DpsCapture, DpsGui, MapFog, EnemyDots, Objectives, Gui, ReportCapture
}

// Unity-thread measurements, including hooks outside Melon's OnUpdate. Nested
// areas are inclusive and must not be added together. No per-call heap objects.
internal static class RuntimePerformance
{
    private static readonly FrameTimingWindow[] windows = Enum.GetValues<PerformanceArea>().Select(_ => new FrameTimingWindow()).ToArray();
    internal static void BeginFrame() { foreach (var window in windows) window.Advance(Time.frameCount); }
    internal static Sample Measure(PerformanceArea area) => new(area);
    internal static object Read() => new { basis = "last 600 completed rendered frames; inclusive nested areas; main-thread allocations only",
        areas = Enum.GetValues<PerformanceArea>().Select(area => new { name = area.ToString(), calls = windows[(int)area].Calls,
            timing = windows[(int)area].Read() }).ToArray() };
    internal readonly struct Sample : IDisposable
    {
        private readonly PerformanceArea area;
        private readonly int frame;
        private readonly long started, allocated;
        internal Sample(PerformanceArea area)
        { this.area = area; frame = Time.frameCount; started = Stopwatch.GetTimestamp(); allocated = GC.GetAllocatedBytesForCurrentThread(); }
        public void Dispose() => windows[(int)area].Record(frame,
            (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency,
            Math.Max(0, GC.GetAllocatedBytesForCurrentThread() - allocated));
    }
}
