using UnityEngine;

namespace RiftSurvivor
{
    internal static class JieBlinkVisual
    {
        internal static GameObject Show(Vector3 from, Vector3 to) => SkillBlinkVisual.Show(from, to, true, false);
    }
}
