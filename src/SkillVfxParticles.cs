using UnityEngine;
using UnityEngine.Rendering;

namespace RiftSurvivor
{
    // Shared soft sprite, bounded emission, no dynamic lights or collision.
    internal static class SkillVfxParticles
    {
        private static Material material;
        internal static ParticleSystem Burst(Transform parent, Color color, int count, float radius, float speed,
            float lifetime = .42f, bool rising = false)
        {
            var root = new GameObject("SkillImpactMotes");
            root.transform.SetParent(parent, false);
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = false; main.playOnAwake = false; main.duration = lifetime;
            main.maxParticles = 32; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = lifetime; main.startSpeed = 0f;
            main.gravityModifier = rising ? -.08f : .12f;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = false;
            var colorLife = particles.colorOverLifetime; colorLife.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, .22f),
                new GradientColorKey(color, 1f) }, new[] { new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(.85f, .06f), new GradientAlphaKey(.45f, .35f), new GradientAlphaKey(0f, 1f) });
            colorLife.color = gradient;
            var sizeLife = particles.sizeOverLifetime; sizeLife.enabled = true;
            sizeLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
            var render = particles.GetComponent<ParticleSystemRenderer>();
            if (!material) material = CombatVisuals.SoftRadiance("SkillSoftMotes", false);
            render.sharedMaterial = material; render.renderMode = ParticleSystemRenderMode.Billboard;
            render.shadowCastingMode = ShadowCastingMode.Off; render.receiveShadows = false;
            particles.Play();
            count = Mathf.Clamp(count, 1, 32);
            for (int i = 0; i < count; i++)
            {
                float angle = i * 2.399963f;
                var radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), .18f * Mathf.Sin(i * 5.7f));
                var velocity = rising ? Vector3.up * speed + parent.right * Mathf.Cos(angle) * speed * .2f :
                    parent.TransformDirection(radial) * speed * (.6f + .4f * ((i * 7) % 11) / 10f);
                particles.Emit(new ParticleSystem.EmitParams {
                    position = parent.position + parent.TransformDirection(radial) * radius * (.25f + .75f * i / count),
                    velocity = velocity, startColor = Color.white, startSize = .055f + .11f * ((i * 3) % 7) / 6f,
                    startLifetime = lifetime * (.65f + .35f * (i % 5) / 4f)
                }, 1);
            }
            return particles;
        }
    }
}
