namespace DoTweenTimeline.Runtime.Timeline {
    public interface ITimeline {
        public void Play();
        public void Pause();
        public void Resume();
        public void SkipTo(float t, bool isPlay = true);
    }
}
