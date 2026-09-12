using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace Hitwicket.DoTweenTimeline
{
    [AddComponentMenu("Hitwicket/DoTween Timeline")]
    public class DoTweenTimeline : MonoBehaviour
    {
        public float timelineDuration = 5f;

        // SerializeReference stores each concrete DoTweenBlock subclass.
        // SubclassSelector is our inspector type picker (not Odin).
        [SerializeReference, SubclassSelector]
        public List<DoTweenBlock> blocks = new List<DoTweenBlock>();

        private Sequence currentSequence;

        private void Start()
        {
            // Optionally play on awake if needed, or wait for manual trigger.
            // Play();
        }

        public void Play()
        {
            if (currentSequence != null) currentSequence.Kill();
            currentSequence = GenerateSequence();
            currentSequence.Play();
        }

        public Sequence GenerateSequence()
        {
            Sequence seq = DOTween.Sequence();
            foreach (var block in blocks)
            {
                if (block == null) continue;
                
                Tween t = block.GenerateTween();
                if (t != null)
                {
                    seq.Insert(block.startTime, t);
                }
            }
            return seq;
        }

        private void OnDestroy()
        {
            if (currentSequence != null) currentSequence.Kill();
        }
    }
}
