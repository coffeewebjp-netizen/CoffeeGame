using System.Collections.Generic;
using UnityEngine;

namespace CoffeeGame.Combat
{
    // Local geometry keeps the gate's transparent membrane and silhouette readable from any camera.
    public sealed class DragonEffect : MonoBehaviour
    {
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<LineRenderer> lines = new List<LineRenderer>();
        private GameObject owner;
        private float duration, elapsed, radius;
        private bool wave;
        private bool descending;
        private Vector3 destination;
        private Vector3 scale;
        private static DragonEffect New(string name, Vector3 position, Vector3 facing, float seconds, GameObject source)
        {
            var root = new GameObject(name); root.transform.position = position;
            root.transform.rotation = Quaternion.LookRotation(facing.sqrMagnitude > .001f ? facing : Vector3.forward);
            var effect = root.AddComponent<DragonEffect>(); effect.owner = source; effect.duration = seconds;
            return effect;
        }
        private Material Glow(Color color)
        {
            var material = CombatGlowVisuals.CreateMaterial("Dragon light", color); materials.Add(material); return material;
        }
        private Material Body(Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name="Dragon pearl scales" };
            material.SetColor("_BaseColor",color);material.SetFloat("_Metallic",.28f);material.SetFloat("_Smoothness",.4f);
            material.SetFloat("_Cull",0f);materials.Add(material);return material;
        }
        private LineRenderer Line(string name, Vector3[] points, float width, Material material, bool loop = false)
        {
            var line = CombatGlowVisuals.Line(transform, name, material, points.Length, width, loop);
            line.SetPositions(points); lines.Add(line); return line;
        }
        private void Solid(string name, Vector3 position, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Sphere); part.name = name;
            part.transform.SetParent(transform, false); part.transform.localPosition = position; part.transform.localScale = size;
            var collider = part.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            part.GetComponent<Renderer>().sharedMaterial = material;
        }
        private void Horn(Vector3 start, Vector3 tip, Material material, float width)
        {
            if (material.shader.name == "Universal Render Pipeline/Lit")
            {
                Tube("Dragon horn", new[] { start, Vector3.Lerp(start, tip, .6f) + Vector3.back * .07f, tip }, width * .5f, material, true);
                return;
            }
            var line = Line("Dragon horn", new[] { start, Vector3.Lerp(start, tip, .6f) + Vector3.back * .07f, tip }, width, material);
            line.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, .02f);
        }
        private void Tube(string name, Vector3[] points, float radius, Material material, bool taper = false)
        {
            const int sides = 10;
            var vertices = new Vector3[points.Length * sides];
            var triangles = new int[(points.Length - 1) * sides * 6];
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 tangent = (points[Mathf.Min(i + 1, points.Length - 1)] - points[Mathf.Max(0, i - 1)]).normalized;
                Vector3 normal = Vector3.Cross(tangent, Mathf.Abs(tangent.z) < .9f ? Vector3.forward : Vector3.up).normalized;
                Vector3 binormal = Vector3.Cross(tangent, normal).normalized;
                float r = radius * (taper ? Mathf.Lerp(1f, .025f, i / (float)(points.Length - 1)) : 1f);
                for (int side = 0; side < sides; side++)
                {
                    float a = side * Mathf.PI * 2f / sides;
                    vertices[i * sides + side] = points[i] + r * (normal * Mathf.Cos(a) + binormal * Mathf.Sin(a));
                    if (i == points.Length - 1) continue;
                    int n = (side + 1) % sides, at = (i * sides + side) * 6;
                    int v = i * sides + side, next = i * sides + n;
                    triangles[at] = v; triangles[at + 1] = next; triangles[at + 2] = v + sides;
                    triangles[at + 3] = next; triangles[at + 4] = next + sides; triangles[at + 5] = v + sides;
                }
            }
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshes.Add(mesh);
            var part = new GameObject(name); part.transform.SetParent(transform, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
        private void Head(Vector3 p, float size, Material body, Material light)
        {
            Solid("Dragon skull", p, new Vector3(.42f,.3f,.32f)*size, body);
            Solid("Dragon muzzle", p+new Vector3(.22f,-.04f,0)*size,new Vector3(.35f,.16f,.23f)*size,body);
            Solid("Dragon lower jaw",p+new Vector3(.24f,-.14f,0)*size,new Vector3(.32f,.06f,.20f)*size,body);
            for (int side=-1;side<=1;side+=2)
            {
                Solid("Dragon eye",p+new Vector3(.1f,.08f,side*.15f)*size,Vector3.one*.055f*size,light);
                Horn(p+new Vector3(-.02f,.14f,side*.14f)*size,p+new Vector3(.18f,.16f,side*.17f)*size,body,.06f*size);
                for(int tooth=0;tooth<3;tooth++)Horn(p+new Vector3(.14f+tooth*.09f,-.08f,side*.09f)*size,p+new Vector3(.17f+tooth*.09f,-.16f,side*.08f)*size,body,.028f*size);
                Horn(p+new Vector3(-.12f,.1f,side*.08f)*size,p+new Vector3(-.35f,.52f,side*.23f)*size,body,.09f*size);
                Horn(p+new Vector3(.3f,-.02f,side*.09f)*size,p+new Vector3(.1f,-.16f,side*.45f)*size,light,.022f*size);
            }
        }
        public static DragonEffect Gate(Vector3 center, Vector3 facing, float r, float seconds, GameObject owner)
        {
            var e = New("Dragon gate — translucent mirror",center,facing,seconds,owner);
            Material pearl=e.Body(new Color(.5f,.65f,.72f,1f)), ink=e.Body(new Color(.12f,.20f,.25f,1f)), gold=e.Glow(new Color(.8f,.65f,.3f,.6f));
            var points=new Vector3[97];
            for(int i=0;i<points.Length;i++) {float a=i*Mathf.PI*2/96;points[i]=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*r;}
            e.Tube("Coiled dragon body",points,r*.065f,pearl);
            for(int i=0;i<40;i++)
            {
                float a=i*Mathf.PI*2/40;Vector3 normal=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);
                Vector3 tangent=new Vector3(-normal.y,normal.x,0);
                e.Horn(normal*r,normal*r*1.16f-tangent*r*.08f,i%2==0?pearl:ink,r*.09f);
            }
            e.Head(new Vector3(-.05f,r,0),r*.9f,pearl,gold);
            e.Head(new Vector3(.05f,-r,0),r*.8f,ink,gold);
            Material membrane=e.Glow(new Color(.25f,.65f,.85f,.13f));
            var vertices=new Vector3[66];var triangles=new int[64*3];vertices[0]=Vector3.zero;
            for(int i=0;i<=64;i++) vertices[i+1]=new Vector3(Mathf.Cos(i*Mathf.PI*2/64),Mathf.Sin(i*Mathf.PI*2/64),.02f)*r*.94f;
            for(int i=0;i<64;i++){triangles[i*3]=0;triangles[i*3+1]=i+1;triangles[i*3+2]=i+2;}
            var mesh=new Mesh { name="Mirror membrane",vertices=vertices,triangles=triangles };mesh.RecalculateNormals();e.meshes.Add(mesh);
            var disk=new GameObject("Thin transparent membrane");disk.transform.SetParent(e.transform,false);disk.AddComponent<MeshFilter>().sharedMesh=mesh;disk.AddComponent<MeshRenderer>().sharedMaterial=membrane;
            for(int ring=0;ring<3;ring++)
            {
                var ripple=new Vector3[65];for(int i=0;i<65;i++){float a=i*Mathf.PI*2/64;ripple[i]=new Vector3(Mathf.Cos(a),Mathf.Sin(a),-.025f)*r*(.32f+ring*.22f);}
                e.Line("Mirror ripple",ripple,.009f,membrane,true);
            }
            e.scale=Vector3.one;return e;
        }
        public static DragonEffect Dragon(Vector3 position,Vector3 facing,float seconds,GameObject owner)
        {
            var e=New("Allied celestial dragon",position,facing,seconds,owner);
            e.descending=true; e.destination=position;
            e.transform.position=position+Vector3.up*5f;
            var body=e.Body(new Color(.28f,.5f,.57f,1f));var light=e.Glow(new Color(.8f,.7f,.4f,.65f));
            var spine=new Vector3[32];for(int i=0;i<spine.Length;i++){float t=i/31f;spine[i]=new Vector3(Mathf.Sin(t*7)*.6f,Mathf.Cos(t*5)*.25f,-t*3.8f);}
            e.Tube("Serpentine dragon",spine,.19f,body,true);
            e.Head(spine[0],1.7f,body,light);
            for(int i=2;i<27;i+=2)e.Horn(spine[i],spine[i]+Vector3.up*(.45f-i*.009f),light,.13f);
            for(int side=-1;side<=1;side+=2)for(int limb=0;limb<2;limb++)
            {
                Vector3 p=spine[5+limb*9];Vector3 elbow=p+new Vector3(side*.55f,-.25f,.2f),hand=elbow+new Vector3(side*.15f,-.3f,.2f);
                e.Tube("Dragon leg",new[]{p,elbow,hand},.06f,body);
                for(int claw=0;claw<3;claw++)e.Horn(hand,hand+new Vector3((claw-1)*.14f,-.15f,.25f),light,.04f);
            }
            e.scale=Vector3.one*.58f;return e;
        }
        public static DragonEffect Beam(Vector3 from,Vector3 to,float width,float seconds,GameObject owner)
        {
            var e=New("Dragon descent beam",from,(to-from).normalized,seconds,owner);
            float length=Vector3.Distance(from,to);
            var outer=e.Glow(new Color(.25f,.6f,1f,.4f));var inner=e.Glow(new Color(.9f,.98f,1f,.85f));
            e.Line("Dragon breath outer",new[]{Vector3.zero,Vector3.forward*length},width,outer);
            e.Line("Dragon breath core",new[]{Vector3.zero,Vector3.forward*length},width*.32f,inner);
            e.Head(Vector3.forward*.5f,width*.6f,outer,inner);e.scale=Vector3.one;return e;
        }
        public static DragonEffect Claw(Vector3 position,Vector3 facing,float radius,int stage,GameObject owner)
        {
            var e=New("Three claw trails",position,facing,.32f,owner);var mat=e.Glow(new Color(.75f,.95f,1f,.9f));
            for(int claw=0;claw<3;claw++)
            {
                var points=new Vector3[20];for(int i=0;i<20;i++){float a=Mathf.Lerp(-1.2f,1.2f,i/19f);points[i]=new Vector3(Mathf.Sin(a)*radius,(claw-1)*.16f+Mathf.Sin(a)*(stage==2?-.28f:.28f),Mathf.Cos(a)*radius);}
                e.Line("Claw "+claw,points,stage==3?.13f:.065f,mat);
            }
            e.scale=Vector3.one;return e;
        }
        public static DragonEffect Wave(Vector3 position,float radius,float seconds,GameObject owner)
        {
            var e=New("Expanding wind stomp",position+Vector3.up*.08f,Vector3.forward,seconds,owner);e.wave=true;e.radius=radius;
            var mat=e.Glow(new Color(.6f,.95f,1f,.7f));
            for(int ring=0;ring<3;ring++) {var points=new Vector3[65];for(int i=0;i<65;i++){float a=i*Mathf.PI*2/64;points[i]=new Vector3(Mathf.Cos(a),ring*.08f,Mathf.Sin(a))*(1-ring*.13f);}e.Line("Wind ring",points,.04f,mat,true);}
            e.scale=Vector3.one;return e;
        }
        private void Update()
        {
            elapsed+=CombatClock.DeltaTime(owner);
            if(elapsed>=duration || owner==null || !owner.activeInHierarchy){Destroy(gameObject);return;}
            float fade=Mathf.Min(1f,elapsed/.12f)*Mathf.Clamp01((duration-elapsed)/.3f);
            transform.localScale=wave?Vector3.one*Mathf.Lerp(.3f,radius,elapsed/duration):scale*Mathf.Lerp(.75f,1f,Mathf.Clamp01(elapsed/.18f));
            if(descending) transform.position=destination+Vector3.up*(5f*(1f-Mathf.SmoothStep(0f,1f,elapsed/.9f))+Mathf.Sin(elapsed*2f)*.12f);
            foreach(var line in lines)CombatGlowVisuals.Alpha(line,fade);
        }
        private void OnDestroy(){foreach(var m in materials)if(m!=null)Destroy(m);foreach(var m in meshes)if(m!=null)Destroy(m);}
    }
}
