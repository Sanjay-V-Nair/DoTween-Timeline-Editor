using System;
using DG.Tweening;
using UnityEngine;

namespace DoTweenTimeline.Runtime.SequenceRunner {

    public class SequenceRunner {
        
        private Sequence _currentSequence;
#if UNITY_EDITOR
        public static Action<string, string> OnShowWarningToast;
#endif
        
        public void Setup(Sequence sequence) {
            Reset();
            _currentSequence = sequence;
        }

        public void Play() {
            if (_currentSequence == null) {
#if UNITY_EDITOR
                OnShowWarningToast?.Invoke("Sequence Error", "No active sequence found!");
#endif
                Debug.LogError($"Sequence Error: Sequence is Null!");
            }
            _currentSequence.Play();
        }

        public float Elapsed => _currentSequence != null && _currentSequence.active ? _currentSequence.Elapsed() : 0f;

        public bool IsSequenceRunning() {
            return _currentSequence?.IsPlaying() ?? false;
        }

        public void Pause() {
            if (!_currentSequence.IsPlaying()) return;
            _currentSequence?.Pause();
        }
        
        public void OnPauseToggle() {
            _currentSequence?.TogglePause();
        }

        public void Resume() {
            if (_currentSequence.IsPlaying()) return;
            _currentSequence?.Play();
        }

        public void SkipTo(float t, bool isPlay = true) {
            _currentSequence?.Goto(t, isPlay);
        }
        
        public void Reset() {
            _currentSequence?.Kill();
            _currentSequence = null;
        }
        
    }
}