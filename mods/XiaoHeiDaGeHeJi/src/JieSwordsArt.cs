using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiftSurvivor
{
    // Shared faceted steel, gilt furniture and a feathered jade/gold light inlay.
    // All geometry is collider-free; every instance uses the same three submeshes.
    internal static class JieSwordArt
    {
        private static Mesh sword;
        private static Material fittings, trailMaterial;
        private static Texture2D palette;
        private static Color32[] paletteColors;
        private static readonly Dictionary<Color, Material[]> finishes = new Dictionary<Color, Material[]>();
        internal static readonly Color Jade = new Color(.12f, .94f, .79f);

        internal static void Attach(Transform parent, Color color)
        {
            if (!sword) sword = Build();
            if (!fittings) fittings = CombatVisuals.Surface("JieSwordGiltFittings", palette, .76f);
            if (!finishes.TryGetValue(color, out var materials))
            {
                var tones = (Color32[])paletteColors.Clone();
                var emission = new Color32[tones.Length];
                bool golden = color.r > .8f && color.g < .85f;
                for (int i = 0; i < 4; i++) tones[i] = Color.Lerp(tones[i], color, golden ? .38f : .12f);
                tones[4] = Color.Lerp(color, Color.white, .32f);
                emission[4] = Color.Lerp(color, Color.white, .12f);
                var blade = CombatVisuals.Surface("JieSwordPolishedSteel",
                    FriendSkin.Palette("JieSwordSteelFinish", tones), .82f,
                    FriendSkin.Palette("JieSwordLuminousInlay", emission), 2.7f);
                if (blade.HasProperty("_SpecularStrength")) blade.SetFloat("_SpecularStrength", .55f);
                var light = CombatVisuals.Radiance("JieSwordInlayBloom");
                if (light.HasProperty("_TintColor")) light.SetColor("_TintColor", color);
                materials = new[] { blade, fittings, light };
                finishes.Add(color, materials);
            }
            parent.gameObject.AddComponent<MeshFilter>().sharedMesh = sword;
            var renderer = parent.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        internal static TrailRenderer[] Trail(Transform parent, Color color, float width, float lifetime)
        {
            if (!trailMaterial) trailMaterial = CombatVisuals.SoftRadiance("JieSwordSoftFlightTrail", true);
            var result = new TrailRenderer[2];
            for (int i = 0; i < result.Length; i++)
            {
                var node = new GameObject(i == 0 ? "SwordMistTrail" : "SwordWhiteCoreTrail");
                node.transform.SetParent(parent, false);
                var trail = node.AddComponent<TrailRenderer>();
                trail.sharedMaterial = trailMaterial;
                trail.time = lifetime * (i == 0 ? 1f : .65f);
                trail.minVertexDistance = .055f;
                trail.widthMultiplier = width * (i == 0 ? 1f : .20f);
                trail.widthCurve = new AnimationCurve(new Keyframe(0f,.68f),new Keyframe(.12f,1f),new Keyframe(1f,0f));
                var hue = i == 0 ? color : Color.Lerp(color, Color.white, .8f);
                trail.colorGradient = new Gradient {
                    colorKeys = new[] {new GradientColorKey(hue,0f),new GradientColorKey(color,1f)},
                    alphaKeys = new[] {new GradientAlphaKey(i == 0 ? .32f : .86f,0f),
                        new GradientAlphaKey(.14f,.55f),new GradientAlphaKey(0f,1f)}
                };
                trail.numCapVertices = trail.numCornerVertices = 4;
                trail.shadowCastingMode = ShadowCastingMode.Off;
                trail.receiveShadows = false;
                result[i] = trail;
            }
            return result;
        }

        private static Mesh Build()
        {
            var positions = new List<Vector3>(); var colors = new List<Color>();
            var uv = new List<Vector2>(); var swatches = new List<Color32>();
            var blade = new List<int>(); var furniture = new List<int>(); var radiance = new List<int>();
            void Face(List<int> triangles, Color tint, params Vector3[] vertices)
            {
                int first = positions.Count;
                int swatch = swatches.IndexOf((Color32)tint);
                if (swatch < 0) { swatch = swatches.Count; swatches.Add(tint); }
                foreach (var vertex in vertices) { positions.Add(vertex); colors.Add(tint); uv.Add(new Vector2(swatch,.5f)); }
                for (int i = 1; i + 1 < vertices.Length; i++)
                { triangles.Add(first); triangles.Add(first+i); triangles.Add(first+i+1); }
            }
            Vector3[] Section(float z, float width, float ridge) => new[] {
                new Vector3(-width,0,z),new Vector3(0,ridge,z),new Vector3(width,0,z),new Vector3(0,-ridge,z) };
            var sections = new[] { Section(-.23f,.047f,.016f),Section(-.13f,.068f,.024f),
                Section(.39f,.043f,.018f),Section(.61f,.025f,.011f),Section(.79f,0f,0f) };
            var facets = new[] {new Color(.91f,.99f,1f),new Color(.39f,.56f,.63f),
                new Color(.66f,.82f,.88f),new Color(.96f,1f,1f)};
            // Add swatches in a fixed order so the emissive texture only lights the inlay.
            foreach (var facet in facets) swatches.Add(facet);
            var inlay = new Color(.22f,.91f,.81f); swatches.Add(inlay);
            for (int i=0;i<4;i++) for (int j=0;j<sections.Length-1;j++)
                Face(blade,facets[i],sections[j][i],sections[j+1][i],sections[j+1][(i+1)%4],sections[j][(i+1)%4]);
            foreach (int sign in new[]{-1,1})
            {
                Face(blade,inlay,new Vector3(-.007f,sign*.025f,-.12f),new Vector3(.007f,sign*.025f,-.12f),
                    new Vector3(.005f,sign*.019f,.38f),new Vector3(0,sign*.013f,.58f),new Vector3(-.005f,sign*.019f,.38f));
                for (int rune=0;rune<4;rune++)
                {
                    float z=.015f+rune*.095f, y=sign*(.025f-.009f*(z+.12f));
                    Face(blade,inlay,new Vector3(-.016f,y,z),new Vector3(0,y+.001f*sign,z+.024f),
                        new Vector3(.016f,y,z),new Vector3(0,y+.001f*sign,z+.041f));
                }
            }
            var bronze = new Color(.54f,.33f,.12f); var brass = new Color(.98f,.78f,.36f);
            var grip = new Color(.045f,.12f,.14f);
            void Sleeve(float z0,float z1,float r0,float r1,Color tint)
            {
                const int count=12;
                Vector3 Ring(int i,float z,float r) => new Vector3(Mathf.Cos(i*2f*Mathf.PI/count)*r,Mathf.Sin(i*2f*Mathf.PI/count)*r,z);
                for(int i=0;i<count;i++)
                {
                    Face(furniture,tint,Ring(i,z0,r0),Ring(i+1,z0,r0),Ring(i+1,z1,r1),Ring(i,z1,r1));
                    Face(furniture,tint,Ring(i,z0,r0),new Vector3(0,0,z0),Ring(i+1,z0,r0));
                    Face(furniture,tint,Ring(i+1,z1,r1),new Vector3(0,0,z1),Ring(i,z1,r1));
                }
            }
            Sleeve(-.59f,-.29f,.024f,.029f,grip);
            for(int i=0;i<8;i++) { float z=-.572f+i*.034f; Sleeve(z,z+.006f,.028f,.028f,bronze); }
            Sleeve(-.325f,-.215f,.035f,.037f,brass);
            Sleeve(-.685f,-.635f,.005f,.049f,brass); Sleeve(-.635f,-.59f,.049f,.024f,bronze);
            // Sculpted swept guards have a pale bevel, a dark sidewall and inset jade gems.
            foreach(int sign in new[]{-1,1})
            {
                var profile=new[]{new Vector2(0,-.225f),new Vector2(.08f,-.21f),new Vector2(.165f,-.135f),
                    new Vector2(.185f,-.15f),new Vector2(.152f,-.244f),new Vector2(.055f,-.285f),new Vector2(0,-.285f)};
                var front=new Vector3[profile.Length]; var back=new Vector3[profile.Length];
                for(int i=0;i<profile.Length;i++)
                { var p=profile[i]; front[i]=new Vector3(sign*p.x,.025f,p.y);back[i]=new Vector3(sign*p.x,-.025f,p.y); }
                Face(furniture,brass,front); Face(furniture,brass,back);
                for(int i=0;i<front.Length;i++)
                { int j=(i+1)%front.Length;Face(furniture,bronze,front[i],back[i],back[j],front[j]); }
                foreach(int side in new[]{-1,1})
                    Face(blade,inlay,new Vector3(sign*.064f,side*.027f,-.244f),new Vector3(sign*.09f,side*.027f,-.231f),
                        new Vector3(sign*.131f,side*.027f,-.203f),new Vector3(sign*.105f,side*.027f,-.244f));
            }
            // Two intersecting feathered strips keep the bloom visible from either flank.
            // Their transparent outer edges preserve the readable metal silhouette.
            void GlowStrip(Quaternion rotation)
            {
                int first=positions.Count;
                var x=new[]{-.055f,-.009f,.009f,.055f}; var alpha=new[]{0f,.42f,.42f,0f};
                for(int row=0;row<3;row++) for(int col=0;col<4;col++)
                {
                    float z=row==0?-.13f:row==1?.35f:.70f;
                    positions.Add(rotation*new Vector3(x[col]*(row==2?.12f:1f),.029f,z));
                    colors.Add(new Color(1f,1f,1f,alpha[col]*(row==2?0f:1f))); uv.Add(new Vector2(0,.5f));
                }
                for(int row=0;row<2;row++)for(int col=0;col<3;col++)
                { int i=first+row*4+col;radiance.AddRange(new[]{i,i+4,i+1,i+1,i+4,i+5}); }
            }
            GlowStrip(Quaternion.identity);GlowStrip(Quaternion.Euler(0,0,180));
            GlowStrip(Quaternion.Euler(0,0,90));GlowStrip(Quaternion.Euler(0,0,270));
            var mesh=new Mesh{name="JieCelestialForgedSword",subMeshCount=3};
            mesh.SetVertices(positions);mesh.SetColors(colors);
            for(int i=0;i<uv.Count;i++)uv[i]=new Vector2((uv[i].x+.5f)/swatches.Count,.5f);
            mesh.SetUVs(0,uv);paletteColors=swatches.ToArray();
            palette=FriendSkin.Palette("JieSwordMetalPalette",paletteColors);
            mesh.SetTriangles(blade,0);mesh.SetTriangles(furniture,1);mesh.SetTriangles(radiance,2);
            mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
