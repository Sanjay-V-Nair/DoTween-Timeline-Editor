using UnityEditor;
using UnityEngine;
using Timeline = DoTweenTimeline.Runtime.DoTweenTimeline;

namespace DoTweenTimeline.Editor {
    /// <summary>Temporary test UI for the edit-mode preview. The timeline window will drive TimelinePreview the same way.</summary>
    [CustomEditor(typeof(Timeline))]
    public class DoTweenTimelineInspector : UnityEditor.Editor {

        private void OnEnable() => TimelinePreview.Changed += Repaint;
        private void OnDisable() => TimelinePreview.Changed -= Repaint;

        public override void OnInspectorGUI() {
            var timeline = (Timeline)target;
            var previewing = TimelinePreview.IsActive && TimelinePreview.Target == timeline;

            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            // Built sequence is stale once blocks change, so rebuild it.
            if (EditorGUI.EndChangeCheck() && previewing) {
                TimelinePreview.Rebuild();
                previewing = TimelinePreview.IsActive && TimelinePreview.Target == timeline;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Edit-Mode Preview", EditorStyles.boldLabel);

            if (Application.isPlaying) {
                EditorGUILayout.HelpBox("Preview is edit-mode only. Use Play in Play mode.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            if(GUILayout.Button("Open Timeline Editor")) {
                DoTweenTimelineEditorWindow.Open(timeline);
            }
            
            if (!previewing) {
                if (GUILayout.Button("Start Preview")) TimelinePreview.Begin(timeline);
                return;
            }
            
            
            EditorGUI.BeginChangeCheck();
            var time = EditorGUILayout.Slider("Time", TimelinePreview.Time, 0f, TimelinePreview.Duration);
            if (EditorGUI.EndChangeCheck()) {
                TimelinePreview.Pause();
                TimelinePreview.Scrub(time);
            }

            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button(TimelinePreview.IsPlaying ? "Pause" : "Play")) {
                    if (TimelinePreview.IsPlaying) TimelinePreview.Pause();
                    else TimelinePreview.Play();
                }
                if (GUILayout.Button("Stop")) TimelinePreview.Stop();
            }
        }
    }
}
