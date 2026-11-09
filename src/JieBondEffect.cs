using System;
using R2API;
using RoR2;
using UnityEngine;

namespace RiftSurvivor
{
    // One transmitted effect carries the landing ring or the camera-frame cue.
    internal sealed class JieBondEffect : MonoBehaviour
    {
        private static GameObject prefab;
        private bool frame;
        private float age;
        private LineRenderer[] strokes;

        internal static void Register()
        {
            prefab = CombatVisuals.EffectPrefab("JieGeBondEffect");
            prefab.AddComponent<EffectComponent>().applyScale = false;
            var attributes = prefab.AddComponent<VFXAttributes>();
            attributes.vfxPriority = VFXAttributes.VFXPriority.Always;
            attributes.DoNotPool = true;
            prefab.AddComponent<JieBondEffect>();
            if (!ContentAddition.AddEffect(prefab)) throw new InvalidOperationException("Could not register Jie Ge's bond effect.");
        }

        internal static void Emit(Vector3 position, bool frame) =>
            EffectManager.SpawnEffect(prefab, new EffectData { origin = position, genericBool = frame }, true);

        private void Start()
        {
            var data = GetComponent<EffectComponent>().effectData;
            frame = data.genericBool;
            transform.position = data.origin;
            if (frame)
            {
                gameObject.AddComponent<Billboard>();
                strokes = new LineRenderer[4];
                for (int i = 0; i < strokes.Length; i++)
                {
                    float x = i % 2 == 0 ? -1f : 1f;
                    float y = i < 2 ? -1f : 1f;
                    strokes[i] = MakeStroke(.075f, new[] { new Vector3(x, y * .24f), new Vector3(x, y * .56f),
                        new Vector3(x * .61f, y * .56f) });
                }
            }
            else
            {
                var rim = new Vector3[49];
                for (int i = 0; i < rim.Length; i++)
                {
                    float angle = i * 2f * Mathf.PI / (rim.Length - 1);
                    rim[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                }
                strokes = new[] { MakeStroke(.12f, rim) };
            }
            Draw(0f);
        }

        private LineRenderer MakeStroke(float width, Vector3[] points)
        {
            var line = JieStormEffects.Line(transform, width);
            line.positionCount = points.Length;
            line.SetPositions(points);
            return line;
        }

        private void Update()
        {
            if (strokes == null) return;
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / (frame ? .75f : .36f));
            Draw(t);
            if (t >= 1f) Destroy(gameObject);
        }

        private void Draw(float t)
        {
            float size = frame ? Mathf.Lerp(1.35f, 1f, Mathf.Min(t * 4f, 1f)) : Mathf.Lerp(.8f, 8f, 1f - (1f - t) * (1f - t));
            transform.localScale = Vector3.one * size;
            var color = frame ? new Color(1f, .85f, .39f) : new Color(.42f, 1f, .84f);
            color.a = Mathf.Clamp01((1f - t) * 3f);
            foreach (var stroke in strokes) stroke.startColor = stroke.endColor = color;
        }
    }
}
