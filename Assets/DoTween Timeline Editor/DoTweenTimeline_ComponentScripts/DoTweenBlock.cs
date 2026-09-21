using UnityEngine;
using DG.Tweening;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hitwicket.DoTweenTimeline
{
    [Serializable]
    public abstract class DoTweenBlock {
        public bool isExpanded = false; // Used by custom editor to expand/collapse block details
        public string blockName = "Tween";
        public float startTime = 0f;
        public float duration = 1f;
        public Ease easeType = Ease.OutQuad;
        public GameObject target;

        public abstract Tween GenerateTween();

#if UNITY_EDITOR
        // This allows drawing custom properties inline in the timeline block UI or inspector
        public virtual void DrawInlineProperties() {
            blockName = EditorGUILayout.TextField("Name", blockName);
            target = (GameObject)EditorGUILayout.ObjectField("Target", target, typeof(GameObject), true);
            easeType = (Ease)EditorGUILayout.EnumPopup("Ease", easeType);
            startTime = EditorGUILayout.FloatField("Start Time", startTime);
            duration = EditorGUILayout.FloatField("Duration", duration);
            
            // Ensure values are sensible
            startTime = Mathf.Max(0, startTime);
            duration = Mathf.Max(0.01f, duration);
        }
#endif
    }
}
