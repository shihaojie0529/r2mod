using System;
using System.Collections.Generic;
using RoR2;
using UnityEngine;

namespace RiftSurvivor
{
    // The game keeps its native motor, hitboxes and model pivot. The visible skin
    // uses the same fifteen-joint rest pose as the editable animation source.
    internal sealed class CuiRig
    {
        internal readonly Transform Root;
        internal readonly Dictionary<string, Transform> Bones = new Dictionary<string, Transform>();
        internal readonly Transform[] OrderedBones;
        internal static readonly string[] Names = {
            "Hips", "Chest", "Head", "LeftUpperArm", "LeftLowerArm", "LeftHand",
            "RightUpperArm", "RightLowerArm", "RightHand", "LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
            "RightUpperLeg", "RightLowerLeg", "RightFoot"
        };
        private static readonly int[] Parents = { -1,0,1,1,3,4,1,6,7,0,9,10,0,12,13 };

        internal CuiRig(Transform model, SkinnedMeshRenderer original, CharacterBody body, Vector3[] pivots)
        {
            Root = new GameObject("CuiGeRig").transform;
            try
            {
                Align(model, original, body, pivots);
                OrderedBones = new Transform[Names.Length];
                for (int i = 0; i < Names.Length; ++i)
                {
                    int parent = Parents[i];
                    var joint = new GameObject(Names[i]).transform;
                    joint.SetParent(parent < 0 ? Root : OrderedBones[parent], false);
                    joint.localPosition = pivots[i] - (parent < 0 ? Vector3.zero : pivots[parent]);
                    OrderedBones[i] = joint;
                    Bones.Add(Names[i], joint);
                }
            }
            catch { UnityEngine.Object.Destroy(Root.gameObject); throw; }
        }

        private void Align(Transform model, SkinnedMeshRenderer original, CharacterBody body, Vector3[] pivots)
        {
            // Work in world space before parenting. Imported Commando assets and
            // the character select display can have different scales / mesh axes.
            var scale = Mathf.Max(.01f, Mathf.Abs(model.lossyScale.y));
            var facing = model.rotation;
            var feet = body ? body.footPosition : model.position;
            var bounds = original.bounds;
            if (bounds.size.y > .1f)
            {
                scale = bounds.size.y / 1.85f;
                if (!body) feet = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            }
            var native = original.bones;
            int hips = Find(native, "pelvis", "hips"), head = Find(native, "head");
            int left = Find(native, "thighl", "leftupperleg"), right = Find(native, "thighr", "rightupperleg");
            if (original.sharedMesh && hips >= 0 && head >= 0 && left >= 0 && right >= 0)
            {
                var matrices = ReadBindposes(original.sharedMesh);
                if (Math.Max(Math.Max(hips, head), Math.Max(left, right)) < matrices.Length)
                {
                    Vector3 Rest(int i) => original.transform.TransformPoint(matrices[i].inverse.MultiplyPoint3x4(Vector3.zero));
                    Vector3 up = Rest(head) - Rest(hips);
                    Vector3 horizontal = Vector3.ProjectOnPlane(Rest(right) - Rest(left), up);
                    if (up.sqrMagnitude > .0001f && horizontal.sqrMagnitude > .00001f)
                    {
                        var forward = Vector3.Cross(horizontal, up).normalized;
                        int toe = Find(native, "toel", "toesl", "toe1l", "lefttoes");
                        int foot = Find(native, "footl", "leftfoot");
                        Vector3 expected = toe >= 0 && toe < matrices.Length && foot >= 0 && foot < matrices.Length
                            ? Rest(toe) - Rest(foot) : model.forward;
                        if (Vector3.Dot(forward, expected) < 0) forward = -forward;
                        facing = Quaternion.LookRotation(forward, up.normalized);
                        scale = up.magnitude / (pivots[2].y-pivots[0].y);
                        feet = Rest(hips) - facing * (pivots[0] * scale);
                    }
                }
            }
            Root.SetPositionAndRotation(feet, facing);
            Root.localScale = Vector3.one * scale;
            Root.SetParent(model, true);
        }

        private static Matrix4x4[] ReadBindposes(Mesh mesh)
        {
            // Some skins strip their CPU-side mesh data. Bounds and the body's
            // foot anchor above still give the authored rig a usable placement.
            try { return mesh.bindposes ?? Array.Empty<Matrix4x4>(); }
            catch (UnityException) { return Array.Empty<Matrix4x4>(); }
        }

        private static int Find(Transform[] bones, params string[] aliases)
        {
            for (int i=0; i<bones.Length; ++i)
            {
                if (!bones[i]) continue;
                string name = bones[i].name.Replace(".", "").Replace("_", "").Replace(" ", "").ToLowerInvariant();
                if (Array.IndexOf(aliases, name) >= 0) return i;
            }
            return -1;
        }
    }
}
