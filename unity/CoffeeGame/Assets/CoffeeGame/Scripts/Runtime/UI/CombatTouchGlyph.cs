using CoffeeGame.Input;
using UnityEngine;
using UnityEngine.UI;

namespace CoffeeGame.UI
{
    // Resolution-independent, original UI geometry: no imported game artwork.
    public sealed class CombatTouchGlyph : MaskableGraphic
    {
        private GameInputSemantic action;
        private bool cat;
        private bool ring;
        private float fraction = 1f, thickness = .035f;
        public void Icon(GameInputSemantic value, bool isCat)
        {
            if (!ring && action == value && cat == isCat) return;
            ring = false; action = value; cat = isCat; SetVerticesDirty();
        }
        public void Ring(float value = 1f, float width = .035f)
        {
            value = Mathf.Clamp01(value);
            if (ring && Mathf.Approximately(value, fraction) && Mathf.Approximately(width, thickness)) return;
            ring = true; fraction = value; thickness = width; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (ring) { Arc(mesh, .47f, 90, -360 * fraction, thickness); return; }
            switch (action)
            {
                case GameInputSemantic.Sword:
                    if (cat) {
                        Polygon(mesh,new Vector2(0,.4f),new Vector2(.08f,.13f),new Vector2(.26f,.22f),new Vector2(.29f,-.13f),new Vector2(.14f,-.33f),new Vector2(-.12f,-.34f),new Vector2(-.3f,-.1f),new Vector2(-.2f,.18f));
                        Line(mesh,-.06f,-.2f,.02f,.03f,.045f);
                    }
                    else {
                        Polygon(mesh, new Vector2(-.16f,-.07f), new Vector2(.24f,.35f), new Vector2(.37f,.39f), new Vector2(.31f,.24f), new Vector2(-.08f,-.16f));
                        Line(mesh, -.25f, -.04f, .02f, -.3f, .07f); Line(mesh, -.14f, -.2f, -.3f, -.36f, .085f);
                    } break;
                case GameInputSemantic.Guard:
                    Path(mesh, .052f, new Vector2(0,.38f),new Vector2(.28f,.26f),new Vector2(.25f,-.08f),new Vector2(0,-.37f),new Vector2(-.25f,-.08f),new Vector2(-.28f,.26f),new Vector2(0,.38f));
                    Line(mesh, 0,.23f,0,-.16f,.04f); Line(mesh,-.13f,.06f,.13f,.06f,.04f); break;
                case GameInputSemantic.Jump:
                    Path(mesh,.065f,new Vector2(-.24f,.06f),new Vector2(0,.34f),new Vector2(.24f,.06f));
                    Line(mesh,0,.3f,0,-.19f,.065f); Arc(mesh,.31f,205,130,.035f); break;
                case GameInputSemantic.Dodge:
                    Path(mesh,.065f,new Vector2(-.18f,.25f),new Vector2(.1f,0),new Vector2(-.18f,-.25f));
                    Path(mesh,.065f,new Vector2(.05f,.25f),new Vector2(.33f,0),new Vector2(.05f,-.25f));
                    Line(mesh,-.37f,.12f,-.21f,.12f,.035f); Line(mesh,-.4f,-.1f,-.25f,-.1f,.035f); break;
                case GameInputSemantic.Magic:
                    if (cat) { Arc(mesh,.35f,0,360,.025f); Polygon(mesh,new Vector2(.04f,.32f),new Vector2(-.2f,-.02f),new Vector2(-.02f,-.02f),new Vector2(-.08f,-.33f),new Vector2(.23f,.07f),new Vector2(.05f,.07f)); }
                    else for (int i=0;i<6;i++) {
                        float angle=i*Mathf.PI/3; Vector2 tip=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*.34f;
                        Segment(mesh,Vector2.zero,tip,.037f);
                        Vector2 side=new Vector2(-tip.y,tip.x)*.3f, stem=tip*.57f;
                        Segment(mesh,stem,tip*.77f+side,.03f); Segment(mesh,stem,tip*.77f-side,.03f);
                    } break;
                case GameInputSemantic.Special:
                    if (cat) { Arc(mesh,.32f,0,360,.045f); Line(mesh,0,.21f,0,0,.055f); Line(mesh,0,0,.17f,-.1f,.055f); }
                    else { Star(mesh,Vector2.zero,.38f); Arc(mesh,.27f,20,65,.024f); Arc(mesh,.27f,200,65,.024f); }
                    break;
                case GameInputSemantic.LockOn:
                    for(int i=0;i<4;i++) Arc(mesh,.31f,i*90+15,60,.04f);
                    Arc(mesh,.095f,0,360,.035f); break;
                case GameInputSemantic.SwitchCharacter:
                    Arc(mesh,.3f,25,125,.04f); Arc(mesh,.3f,205,125,.04f);
                    Path(mesh,.04f,new Vector2(-.29f,.33f),new Vector2(-.26f,.15f),new Vector2(-.06f,.18f));
                    Path(mesh,.04f,new Vector2(.29f,-.33f),new Vector2(.26f,-.15f),new Vector2(.06f,-.18f));
                    Arc(mesh,.09f,0,360,.04f); break;
            }
        }
        private void Star(VertexHelper m,Vector2 center,float r)
        { Polygon(m,center+new Vector2(0,r),center+new Vector2(.25f*r,.25f*r),center+new Vector2(r,0),center+new Vector2(.25f*r,-.25f*r),center+new Vector2(0,-r),center+new Vector2(-.25f*r,-.25f*r),center+new Vector2(-r,0),center+new Vector2(-.25f*r,.25f*r)); }
        private void Arc(VertexHelper m,float radius,float start,float sweep,float width)
        {
            int count=Mathf.Max(1,Mathf.CeilToInt(Mathf.Abs(sweep)/5));
            for(int i=0;i<count;i++) {
                float a=(start+sweep*i/count)*Mathf.Deg2Rad,b=(start+sweep*(i+1)/count)*Mathf.Deg2Rad;
                Vector2 p=new Vector2(Mathf.Cos(a),Mathf.Sin(a)),q=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
                Polygon(m,p*(radius-width/2),p*(radius+width/2),q*(radius+width/2),q*(radius-width/2));
            }
        }
        private void Path(VertexHelper m,float width,params Vector2[] points) { for(int i=1;i<points.Length;i++) Segment(m,points[i-1],points[i],width); }
        private void Line(VertexHelper m,float x,float y,float xx,float yy,float width) => Segment(m,new Vector2(x,y),new Vector2(xx,yy),width);
        private void Segment(VertexHelper m,Vector2 a,Vector2 b,float width)
        { Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*width/2; Polygon(m,a-n,a+n,b+n,b-n); }
        private void Polygon(VertexHelper m,params Vector2[] points)
        {
            var rect=rectTransform.rect; float scale=Mathf.Min(rect.width,rect.height); int first=m.currentVertCount;
            Vector2 center=Vector2.zero; foreach(var p in points)center+=p; center/=points.Length;
            m.AddVert(rect.center+center*scale,color,Vector2.zero);
            foreach(var p in points)m.AddVert(rect.center+p*scale,color,Vector2.zero);
            for(int i=0;i<points.Length;i++)m.AddTriangle(first,first+1+i,first+1+(i+1)%points.Length);
        }
    }
}
