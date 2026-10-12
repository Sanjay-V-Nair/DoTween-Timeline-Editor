using System;
using System.Collections.Generic;
using DG.Tweening;
using DoTweenTimeline.Runtime.Block;
using DoTweenTimeline.Runtime.Timeline;
using UnityEngine;

namespace DoTweenTimeline.Runtime {
    public class DoTweenTimeline : MonoBehaviour, ITimeline {
        
        [SerializeReference, SubclassSelector] private List<TweenBlock> tweens;
        
        private SequenceBuilder.SequenceBuilder _sequenceBuilder =  new ();
        private SequenceRunner.SequenceRunner _sequenceRunner = new ();
        private Sequence _currentSeq;

        private bool _isSequenceUpdated;
        
        public IReadOnlyList<TweenBlock> Blocks => tweens;
        public bool IsPlaying => _sequenceRunner.IsSequenceRunning();
        public float PlaybackTime => _sequenceRunner.Elapsed;

        // Any serialized change (inspector or timeline window) makes the built sequence stale.
        private void OnValidate() {
            InvalidateSequence();
        }

        public void InvalidateSequence() {
            ResetAll();
            _currentSeq = null;
        }

        /// <summary>Builds a standalone sequence the caller owns (edit-mode preview). Does not touch the runtime runner.</summary>
        public Sequence BuildPreviewSequence() {
            return new SequenceBuilder.SequenceBuilder().CreateSequence(tweens);
        }

        private void OnDestroy() {
            ResetAll();
        }

        [ContextMenu("Play Tween Sequence")]
        public void Play() {
            if(_sequenceRunner.IsSequenceRunning()) return;
            if(_currentSeq == null) UpdateSequence();
            _sequenceRunner.Play();
        }

        private void UpdateSequence() {
            _currentSeq = _sequenceBuilder.CreateSequence(tweens);
            _sequenceRunner.Setup(_currentSeq);
            _currentSeq.SetAutoKill(false);
            _currentSeq.SetLink(gameObject);
            _currentSeq.Pause(); // DOTween.Sequence() auto-plays; Play() starts it explicitly
        }
        
        public void OnPauseToggle() {
            _sequenceRunner?.OnPauseToggle();
        }

        [ContextMenu("Pause")]
        public void Pause() {
            _sequenceRunner?.Pause();
        }

        [ContextMenu("Resume")]
        public void Resume() {
            _sequenceRunner?.Play();
        }

        public void SkipTo(float t, bool isPlay = true) {
            if (_currentSeq == null) UpdateSequence();
            _sequenceRunner.SkipTo(t, isPlay);
        }

        public void ResetAll() {
            _sequenceBuilder?.Reset();
            _sequenceRunner?.Reset();
        }
    }
}