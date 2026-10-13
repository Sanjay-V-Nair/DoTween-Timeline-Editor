using System;
using DG.Tweening;
using UnityEngine;

namespace DoTweenTimeline.Runtime.SequenceRunner {

    public class SequenceRunner {
        
        private Sequence _currentSequence;
#if UNITY_EDITOR
        public static Action<string, string> OnShowWarningToast;
#endif

        private bool HasSequence => _currentSequence != null && _currentSequence.active;

        public float Elapsed => HasSequence ? _currentSequence.Elapsed() : 0f;

        public void Setup(Sequence sequence) {
            Reset();
            _currentSequence = sequence;
        }

        public void Play() {
            if (!HasSequence) {
#if UNITY_EDITOR
                OnShowWarningToast?.Invoke("Sequence Error", "No active sequence found!");
#endif
                Debug.LogError("Sequence Error: Sequence is Null!");
                return;
            }

            // A finished sequence ignores Play(), so start it over.
            if (_currentSequence.IsComplete()) _currentSequence.Restart();
            else _currentSequence.Play();
        }

        public bool IsSequenceRunning() {
            return HasSequence && _currentSequence.IsPlaying();
        }

        public void Pause() {
            if (!IsSequenceRunning()) return;
            _currentSequence.Pause();
        }
        
        public void OnPauseToggle() {
            if (HasSequence) _currentSequence.TogglePause();
        }

        public void Resume() {
            if (!HasSequence || _currentSequence.IsPlaying()) return;
            _currentSequence.Play();
        }

        public void SkipTo(float t, bool isPlay = true) {
            // Goto() on a sequence without tweens throws IndexOutOfRangeException.
            if (!HasSequence || _currentSequence.Duration() <= 0f) return;
            _currentSequence.Goto(t, isPlay);
        }
        
        public void Reset() {
            _currentSequence?.Kill();
            _currentSequence = null;
        }
        
    }
}
