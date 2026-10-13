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

        /// <summary>Logs and returns false when the block has no GameObject assigned.</summary>
        protected bool HasTarget() {
            if (gameObject != null) return true;
            ThrowError($"{GetType().Name} '{name}' has no GameObject");
            return false;
        }

        /// <summary>Finds the component this block animates. Logs and returns false when it is missing.</summary>
        protected bool TryGetTarget<T>(out T component) where T : class {
            component = null;
            if (!HasTarget()) return false;
            if (gameObject.TryGetComponent(out component)) return true;
            ThrowError($"{GetType().Name} '{name}': no {typeof(T).Name} on '{gameObject.name}'");
            return false;
        }

        /// <summary>
        /// Drives a 0..1 value through <paramref name="apply"/>. Used by blocks that compute their own motion
        /// (slerp, jump, path, spiral). Start values are explicit, so nothing depends on the object's state at build time.
        /// </summary>
        protected static Tweener Normalized(Action<float> apply, float duration) {
            return DOTween.To(() => 0f, x => apply(x), 1f, duration);
        }

        /// <summary>Applies the ease: the custom curve wins when it has keys, otherwise the ease enum.</summary>
        protected Tween Finish(Tween tween) {
            return curve != null && curve.length > 0 ? tween.SetEase(curve) : tween.SetEase(easeType);
        }
    }
}
