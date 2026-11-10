using System;
using System.Collections.Generic;
using System.IO;
using RoR2;
using UnityEngine;

namespace RiftSurvivor
{
    // Apply the authored pose after the mesh is fitted and before CuiGear samples the throwing hand.
    [DisallowMultipleComponent, DefaultExecutionOrder(30500)]
    public sealed class CuiMotion : MonoBehaviour
    {
        [Serializable] private sealed class Library { public Clip[] clips = null; }

        [Serializable] private sealed class Clip
        {
            public string name = null;
            public float duration = 0;
            public bool loop = false;
            public Track[] tracks = null;
            [NonSerialized] public Dictionary<string, Track> byBone;
        }

        [Serializable] private sealed class Track
        {
            public string bone = null;
            public Key[] keys = null;

            public void Sample(float t, out Quaternion rotation, out Vector3 offset)
            {
                int end = 0;
                while (end < keys.Length - 1 && keys[end].t < t) ++end;
                Key a = keys[Mathf.Max(0, end - 1)], b = keys[end];
                float alpha = b.t > a.t ? Mathf.Clamp01((t - a.t) / (b.t - a.t)) : 0f;
                rotation = Quaternion.SlerpUnclamped(a.rotation, b.rotation, alpha);
                offset = Vector3.LerpUnclamped(a.offset, b.offset, alpha);
            }
        }

        [Serializable] private sealed class Key
        {
            public float t = 0;
            public float[] r = null, p = null;
            [NonSerialized] public Quaternion rotation;
            [NonSerialized] public Vector3 offset;
        }

        private struct Cue
        {
            public string name;
            public float start, duration;
            public int serial;
            public bool Active(float now) => name != null && now < start + duration;
        }

        private static Dictionary<string, Clip> library;
        private CuiRig rig;
        private CuiGear gear;
        private CuiFistGlow fistGlow;
        private CharacterBody body, presentedBody, pendingBottle;
        private float bottleRelease;
        private Transform[] bones;
        private string[] boneNames;
        private Quaternion[] previousRotations;
        private Transform hips;
        private Vector3 hipRest, previousHip, lastWorld;
        private Quaternion plate = Quaternion.identity, shaker = Quaternion.identity;
        private Quaternion previousPlate, previousShaker;
        private bool blendPlate, blendShaker;
        private Cue action, dash, aim, ready, spawn, hit, death;
        private float queuedReady;
        private int serial, shownSerial;
        private Clip shown, locomotion;
        private float transitionStart, transitionLength, locomotionTime, horizontalSpeed, airborneTime, landingUntil;
        private bool sampledPosition, wasGrounded, moving, dead;
        private bool sampledHealth;
        private float lastHealth, lastMaxHealth, lastBarrier, lastHitStamp, nextFlinch;

        private float Now => body ? Time.time : Time.unscaledTime;

        private static void LoadLibrary()
        {
            if (library != null) return;
            using var stream = typeof(CuiMotion).Assembly.GetManifestResourceStream("RiftSurvivor.motion.json")
                ?? throw new InvalidDataException("Missing embedded Cui Ge motion library.");
            using var reader = new StreamReader(stream);
            var result = new Dictionary<string, Clip>(StringComparer.Ordinal);
            foreach (var clip in JsonUtility.FromJson<Library>(reader.ReadToEnd()).clips)
            {
                clip.byBone = new Dictionary<string, Track>(StringComparer.Ordinal);
                foreach (var track in clip.tracks)
                {
                    foreach (var key in track.keys)
                    {
                        key.rotation = new Quaternion(key.r[0], key.r[1], key.r[2], key.r[3]).normalized;
                        if (key.p != null && key.p.Length == 3) key.offset = new Vector3(key.p[0], key.p[1], key.p[2]);
                    }
                    clip.byBone.Add(track.bone, track);
                }
                result.Add(clip.name, clip);
            }
            library = result;
        }

        internal void Bind(CuiRig skeleton, CharacterModel model, CharacterBody character, SkinnedMeshRenderer renderer)
        {
            LoadLibrary();
            if (rig == skeleton && body == character) return;
            rig = skeleton;
            body = character;
            hips = rig.Bones["Hips"];
            hipRest = hips.localPosition;
            boneNames = new string[rig.Bones.Count];
            bones = new Transform[boneNames.Length];
            previousRotations = new Quaternion[bones.Length];
            int index = 0;
            foreach (var entry in rig.Bones) { boneNames[index] = entry.Key; bones[index++] = entry.Value; }
            gear = GetComponent<CuiGear>();
            if (!gear) gear = gameObject.AddComponent<CuiGear>();
            gear.Bind(rig, renderer, model);
            fistGlow = GetComponent<CuiFistGlow>();
            if (!fistGlow) fistGlow = gameObject.AddComponent<CuiFistGlow>();
            fistGlow.Bind(rig, renderer, model, body);
            if (pendingBottle && Now - bottleRelease < .25f)
                gear.Throw(pendingBottle, Mathf.Max(0, bottleRelease - Now));
            pendingBottle = null;
            shown = locomotion = null;
            sampledPosition = sampledHealth = false;
            if (character && character != presentedBody)
            {
                presentedBody = character;
                death = default;
                dead = false;
                if (library.TryGetValue("spawn", out var entrance))
                    spawn = NewCue("spawn", entrance.duration);
            }
            else if (!character) { death = default; dead = false; }
            Tick(0f);
        }

        private static CuiMotion Find(CharacterBody owner)
        {
            if (!owner || !owner.modelLocator || !owner.modelLocator.modelTransform) return null;
            var controller = owner.modelLocator.modelTransform.GetComponentInChildren<CuiMotion>(true);
            if (controller && !controller.body) controller.body = owner;
            return controller;
        }

        public static void Play(CharacterBody body, string cue, float duration)
        {
            var controller = Find(body);
            if (controller && duration > 0) controller.PlayCue(cue, duration);
        }

        public static void Stop(CharacterBody body, string cue)
        {
            var controller = Find(body);
            if (!controller) return;
            if (controller.aim.name == cue) controller.aim = default;
            if (controller.dash.name == cue) controller.dash = default;
            if (controller.action.name == cue) controller.action = default;
            if (cue == "ready") { controller.queuedReady = 0f; controller.ready = default; }
        }

        public static void ThrowBottle(CharacterBody source, CharacterBody target, float delay)
        {
            var controller = Find(source);
            if (!controller || !target) return;
            if (controller.gear) controller.gear.Throw(target, Mathf.Max(0f, delay));
            else { controller.pendingBottle = target; controller.bottleRelease = controller.Now + delay; }
        }

        private void PlayCue(string name, float duration)
        {
            if (name == "ready") { queuedReady = duration; return; }
            if (name == "aim" && aim.name != null) return; // Renewals must not restart the pose.
            spawn = hit = default;
            var cue = NewCue(name, duration);
            if (name == "charge") dash = cue;
            else if (name == "aim") aim = cue;
            else action = cue;
            ready = default;
        }

        private Cue NewCue(string name, float duration) =>
            new Cue { name = name, start = Now, duration = duration, serial = ++serial };

        private void LateUpdate()
        {
            if (rig != null) Tick(body ? Time.deltaTime : Time.unscaledDeltaTime);
        }

        private void Tick(float dt)
        {
            float now = Now;
            var health = body ? body.healthComponent : null;
            bool dying = health ? !DownedRescue.IsUp(body) : dead;
            if (dying && !dead)
            {
                dead = true;
                ClearCues();
                if (library.TryGetValue("death", out var fall)) death = NewCue("death", fall.duration);
            }
            if (dying && death.name == null)
            {
                gear.Show("", 1f, Quaternion.identity, Quaternion.identity);
                fistGlow.Show("death", 1f);
                return;
            }
            if (!dying)
            {
                if (dead) { death = default; sampledHealth = sampledPosition = false; }
                dead = false;
                UpdateLocomotion(now, dt);
                if (body)
                {
                    var input = body.inputBank;
                    if (spawn.name != null && (!spawn.Active(now) || moving || action.Active(now) || dash.Active(now) || aim.name != null ||
                        (input && (input.moveVector.sqrMagnitude > .01f || input.jump.down || input.sprint.down ||
                                   input.skill1.down || input.skill2.down || input.skill3.down || input.skill4.down))))
                        spawn = default;
                    if (health) ObserveDamage(health, now);
                }
            }
            // A dead body samples only this nonlooping clip, including its final pose.
            Cue cue = dying ? death : SelectCue(now);
            var clip = cue.name != null && library.TryGetValue(cue.name, out var overrideClip) ? overrideClip : locomotion;
            float t = clip == locomotion ? locomotionTime : (now - cue.start) / Mathf.Max(.001f, cue.duration);
            t = clip.loop ? Mathf.Repeat(t, 1f) : Mathf.Clamp01(t);
            if (shown != clip || shownSerial != cue.serial)
            {
                for (int i = 0; i < bones.Length; ++i) previousRotations[i] = bones[i].localRotation;
                previousHip = hips.localPosition;
                previousPlate = plate; previousShaker = shaker;
                blendPlate = shown != null && shown.byBone.ContainsKey("Plate");
                blendShaker = shown != null && shown.byBone.ContainsKey("Shaker");
                transitionLength = shown == null ? 0f : cue.name == null ? .105f : Mathf.Min(.07f, cue.duration * .15f);
                transitionStart = now;
                shown = clip; shownSerial = cue.serial;
            }
            float blend = transitionLength == 0 ? 1 : Mathf.SmoothStep(0, 1, (now - transitionStart) / transitionLength);
            float feet = 0f;
            if (!dying && shown != locomotion && shown.name != "spawn" &&
                shown.name != "charge" && shown.name != "slam" && shown.name != "slamHeavy")
                feet = wasGrounded ? Mathf.Clamp01((horizontalSpeed - .25f) / 2f) : 1f;
            float strideT = feet > 0f && locomotion.loop ? Mathf.Repeat(locomotionTime, 1f) : Mathf.Clamp01(locomotionTime);
            for (int i = 0; i < bones.Length; ++i)
            {
                string name = boneNames[i];
                if (!shown.byBone.TryGetValue(name, out var track)) continue;
                track.Sample(t, out var r, out var p);
                if (feet > 0f && IsLeg(name) && locomotion.byBone.TryGetValue(name, out var leg))
                {
                    leg.Sample(strideT, out var legRotation, out _);
                    r = Quaternion.SlerpUnclamped(r, legRotation, feet);
                }
                bones[i].localRotation = Quaternion.SlerpUnclamped(previousRotations[i], r, blend);
                if (bones[i] == hips) hips.localPosition = Vector3.LerpUnclamped(previousHip, hipRest + p, blend);
            }
            plate = Prop(shown, "Plate", t, previousPlate, blendPlate ? blend : 1f);
            shaker = Prop(shown, "Shaker", t, previousShaker, blendShaker ? blend : 1f);
            gear.Show(shown.name, t, plate, shaker);
            fistGlow.Show(shown.name, t);
        }

        private static bool IsLeg(string name) => name.EndsWith("Leg", StringComparison.Ordinal)
            || name.EndsWith("Foot", StringComparison.Ordinal);

        private static Quaternion Prop(Clip clip, string bone, float t, Quaternion from, float blend)
        {
            if (!clip.byBone.TryGetValue(bone, out var track)) return Quaternion.identity;
            track.Sample(t, out var orientation, out _);
            return Quaternion.SlerpUnclamped(from, orientation, blend);
        }

        private Cue SelectCue(float now)
        {
            if (dash.Active(now)) return dash;
            if (action.Active(now)) return action;
            if (aim.name != null) return aim;
            if (queuedReady > 0f)
            {
                if (body && CuiVitals.IsFull(body)) ready = NewCue("ready", queuedReady);
                queuedReady = 0f;
            }
            if (ready.Active(now) && body && CuiVitals.IsFull(body)) return ready;
            if (hit.Active(now)) return hit;
            return spawn.Active(now) ? spawn : default;
        }

        private void ObserveDamage(HealthComponent health, float now)
        {
            float current = health.combinedHealth, maximum = health.fullCombinedHealth;
            float barrier = health.barrier, stamp = health.lastHitTime.t;
            // Health and shield are replicated. Natural barrier decay is not a hit;
            // count a barrier loss only when the engine reports a new impact as well.
            float prior = lastMaxHealth > maximum && lastMaxHealth > 0f ? lastHealth * maximum / lastMaxHealth : lastHealth;
            float lost = Mathf.Min(prior, maximum) - current;
            if (stamp > lastHitStamp) lost += Mathf.Max(0f, lastBarrier - barrier);
            if (sampledHealth && now >= nextFlinch && lost >= Mathf.Max(1f, maximum * .008f) &&
                !action.Active(now) && !dash.Active(now) && aim.name == null && !ready.Active(now) &&
                queuedReady <= 0f && library.TryGetValue("hit", out var recoil))
            {
                hit = NewCue("hit", recoil.duration);
                spawn = default;
                nextFlinch = now + .32f;
            }
            sampledHealth = true;
            lastHealth = current; lastMaxHealth = maximum; lastBarrier = barrier; lastHitStamp = stamp;
        }

        private void UpdateLocomotion(float now, float dt)
        {
            string name = "lobby";
            if (body)
            {
                var motor = body.characterMotor;
                Vector3 pos = body.transform.position;
                bool grounded = !motor || motor.isGrounded;
                if (sampledPosition && dt > 0f)
                {
                    var delta = pos - lastWorld;
                    delta.y = 0;
                    float measured = delta.magnitude / dt;
                    if (body.hasEffectiveAuthority && motor)
                        measured = new Vector2(motor.velocity.x, motor.velocity.z).magnitude;
                    else if (delta.sqrMagnitude > 9f) measured = 0f; // Stage warp, not a stride.
                    horizontalSpeed = Mathf.Lerp(horizontalSpeed, measured, 1f - Mathf.Exp(-12f * dt));
                    if (!grounded) airborneTime += dt;
                    else if (!wasGrounded)
                    {
                        if (airborneTime >= .1f) landingUntil = now + library["land"].duration;
                        airborneTime = 0f;
                    }
                }
                sampledPosition = true; lastWorld = pos; wasGrounded = grounded;
                moving = horizontalSpeed > (moving ? .32f : .60f);
                if (!grounded) name = "air";
                else if (now < landingUntil) name = "land";
                else name = moving ? "run" : "idle";
            }
            var next = library[name];
            if (locomotion != next) { locomotion = next; locomotionTime = 0; }
            else
            {
                float rate = name == "run" ? Mathf.Clamp(horizontalSpeed / 7f, .45f, 2.75f) : 1f;
                locomotionTime += dt * rate / next.duration;
                if (next.loop) locomotionTime = Mathf.Repeat(locomotionTime, 1f);
                else locomotionTime = Mathf.Min(locomotionTime, 1f);
            }
        }

        private void ClearCues()
        {
            action = dash = aim = ready = spawn = hit = default;
            queuedReady = 0f;
            pendingBottle = null;
        }

        private void OnDisable()
        {
            ClearCues();
            if (gear) gear.Show("", 1f, Quaternion.identity, Quaternion.identity);
            if (fistGlow) fistGlow.Show("", 1f);
            sampledPosition = sampledHealth = moving = false;
            horizontalSpeed = airborneTime = landingUntil = 0f;
            shown = locomotion = null;
        }
    }
}
