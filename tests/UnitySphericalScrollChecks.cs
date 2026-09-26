#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Bird3DCursor.UI;
using Bird3DCursor.Samples;
using UnityEngine;

public static class UnitySphericalScrollChecks
{
    static int checks;
    static void Require(bool value,string message) { checks++; if (!value) throw new Exception("Spherical scroll: "+message); }
    sealed class Fixture : IDisposable
    {
        public readonly GameObject root=new GameObject("Spherical scroll checks");
        public readonly SphereCollider sphere;
        public readonly BirdPointerInput pointer;
        public readonly BirdSphericalScroll scroll;
        public readonly Transform target;
        public Vector3 origin=new Vector3(0,0,-3);
        public Fixture()
        {
            sphere=root.AddComponent<SphereCollider>(); sphere.radius=1;
            target=new GameObject("Rotating content").transform; target.SetParent(root.transform,false);
            pointer=new GameObject("Pointer").AddComponent<BirdPointerInput>(); pointer.transform.SetParent(root.transform,false);
            scroll=root.AddComponent<BirdSphericalScroll>(); scroll.Configure(sphere,target,new[]{pointer});
            Physics.SyncTransforms();
        }
        public void Feed(Vector3 point,float dt=.01f,bool pressed=false)
        { pointer.Submit(origin,point,true,pressed); scroll.Process(dt); }
        public void Normal(Vector3 normal,float dt=.01f)
        { Feed(origin+(normal-origin)*2,dt); }
        public void Dispose() { UnityEngine.Object.DestroyImmediate(root); }
    }

    public static string Run()
    {
        checks=0;
        Contact(); Lifecycle(); FrameRates(); RigidMotion(); Layout(); Preview();
        return checks+" spherical assertions: back-only engagement, physics contact, damping/rates, lifecycle, layout";
    }

    static void Contact()
    {
        using (var f=new Fixture())
        {
            Vector3 normal,hit;
            foreach(float z in new[]{-2f,-1f,-.8f,0f,.8f,.999f,1f})
                Require(!BirdSphereContact.TryGetBackSurface(f.sphere,f.origin,new Vector3(0,0,z),0,out normal,out hit),"front/interior/back boundary must not engage at z="+z);
            Require(BirdSphereContact.TryGetBackSurface(f.sphere,f.origin,new Vector3(0,0,1.01f),0,out normal,out hit) && Vector3.Distance(hit,Vector3.forward)<1e-6f,"far intersection");
            Require(BirdSphereContact.TryGetBackSurface(f.sphere,f.origin,Vector3.forward*1e12f,0,out normal,out hit),"enormous logical reach without a cast cap");
            Require(BirdSphereContact.TryGetBackSurface(f.sphere,Vector3.zero,Vector3.forward*2,0,out normal,out hit),"origin inside exits at back surface");
            Require(!BirdSphereContact.TryGetBackSurface(f.sphere,new Vector3(1,0,-3),new Vector3(1,0,3),0,out normal,out hit),"tangent does not traverse sphere");
            Require(!BirdSphereContact.TryGetBackSurface(f.sphere,f.origin,f.origin-Vector3.forward,0,out normal,out hit),"backward ray");
            Require(!BirdSphereContact.TryGetBackSurface(f.sphere,f.origin,new Vector3(float.NaN,0,2),0,out normal,out hit),"invalid point");
            f.sphere.center=new Vector3(.3f,-.1f,.2f);
            f.root.transform.position=new Vector3(50,-20,70);
            f.root.transform.rotation=Quaternion.Euler(24,-36,19);
            foreach(var scale in new[]{Vector3.one*2,new Vector3(1,2,.5f),new Vector3(-1,2,.5f)})
            {
                f.root.transform.localScale=scale; Physics.SyncTransforms();
                var center=f.root.transform.TransformPoint(f.sphere.center);
                for(int i=0;i<40;i++)
                {
                    Vector3 axis=Quaternion.Euler(i*19,i*31,0)*Vector3.forward;
                    Vector3 origin=center-axis*8,point=center+axis*8;
                    RaycastHit physical;
                    Require(f.sphere.Raycast(new Ray(point,-axis),out physical,16),"Unity reverse cast control");
                    Require(BirdSphereContact.TryGetBackSurface(f.sphere,origin,point,0,out normal,out hit) && Vector3.Distance(hit,physical.point)<.0001f,"analytic far contact matches Unity offset/scaled sphere");
                }
            }
        }
    }

    static void Lifecycle()
    {
        using(var f=new Fixture())
        {
            int begins=0,ends=0;
            f.scroll.Started.AddListener(p=>begins++); f.scroll.Stopped.AddListener(p=>ends++);
            for(int i=0;i<30;i++) f.Feed(new Vector3(Mathf.Sin(i*.3f)*.4f,0,.5f),.01f,i%2==0);
            Require(begins==0 && Quaternion.Angle(f.target.rotation,Quaternion.identity)<.001f,"interior movement/clicking never drives rotation");
            f.Feed(new Vector3(0,0,1.002f)); Require(begins==0,"entry margin");
            f.Normal(Vector3.forward); Require(begins==1 && f.scroll.AngularVelocity==Vector3.zero,"acquire without rotation jump or click");
            for(int i=1;i<=50;i++) f.Normal(Quaternion.AngleAxis(i*.9f,Vector3.up)*Vector3.forward);
            float driven=Quaternion.Angle(f.target.rotation,Quaternion.identity);
            Require(driven>20 && driven<45 && f.scroll.AngularVelocity.y>0,"smooth input-driven turn follows cross-product sign");
            f.Feed(Vector3.zero);
            Require(ends==1 && f.scroll.ActivePointer==null && f.scroll.AngularVelocity.magnitude>0,"withdrawal releases drive but retains momentum");
            float speed=f.scroll.AngularVelocity.magnitude;
            Quaternion before=f.target.rotation;
            for(int i=0;i<100;i++) f.Feed(Vector3.zero);
            Require(Mathf.Abs(f.scroll.AngularVelocity.magnitude-speed*Mathf.Exp(-.99f))<.00001f,"coast damping in seconds");
            Require(Quaternion.Angle(before,f.target.rotation)>10,"withdrawn selector actually coasts");
            f.pointer.Cancel(); f.scroll.Process(.01f);
            Require(f.scroll.AngularVelocity==Vector3.zero,"loss during coast cancels inertia");
            before=f.target.rotation; f.Normal(Vector3.right);
            Require(Quaternion.Angle(before,f.target.rotation)<.001f,"recovery rebases contact without a jump");
            f.pointer.Cancel();
            before=f.target.rotation; f.Normal(Vector3.forward);
            Require(Quaternion.Angle(before,f.target.rotation)<.001f && f.scroll.AngularVelocity==Vector3.zero,"loss and recovery between router frames rebases contact");
            f.Normal(Vector3.forward); f.scroll.Process(.01f);
            Require(!float.IsNaN(f.target.rotation.x),"large input change and duplicate sample remain finite");
            f.scroll.enabled=false;
            Require(f.scroll.ActivePointer==null && f.scroll.AngularVelocity==Vector3.zero,"disable clears drive and inertia");
            f.scroll.enabled=true;
            var other=new GameObject("Other hand").AddComponent<BirdPointerInput>(); other.transform.SetParent(f.root.transform,false);
            f.scroll.Configure(f.sphere,f.target,new[]{f.pointer,other});
            f.Normal(Vector3.forward);
            other.Submit(f.origin,f.origin+(Vector3.right-f.origin)*2,true,false); f.scroll.Process(.01f);
            Require(f.scroll.ActivePointer==f.pointer,"other hand cannot steal active contact");
            before=f.target.rotation; f.Feed(Vector3.zero);
            Require(f.scroll.ActivePointer==other && Quaternion.Angle(before,f.target.rotation)<.001f,"hand transfer rebases without an impulse");
            other.Submit(f.origin,Vector3.zero,true,false); f.scroll.Process(.01f);
            UnityEngine.Object.DestroyImmediate(other.gameObject); f.scroll.Process(.01f);
            Require(f.scroll.AngularVelocity==Vector3.zero,"destroyed inertia owner cancels safely");
            f.Normal(Vector3.forward); f.Normal(Quaternion.AngleAxis(20,Vector3.up)*Vector3.forward);
            f.sphere.radius=0; before=f.target.rotation; f.scroll.Process(.01f);
            Require(f.scroll.AngularVelocity==Vector3.zero && Quaternion.Angle(before,f.target.rotation)<.001f,"invalid sphere cancels without rotating");
            f.sphere.radius=1; f.Normal(Vector3.forward); f.scroll.Process(1);
            Require(f.scroll.ActivePointer==null && f.scroll.AngularVelocity==Vector3.zero,"long pause cancels stale movement");
            f.scroll.Started.AddListener(p=>{ f.scroll.enabled=false; f.scroll.Process(.01f); });
            f.Normal(Vector3.forward);
            Require(!f.scroll.enabled && f.scroll.AngularVelocity==Vector3.zero,"reentrant start listener may disable safely");
        }
        using(var f=new Fixture())
        {
            var panel=f.root.AddComponent<BirdMenuPanel>(); panel.Configure(null);
            f.scroll.Configure(f.sphere,f.target,new[]{f.pointer},panel);
            f.Normal(Vector3.forward); Require(f.scroll.ActivePointer==null,"closed panel rejects scrolling");
            panel.Open(f.pointer); f.Normal(Vector3.forward); Require(f.scroll.ActivePointer==f.pointer,"owner panel enables scrolling");
            f.Normal(Quaternion.AngleAxis(15,Vector3.up)*Vector3.forward);
            panel.Close(); f.scroll.Process(.01f);
            Require(f.scroll.ActivePointer==null && f.scroll.AngularVelocity==Vector3.zero,"closing panel stops inertia");
        }
    }

    static void FrameRates()
    {
        var measurements=new StringBuilder("hz,drive_end_radians_per_second,coast_end_degrees,analytic_end_degrees,curved_difference_degrees\n");
        Quaternion reference=Quaternion.identity, curvedReference=Quaternion.identity;
        float expectedVelocity=60*Mathf.Deg2Rad*10/10.99f*(1-Mathf.Exp(-10.99f));
        float expectedAngle=60*10/10.99f*(1-(1-Mathf.Exp(-10.99f))/10.99f)+expectedVelocity*Mathf.Rad2Deg*(1-Mathf.Exp(-.99f))/.99f;
        foreach(int hz in new[]{120,72,30})
        {
            float velocity,endAngle;
            using(var f=new Fixture())
            {
                float dt=1f/hz; f.Normal(Vector3.forward,dt);
                for(int i=1;i<=hz;i++) f.Normal(Quaternion.AngleAxis(60f*i/hz,Vector3.up)*Vector3.forward,dt);
                Require(Mathf.Abs(f.scroll.AngularVelocity.magnitude-expectedVelocity)<.0001f,"fixed-axis filtered velocity agrees with continuous solution at "+hz);
                velocity=f.scroll.AngularVelocity.magnitude;
                for(int i=0;i<hz;i++) f.Feed(Vector3.zero,dt);
                Require(Mathf.Abs(Quaternion.Angle(Quaternion.identity,f.target.rotation)-expectedAngle)<.005f,"fixed-axis integrated drive and coast at "+hz);
                if(hz==120) reference=f.target.rotation;
                Require(AngleBetween(reference,f.target.rotation)<.05f,"fixed-axis frame-rate agreement at "+hz);
                endAngle=Quaternion.Angle(Quaternion.identity,f.target.rotation);
            }
            using(var f=new Fixture())
            {
                float dt=1f/hz; f.Normal(Vector3.forward,dt);
                for(int i=1;i<=hz*2;i++)
                {
                    float t=(float)i/hz;
                    f.Normal(Quaternion.Euler(20*Mathf.Sin(t*2.2f),30*Mathf.Sin(t*3.1f),0)*Vector3.forward,dt);
                }
                for(int i=0;i<hz;i++) f.Feed(Vector3.zero,dt);
                if(hz==120) curvedReference=f.target.rotation;
                Require(AngleBetween(curvedReference,f.target.rotation)<.1f,"multi-axis flick/reversal frame-rate agreement at "+hz);
                measurements.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0},{1:R},{2:R},{3:R},{4:R}",hz,velocity,endAngle,expectedAngle,AngleBetween(curvedReference,f.target.rotation)));
            }
        }
        File.WriteAllText("MenuCaptures/spherical-frame-rates.csv",measurements.ToString());
    }

    static float AngleBetween(Quaternion a,Quaternion b)
    {
        // Quaternion.Angle deliberately returns zero for small angles; retain resolution for measurements.
        var delta=Quaternion.Inverse(a)*b;
        double sine=Math.Sqrt((double)delta.x*delta.x+(double)delta.y*delta.y+(double)delta.z*delta.z);
        return (float)(2*Math.Atan2(sine,Math.Abs(delta.w))*Mathf.Rad2Deg);
    }

    static void RigidMotion()
    {
        Quaternion baseline;
        using(var f=new Fixture())
        {
            f.Normal(Vector3.forward);
            for(int i=1;i<=60;i++) f.Normal(Quaternion.Euler(i*.4f,i*.7f,0)*Vector3.forward);
            baseline=f.target.rotation;
        }
        using(var f=new Fixture())
        {
            Vector3 offset=new Vector3(4,7,-2);
            Quaternion rotation=Quaternion.Euler(173,57,-21);
            f.root.transform.SetPositionAndRotation(offset,rotation);
            f.target.localPosition=new Vector3(.2f,0,0); f.origin=offset+rotation*f.origin;
            for(int i=0;i<=60;i++)
            {
                var hit=offset+rotation*(Quaternion.Euler(i*.4f,i*.7f,0)*Vector3.forward);
                f.Feed(f.origin+(hit-f.origin)*2);
            }
            Require(AngleBetween(baseline,Quaternion.Inverse(rotation)*f.target.rotation)<.01f,"rotation behavior is equivariant under an upside-down rigid frame");
            Require(Mathf.Abs(Vector3.Distance(f.target.position,offset)-.2f)<.00001f,"offset rotation target orbits the actual sphere center");
        }
    }

    static void Layout()
    {
        var go=new GameObject("Dodecahedron layout check");
        try
        {
            go.transform.position=new Vector3(10,20,30); go.transform.rotation=Quaternion.Euler(20,30,40); go.transform.localScale=Vector3.one*2;
            var items=new Transform[12];
            for(int i=0;i<12;i++) { items[i]=new GameObject("Color "+i).transform; items[i].SetParent(go.transform,false); }
            go.AddComponent<BirdDodecahedronLayout>().Configure(items,.5f);
            for(int i=0;i<12;i++)
            {
                Require(Mathf.Abs((items[i].position-go.transform.position).magnitude-1)<.00001f,"layout radius in local space");
                int neighbors=0,opposites=0;
                for(int j=0;j<12;j++) if(i!=j)
                {
                    float dot=Vector3.Dot(BirdDodecahedronLayout.Direction(i),BirdDodecahedronLayout.Direction(j));
                    if(Mathf.Abs(dot-1/Mathf.Sqrt(5))<.00001f) neighbors++;
                    if(dot<-.999f) opposites++;
                }
                Require(neighbors==5 && opposites==1,"twelve normals are dodecahedron face centers");
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    static void Preview()
    {
        var root=new GameObject("Spherical preview check");
        var preview=root.AddComponent<BirdSphericalSelectorPreview>(); preview.desktopInput=false; preview.Initialize();
        var texture=new RenderTexture(1280,900,24) { antiAliasing=4 };
        var pixels=new Texture2D(1280,900,TextureFormat.RGB24,false);
        try
        {
            Require(preview.Choices.Length==12 && preview.EdgeCount==30,"preview has twelve independent choices and thirty dodecahedron edges");
            preview.View.targetTexture=texture;
            Action<string> capture=name=>{
                preview.RefreshVisuals(); preview.View.Render(); RenderTexture.active=texture;
                pixels.ReadPixels(new Rect(0,0,1280,900),0,0); pixels.Apply();
                string path=name.StartsWith("frame-") ? "MenuCaptures/SphereMotionFrames/"+name+".png" : "MenuCaptures/sphere-"+name+".png";
                File.WriteAllBytes(path,pixels.EncodeToPNG());
            };
            var origin=preview.View.transform.position;
            var chosen=preview.Choices[4];
            preview.Pointer.Submit(origin,chosen.transform.position,true,false); preview.Interactor.Process(); preview.Scroll.Process(.01f);
            capture("inside");
            preview.Pointer.Submit(origin,chosen.transform.position,true,true); preview.Interactor.Process(); preview.Scroll.Process(.01f);
            Require(preview.SelectionCount==1 && preview.Scroll.ActivePointer==null,"selecting an interior color does not start scrolling");
            capture("selected");
            var center=preview.Sphere.transform.position;
            preview.Pointer.Submit(origin,origin+(center+Vector3.forward*1.1f-origin)*2,true,false); preview.Scroll.Process(.01f);
            for(int i=1;i<=50;i++)
            {
                var normal=Quaternion.Euler(i*.5f,i*.9f,0)*Vector3.forward;
                preview.Pointer.Submit(origin,origin+(center+normal*1.1f-origin)*2,true,false); preview.Scroll.Process(.01f);
            }
            Require(preview.Scroll.ActivePointer==preview.Pointer && Quaternion.Angle(preview.Content.rotation,Quaternion.identity)>20,"preview scrolls in multiple axes");
            capture("driven");
            var before=preview.Content.rotation;
            for(int i=0;i<60;i++) { preview.Pointer.Submit(origin,center,true,false); preview.Scroll.Process(.01f); }
            Require(preview.Scroll.ActivePointer==null && Quaternion.Angle(before,preview.Content.rotation)>10,"preview withdraws and coasts");
            capture("coast");
            Directory.CreateDirectory("MenuCaptures/SphereMotionFrames");
            preview.Scroll.Cancel(); preview.Content.localRotation=Quaternion.identity;
            var cursor=GameObject.CreatePrimitive(PrimitiveType.Sphere); cursor.name="Rendered test point";
            cursor.transform.SetParent(root.transform,false); cursor.transform.localScale=Vector3.one*.032f;
            cursor.GetComponent<Collider>().enabled=false;
            cursor.GetComponent<Renderer>().sharedMaterial=Resources.Load<Material>("BirdMenuPreviewSurface");
            for(int i=0;i<120;i++)
            {
                float t=i/30f; Vector3 point;
                if(t<.6f || (t>=1.6f && t<3.3f)) point=center+new Vector3(.2f*Mathf.Sin(t*4),.12f*Mathf.Cos(t*3),0);
                else
                {
                    float s=Mathf.Clamp01(t-.6f);
                    var normal=t>=3.3f ? Quaternion.Euler(-15,-20,0)*Vector3.forward : Quaternion.Euler(20*Mathf.Sin(s*Mathf.PI),70*s,0)*Vector3.forward;
                    point=origin+(center+normal*1.1f-origin)*1.1f;
                }
                preview.Pointer.Submit(origin,point,true,false); cursor.transform.position=point;
                preview.Interactor.Process(); preview.Scroll.Process(1f/30);
                capture("frame-"+i.ToString("D3"));
            }
        }
        finally
        {
            RenderTexture.active=null; texture.Release(); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(pixels);
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
#endif
