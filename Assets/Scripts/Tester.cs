using System;
using System.Collections.Generic;
using DG.Tweening;
using DoTweenTimeline.Runtime.Block;
using DoTweenTimeline.Runtime.Block.Transform;
using DoTweenTimeline.Runtime.SequenceBuilder;
using DoTweenTimeline.Runtime.SequenceRunner;
using UnityEngine;

namespace DoTweenTimeline.Scripts {
    public class Tester: MonoBehaviour {
        [SerializeField] private DoLocalMoveBlock moveBlock;
        
        private Sequence _currentSequence;

        private void Start() {
            TestTweenAction();
        }

        [ContextMenu("Play Tween Sequence")]
        public void TestTweenAction() {
            var builder = new SequenceBuilder();
            var sequencer = new SequenceRunner();
            var seq = builder.CreateSequence(new List<TweenBlock>() { moveBlock });
            sequencer.Setup(seq);
            sequencer.Play();
        }
        
    }
}