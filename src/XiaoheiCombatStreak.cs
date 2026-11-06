namespace RiftSurvivor
{
    // Only personal pistol shots advance this sequence. A seeker can repair one
    // specific miss, even if another pistol round hit while it was in flight.
    internal sealed class XiaoheiStreak
    {
        private int hits, beforeMiss;
        private float deadline = float.NegativeInfinity;
        internal uint MissId { get; private set; }

        internal bool Hit() => AddHits(1);

        internal void Miss(float now)
        {
            MissId++;
            beforeMiss = hits;
            hits = 0;
            deadline = now + 1.5f;
        }

        internal bool Repair(uint missId, float now)
        {
            if (missId != MissId || now > deadline) return false;
            deadline = float.NegativeInfinity;
            return AddHits(beforeMiss + 1);
        }

        private bool AddHits(int amount)
        {
            hits += amount;
            if (hits < 3) return false;
            hits -= 3;
            return true;
        }
    }
}
