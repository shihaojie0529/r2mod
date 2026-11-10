using System;
using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

public partial class Smoke
{
    // The rescue guard depends on this vanilla behaviour. It cannot be confirmed
    // from the stripped reference assemblies, only from the shipped game.
    void CheckDownedRescue()
    {
        bool wasServer = NetworkServer.active;
        var roots = new List<GameObject>();
        try
        {
            if (!wasServer) { NetworkServer.dontListen = true; if (!NetworkServer.Listen(0)) throw new Exception("No rescue test server"); }
            if (!GlobalEventManager.instance)
            {
                var events = new GameObject("RescueHitEventFixture"); roots.Add(events);
                events.AddComponent<GlobalEventManager>();
            }
            var go = Instantiate(BodyCatalog.FindBodyPrefab("BeetleBody"), new Vector3(1000, 1000, 1000), Quaternion.identity);
            roots.Add(go);
            foreach (var behaviour in go.GetComponents<Behaviour>()) behaviour.enabled = false;
            var body = go.GetComponent<CharacterBody>();
            var health = body.healthComponent;
            NetworkServer.Spawn(go);
            CheckOne("NonLethal holds a lethal hit at one health", () => {
                health.Networkhealth = 100f;
                health.TakeDamage(new DamageInfo { damage = 5000f, position = go.transform.position, damageType = DamageType.NonLethal, procCoefficient = 0f });
                if (!health.alive) throw new Exception("NonLethal hit killed the body");
                if (health.combinedHealth > 1.01f) throw new Exception("Lethal hit left " + health.combinedHealth + " health");
            });
            CheckOne("A downed body is alive but no longer up", () => {
                health.Networkhealth = 100f;
                if (!(bool)Call("DownedRescue", "IsUp", body)) throw new Exception("Healthy body not up");
                var seconds = (BuffDef)T("DownedRescue").GetField("secondsBuff", Flags).GetValue(null);
                body.SetBuffCount(seconds.buffIndex, 12);
                if (!(bool)Call("DownedRescue", "IsDowned", body)) throw new Exception("Published seconds not read as downed");
                if ((bool)Call("DownedRescue", "IsUp", body)) throw new Exception("Downed body still up");
                if (!health.alive) throw new Exception("Downed body must stay alive");
                body.SetBuffCount(seconds.buffIndex, 0);
                if (!(bool)Call("DownedRescue", "IsUp", body)) throw new Exception("Cleared body not up");
            });
        }
        catch (Exception e) { Logger.LogError("SMOKE FAIL Downed rescue fixture " + e); }
        finally
        {
            foreach (var root in roots) if (root) DestroyImmediate(root);
            if (!wasServer) { NetworkServer.Shutdown(); NetworkServer.dontListen = false; }
        }
    }
}
