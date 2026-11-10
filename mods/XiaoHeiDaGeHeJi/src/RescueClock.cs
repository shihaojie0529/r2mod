using System;

namespace RiftSurvivor
{
    internal enum RescueStep { Waiting, Rescued, BledOut }

    // Bleed-out and rescue arithmetic only, so the rules stay readable and can be
    // exercised without a running game.
    internal struct RescueClock
    {
        internal const int MaxDowns = 3;
        internal const float ReviveSeconds = 3f, DecayPerSecond = .1f, GroupSpeed = 1.5f;

        internal float Remaining, Progress;

        internal static float BleedoutFor(int downNumber) => downNumber <= 1 ? 30f : downNumber == 2 ? 20f : 10f;
        internal static RescueClock Start(int downNumber) => new RescueClock { Remaining = BleedoutFor(downNumber) };
        internal void Supply(float amount) => Progress = Math.Min(1f, Progress + Math.Max(0f, amount));

        // The bleed-out timer holds while someone is actively reviving, so a rescue
        // already under way is never lost to the last second of the countdown.
        internal RescueStep Tick(float dt, int rescuers)
        {
            if (Progress >= 1f) return RescueStep.Rescued;
            if (rescuers > 0)
            {
                Progress = Math.Min(1f, Progress + dt * (rescuers > 1 ? GroupSpeed : 1f) / ReviveSeconds);
                return Progress >= 1f ? RescueStep.Rescued : RescueStep.Waiting;
            }
            Progress = Math.Max(0f, Progress - dt * DecayPerSecond);
            Remaining -= dt;
            return Remaining <= 0f ? RescueStep.BledOut : RescueStep.Waiting;
        }
    }
}
