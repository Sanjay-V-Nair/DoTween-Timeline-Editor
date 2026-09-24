using System;
using DG.Tweening;
using UnityEngine;

namespace DoTweenTimeline.Scripts.Core.Block {
    [Serializable]
    public class TweenBlock {
        public string name;
        public float duration = 0f;
        public Ease easeType = Ease.Linear;
        public GameObject gameObject;
    }
}