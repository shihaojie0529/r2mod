using System;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiftSurvivor
{
    internal enum JieStormVisualKind { Fan, Bolt, Seal, Heaven }

    internal static class JieStormEffects
    {
        internal const float DropTime = .19f;
        internal static readonly Color Gold = new Color(1f, .71f, .15f);
        private static GameObject fan, bolt, seal, heaven;
        private static Material lineMaterial;

        internal static void Register()
        {
            fan = Register("JieThirtySixSwords", JieStormVisualKind.Fan);
            bolt = Register("JieGoldSwordBolt", JieStormVisualKind.Bolt);
            seal = Register("JieGoldTargetSeal", JieStormVisualKind.Seal);
            heaven = Register("JieFallingHeavenSword", JieStormVisualKind.Heaven);
        }

        private static GameObject Register(string name, JieStormVisualKind kind)
        {
            var prefab = PrefabAPI.CreateEmptyPrefab(name, false);
            prefab.AddComponent<EffectComponent>().applyScale = false;
            var attributes = prefab.AddComponent<VFXAttributes>();
            attributes.DoNotPool = true;
            attributes.vfxPriority = VFXAttributes.VFXPriority.Always;
            prefab.AddComponent<JieStormVisual>().kind = kind;
            if (!ContentAddition.AddEffect(prefab)) throw new InvalidOperationException("Could not register " + name);
            return prefab;
        }

        private static void Follow(GameObject prefab, CharacterBody body)
        {
            var data = new EffectData { origin = body.corePosition, genericFloat = Run.FixedTimeStamp.now.t };
            data.SetNetworkedObjectReference(body.gameObject);
            EffectManager.SpawnEffect(prefab, data, true);
        }

        internal static void OpenFan(CharacterBody owner) => Follow(fan, owner);
        internal static void Lock(CharacterBody target) => Follow(seal, target);
        internal static void Drop(Vector3 position, float radius) => EffectManager.SpawnEffect(heaven,
            new EffectData { origin = position, scale = radius }, true);

        internal static void Launch(Vector3 from, Vector3 endpoint, HurtBox target, float duration)
        {
            var data = new EffectData { origin = from, start = endpoint, genericFloat = duration };
            if (target) data.SetHurtBoxReference(target);
            EffectManager.SpawnEffect(bolt, data, true);
        }

        internal static Vector3 FlightPoint(Vector3 from, Vector3 to, float t)
        {
            float arc = Mathf.Min(3.2f, Vector3.Distance(from, to) * .12f);
            return Vector3.Lerp(from, to, t) + Vector3.up * (4f * t * (1f - t) * arc);
        }

        internal static void FanPose(CharacterBody body, int index, out Vector3 position, out Quaternion rotation)
        {
            var forward = body.inputBank ? Vector3.ProjectOnPlane(body.inputBank.aimDirection, Vector3.up) : Vector3.zero;
            if (forward.sqrMagnitude < .001f) forward = body.transform.forward;
            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward);
            // Three nested pairs leave in each volley. The outermost pairs frame
            // the silhouette until the last wave even when the wearer strafes.
            int tier = index % 3;
            float sign = (index / 3) % 2 == 0 ? -1f : 1f;
            float angle = sign * ((index / 6) + .5f) * 12.5f * Mathf.Deg2Rad;
            var spoke = right * Mathf.Sin(angle) + Vector3.up * Mathf.Cos(angle);
            position = body.corePosition - forward * (.58f + .16f * tier)
                + Vector3.up * -.12f + spoke * (1.23f + .52f * tier);
            rotation = Quaternion.LookRotation((spoke + forward * .17f).normalized, forward);
        }

        internal static Vector3 SealPoint(CharacterBody body) =>
            body.corePosition + Vector3.up * (Mathf.Max(.6f, body.radius) + .6f);

        internal static GameObject MakeSeal()
        {
            var symbol = new GameObject("JieGoldSeal");
            symbol.AddComponent<Billboard>();
            // Seal border and the strokes of 令, all facing the local camera.
            Stroke(symbol.transform, .036f, true, new Vector2(0,.45f), new Vector2(.39f,0),
                new Vector2(0,-.45f), new Vector2(-.39f,0));
            Stroke(symbol.transform, .033f, false, new Vector2(-.18f,.03f), new Vector2(0,.21f), new Vector2(.18f,.03f));
            Stroke(symbol.transform, .027f, false, new Vector2(-.04f,.06f), new Vector2(.025f,0));
            Stroke(symbol.transform, .028f, false, new Vector2(-.13f,-.04f), new Vector2(.13f,-.04f),
                new Vector2(.018f,-.15f), new Vector2(.06f,-.21f));
            return symbol;
        }

        private static void Stroke(Transform parent, float width, bool loop, params Vector2[] points)
        {
            var line = Line(parent, width);
            line.loop = loop;
            var vertices = Array.ConvertAll(points, p => new Vector3(p.x, p.y, 0));
            line.positionCount = vertices.Length;
            line.SetPositions(vertices);
        }

        internal static LineRenderer Line(Transform parent, float width)
        {
            var line = new GameObject("GoldStroke").AddComponent<LineRenderer>();
            line.transform.SetParent(parent, false);
            if (!lineMaterial) lineMaterial = new Material(Shader.Find("Sprites/Default")) { name = "JieGoldStroke" };
            line.sharedMaterial = lineMaterial;
            line.useWorldSpace = false;
            line.startWidth = line.endWidth = width;
            line.startColor = new Color(1f, .90f, .41f);
            line.endColor = Gold;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }
    }

    internal sealed class JieStormVisual : MonoBehaviour
    {
        [SerializeField] internal JieStormVisualKind kind;
        private CharacterBody reference;
        private HurtBox target;
        private Transform[] blades;
        private Transform prop;
        private LineRenderer ring;
        private Vector3 start, endpoint;
        private float began, duration;
        private int discharged;

        private void Start()
        {
            var data = GetComponent<EffectComponent>().effectData;
            start = data.origin;
            endpoint = data.start;
            began = Time.time;
            if (kind == JieStormVisualKind.Fan || kind == JieStormVisualKind.Seal)
            {
                var owner = data.ResolveNetworkedObjectReference();
                reference = owner ? owner.GetComponent<CharacterBody>() : null;
                if (!reference) { Destroy(gameObject); return; }
                began -= Mathf.Clamp(Run.FixedTimeStamp.now.t - data.genericFloat, 0f, JieGoldVolley.Duration);
            }
            switch (kind)
            {
                case JieStormVisualKind.Fan:
                    blades = new Transform[JieGoldVolley.WaveSize * JieGoldVolley.Waves];
                    for (int i = 0; i < blades.Length; ++i) blades[i] = Sword(.80f);
                    break;
                case JieStormVisualKind.Bolt:
                    var victim = data.ResolveHurtBoxReference();
                    target = victim ? victim.GetComponent<HurtBox>() : null;
                    duration = Mathf.Max(.02f, data.genericFloat);
                    prop = Sword(.90f);
                    break;
                case JieStormVisualKind.Seal:
                    prop = JieStormEffects.MakeSeal().transform;
                    prop.SetParent(transform, false);
                    break;
                case JieStormVisualKind.Heaven:
                    duration = Mathf.Max(1f, data.scale);
                    prop = Sword(Mathf.Lerp(4.8f, 6.8f, Mathf.InverseLerp(8f, 12f, duration)));
                    ring = JieStormEffects.Line(transform, .14f);
                    ring.loop = true;
                    ring.positionCount = 56;
                    for (int i = 0; i < ring.positionCount; ++i)
                    {
                        float angle = 2f * Mathf.PI * i / ring.positionCount;
                        ring.SetPosition(i, new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)));
                    }
                    break;
            }
            Animate();
        }

        private Transform Sword(float size)
        {
            var blade = new GameObject("GoldenBlade").transform;
            blade.SetParent(transform, false);
            blade.localScale = Vector3.one * size;
            JieSwordArt.Attach(blade, JieStormEffects.Gold);
            return blade;
        }

        private void LateUpdate() => Animate();

        private void Animate()
        {
            float age = Time.time - began;
            switch (kind)
            {
                case JieStormVisualKind.Fan:
                    if (!reference || !reference.healthComponent.alive || age >= JieGoldVolley.Duration) { Destroy(gameObject); return; }
                    if (blades == null) return;
                    while (discharged < blades.Length && age >= JieGoldVolley.ReleaseAt(discharged / JieGoldVolley.WaveSize))
                        blades[discharged++].gameObject.SetActive(false);
                    for (int i = discharged; i < blades.Length; ++i)
                    {
                        JieStormEffects.FanPose(reference, i, out var position, out var rotation);
                        blades[i].SetPositionAndRotation(position, rotation);
                    }
                    break;
                case JieStormVisualKind.Bolt:
                    if (!prop) return;
                    if (target && (!target.healthComponent || !target.healthComponent.alive)) { Destroy(gameObject); return; }
                    if (target) endpoint = target.transform.position;
                    float t = Mathf.Clamp01(age / duration);
                    var point = JieStormEffects.FlightPoint(start, endpoint, t);
                    var tangent = JieStormEffects.FlightPoint(start, endpoint, Mathf.Min(1f, t + .025f)) - point;
                    prop.position = point;
                    if (tangent.sqrMagnitude > .000001f) prop.rotation = Quaternion.LookRotation(tangent);
                    if (age >= duration) Destroy(gameObject);
                    break;
                case JieStormVisualKind.Seal:
                    if (!reference || !reference.healthComponent.alive || age >= JieGoldVolley.Duration) { Destroy(gameObject); return; }
                    transform.position = JieStormEffects.SealPoint(reference);
                    break;
                case JieStormVisualKind.Heaven:
                    if (!prop) return;
                    float plunge = Mathf.Clamp01(age / JieStormEffects.DropTime);
                    prop.SetPositionAndRotation(start + Vector3.up * Mathf.Lerp(14f, .32f * prop.localScale.z, plunge * plunge),
                        Quaternion.LookRotation(Vector3.down, Vector3.forward));
                    float pulse = Mathf.Clamp01((age - JieStormEffects.DropTime) / .32f);
                    ring.enabled = pulse > 0f;
                    ring.transform.localScale = Vector3.one * Mathf.Max(.01f, duration * pulse);
                    if (age >= JieStormEffects.DropTime + .35f) Destroy(gameObject);
                    break;
            }
        }
    }
}
