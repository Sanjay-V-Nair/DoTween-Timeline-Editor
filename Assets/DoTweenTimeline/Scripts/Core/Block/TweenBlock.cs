using System;
using DG.Tweening;
using UnityEditor;
using UnityEngine;

namespace DoTweenTimeline.Scripts.Core.Block {
    [Serializable]
    public abstract class TweenBlock {
        public string name;
        public float startTime = 0f;
        public float duration = 0f;
        public Ease easeType = Ease.Linear;
        public GameObject gameObject;
        
        public abstract Tween GenerateTween();
        
        #if UNITY_EDITOR
        public virtual void DrawInlineProperties() {
            name = EditorGUILayout.TextField("Name", name);
            startTime = EditorGUILayout.FloatField("Start Time", startTime);
            duration = EditorGUILayout.FloatField("Duration", duration);
            easeType = (Ease)EditorGUILayout.EnumPopup("Ease", easeType);
            gameObject = EditorGUILayout.ObjectField("GameObject", gameObject, typeof(GameObject), true) as GameObject;
        }
        #endif

        public virtual void ThrowError(string errorMessage) {
            Debug.LogError($"TweenBlock Error Occured: {errorMessage} !");
        }
        
    }
}