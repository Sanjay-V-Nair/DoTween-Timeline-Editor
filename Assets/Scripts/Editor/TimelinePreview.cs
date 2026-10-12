using System;
using System.Collections.Generic;
using DG.DOTweenEditor;
using DG.Tweening;
using UnityEditor;
using UnityEngine;
using Timeline = DoTweenTimeline.Runtime.DoTweenTimeline;

namespace DoTweenTimeline.Editor {
    /// <summary>
    /// Edit-mode preview of one timeline. Static because DOTweenEditorPreview is global: only one preview runs at a time.
    /// Snapshots every component on the tweened objects before building, restores them on Stop.
    /// </summary>
    public static class TimelinePreview {
        private static Timeline _target;
        private static Sequence _sequence;
        private static readonly List<(Component component, string json)> Snapshot = new();
        private static double _lastTickTime;

        public static event Action Changed;

        public static bool IsActive => _sequence != null;
        public static bool IsPlaying { get; private set; }
        public static Timeline Target => _target;
        public static float Time { get; private set; }
        public static float Duration => _sequence?.Duration() ?? 0f;

        [InitializeOnLoadMethod]
        private static void Init() {
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.ExitingEditMode) Stop();
            };
        }

        public static bool Begin(Timeline target) {
            if (Application.isPlaying || target == null) return false;
            Stop();

            // Snapshot first: GenerateTween() can move objects while building.
            CaptureSnapshot(target);

            DOTweenEditorPreview.Start();
            try {
                _sequence = target.BuildPreviewSequence();
            }
            catch (Exception e) {
                Debug.LogException(e);
                DOTweenEditorPreview.Stop(false, true);
                RestoreSnapshot();
                return false;
            }
            // Goto() on a sequence without tweens throws IndexOutOfRangeException, so refuse to preview it.
            if (_sequence == null || _sequence.Duration() <= 0f) {
                _sequence?.Kill();
                _sequence = null;
                DOTweenEditorPreview.Stop(false, true);
                RestoreSnapshot();
                return false;
            }
            DOTweenEditorPreview.PrepareTweenForPreview(_sequence, true, true, false);
            _target = target;
            Time = 0f;
            Scrub(0f);
            return true;
        }

        /// <summary>Rebuilds the preview sequence after blocks changed, keeping time and play state.</summary>
        public static void Rebuild() {
            if (!IsActive) return;
            var target = _target;
            var time = Time;
            var wasPlaying = IsPlaying;
            Stop();
            if (!Begin(target)) return;
            Scrub(time);
            if (wasPlaying) Play();
        }

        public static void Scrub(float time) {
            if (!IsActive) return;
            Time = Mathf.Clamp(time, 0f, Duration);
            _sequence.Goto(Time, false);
            SceneView.RepaintAll();
            Changed?.Invoke();
        }

        public static void Play() {
            if (!IsActive || IsPlaying) return;
            if (Time >= Duration) Scrub(0f);
            IsPlaying = true;
            _lastTickTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            Changed?.Invoke();
        }

        public static void Pause() {
            if (!IsPlaying) return;
            IsPlaying = false;
            EditorApplication.update -= Tick;
            Changed?.Invoke();
        }

        public static void Stop() {
            if (!IsActive) return;
            Pause();

            // Rewinding is handled by the snapshot, so don't ask DOTween to reset targets.
            DOTweenEditorPreview.Stop(false, true);
            _sequence.Kill();
            _sequence = null;
            RestoreSnapshot();

            _target = null;
            Time = 0f;
            SceneView.RepaintAll();
            Changed?.Invoke();
        }

        private static void Tick() {
            var now = EditorApplication.timeSinceStartup;
            var delta = (float)(now - _lastTickTime);
            _lastTickTime = now;

            Scrub(Time + delta);
            if (Time >= Duration) Pause();
        }

        private static void CaptureSnapshot(Timeline target) {
            Snapshot.Clear();
            if (target.Blocks == null) return;

            var seen = new HashSet<GameObject>();
            foreach (var block in target.Blocks) {
                if (block?.gameObject == null || !seen.Add(block.gameObject)) continue;
                foreach (var component in block.gameObject.GetComponents<Component>()) {
                    // Skip the timeline itself so block edits made mid-preview are not reverted.
                    if (component == null || component is Timeline) continue;
                    Snapshot.Add((component, EditorJsonUtility.ToJson(component)));
                }
            }
        }

        private static void RestoreSnapshot() {
            foreach (var (component, json) in Snapshot) {
                if (component != null) EditorJsonUtility.FromJsonOverwrite(json, component);
            }
            Snapshot.Clear();
        }
    }
}
