using System;

namespace RiftSurvivor
{
    // Host-side cap on how fast a remote owner's primary may resolve. State packets
    // travel a reliable, ordered channel, so one retransmission holds back every shot
    // behind it and they all land in the same host frame. At high attack speed that
    // backlog is many shots, and each one the cap refuses is a shot the guest already
    // saw and heard. The allowance is therefore measured in seconds of network stall.
    internal struct RemoteCadence
    {
        internal const double Allowance = .35;
        // Sustained fire never sits below the cap, so without slack a host hitch
        // would use up allowance that could not refill until the guest stopped firing.
        private const double SustainedSlack = .9;

        private double next;

        internal bool TryAccept(double now, double interval)
        {
            if (next > now + Allowance) return false;
            next = Math.Max(next, now - Allowance) + interval * SustainedSlack;
            return true;
        }
    }
}
