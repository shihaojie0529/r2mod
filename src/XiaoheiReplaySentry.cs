using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace RiftSurvivor
{
    // A single network object owns the stationary body, muzzle and server-only volley.
    public sealed class XiaoheiReplaySentry : NetworkBehaviour
    {
        internal const float Duration = 4f;
        internal const float Range = 60f;
        private const float FirstShot = .18f, LastShot = 3.65f;
        private static GameObject prefab;
        private CharacterBody owner;
        private NetworkInstanceId ownerId;
        private GameObject visual;
        private HurtBox target;
        private Vector3 muzzleLocal;
        private float began, ends, clientEnds;
        private int total, scheduled, fired;
        private bool initialized;
        internal Vector3 Muzzle => transform.TransformPoint(muzzleLocal);
        internal static int ShotCount(int highlights) => 18 + 6 * Mathf.Clamp(highlights, 0, 3);

        internal static void Register()
        {
            prefab = PrefabAPI.CreateEmptyPrefab("XiaoheiStationaryGiantReplay", true);
            prefab.AddComponent<XiaoheiReplaySentry>();
        }
        internal static GameObject Spawn(CharacterBody body, Ray aim, int highlights)
        {
            if (!NetworkServer.active || !body || !body.healthComponent || !body.healthComponent.alive) return null;
            var heading = Vector3.ProjectOnPlane(aim.direction, Vector3.up);
            if (heading.sqrMagnitude < .001f) heading = body.transform.forward;
            var go = Instantiate(prefab, body.footPosition, Quaternion.LookRotation(heading));
            var sentry = go.GetComponent<XiaoheiReplaySentry>();
            sentry.owner = body; sentry.ownerId = body.netId;
            sentry.muzzleLocal = XiaoheiModel.ReplayMuzzle(XiaoheiReplayAvatar.Height);
            sentry.total = ShotCount(highlights);
            sentry.began = Time.fixedTime; sentry.ends = Time.fixedTime + Duration;
            sentry.clientEnds = Time.time + Duration; sentry.initialized = true;
            NetworkServer.Spawn(go);
            CombatStatusHud.Replay(body);
            SkillSpectacle.Emit(SpectacleKind.Replay, go.transform.position + Vector3.up, aim.direction, 4f);
            NarutoFx.Emit(NarutoEffect.Chidori, go.transform.position + Vector3.up, Vector3.zero, 3f, 2f);
            return go;
        }
        private static Quaternion Facing(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > .001f ? Quaternion.LookRotation(direction) : Quaternion.identity;
        }
        internal static bool ValidTarget(CharacterBody shooter, HurtBox hurt, Vector3 anchor, Vector3 barrel)
        {
            if (!shooter || !shooter.teamComponent || !XiaoheiBallistics.IsLiveEnemy(shooter, hurt)) return false;
            Vector3 point = hurt.transform.position;
            if ((point-anchor).sqrMagnitude > Range*Range) return false;
            var visibility = hurt.healthComponent.body.GetVisibilityLevel(shooter);
            if (visibility != VisibilityLevel.Visible && visibility != VisibilityLevel.Revealed) return false;
            // The sentry cannot see or fire through a wall, even if the giant barrel
            // extends past it. Both the ground anchor and the actual barrel need LoS.
            Vector3 muzzle = anchor + Facing(point-anchor) * barrel;
            return !Physics.CheckSphere(muzzle, .08f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore) &&
                !Physics.Linecast(anchor+Vector3.up*1.2f, point, LayerIndex.world.mask, QueryTriggerInteraction.Ignore) &&
                !Physics.Linecast(muzzle, point, LayerIndex.world.mask, QueryTriggerInteraction.Ignore);
        }
        internal static HurtBox Acquire(CharacterBody shooter, Vector3 anchor, Vector3 barrel, HurtBox previous)
        {
            if (ValidTarget(shooter, previous, anchor, barrel)) return previous;
            HurtBox best = null; float nearest = Range*Range;
            foreach (var hurt in HurtBox.readOnlyBullseyesList)
            {
                if (!hurt) continue;
                float distance = (hurt.transform.position-anchor).sqrMagnitude;
                if (distance >= nearest || !ValidTarget(shooter,hurt,anchor,barrel)) continue;
                nearest=distance; best=hurt;
            }
            return best;
        }
        private void FixedUpdate()
        {
            if (!NetworkServer.active || !initialized) return;
            if (!owner || !owner.healthComponent || !owner.healthComponent.alive || Time.fixedTime >= ends)
            { NetworkServer.Destroy(gameObject); return; }
            float age = Time.fixedTime-began;
            while (scheduled < total && age >= FirstShot + scheduled*(LastShot-FirstShot)/(total-1))
            {
                scheduled++;
                target = Acquire(owner,transform.position,muzzleLocal,target);
                if (!target) continue;
                var point = target.transform.position;
                transform.rotation = Facing(point-transform.position);
                var origin = Muzzle;
                var direction = (point-origin).normalized;
                XiaoheiBallistics.Fire(owner,new Ray(origin,direction),origin,2.5f,
                    DamageTypeCombo.GenericSpecial,XiaoheiCombat.EchoTracer,
                    fx:NarutoEffect.BoltImpact,fxScale:1.4f,fxShake:1f,fxInterval:.05f);
                if (XiaoheiCombat.MuzzleFlash)
                    EffectManager.SpawnEffect(XiaoheiCombat.MuzzleFlash,new EffectData {
                        origin=origin,rotation=Util.QuaternionSafeLookRotation(direction),scale=2f },true);
                fired++; SetDirtyBit(1);
            }
        }
        public override float GetNetworkSendInterval() => 0f;
        public override bool OnSerialize(NetworkWriter writer,bool initialState)
        {
            writer.Write(ownerId);writer.Write(transform.position);writer.Write(transform.rotation);
            writer.Write(muzzleLocal);writer.Write(Mathf.Max(0f,ends-Time.fixedTime));writer.Write(fired);
            return true;
        }
        public override void OnDeserialize(NetworkReader reader,bool initialState)
        {
            ownerId=reader.ReadNetworkId();var point=reader.ReadVector3();var rotation=reader.ReadQuaternion();
            transform.SetPositionAndRotation(point,rotation);muzzleLocal=reader.ReadVector3();
            clientEnds=Time.time+reader.ReadSingle();fired=reader.ReadInt32();initialized=true;
        }
        private void LateUpdate()
        {
            if (!initialized || !NetworkClient.active || Time.time >= clientEnds) return;
            if (!owner)
            {
                var bodyObject=ClientScene.FindLocalObject(ownerId);
                if (bodyObject) owner=bodyObject.GetComponent<CharacterBody>();
            }
            if (!owner || !owner.healthComponent || !owner.healthComponent.alive) return;
            if (!visual)
            {
                visual=XiaoheiReplayAvatar.Show(owner,transform.position-owner.footPosition,transform.forward);
                visual.transform.SetParent(transform,true);
                visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=Quaternion.identity;
            }
        }
    }
}
