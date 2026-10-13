using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DoTweenTimeline.Runtime.Block.UI {

    #region RectTransform

    [Serializable]
    public abstract class Vector2RectBlock : TweenBlock {
        public Vector2 startValue;
        public Vector2 endValue;

        protected abstract void Apply(RectTransform target, Vector2 value);

        public override Tween GenerateTween() {
            if (!TryGetTarget(out RectTransform target)) return null;
            return Finish(DOTween.To(() => startValue, v => Apply(target, v), endValue, duration));
        }
    }

    [Serializable]
    public class DoAnchorPosBlock : Vector2RectBlock {
        protected override void Apply(RectTransform target, Vector2 value) => target.anchoredPosition = value;
    }

    [Serializable]
    public class DoSizeDeltaBlock : Vector2RectBlock {
        protected override void Apply(RectTransform target, Vector2 value) => target.sizeDelta = value;
    }

    [Serializable]
    public abstract class FloatRectBlock : TweenBlock {
        public float startValue;
        public float endValue;

        protected abstract void Apply(RectTransform target, float value);

        public override Tween GenerateTween() {
            if (!TryGetTarget(out RectTransform target)) return null;
            return Finish(DOTween.To(() => startValue, v => Apply(target, v), endValue, duration));
        }
    }

    [Serializable]
    public class DoAnchorPosXBlock : FloatRectBlock {
        protected override void Apply(RectTransform target, float value) {
            var p = target.anchoredPosition;
            p.x = value;
            target.anchoredPosition = p;
        }
    }

    [Serializable]
    public class DoAnchorPosYBlock : FloatRectBlock {
        protected override void Apply(RectTransform target, float value) {
            var p = target.anchoredPosition;
            p.y = value;
            target.anchoredPosition = p;
        }
    }

    #endregion

    #region Anchors and pivot

    [Serializable]
    public class DoAnchorMinBlock : Vector2RectBlock {
        protected override void Apply(RectTransform target, Vector2 value) => target.anchorMin = value;
    }

    [Serializable]
    public class DoAnchorMaxBlock : Vector2RectBlock {
        protected override void Apply(RectTransform target, Vector2 value) => target.anchorMax = value;
    }

    [Serializable]
    public class DoPivotBlock : Vector2RectBlock {
        public DoPivotBlock() {
            startValue = new Vector2(0.5f, 0.5f);
            endValue = new Vector2(0.5f, 0.5f);
        }

        protected override void Apply(RectTransform target, Vector2 value) => target.pivot = value;
    }

    [Serializable]
    public class DoPivotXBlock : FloatRectBlock {
        public DoPivotXBlock() {
            startValue = 0.5f;
            endValue = 0.5f;
        }

        protected override void Apply(RectTransform target, float value) {
            var p = target.pivot;
            p.x = value;
            target.pivot = p;
        }
    }

    [Serializable]
    public class DoPivotYBlock : FloatRectBlock {
        public DoPivotYBlock() {
            startValue = 0.5f;
            endValue = 0.5f;
        }

        protected override void Apply(RectTransform target, float value) {
            var p = target.pivot;
            p.y = value;
            target.pivot = p;
        }
    }

    [Serializable]
    public class DoAnchorPos3DBlock : TweenBlock {
        public Vector3 startValue;
        public Vector3 endValue;

        public override Tween GenerateTween() {
            if (!TryGetTarget(out RectTransform target)) return null;
            return Finish(DOTween.To(() => startValue, v => target.anchoredPosition3D = v, endValue, duration));
        }
    }

    [Serializable]
    public class DoAnchorPos3DXBlock : FloatRectBlock {
        protected override void Apply(RectTransform target, float value) {
            var p = target.anchoredPosition3D;
            p.x = value;
            target.anchoredPosition3D = p;
        }
    }

    [Serializable]
    public class DoAnchorPos3DYBlock : FloatRectBlock {
        protected override void Apply(RectTransform target, float value) {
            var p = target.anchoredPosition3D;
            p.y = value;
            target.anchoredPosition3D = p;
        }
    }

    [Serializable]
    public class DoAnchorPos3DZBlock : FloatRectBlock {
        protected override void Apply(RectTransform target, float value) {
            var p = target.anchoredPosition3D;
            p.z = value;
            target.anchoredPosition3D = p;
        }
    }

    /// <summary>Moves anchoredPosition from start to end with hops along Y.</summary>
    [Serializable]
    public class DoJumpAnchorPosBlock : TweenBlock {
        public Vector2 startValue;
        public Vector2 endValue;
        public float jumpPower = 50f;
        public int numJumps = 1;

        public override Tween GenerateTween() {
            if (!TryGetTarget(out RectTransform target)) return null;
            var jumps = Mathf.Max(1, numJumps);
            return Finish(Normalized(f => {
                var arc = Mathf.Repeat(f * jumps, 1f); // 0 at each landing, so the last jump ends on the ground
                var height = jumpPower * 4f * arc * (1f - arc);
                target.anchoredPosition = Vector2.LerpUnclamped(startValue, endValue, f) + Vector2.up * height;
            }, duration));
        }
    }

    #endregion

    #region Image / Graphic

    [Serializable]
    public class DoFillAmountBlock : TweenBlock {
        [Range(0f, 1f)] public float startValue = 0f;
        [Range(0f, 1f)] public float endValue = 1f;

        public override Tween GenerateTween() {
            if (!TryGetTarget(out Image image)) return null;
            return Finish(DOTween.To(() => startValue, v => image.fillAmount = v, endValue, duration));
        }
    }

    /// <summary>Fades a CanvasGroup when there is one, otherwise the alpha of a Graphic.</summary>
    [Serializable]
    public class DoFadeBlock : TweenBlock {
        [Range(0f, 1f)] public float startAlpha = 0f;
        [Range(0f, 1f)] public float endAlpha = 1f;

        public override Tween GenerateTween() {
            if (!HasTarget()) return null;

            Action<float> setAlpha;
            if (gameObject.TryGetComponent(out CanvasGroup group)) {
                setAlpha = a => group.alpha = a;
            }
            else if (gameObject.TryGetComponent(out Graphic graphic)) {
                setAlpha = a => {
                    var c = graphic.color;
                    c.a = a;
                    graphic.color = c;
                };
            }
            else {
                ThrowError($"{nameof(DoFadeBlock)} '{name}': needs a CanvasGroup or Graphic on '{gameObject.name}'");
                return null;
            }

            return Finish(DOTween.To(() => startAlpha, v => setAlpha(v), endAlpha, duration));
        }
    }

    /// <summary>Colors a Graphic, or a SpriteRenderer when there is no Graphic.</summary>
    [Serializable]
    public class DoColorBlock : TweenBlock {
        public Color startColor = Color.white;
        public Color endColor = Color.white;

        public override Tween GenerateTween() {
            if (!HasTarget()) return null;

            Action<Color> setColor;
            if (gameObject.TryGetComponent(out Graphic graphic)) {
                setColor = c => graphic.color = c;
            }
            else if (gameObject.TryGetComponent(out SpriteRenderer sprite)) {
                setColor = c => sprite.color = c;
            }
            else {
                ThrowError($"{nameof(DoColorBlock)} '{name}': needs a Graphic or SpriteRenderer on '{gameObject.name}'");
                return null;
            }

            return Finish(DOTween.To(() => startColor, v => setColor(v), endColor, duration));
        }
    }

    /// <summary>Adds a color delta over time instead of setting an absolute color, so several can run on one Graphic.</summary>
    [Serializable]
    public class DoBlendableColorBlock : TweenBlock {
        public Color colorDelta = Color.clear;

        public override Tween GenerateTween() {
            if (!TryGetTarget(out Graphic graphic)) return null;

            var applied = Color.clear;
            return Finish(Normalized(f => {
                var total = colorDelta * f;
                graphic.color += total - applied;
                applied = total;
            }, duration));
        }
    }

    #endregion

    #region Text

    /// <summary>Typewriter reveal of 'endText' on a TMP_Text, or a legacy Text when there is no TMP component.</summary>
    [Serializable]
    public class DoTextBlock : TweenBlock {
        [TextArea] public string endText;

        public override Tween GenerateTween() {
            if (!HasTarget()) return null;

            Action<string> setText;
            if (gameObject.TryGetComponent(out TMP_Text tmp)) {
                setText = s => tmp.text = s;
            }
            else if (gameObject.TryGetComponent(out Text legacy)) {
                setText = s => legacy.text = s;
            }
            else {
                ThrowError($"{nameof(DoTextBlock)} '{name}': needs a TMP_Text or Text on '{gameObject.name}'");
                return null;
            }

            var full = endText ?? string.Empty;
            return Finish(Normalized(f => {
                var count = Mathf.Clamp(Mathf.RoundToInt(f * full.Length), 0, full.Length);
                setText(full.Substring(0, count));
            }, duration));
        }
    }

    #endregion
}
