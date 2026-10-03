using DG.Tweening;

namespace DoTweenTimeline.DoTweenTimeline.Scripts.Core.SequenceRunner {
    public class SequenceRunner {
        
        private Sequence _currentSequence;
        
        public void Setup(Sequence sequence) {
            Reset();
            _currentSequence = sequence;
        }

        public void Play() {
            _currentSequence.Play();
        }

        public void Pause() {
            _currentSequence.Pause();
        }

        public void GoTo(float timePoint) {
            _currentSequence.Goto(timePoint);
        }
        
        public void Reset() {
            _currentSequence.Kill();
            _currentSequence = null;
        }
        
    }
}