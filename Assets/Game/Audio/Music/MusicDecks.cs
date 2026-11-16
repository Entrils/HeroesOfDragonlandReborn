using System;

namespace HeroesOfDragonland.Audio
{
    // Two fixed decks. Mid-fade reversal reuses a live deck; a third track waits for the
    // current fade to finish, so replacing a clip never cuts an audible deck abruptly.
    public sealed class MusicDecks<T> where T : class
    {
        private readonly T[] clips = new T[2];
        private readonly bool[] loops = new bool[2];
        private readonly float[] gains = new float[2], from = new float[2];
        private readonly int[] revisions = new int[2];
        private T pending;
        private bool pendingLoop, hasPending;
        private float pendingSeconds, seconds, elapsed;
        private int target = -1;
        public bool Fading { get; private set; }
        public T Clip(int deck) { return clips[deck]; }
        public bool Loop(int deck) { return loops[deck]; }
        public float Gain(int deck) { return gains[deck]; }
        public int Revision(int deck) { return revisions[deck]; }
        public void Request(T clip, bool loop, float fadeSeconds)
        {
            fadeSeconds = float.IsNaN(fadeSeconds) || float.IsInfinity(fadeSeconds) ? 1 : Math.Max(.05f, Math.Min(10, fadeSeconds));
            int existing = -1;
            if (clip != null) for (int i = 0; i < 2; i++) if (ReferenceEquals(clips[i], clip)) existing = i;
            if (existing >= 0) loops[existing] = loop;
            if (Fading && clip != null && existing < 0)
            { pending = clip; pendingLoop = loop; pendingSeconds = fadeSeconds; hasPending = true; return; }
            hasPending = false; pending = null;
            if ((clip == null && target == -1) || (existing >= 0 && target == existing)) return;
            if (clip != null && existing < 0)
            {
                existing = target == 0 ? 1 : 0;
                clips[existing] = clip; loops[existing] = loop; revisions[existing]++;
            }
            target = clip == null ? -1 : existing;
            from[0] = gains[0]; from[1] = gains[1]; elapsed = 0; seconds = fadeSeconds; Fading = true;
        }
        public void Tick(float delta)
        {
            if (!Fading || float.IsNaN(delta) || float.IsInfinity(delta)) return;
            elapsed += Math.Max(0, delta); float t = Math.Min(1, elapsed / seconds);
            for (int i = 0; i < 2; i++) gains[i] = from[i] + ((i == target ? 1 : 0) - from[i]) * t;
            if (t < 1) return;
            Fading = false;
            for (int i = 0; i < 2; i++) if (i != target) { clips[i] = null; gains[i] = 0; }
            if (hasPending)
            {
                var next = pending; bool loop = pendingLoop; float fade = pendingSeconds;
                hasPending = false; pending = null; Request(next, loop, fade);
            }
        }
        public void Reset()
        {
            for (int i = 0; i < 2; i++) { clips[i] = null; gains[i] = from[i] = 0; }
            target = -1; pending = null; hasPending = Fading = false;
        }
    }
}
