using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace DVBARPG.Tools
{
    public static class RuntimeProfiler
    {
        public sealed class Stat
        {
            public double AvgMs;
            public double FrameMs;
            public int FrameCalls;
            public int Frame;
        }

        private static readonly Dictionary<string, Stat> Stats = new();

        public readonly struct SampleScope : IDisposable
        {
            private readonly string _name;
            private readonly long _startTicks;

            public SampleScope(string name)
            {
                _name = name;
                _startTicks = Stopwatch.GetTimestamp();
            }

            public void Dispose()
            {
                var end = Stopwatch.GetTimestamp();
                var ms = (end - _startTicks) * 1000.0 / Stopwatch.Frequency;
                Record(_name, ms);
            }
        }

        public static SampleScope Sample(string name) => new SampleScope(name);

        private static void Record(string name, double ms)
        {
            var frame = Time.frameCount;
            if (!Stats.TryGetValue(name, out var stat))
            {
                stat = new Stat();
                Stats[name] = stat;
            }

            if (stat.Frame != frame)
            {
                stat.Frame = frame;
                stat.FrameMs = 0;
                stat.FrameCalls = 0;
            }

            stat.FrameMs += ms;
            stat.FrameCalls++;
            stat.AvgMs = stat.AvgMs <= 0 ? ms : (stat.AvgMs * 0.9 + ms * 0.1);
        }

        public static void GetTop(int max, List<KeyValuePair<string, Stat>> output)
        {
            output.Clear();
            foreach (var kv in Stats)
            {
                output.Add(kv);
            }

            output.Sort((a, b) => b.Value.AvgMs.CompareTo(a.Value.AvgMs));
            if (output.Count > max)
            {
                output.RemoveRange(max, output.Count - max);
            }
        }
    }
}
