using System.Collections.Generic;
using DG.Tweening;
using DoTweenTimeline.Runtime.Block;
using UnityEngine;

namespace DoTweenTimeline.Runtime.SequenceBuilder {
    public class SequenceBuilder {

        private Sequence _currentSequence;

        public Sequence CreateSequence(List<TweenBlock> blocks) {
            Reset();
            _currentSequence = DOTween.Sequence();
            AppendSequence(blocks);
            return _currentSequence;
        }
        
        public Sequence AppendSequence(List<TweenBlock> blocks) {
            if (blocks == null) return _currentSequence;
            foreach (var block in blocks) {
                var tween = block?.GenerateTween();
                if (tween == null) continue;
                if (tween.Duration() > 0) {
                    _currentSequence.Insert(Mathf.Max(0f, block.startTime), tween);
                }
                else {
                    tween.Kill(); // not inserted would otherwise run on its own
                }
            }
            return _currentSequence;
        }

        public bool AddCallback(TweenCallback callback, float t) {
            if (callback == null || _currentSequence == null || _currentSequence.Duration() < 0) return false;
            
            _currentSequence.InsertCallback(t, callback);
            return true;
        }

        public void Reset() {
            _currentSequence?.Kill();
            _currentSequence = null;
        }

    }
}