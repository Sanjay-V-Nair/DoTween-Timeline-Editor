using System.Collections.Generic;
using DG.Tweening;
using DoTweenTimeline.Scripts.Core.Block;

namespace DoTweenTimeline.Scripts.Core.SequenceBuilder {
    public class SequenceBuilder {

        private Sequence _currentSequence;

        public Sequence CreateSequence(List<TweenBlock> blocks) {
            Reset();
            _currentSequence = DOTween.Sequence();
            AppendSequence(blocks);
            return _currentSequence;
        }
        
        public Sequence AppendSequence(List<TweenBlock> blocks) {
            foreach (var block in blocks) {
                var tween = block.GenerateTween();
                if (tween != null && tween.Duration() > 0) {
                    _currentSequence.Insert(block.startTime, tween);
                }
            }
            return _currentSequence;
        }

        public void Reset() {
            _currentSequence.Kill();
            _currentSequence = null;
        }

    }
}