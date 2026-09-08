using System.Collections.Generic;
using CoffeeGame.Presentation;
using UnityEngine;

namespace CoffeeGame.Combat
{
    public enum CatElementEffect { FireShot, FireImpact, WindShot, WindImpact, ThunderCharge, ThunderStrike, EarthWave, TimeSigil }

    // Small, clock-owned mesh/line effects suitable for both desktop and mobile.
    // Hit detection stays in the combat controller/projectile.
    public sealed class CatElementVfx : MonoBehaviour
    {
        private readonly List<LineRenderer> lines=new List<LineRenderer>();
        private readonly List<Transform> rocks=new List<Transform>();
        private readonly List<Material> materials=new List<Material>();
        private float age,lifetime,radius;
        private Vector3[] strikes;
        private Mesh fragmentMesh;
        public CatElementEffect Kind { get; private set; }
        public float Elapsed => age;
        public float Radius => radius;
        public static CatElementVfx Spawn(CatElementEffect kind,Vector3 position,Vector3 facing,float radius,float duration,GameObject owner,Vector3[] strikePoints=null)
        {
            var go=new GameObject("Cat "+kind+" V17");go.transform.position=position;
            if(facing.sqrMagnitude>.001f)go.transform.rotation=Quaternion.LookRotation(facing);
            CombatOwnership.Assign(go,owner);
            var effect=go.AddComponent<CatElementVfx>();effect.Kind=kind;effect.radius=radius;effect.lifetime=Mathf.Max(.05f,duration);
            effect.strikes=strikePoints;effect.Build();effect.Animate();return effect;
        }
        private Material Glow(Color color)
        { var m=CombatGlowVisuals.CreateMaterial("Cat elemental glow",color);materials.Add(m);return m; }
        private LineRenderer Line(Material m,int count,float width,bool loop=false)
        { var l=CombatGlowVisuals.Line(transform,"Element trail",m,count,width,loop);lines.Add(l);return l; }
        private void Build()
        {
            Color orange=new Color(1,.3f,.045f),gold=new Color(1,.88f,.3f),wind=new Color(.35f,1,.73f),thunder=new Color(.65f,.55f,1);
            switch(Kind) {
                case CatElementEffect.FireShot:
                    Line(Glow(orange),17,.16f);Line(Glow(gold),17,.075f);
                    for(int i=0;i<4;i++)Line(materials[0],12,.028f);
                    break;
                case CatElementEffect.WindShot:
                    for(int i=0;i<3;i++)Line(i==0?Glow(wind):materials[0],33,i==0?.12f:.038f);
                    break;
                case CatElementEffect.ThunderStrike:
                    var bolt=Glow(thunder);var core=Glow(new Color(1,.98f,.78f));
                    int count=8+(strikes?.Length??0);
                    for(int i=0;i<count;i++) {Line(bolt,11,.105f);Line(core,11,.032f);}
                    Line(core,65,.055f,true);break;
                case CatElementEffect.EarthWave:
                    var dust=Glow(new Color(.86f,.61f,.29f,.6f));for(int i=0;i<3;i++)Line(dust,65,.075f,true);
                    var stone=RuntimeMaterialFactory.CreateLit("Cat earth fragments",new Color(.36f,.3f,.23f));materials.Add(stone);
                    fragmentMesh=CreateFragmentMesh();
                    for(int i=0;i<36;i++) {
                        var rock=new GameObject("Earth fragment");rock.transform.SetParent(transform,false);
                        rock.AddComponent<MeshFilter>().sharedMesh=fragmentMesh;
                        rock.AddComponent<MeshRenderer>().sharedMaterial=stone;rocks.Add(rock.transform);
                    } break;
                default:
                    var color=Kind==CatElementEffect.FireImpact?orange:Kind==CatElementEffect.ThunderCharge?thunder:Kind==CatElementEffect.TimeSigil?new Color(1,.8f,.35f):wind;
                    var material=Glow(color);for(int i=0;i<3;i++)Line(material,49,.04f,true);break;
            }
        }
        private void Update() => Advance(CombatClock.DeltaTime(gameObject));
        public void Advance(float seconds)
        {
            if(seconds<=0)return;age+=seconds;
            if(age>=lifetime){Destroy(gameObject);return;}Animate();
        }
        private void Animate()
        {
            float p=Mathf.Clamp01(age/lifetime),fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,1,p));
            switch(Kind) {
                case CatElementEffect.FireShot:
                    for(int n=0;n<lines.Count;n++)for(int i=0;i<lines[n].positionCount;i++) {
                        float t=i/(float)(lines[n].positionCount-1),a=t*10+age*16+n*1.5f;
                        float r=(n<2?.06f:.13f)*Mathf.Sin(t*Mathf.PI);
                        lines[n].SetPosition(i,new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,-t*(n<2?.7f:.95f)));
                    } break;
                case CatElementEffect.WindShot:
                    for(int n=0;n<lines.Count;n++)for(int i=0;i<33;i++) {
                        float a=Mathf.Lerp(-1.2f,1.2f,i/32f);float r=radius*(1+n*.16f);
                        lines[n].SetPosition(i,new Vector3(Mathf.Sin(a)*r,Mathf.Cos(a)*r*.35f-.15f*n,Mathf.Cos(a)*r*.22f));
                    } break;
                case CatElementEffect.ThunderStrike:
                    for(int n=0;n<(lines.Count-1)/2;n++) {
                        float a=n*Mathf.PI/4;Vector3 foot=n<8?new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius*.76f:transform.InverseTransformPoint(strikes[n-8]);
                        float flash=age<.12f?age/.12f:Mathf.Clamp01(1-(age-.12f)/.5f);
                        for(int j=0;j<11;j++) {
                            float t=j/10f;Vector3 offset=j==0||j==10?Vector3.zero:new Vector3(Mathf.Sin(n*19+j*7),0,Mathf.Cos(n*7+j*11))*.18f;
                            Vector3 point=foot+Vector3.up*(5.5f*(1-t))+offset;
                            lines[n*2].SetPosition(j,point);lines[n*2+1].SetPosition(j,point);
                        }
                        CombatGlowVisuals.Alpha(lines[n*2],flash);CombatGlowVisuals.Alpha(lines[n*2+1],flash);
                    }
                    Ring(lines[lines.Count-1],radius*Mathf.Clamp01(age*3),.04f);break;
                case CatElementEffect.EarthWave:
                    for(int n=0;n<3;n++) {float wave=Mathf.Clamp01((age-n*.07f)/.46f);Ring(lines[n],Mathf.Lerp(.15f,radius,wave),.035f+n*.02f);CombatGlowVisuals.Alpha(lines[n],(1-wave)*.8f);}
                    for(int i=0;i<rocks.Count;i++) {
                        float a=i*2.399963f,wave=Mathf.Clamp01((age-(i%3)*.07f)/.5f),r=Mathf.Lerp(.2f,radius*(.7f+(i%5)*.06f),wave);
                        rocks[i].localPosition=new Vector3(Mathf.Cos(a)*r,Mathf.Sin(wave*Mathf.PI)*(.2f+(i%4)*.075f),Mathf.Sin(a)*r);
                        rocks[i].localRotation=Quaternion.Euler(i*37+wave*190,i*71+wave*90,i*13);
                        rocks[i].localScale=new Vector3(.14f+(i%3)*.035f,.12f,.20f)*fade;
                    } break;
                default:
                    for(int n=0;n<lines.Count;n++) {
                        bool charge=Kind==CatElementEffect.ThunderCharge;
                        float r=charge?radius*(.65f+.2f*Mathf.Sin(age*5+n)):radius*Mathf.Clamp01(p*2+n*.1f);
                        Ring(lines[n],r,charge?.06f+n*.38f:.04f+n*.12f);
                        if(charge)lines[n].transform.localRotation=Quaternion.Euler(n*18,age*90*(n%2==0?1:-1),0);
                    } break;
            }
            if(Kind!=CatElementEffect.ThunderStrike&&Kind!=CatElementEffect.EarthWave)
                foreach(var line in lines)CombatGlowVisuals.Alpha(line,fade);
        }
        private static void Ring(LineRenderer line,float r,float height)
        {
            for(int i=0;i<line.positionCount;i++){float a=i*Mathf.PI*2/(line.positionCount-1);line.SetPosition(i,new Vector3(Mathf.Cos(a)*r,height,Mathf.Sin(a)*r));}
        }
        private static Mesh CreateFragmentMesh()
        {
            Vector3[] corners={new Vector3(.1f,.7f,0),new Vector3(-.15f,-.5f,.1f),new Vector3(.6f,0,.1f),new Vector3(0,.1f,.6f),new Vector3(-.5f,0,-.1f),new Vector3(0,-.1f,-.5f)};
            int[] faces={0,3,2,0,4,3,0,5,4,0,2,5,1,2,3,1,3,4,1,4,5,1,5,2};
            var vertices=new Vector3[faces.Length];var triangles=new int[faces.Length];
            for(int i=0;i<faces.Length;i++){vertices[i]=corners[faces[i]];triangles[i]=i;}
            var mesh=new Mesh{name="Faceted earth shard",vertices=vertices,triangles=triangles};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private void OnDestroy(){foreach(var m in materials)if(m!=null)Destroy(m);if(fragmentMesh!=null)Destroy(fragmentMesh);}
    }
}
