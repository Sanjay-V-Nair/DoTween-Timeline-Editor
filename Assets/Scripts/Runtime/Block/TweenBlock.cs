using System;
using DG.Tweening;
using UnityEngine;

namespace DoTweenTimeline.Runtime.Block {
    [Serializable]
    public abstract class TweenBlock {
        public string name;
        public float startTime = 0f;
        public float duration = 0f;
        public int track;
        public Ease easeType = Ease.Linear;
        public GameObject gameObject;
        public AnimationCurve curve;
        
        public abstract Tween GenerateTween();
        
        public virtual void ThrowError(string errorMessage) {
            Debug.LogError($"TweenBlock Error Occured: {errorMessage} !");
        }
        
    }
}