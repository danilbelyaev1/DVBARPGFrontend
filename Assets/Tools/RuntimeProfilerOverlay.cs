using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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

    public sealed class RuntimeProfilerOverlay : MonoBehaviour
    {
        [SerializeField] private bool visible = true;
        [SerializeField] private int maxLines = 12;
        [SerializeField] private KeyCode toggleKey = KeyCode.F3;

        private readonly List<KeyValuePair<string, RuntimeProfiler.Stat>> _top = new();
        private float _lastMemMb;
        private float _memDeltaMb;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Spawn()
        {
            var go = new GameObject("RuntimeProfilerOverlay");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.DontSave;
            go.AddComponent<RuntimeProfilerOverlay>();
        }

        private void Update()
        {
            if (IsTogglePressed())
            {
                visible = !visible;
            }

            var memMb = (float)GC.GetTotalMemory(false) / (1024f * 1024f);
            _memDeltaMb = memMb - _lastMemMb;
            _lastMemMb = memMb;
        }

        private bool IsTogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            return toggleKey switch
            {
                KeyCode.F3 => keyboard.f3Key.wasPressedThisFrame,
                KeyCode.F2 => keyboard.f2Key.wasPressedThisFrame,
                KeyCode.F4 => keyboard.f4Key.wasPressedThisFrame,
                KeyCode.F5 => keyboard.f5Key.wasPressedThisFrame,
                KeyCode.F6 => keyboard.f6Key.wasPressedThisFrame,
                KeyCode.F7 => keyboard.f7Key.wasPressedThisFrame,
                KeyCode.F8 => keyboard.f8Key.wasPressedThisFrame,
                KeyCode.F9 => keyboard.f9Key.wasPressedThisFrame,
                KeyCode.F10 => keyboard.f10Key.wasPressedThisFrame,
                KeyCode.F11 => keyboard.f11Key.wasPressedThisFrame,
                KeyCode.F12 => keyboard.f12Key.wasPressedThisFrame,
                _ => false
            };
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(toggleKey);
#else
            return false;
#endif
        }

        private void OnGUI()
        {
            if (!visible) return;

            RuntimeProfiler.GetTop(maxLines, _top);

            GUILayout.BeginArea(new Rect(10, 10, 520, 600), GUI.skin.box);
            GUILayout.Label($"FPS: {1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f):0.0}  (dt {Time.unscaledDeltaTime * 1000f:0.0} ms)");
            GUILayout.Label($"GC: {_lastMemMb:0.0} MB (delta {_memDeltaMb:+0.00;-0.00;0.00} MB)");
            GUILayout.Label($"VSync: {QualitySettings.vSyncCount}  TargetFPS: {Application.targetFrameRate}");
            GUILayout.Space(6);

            for (int i = 0; i < _top.Count; i++)
            {
                var kv = _top[i];
                var stat = kv.Value;
                GUILayout.Label($"{kv.Key}: avg {stat.AvgMs:0.000} ms  frame {stat.FrameMs:0.000} ms  calls {stat.FrameCalls}");
            }

            GUILayout.EndArea();
        }
    }
}
