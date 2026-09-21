using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hitwicket.DoTweenTimeline
{
    [Serializable]
    public class DoTweenMoveBlock : DoTweenBlock
    {
        public Vector3 startValue;
        public Vector3 endValue;
        public bool isLocal = false;

        public DoTweenMoveBlock() { blockName = "Move"; }

        public override Tween GenerateTween()
        {
            if (target == null) return null;
            if (isLocal)
                return target.transform.DOLocalMove(endValue, duration).From(startValue, false).SetEase(easeType);
            else
                return target.transform.DOMove(endValue, duration).From(startValue, false).SetEase(easeType);
        }

#if UNITY_EDITOR
        public override void DrawInlineProperties()
        {
            base.DrawInlineProperties();
            isLocal = EditorGUILayout.Toggle("Local", isLocal);
            startValue = EditorGUILayout.Vector3Field("Start Value", startValue);
            endValue = EditorGUILayout.Vector3Field("End Value", endValue);
        }
#endif
    }

    [Serializable]
    public class DoTweenRotateBlock : DoTweenBlock
    {
        public Vector3 startValue;
        public Vector3 endValue;
        public bool isLocal = false;
        public RotateMode rotateMode = RotateMode.Fast;

        public DoTweenRotateBlock() { blockName = "Rotate"; }

        public override Tween GenerateTween()
        {
            if (target == null) return null;
            if (isLocal)
                return target.transform.DOLocalRotate(endValue, duration, rotateMode).From(startValue, false).SetEase(easeType);
            else
                return target.transform.DORotate(endValue, duration, rotateMode).From(startValue, false).SetEase(easeType);
        }

#if UNITY_EDITOR
        public override void DrawInlineProperties()
        {
            base.DrawInlineProperties();
            isLocal = EditorGUILayout.Toggle("Local", isLocal);
            rotateMode = (RotateMode)EditorGUILayout.EnumPopup("Rotate Mode", rotateMode);
            startValue = EditorGUILayout.Vector3Field("Start Value", startValue);
            endValue = EditorGUILayout.Vector3Field("End Value", endValue);
        }
#endif
    }

    [Serializable]
    public class DoTweenScaleBlock : DoTweenBlock
    {
        public Vector3 startValue = Vector3.one;
        public Vector3 endValue = Vector3.one;

        public DoTweenScaleBlock() { blockName = "Scale"; }

        public override Tween GenerateTween()
        {
            if (target == null) return null;
            return target.transform.DOScale(endValue, duration).From(startValue, false).SetEase(easeType);
        }

#if UNITY_EDITOR
        public override void DrawInlineProperties()
        {
            base.DrawInlineProperties();
            startValue = EditorGUILayout.Vector3Field("Start Scale", startValue);
            endValue = EditorGUILayout.Vector3Field("End Scale", endValue);
        }
#endif
    }

    [Serializable]
    public class DoTweenFadeBlock : DoTweenBlock
    {
        public float startValue = 1f;
        public float endValue = 0f;

        public DoTweenFadeBlock() { blockName = "Fade"; }

        public override Tween GenerateTween()
        {
            if (target == null) return null;

            var canvasGroup = target.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
                return canvasGroup.DOFade(endValue, duration).From(startValue, false).SetEase(easeType);

            var graphic = target.GetComponent<Graphic>();
            if (graphic != null)
                return graphic.DOFade(endValue, duration).From(startValue, false).SetEase(easeType);

            var spriteRenderer = target.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
                return spriteRenderer.DOFade(endValue, duration).From(startValue, false).SetEase(easeType);

            Debug.LogWarning($"[DoTweenTimeline] Block '{blockName}': Target {(target ? target.name : "Null")} doesn't have CanvasGroup, Graphic, or SpriteRenderer for Fade.");
            return null;
        }

#if UNITY_EDITOR
        public override void DrawInlineProperties()
        {
            base.DrawInlineProperties();
            startValue = EditorGUILayout.Slider("Start Fade", startValue, 0f, 1f);
            endValue = EditorGUILayout.Slider("End Fade", endValue, 0f, 1f);
        }
#endif
    }

    [Serializable]
    public class DoTweenColorBlock : DoTweenBlock
    {
        public Color startValue = Color.white;
        public Color endValue = Color.white;

        public DoTweenColorBlock() { blockName = "Color"; }

        public override Tween GenerateTween()
        {
            if (target == null) return null;

            var graphic = target.GetComponent<Graphic>();
            if (graphic != null)
                return graphic.DOColor(endValue, duration).From(startValue, false).SetEase(easeType);

            var spriteRenderer = target.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
                return spriteRenderer.DOColor(endValue, duration).From(startValue, false).SetEase(easeType);

            Debug.LogWarning($"[DoTweenTimeline] Block '{blockName}': Target {(target ? target.name : "Null")} doesn't have a Graphic or SpriteRenderer for Color.");
            return null;
        }

#if UNITY_EDITOR
        public override void DrawInlineProperties()
        {
            base.DrawInlineProperties();
            startValue = EditorGUILayout.ColorField("Start Color", startValue);
            endValue = EditorGUILayout.ColorField("End Color", endValue);
        }
#endif
    }

    [Serializable]
    public class DoTweenAnchorPosBlock : DoTweenBlock
    {
        public Vector2 startValue;
        public Vector2 endValue;

        public DoTweenAnchorPosBlock() { blockName = "Anchor Pos"; }

        public override Tween GenerateTween()
        {
            if (target == null) return null;

            var rectTransform = target.GetComponent<RectTransform>();
            if (rectTransform != null)
                return rectTransform.DOAnchorPos(endValue, duration).From(startValue, false).SetEase(easeType);

            Debug.LogWarning($"[DoTweenTimeline] Block '{blockName}': Target {(target ? target.name : "Null")} doesn't have a RectTransform for AnchorPos.");
            return null;
        }

#if UNITY_EDITOR
        public override void DrawInlineProperties()
        {
            base.DrawInlineProperties();
            startValue = EditorGUILayout.Vector2Field("Start Value", startValue);
            endValue = EditorGUILayout.Vector2Field("End Value", endValue);
        }
#endif
    }

    [Serializable]
    public class DoTweenSizeDeltaBlock : DoTweenBlock
    {
        public Vector2 startValue;
        public Vector2 endValue;

        public DoTweenSizeDeltaBlock() { blockName = "Size Delta"; }

        public override Tween GenerateTween()
        {
            if (target == null) return null;

            var rectTransform = target.GetComponent<RectTransform>();
            if (rectTransform != null)
                return rectTransform.DOSizeDelta(endValue, duration).From(startValue, false).SetEase(easeType);

            Debug.LogWarning($"[DoTweenTimeline] Block '{blockName}': Target {(target ? target.name : "Null")} doesn't have a RectTransform for SizeDelta.");
            return null;
        }

#if UNITY_EDITOR
        public override void DrawInlineProperties()
        {
            base.DrawInlineProperties();
            startValue = EditorGUILayout.Vector2Field("Start Size", startValue);
            endValue = EditorGUILayout.Vector2Field("End Size", endValue);
        }
#endif
    }

    [Serializable]
    public class DoTweenFillAmountBlock : DoTweenBlock
    {
        public float startValue = 0f;
        public float endValue = 1f;

        public DoTweenFillAmountBlock() { blockName = "Fill Amount"; }

        public override Tween GenerateTween()
        {
            if (target == null) return null;

            var image = target.GetComponent<Image>();
            if (image != null)
            {
                if (image.type != Image.Type.Filled)
                    image.type = Image.Type.Filled;

                return image.DOFillAmount(endValue, duration).From(startValue, false).SetEase(easeType);
            }

            Debug.LogWarning($"[DoTweenTimeline] Block '{blockName}': Target {(target ? target.name : "Null")} doesn't have an Image component for DOFillAmount.");
            return null;
        }

#if UNITY_EDITOR
        public override void DrawInlineProperties()
        {
            base.DrawInlineProperties();
            startValue = EditorGUILayout.Slider("Start Fill", startValue, 0f, 1f);
            endValue = EditorGUILayout.Slider("End Fill", endValue, 0f, 1f);
        }
#endif
    }

    [Serializable]
    public class DoTweenTextBlock : DoTweenBlock
    {
        public string startValue = "";
        public string endValue = "";
        public bool richTextEnabled = true;
        public ScrambleMode scrambleMode = ScrambleMode.None;

        public DoTweenTextBlock() { blockName = "Text"; }

        public override Tween GenerateTween()
        {
            if (target == null) return null;

            var text = target.GetComponent<Text>();
            if (text != null)
                return text.DOText(endValue, duration, richTextEnabled, scrambleMode).From(startValue, false).SetEase(easeType);

            Debug.LogWarning($"[DoTweenTimeline] Block '{blockName}': Target {(target ? target.name : "Null")} doesn't have a Text component.");
            return null;
        }

#if UNITY_EDITOR
        public override void DrawInlineProperties()
        {
            base.DrawInlineProperties();
            startValue = EditorGUILayout.TextField("Start Text", startValue);
            endValue = EditorGUILayout.TextField("End Text", endValue);
            richTextEnabled = EditorGUILayout.Toggle("Rich Text", richTextEnabled);
            scrambleMode = (ScrambleMode)EditorGUILayout.EnumPopup("Scramble", scrambleMode);
        }
#endif
    }

    [Serializable]
    public class DoTweenAnchorMinBlock : DoTweenBlock
    {
        public Vector2 startValue;
        public Vector2 endValue;

        public DoTweenAnchorMinBlock() { blockName = "Anchor Min"; }

        public override Tween GenerateTween()
        {
            if (target == null) return null;

            var rectTransform = target.GetComponent<RectTransform>();
            if (rectTransform != null)
                return rectTransform.DOAnchorMin(endValue, duration).From(startValue, false).SetEase(easeType);

            Debug.LogWarning($"[DoTweenTimeline] Block '{blockName}': Target {(target ? target.name : "Null")} doesn't have a RectTransform for AnchorMin.");
            return null;
        }

#if UNITY_EDITOR
        public override void DrawInlineProperties()
        {
            base.DrawInlineProperties();
            startValue = EditorGUILayout.Vector2Field("Start Anchor Min", startValue);
            endValue = EditorGUILayout.Vector2Field("End Anchor Min", endValue);
        }
#endif
    }

    [Serializable]
    public class DoTweenAnchorMaxBlock : DoTweenBlock
    {
        public Vector2 startValue;
        public Vector2 endValue;

        public DoTweenAnchorMaxBlock() { blockName = "Anchor Max"; }

        public override Tween GenerateTween()
        {
            if (target == null) return null;

            var rectTransform = target.GetComponent<RectTransform>();
            if (rectTransform != null)
                return rectTransform.DOAnchorMax(endValue, duration).From(startValue, false).SetEase(easeType);

            Debug.LogWarning($"[DoTweenTimeline] Block '{blockName}': Target {(target ? target.name : "Null")} doesn't have a RectTransform for AnchorMax.");
            return null;
        }

#if UNITY_EDITOR
        public override void DrawInlineProperties()
        {
            base.DrawInlineProperties();
            startValue = EditorGUILayout.Vector2Field("Start Anchor Max", startValue);
            endValue = EditorGUILayout.Vector2Field("End Anchor Max", endValue);
        }
#endif
    }

    [Serializable]
    public class DoTweenPivotBlock : DoTweenBlock
    {
        public Vector2 startValue;
        public Vector2 endValue;

        public DoTweenPivotBlock() { blockName = "Pivot"; }

        public override Tween GenerateTween()
        {
            if (target == null) return null;

            var rectTransform = target.GetComponent<RectTransform>();
            if (rectTransform != null)
                return rectTransform.DOPivot(endValue, duration).From(startValue, false).SetEase(easeType);

            Debug.LogWarning($"[DoTweenTimeline] Block '{blockName}': Target {(target ? target.name : "Null")} doesn't have a RectTransform for Pivot.");
            return null;
        }

#if UNITY_EDITOR
        public override void DrawInlineProperties()
        {
            base.DrawInlineProperties();
            startValue = EditorGUILayout.Vector2Field("Start Pivot", startValue);
            endValue = EditorGUILayout.Vector2Field("End Pivot", endValue);
        }
#endif
    }
}
