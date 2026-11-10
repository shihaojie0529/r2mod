using RoR2;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiftSurvivor
{
    // The projectile's amber collar opens at six o'clock and points down to
    // the marked body. The same silhouette is visible to every teammate.
    internal sealed class XiaoheiMarkVisual : MonoBehaviour
    {
        private const float RiseTime = .24f;
        private static readonly Vector3[] collar = CollarArc();
        private static readonly Vector3[] pointer = {
            new Vector3(-.10f, -.34f), new Vector3(0f, -.47f), new Vector3(.10f, -.34f)
        };

        private CharacterBody target;
        private BuffDef buff;
        private GameObject symbol;
        private LineRenderer rim;
        private Vector3 impactOffset;
        private float began;
        private bool confirmed;

        internal void Show(CharacterBody body, BuffDef mark, Vector3? contact)
        {
            target = body;
            buff = mark;
            if (!symbol)
            {
                symbol = new GameObject("XiaoheiSeekerMark");
                symbol.AddComponent<Billboard>();
                rim = Stroke("AmberCollar", collar, .056f, XiaoheiRoundArt.Amber);
                Stroke("Index", pointer, .045f, XiaoheiRoundArt.Core);
                // A late observer sees the settled mark immediately.
                began = Time.time - RiseTime;
            }
            if (contact.HasValue)
            {
                impactOffset = contact.Value - body.corePosition;
                began = Time.time;
            }
            // Keep an early impact's position, but only display a combo marker
            // while the debuff is present. A notice can arrive after a cleanse.
            confirmed = body.HasBuff(mark);
            enabled = true;
            symbol.SetActive(confirmed && isActiveAndEnabled);
            Place();
        }

        private static Vector3[] CollarArc()
        {
            const int segments = 36;
            var arc = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float angle = (-70f + 320f * i / segments) * Mathf.Deg2Rad;
                arc[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * .36f;
            }
            return arc;
        }

        private LineRenderer Stroke(string name, Vector3[] points, float width, Color color)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(symbol.transform, false);
            line.useWorldSpace = false;
            line.sharedMaterial = XiaoheiRoundArt.Glow;
            line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = color;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private void LateUpdate()
        {
            if (!target || !target.healthComponent || !target.healthComponent.alive) { enabled = false; return; }
            // A temporarily deactivated body can return with its buff intact.
            if (!symbol)
            {
                if (target.HasBuff(buff)) Show(target, buff, null);
                else enabled = false;
                return;
            }
            if (target.HasBuff(buff))
            {
                confirmed = true;
                if (!symbol.activeSelf) symbol.SetActive(true);
            }
            else if (confirmed || Time.time - began > 1f) { enabled = false; return; }
            Place();
        }

        private void Place()
        {
            float t = Mathf.Clamp01((Time.time - began) / RiseTime);
            float rise = 1f - (1f - t) * (1f - t);
            // Leave room for Jie Ge's gold seal and Wei Ge's targeting diamond below.
            float height = Mathf.Max(target.height * .5f + .8f, target.radius + 1.7f);
            var above = Vector3.up * height;
            symbol.transform.position = target.corePosition + Vector3.Lerp(impactOffset, above, rise);
            float size = Mathf.Clamp(target.radius * .48f + .8f, 1f, 1.65f);
            symbol.transform.localScale = Vector3.one * (size * Mathf.Lerp(.32f, 1f, rise));
            rim.startColor = rim.endColor = Color.Lerp(new Color(1f, .86f, .56f), XiaoheiRoundArt.Amber, rise);
        }

        private void OnDisable()
        {
            if (!symbol) return;
            symbol.SetActive(false);
            Destroy(symbol);
            symbol = null;
        }
    }
}
