using System;
using System.Collections.Generic;
using CoffeeGame.World;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CoffeeGame.Presentation
{
    // Scenery is assembled once into spatial batches. It never participates in
    // collision, spawning, navigation or Unity's gameplay random stream.
    [ExecuteAlways]
    public sealed class ForestArenaVisuals : MonoBehaviour
    {
        public const string GroundTextureResource = "Art/Environment/ForestV20/forest-soil-albedo";
        public const string ShaderResource = "Materials/ForestSurface";
        private static readonly int FocusId = Shader.PropertyToID("_ForestFocus");
        private readonly List<Object> owned = new List<Object>();
        private Transform focus;
        public Material GroundMaterial { get; private set; }
        public int TreeCount { get; private set; }
        public int RockCount { get; private set; }
        public int FernCount { get; private set; }
        public int GrassCount { get; private set; }
        public int TriangleCount { get; private set; }
        public int BatchCount { get; private set; }

        public static bool UseForest => !Array.Exists(Environment.GetCommandLineArgs(),
            argument => string.Equals(argument, "-legacyGrassland", StringComparison.OrdinalIgnoreCase));

        public static ForestArenaVisuals Create(Transform parent)
        {
            var root = new GameObject("Forest clearing scenery (visual only)");
            root.transform.SetParent(parent, false);
            var forest = root.AddComponent<ForestArenaVisuals>();
            forest.Build();
            return forest;
        }

        public void SetFocus(Transform target)
        {
            focus = target;
            UpdateFocus();
        }

        private void LateUpdate() => UpdateFocus();

        private void UpdateFocus()
        {
            Vector3 point = focus != null ? focus.position + Vector3.up * 0.9f : new Vector3(-1.6f, 0.9f, 0f);
            Shader.SetGlobalVector(FocusId, new Vector4(point.x, point.y, point.z, 1f));
        }

        private void Build()
        {
            Shader shader = Resources.Load<Shader>(ShaderResource);
            if (shader == null) throw new InvalidOperationException("Forest surface shader is missing.");
            var material = Own(new Material(shader) { name = "Forest bark, moss and foliage" });
            GroundMaterial = Own(new Material(shader) { name = "Forest clearing soil and moss" });
            GroundMaterial.SetFloat("_Ground", 1f);
            GroundMaterial.SetTexture("_BaseMap", Resources.Load<Texture2D>(GroundTextureResource));
            // Extend only the visible floor beyond the unchanged arena collider.
            // The edge camera must not reveal floating trees over the clear color.
            var skirt = new Geometry();
            const float far = 65f, y = -0.006f;
            void FloorPatch(float left, float right, float bottom, float top)
            {
                skirt.Quad(new Vector3(left,y,bottom),new Vector3(left,y,top),
                    new Vector3(right,y,top),new Vector3(right,y,bottom),Color.white,0);
            }
            FloorPatch(-far,far,StageLayout.MaxZ,far);
            FloorPatch(-far,far,-far,StageLayout.MinZ);
            FloorPatch(-far,StageLayout.MinX,StageLayout.MinZ,StageLayout.MaxZ);
            FloorPatch(StageLayout.MaxX,far,StageLayout.MinZ,StageLayout.MaxZ);
            var extension = new GameObject("Forest floor extension (visual only)");
            extension.transform.SetParent(transform,false);
            extension.AddComponent<MeshFilter>().sharedMesh = Own(skirt.ToMesh(extension.name));
            MeshRenderer extensionRenderer = extension.AddComponent<MeshRenderer>();
            extensionRenderer.sharedMaterial = GroundMaterial;
            extensionRenderer.shadowCastingMode = ShadowCastingMode.Off;
            TriangleCount += 8;
            BatchCount++;
            UpdateFocus();
            var random = new System.Random(210906);
            var batches = new Dictionary<Vector2Int, Geometry>();
            Geometry At(Vector3 point)
            {
                var key = new Vector2Int(Mathf.FloorToInt(point.x / 7f), Mathf.FloorToInt(point.z / 7f));
                if (!batches.TryGetValue(key, out Geometry geometry)) batches.Add(key, geometry = new Geometry());
                return geometry;
            }

            // Asymmetric near trees frame the initial camera. A broad opening
            // keeps the original hero/slime spawn area readable.
            Vector3[] nearTrees = {
                new Vector3(-5.9f,0,-1.1f), new Vector3(3.1f,0,2.1f),
                new Vector3(-5.1f,0,4.4f), new Vector3(-1.8f,0,5.7f),
                new Vector3(2.2f,0,6.4f), new Vector3(6.2f,0,-1.8f),
                new Vector3(-8.3f,0,1.8f), new Vector3(5.7f,0,5.6f),
                new Vector3(-4.7f,0,-5.4f), new Vector3(3.5f,0,-5.8f)
            };
            foreach (Vector3 position in nearTrees) AddTree(At(position), position, Range(random, 4.3f, 5.8f), random);
            for (int z = -15; z <= 17; z += 4)
                for (int x = -25; x <= 25; x += 4)
                {
                    Vector3 position = new Vector3(x + Range(random,-1.4f,1.4f),0,z + Range(random,-1.3f,1.3f));
                    if (position.x > -9.8f && position.x < 8f && position.z > -7.6f && position.z < 8.3f) continue;
                    if (random.NextDouble() < 0.28) continue;
                    AddTree(At(position), position, Range(random,4.2f,7.8f), random);
                }

            for (int i = 0; i < 190; i++)
            {
                Vector3 p = new Vector3(Range(random,-20f,20f),0,Range(random,-12f,13f));
                if (InClearing(p, 1.12f)) continue;
                float size = Range(random,0.22f,0.8f);
                if (i % 3 == 0)
                {
                    Rock(At(p), p, size, random);
                    RockCount++;
                }
                else
                {
                    At(p).Occluder = new Vector4(p.x,p.y,p.z,size*0.7f);
                    // Rounded low shrubs are built from leafy twigs, not spheres.
                    for (int branch = 0; branch < 5; branch++)
                    {
                        Vector3 tip = p + new Vector3(Range(random,-0.42f,0.42f),size,Range(random,-0.42f,0.42f));
                        At(p).Tube(p, tip, 0.025f, 0.006f, 5, Bark, random);
                        LeafSpray(At(p), tip, Vector3.one * size * 0.65f, 24, random);
                    }
                    At(p).Occluder = Vector4.zero;
                }
            }
            for (int i = 0; i < 1800; i++)
            {
                Vector3 p = new Vector3(Range(random,-20f,20f),0.012f,Range(random,-12f,13f));
                float clearing = ClearingDistance(p);
                if (clearing < 0.86f || (clearing < 1.1f && i % 5 != 0)) continue;
                if (i % 8 == 0)
                {
                    Fern(At(p), p, Range(random,0.45f,0.8f), random);
                    FernCount++;
                }
                else
                {
                    Grass(At(p), p, Range(random,0.12f,0.34f), random);
                    GrassCount++;
                }
            }
            // Flat leaf litter and pebbles give the clearing scale without
            // hiding feet, enemy tells or sword effects.
            for (int i = 0; i < 1100; i++)
            {
                Vector3 p = new Vector3(Range(random,-19f,19f),0.014f,Range(random,-10.5f,10.5f));
                float a = Range(random,0,Mathf.PI*2f);
                Vector3 direction = new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                Color color = Color.Lerp(new Color(0.3f,0.23f,0.1f),new Color(0.61f,0.48f,0.19f),Range(random,0,1));
                At(p).Leaf(p,direction*Range(random,0.07f,0.15f),Vector3.Cross(direction,Vector3.up)*0.038f,0.009f,color,0);
            }
            // Fallen timber and a mossy boulder form recognizable landmarks.
            AddLog(At(new Vector3(-6.4f,0,2.2f)),new Vector3(-6.4f,0.3f,2.2f),new Vector3(-4.6f,0.22f,3.6f),random);
            Rock(At(new Vector3(2.7f,0,3.2f)),new Vector3(2.7f,0,3.2f),0.85f,random); RockCount++;
            foreach (var pair in batches)
            {
                if (pair.Value.VertexCount == 0) continue;
                var batch = new GameObject($"Forest sector {pair.Key.x},{pair.Key.y}");
                batch.transform.SetParent(transform,false);
                Mesh mesh = Own(pair.Value.ToMesh(batch.name));
                batch.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = batch.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.TwoSided;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                TriangleCount += mesh.triangles.Length / 3;
                BatchCount++;
            }
            Debug.Log($"CoffeeGAME forest: trees={TreeCount}, rocks={RockCount}, ferns={FernCount}, grass={GrassCount}, triangles={TriangleCount}, batches={BatchCount}; visual-only.");
        }

        private static readonly Color Bark = new Color(0.27f,0.2f,0.13f);
        private static readonly Color LeafDark = new Color(0.16f,0.32f,0.11f);
        private static readonly Color LeafLight = new Color(0.43f,0.6f,0.22f);
        private static float Range(System.Random random, float min, float max) => min + (max-min)*(float)random.NextDouble();
        public static float ClearingDistance(Vector3 p) => new Vector2((p.x+1.6f)/4.1f,p.z/3.1f).magnitude;
        private static bool InClearing(Vector3 p,float margin) => ClearingDistance(p) < margin;

        private void AddTree(Geometry g, Vector3 p, float height, System.Random random)
        {
            g.Occluder = new Vector4(p.x,p.y,p.z,height*0.34f);
            TreeCount++;
            float yaw = Range(random,0,Mathf.PI*2f);
            Vector3 lean = new Vector3(Mathf.Cos(yaw),0,Mathf.Sin(yaw))*height*0.055f;
            Vector3 lower=p,upper=p;
            for (int segment=0;segment<5;segment++)
            {
                float t=(segment+1)/5f;
                upper=p+Vector3.up*(height*t)+lean*t*t;
                g.Tube(lower,upper,Mathf.Lerp(0.26f,0.04f,(t-0.2f))*height/5f,Mathf.Lerp(0.26f,0.04f,t)*height/5f,9,Bark,random);
                lower=upper;
            }
            for (int root=0;root<7;root++)
            {
                float a=yaw+root*Mathf.PI*2f/7f;
                Vector3 end=p+new Vector3(Mathf.Cos(a),0.025f,Mathf.Sin(a))*Range(random,0.65f,1.1f);
                g.Tube(p+Vector3.up*0.25f,end,0.16f,0.025f,6,Bark,random);
            }
            for (int branch=0;branch<9;branch++)
            {
                float a=yaw+branch*2.399f;
                float level=0.48f+branch*0.047f;
                Vector3 from=p+Vector3.up*(height*level)+lean*level;
                float spread=height*Range(random,0.18f,0.33f)*(1-branch*0.035f);
                Vector3 to=from+new Vector3(Mathf.Cos(a)*spread,height*0.16f,Mathf.Sin(a)*spread);
                Vector3 fork=Vector3.Lerp(from,to,0.6f)+Vector3.up*height*0.045f;
                g.Tube(from,fork,0.095f,0.045f,6,Bark,random);
                g.Tube(fork,to,0.045f,0.009f,5,Bark,random);
                LeafSpray(g,to,new Vector3(spread*0.83f,height*0.105f,spread*0.8f),36,random);
            }
            LeafSpray(g,upper,new Vector3(height*0.17f,height*0.12f,height*0.17f),40,random);
            g.Occluder = Vector4.zero;
        }

        private static void LeafSpray(Geometry g,Vector3 center,Vector3 radius,int leaves,System.Random random)
        {
            // Overlapping folded leaves give a lobed silhouette, translucent
            // underside lighting and real dappled shadow without alpha textures.
            for(int i=0;i<leaves;i++)
            {
                float a=Range(random,0,Mathf.PI*2f),y=Range(random,-1,1),r=Mathf.Sqrt(1-y*y);
                Vector3 p=center+Vector3.Scale(new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r),radius)*Range(random,0.25f,1f);
                Vector3 axis=new Vector3(Mathf.Cos(a),Range(random,-0.35f,0.6f),Mathf.Sin(a)).normalized;
                float size=Range(random,0.18f,0.32f);
                g.Leaf(p,axis*size,Vector3.Cross(axis,Vector3.up).normalized*size*0.48f,size*0.075f,
                    Color.Lerp(LeafDark,LeafLight,Mathf.Clamp01(y*0.3f+Range(random,0.2f,0.7f))),1f);
            }
        }

        private static void Grass(Geometry g,Vector3 p,float size,System.Random random)
        {
            for(int blade=0;blade<7;blade++)
            {
                float a=Range(random,0,Mathf.PI*2f);
                Vector3 side=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                Vector3 b=p+side*Range(random,0,0.12f),tip=b+Vector3.up*size+side*size*0.48f;
                Color color=Color.Lerp(LeafDark,LeafLight,Range(random,0.1f,0.9f));
                Vector3 middle=b+Vector3.up*size*0.57f+side*size*0.16f;
                g.Quad(b-side*0.014f,b+side*0.014f,middle+side*0.01f,middle-side*0.01f,color*0.88f,0.75f);
                g.Triangle(middle-side*0.01f,middle+side*0.01f,tip,color,0.75f);
            }
        }

        private static void Fern(Geometry g,Vector3 p,float size,System.Random random)
        {
            for(int frond=0;frond<7;frond++)
            {
                float a=frond*Mathf.PI*2f/7f+Range(random,-0.2f,0.2f);
                Vector3 d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),side=Vector3.Cross(d,Vector3.up);
                for(int segment=1;segment<8;segment++)
                {
                    float t=segment/8f;
                    Vector3 c=p+d*size*t+Vector3.up*(Mathf.Sin(t*Mathf.PI*0.85f)*size*0.58f);
                    float length=size*0.23f*(1-t*0.65f);
                    Color color=Color.Lerp(LeafDark,LeafLight,0.25f+t*0.45f);
                    g.Leaf(c,(side+d*0.25f)*length,d*length*0.27f,0.025f,color,0.8f);
                    g.Leaf(c,(-side+d*0.25f)*length,d*length*0.27f,0.025f,color,0.8f);
                }
            }
        }

        private static void Rock(Geometry g,Vector3 p,float size,System.Random random)
        {
            const int sides=9,rings=4;
            var points=new Vector3[(rings+1)*sides];
            for(int ring=0;ring<=rings;ring++)
                for(int i=0;i<sides;i++)
                {
                    float t=ring/(float)rings,a=i*Mathf.PI*2f/sides;
                    float radius=Mathf.Sin(t*Mathf.PI)*Range(random,0.8f,1.14f);
                    points[ring*sides+i]=p+new Vector3(Mathf.Cos(a)*radius,t*1.1f-0.09f,Mathf.Sin(a)*radius*0.77f)*size;
                }
            for(int ring=0;ring<rings;ring++)
                for(int i=0;i<sides;i++)
                {
                    int j=(i+1)%sides;
                    Color color=Color.Lerp(new Color(0.35f,0.38f,0.32f),new Color(0.39f,0.42f,0.35f),Range(random,0,1));
                    if(ring>=2&&random.NextDouble()>0.25)color=Color.Lerp(new Color(0.31f,0.39f,0.2f),new Color(0.35f,0.42f,0.22f),Range(random,0,1));
                    g.RoundedRockQuad(points[ring*sides+i],points[(ring+1)*sides+i],points[(ring+1)*sides+j],points[ring*sides+j],p+Vector3.up*size*0.46f,color);
                }
        }

        private static void AddLog(Geometry g,Vector3 a,Vector3 b,System.Random random)
        {
            g.Tube(a,b,0.23f,0.18f,12,Bark,random);
            g.Tube(a-Vector3.up*0.07f,b-Vector3.up*0.04f,0.14f,0.14f,10,new Color(0.49f,0.35f,0.17f),random);
            for(int i=0;i<8;i++)LeafSpray(g,Vector3.Lerp(a,b,i/8f)+Vector3.up*0.15f,Vector3.one*0.16f,7,random);
        }

        private T Own<T>(T item) where T:Object { owned.Add(item); return item; }
        private void OnDestroy()
        {
            Shader.SetGlobalVector(FocusId,Vector4.zero);
            foreach(Object item in owned)
                if(item!=null) { if(Application.isPlaying) Destroy(item); else DestroyImmediate(item); }
        }

        private sealed class Geometry
        {
            private readonly List<Vector3> vertices=new List<Vector3>();
            private readonly List<Color> colors=new List<Color>();
            private readonly List<Vector3> normals=new List<Vector3>();
            private readonly List<Vector4> occluders=new List<Vector4>();
            private readonly List<int> indices=new List<int>();
            public Vector4 Occluder { get; set; }
            public int VertexCount=>vertices.Count;
            public void Triangle(Vector3 a,Vector3 b,Vector3 c,Color color,float wind)
            {
                int start=vertices.Count;
                vertices.Add(a);vertices.Add(b);vertices.Add(c);
                Vector3 normal=Vector3.Cross(b-a,c-a).normalized;
                normals.Add(normal);normals.Add(normal);normals.Add(normal);
                occluders.Add(Occluder);occluders.Add(Occluder);occluders.Add(Occluder);
                color=RuntimeMaterialFactory.ToShaderColor(color);color.a=wind;
                colors.Add(color);colors.Add(color);colors.Add(color);
                indices.Add(start);indices.Add(start+1);indices.Add(start+2);
            }
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,float wind)
            { Triangle(a,b,c,color,wind);Triangle(a,c,d,color,wind); }
            public void RoundedRockQuad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 center,Color color)
            {
                Quad(a,b,c,d,color,0);
                for(int i=vertices.Count-6;i<vertices.Count;i++)
                    normals[i]=Vector3.Scale(vertices[i]-center,new Vector3(1f,1.6f,1.69f)).normalized;
            }
            public void Leaf(Vector3 start,Vector3 axis,Vector3 side,float fold,Color color,float wind)
            {
                int first=vertices.Count;
                Vector3 center=start+axis*0.52f+Vector3.up*fold;
                Vector3 a=start+axis*0.28f+side*0.74f,b=start+axis*0.68f+side*0.68f;
                Vector3 c=start+axis*0.68f-side*0.68f,d=start+axis*0.28f-side*0.74f;
                Triangle(start,a,center,color,wind);
                Triangle(a,b,center,color,wind);
                Triangle(b,start+axis,center,color,wind);
                Color shade=color*new Color(0.91f,0.96f,0.89f,1);
                Triangle(start+axis,c,center,shade,wind);
                Triangle(c,d,center,shade,wind);
                Triangle(d,start,center,color,wind);
                // A gently curved surface, not six independently lit flat facets.
                Vector3 normal=Vector3.Cross(side,axis).normalized;
                for(int i=first;i<vertices.Count;i++)
                {
                    float edge=Vector3.Dot(vertices[i]-start,side.normalized)/Mathf.Max(side.magnitude,0.001f);
                    normals[i]=(normal+side.normalized*edge*0.22f).normalized;
                }
            }
            public void Tube(Vector3 a,Vector3 b,float r0,float r1,int sides,Color color,System.Random random)
            {
                Vector3 axis=(b-a).normalized;
                Vector3 u=Vector3.Cross(axis,Mathf.Abs(axis.y)>0.9f?Vector3.forward:Vector3.up).normalized;
                Vector3 v=Vector3.Cross(axis,u);
                for(int i=0;i<sides;i++)
                {
                    float angle=i*Mathf.PI*2f/sides,next=(i+1)*Mathf.PI*2f/sides;
                    Vector3 n=u*Mathf.Cos(angle)+v*Mathf.Sin(angle),m=u*Mathf.Cos(next)+v*Mathf.Sin(next);
                    Color shade=color*Range(random,0.94f,1.06f);shade.a=1;
                    Quad(a+n*r0,a+m*r0,b+m*r1,b+n*r1,shade,0);
                    int q=normals.Count-6;
                    normals[q]=n;normals[q+1]=m;normals[q+2]=m;
                    normals[q+3]=n;normals[q+4]=m;normals[q+5]=n;
                    Triangle(b,b+n*r1,b+m*r1,color,0);
                    Triangle(a,a+m*r0,a+n*r0,color,0);
                }
            }
            public Mesh ToMesh(string name)
            {
                var mesh=new Mesh { name=name,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16 };
                mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetNormals(normals);
                mesh.SetUVs(0,occluders);mesh.SetTriangles(indices,0);
                mesh.RecalculateBounds();
                Bounds bounds=mesh.bounds;bounds.Expand(0.3f);mesh.bounds=bounds;
                return mesh;
            }
        }
    }
}
