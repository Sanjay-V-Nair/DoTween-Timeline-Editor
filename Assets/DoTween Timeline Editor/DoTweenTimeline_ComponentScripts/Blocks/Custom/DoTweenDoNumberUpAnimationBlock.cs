using UnityEngine;
using DG.Tweening;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hitwicket.DoTweenTimeline.Custom
{
    [System.Serializable]
    public class DoTweenDoNumberUpAnimationBlock : DoTweenBlock
    {
        public TMPro.TMP_Text text;
        public int currentValue;
        public int targetValue;
        public string prefix;
        public string suffix;
        public float duration;
        public int delay;

        public DoTweenDoNumberUpAnimationBlock() { blockName = "DoNumberUpAnimation"; }

        public override Tween GenerateTween()
        {
            if (target == null || text == null) return null;

            int start = currentValue;
            DOTween.Kill(text);

            return DOTween.To(
                    () => start,
                    x => {
                        start = x;
                        currentValue = x;
                        text.text = prefix + x + suffix;
                    },
                    targetValue,
                    duration)
                .From(start, false)
                .SetEase(Ease.OutQuad)
                .SetTarget(text)
                .SetLink(text.gameObject);
        }

#if UNITY_EDITOR
        public override void DrawInlineProperties()
        {
            base.DrawInlineProperties();
            text = (TMPro.TMP_Text)EditorGUILayout.ObjectField("Text", text, typeof(TMPro.TMP_Text), true);
            currentValue = EditorGUILayout.IntField("Current Value", currentValue);
            targetValue = EditorGUILayout.IntField("Target Value", targetValue);
            prefix = EditorGUILayout.TextField("Prefix", prefix);
            suffix = EditorGUILayout.TextField("Suffix", suffix);
            duration = EditorGUILayout.FloatField("Duration", duration);
            delay = EditorGUILayout.IntField("Delay", delay);

        }
#endif
    }
}
