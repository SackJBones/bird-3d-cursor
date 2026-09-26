#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Bird3DCursor.UI;
using Bird3DCursor.Samples;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class UnityMapChecks : MonoBehaviour
{
    const string Active="Bird.Map.Checks";
    static int checks;
    sealed class Fixture : IDisposable
    {
        public GameObject root=new GameObject("Range scale fixture");
        public BirdRangeScale scale;
        public BirdPointerInput left,right;
        public BoxCollider volume;
        public Transform target;
        public Vector3 origin=new Vector3(0,0,-3);
        public Fixture()
        {
            volume=root.AddComponent<BoxCollider>(); volume.size=Vector3.one;
            target=new GameObject("Scaled content").transform; target.SetParent(root.transform,false);
            left=new GameObject("Left").AddComponent<BirdPointerInput>(); left.transform.SetParent(root.transform,false);
            right=new GameObject("Right").AddComponent<BirdPointerInput>(); right.transform.SetParent(root.transform,false);
            scale=root.AddComponent<BirdRangeScale>(); scale.Configure(volume,target,new[]{left,right}); Physics.SyncTransforms();
        }
        public void Feed(float range,float dt=1f/72,BirdPointerInput pointer=null)
        { (pointer??left).Submit(origin,origin+Vector3.forward*range,true,false); scale.Process(dt); }
        public void Settle(float range,int count=150) { for(int i=0;i<count;i++) Feed(range); }
        public void Dispose() { DestroyImmediate(root); }
    }
    public static void Run()
    {
        File.WriteAllText("map-result.txt","PENDING"); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("Bird Map Preview").AddComponent<BirdMapPreview>(); EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/MapPreview.unity");
        SessionState.SetBool(Active,true); EditorApplication.isPlaying=true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Begin() { if(SessionState.GetBool(Active,false)) new GameObject("Map checks").AddComponent<UnityMapChecks>(); }
    void Start()
    {
        try
        {
            Directory.CreateDirectory("MapCaptures"); Directory.CreateDirectory("MenuCaptures"); Laws(); Lifecycle(); Frames(); Preview(); Prefab();
            string regression=UnitySphericalScrollChecks.Run();
            File.WriteAllText("map-result.txt","PASS: "+checks+" map/range-scale assertions plus "+regression+"; saved map preview, six map camera captures and prefab event round trip. Actual Unity "+Application.unityVersion+" / "+SystemInfo.graphicsDeviceType+". Synthetic input; not Udon or headset validation.");
            SessionState.SetBool(Active,false); EditorApplication.Exit(0);
        }
        catch(Exception e) { File.WriteAllText("map-result.txt","FAIL: "+e); Debug.LogException(e); SessionState.SetBool(Active,false); EditorApplication.Exit(1); }
    }
    static void Laws()
    {
        using(var f=new Fixture())
        {
            f.Feed(4); f.Feed(8); Require(f.scale.Factor==1 && f.scale.ActivePointer==null,"Scaling starts disarmed");
            f.scale.SetLimits(.1f,16,2,0); f.scale.StartScaling(); f.Feed(4); Require(f.scale.ActivePointer==f.left && f.scale.Factor==1,"Arm and acquire without jump");
            foreach(float ratio in new[]{1f,1.2f,2f,3f,.8f})
            { f.Feed(4*ratio); Near(f.scale.Factor,ratio*ratio,.00002f,"Legacy squared range law"); Require(f.target.position==Vector3.zero,"Own pivot stays fixed"); }
            float last=f.scale.Factor; f.Feed(2); Require(f.scale.ActivePointer==null && f.scale.Factor==last,"Withdraw before front surface freezes displayed size");
            f.Feed(6); Near(f.scale.Factor,last,.00002f,"Reenter rebases from current factor");
            f.Feed(12); Near(f.scale.Factor,last*4,.00004f,"Reengaged squared law uses current size");
            f.Feed(1e12f); Near(f.scale.Factor,16,.00001f,"Huge finite reach clamps view factor");
            Require(f.left.Position==f.origin+Vector3.forward*1e12f,"Scaling never changes geometric input");
            f.scale.StopScaling(); f.Feed(1e12f); f.scale.StartScaling(); f.Feed(1e12f); f.Feed(4); Near(f.scale.Factor,.1f,.00001f,"Extreme contraction stays positive and bounded");
            f.scale.ResetScale(); Near(f.scale.Factor,1,.00001f,"Reset restores rest factor"); Require(!f.scale.ScalingEnabled,"Reset disarms");
            f.target.localScale=new Vector3(-.01f,.02f,.03f); f.scale.Configure(f.volume,f.target,new[]{f.left}); f.scale.SetLimits(.1f,16,2,0);
            f.scale.StartScaling(); f.Feed(4); f.Feed(8); Require(Vector3.Distance(f.target.localScale,new Vector3(-.04f,.08f,.12f))<1e-6f,"Uniform factor preserves original aspect and reflection");
            bool bad=false; try { f.scale.SetLimits(0,2); } catch(ArgumentOutOfRangeException) { bad=true; } Require(bad,"Zero minimum rejected");
            bad=false; try { f.scale.SetLimits(.5f,float.PositiveInfinity); } catch(ArgumentOutOfRangeException) { bad=true; } Require(bad,"Infinite scale bound rejected");
            var serialized=new SerializedObject(f.scale); serialized.FindProperty("minimumFactor").floatValue=0; serialized.ApplyModifiedPropertiesWithoutUndo();
            f.scale.ResetScale(); f.scale.StartScaling(); Require(!f.scale.ScalingEnabled && !float.IsNaN(f.scale.Factor),"Invalid serialized limits cannot arm or corrupt reset");
        }
        using(var f=new Fixture())
        {
            f.volume.center=Vector3.back*.5f; Physics.SyncTransforms(); f.scale.StartScaling(); f.Feed(4);
            Require(f.scale.ActivePointer!=null,"Contact at world origin is not a missing-hit sentinel");
            f.scale.StopScaling(); f.target.gameObject.AddComponent<BoxCollider>(); f.scale.Configure(f.target.GetComponent<BoxCollider>(),f.target,new[]{f.left}); f.scale.StartScaling(); f.Feed(4);
            Require(f.scale.ActivePointer==null,"Self-scaling engagement volume rejected");
        }
    }
    static void Lifecycle()
    {
        using(var f=new Fixture())
        {
            int begins=0,ends=0; f.scale.Started.AddListener(p=>begins++); f.scale.Stopped.AddListener(p=>ends++);
            f.scale.StartScaling(); f.Feed(4); f.Feed(8); float visible=f.scale.Factor;
            f.left.Cancel(); f.scale.Process(.01f); Require(f.scale.ActivePointer==null && f.scale.Factor==visible,"Loss freezes pending zoom immediately");
            for(int i=0;i<30;i++) f.scale.Process(.01f); Near(f.scale.Factor,visible,1e-7f,"No abandoned tween after loss");
            f.Feed(9); Near(f.scale.Factor,visible,1e-7f,"Recovery starts at current view");
            f.Feed(12,pointer:f.right); Require(f.scale.ActivePointer==f.left,"Second hand cannot steal a gesture");
            f.left.Cancel(); f.scale.Process(.01f); f.Feed(12,pointer:f.right); Require(f.scale.ActivePointer==f.right,"Other hand starts a fresh gesture");
            visible=f.scale.Factor; f.right.SetUser("SomeoneElse"); f.Feed(14,pointer:f.right); Require(f.scale.ActivePointer==null && f.scale.Factor==visible,"Owner change ends gesture without cross-user delta");
            f.scale.StopScaling(); Require(begins==ends,"Every acquisition has one stop callback");
            f.scale.StartScaling(); f.Feed(4); f.scale.Process(.3f); Require(!f.scale.ScalingEnabled && f.scale.ActivePointer==null,"Long pause disarms");
            f.scale.StartScaling(); f.Feed(4); f.scale.enabled=false; Require(!f.scale.ScalingEnabled && f.scale.ActivePointer==null,"Disable freezes and disarms"); f.scale.enabled=true;
            f.scale.StartScaling(); f.Feed(4); f.target.localScale*=1.1f; f.scale.Process(.01f); Require(!f.scale.ScalingEnabled,"External scale writer is not fought");
            f.scale.Configure(f.volume,f.target,new[]{f.left}); f.scale.StartScaling(); f.Feed(4); f.root.transform.position=Vector3.right; Physics.SyncTransforms(); f.scale.Process(.01f); Require(f.scale.ActivePointer==null,"Moving reference frame ends gesture");
        }
        using(var f=new Fixture())
        {
            f.scale.Started.AddListener(p=>f.scale.Cancel()); f.scale.StartScaling(); f.Feed(4); Require(!f.scale.ScalingEnabled && f.scale.ActivePointer==null,"Started listener may cancel safely");
            f.scale.Started.RemoveAllListeners(); f.scale.StartScaling(); f.Feed(4); f.scale.Changed.AddListener(v=>f.scale.StopScaling()); f.Feed(8); Require(!f.scale.ScalingEnabled && f.scale.ActivePointer==null,"Changed listener may stop safely");
            f.scale.Changed.RemoveAllListeners(); f.scale.StartScaling(); f.Feed(4); f.scale.Process(float.NaN); Require(!f.scale.ScalingEnabled,"Invalid dt disarms");
            f.scale.StartScaling(); f.Feed(4); f.left.Submit(f.origin,new Vector3(float.NaN,0,0),true,false); f.scale.Process(.01f); Require(f.scale.ActivePointer==null,"Invalid logical sample ends gesture");
        }
        using(var f=new Fixture())
        {
            var parent=new GameObject("Menu").AddComponent<BirdMenuPanel>(); parent.transform.SetParent(f.root.transform); var content=new GameObject("Content"); content.transform.SetParent(parent.transform); parent.Configure(content);
            f.scale.Configure(f.volume,f.target,new[]{f.left,f.right},parent); f.scale.StartScaling(); f.Feed(4); Require(f.scale.ActivePointer==null,"Closed menu rejects scaling");
            parent.Open(f.left); f.Feed(4); Require(f.scale.ActivePointer==f.left,"Owned open menu permits scaling");
            parent.Close(); f.Feed(5); Require(f.scale.ActivePointer==null,"Closing menu ends gesture");
        }
    }
    static void Frames()
    {
        var csv=new List<string>{"hz,final_factor,final_target"}; var values=new List<float>();
        foreach(int hz in new[]{30,72,120}) using(var f=new Fixture())
        {
            f.scale.SetLimits(.25f,8,2,10); f.scale.StartScaling(); f.Feed(4,1f/hz);
            for(int i=1;i<=hz;i++) f.Feed(4*Mathf.Exp(.5f*i/hz),1f/hz);
            values.Add(f.scale.Factor); csv.Add(hz+","+f.scale.Factor.ToString("R",CultureInfo.InvariantCulture)+","+f.scale.DesiredFactor.ToString("R",CultureInfo.InvariantCulture));
            double expected=Math.Exp(1-(1-Math.Exp(-10))/10); Near(f.scale.Factor,(float)expected,.00001f,"Analytic log follower "+hz);
            float held=f.scale.Factor; for(int i=0;i<50;i++) f.scale.Process(1e-8f); Require(f.scale.Factor>=held && !float.IsNaN(f.scale.Factor),"Tiny dt remains finite");
        }
        Near(values[0],values[2],.00001f,"30/120 agreement"); Near(values[1],values[2],.00001f,"72/120 agreement");
        File.WriteAllLines("MapCaptures/range-scale-rates.csv",csv);
        foreach(Quaternion rotation in new[]{Quaternion.identity,Quaternion.Euler(0,0,180),Quaternion.Euler(17,65,31)})
        foreach(Vector3 scale in new[]{Vector3.one,new Vector3(-2,.5f,1.3f)}) using(var f=new Fixture())
        {
            f.root.transform.position=new Vector3(3,-2,7); f.root.transform.rotation=rotation; f.root.transform.localScale=scale;
            f.scale.Configure(f.volume,f.target,new[]{f.left}); f.scale.SetLimits(.25f,4,2,0); f.scale.StartScaling(); Physics.SyncTransforms();
            Vector3 axis=f.root.transform.forward; Vector3 origin=f.root.transform.position-axis*4;
            f.left.Submit(origin,origin+axis*6,true,false); f.scale.Process(.01f);
            f.left.Submit(origin,origin+axis*9,true,false); f.scale.Process(.01f);
            Near(f.scale.Factor,2.25f,.00001f,"Rigid/mirrored/nonuniform frame range ratio");
        }
    }
    static void Preview()
    {
        var demo=FindObjectOfType<BirdMapPreview>(); demo.desktopInput=false; demo.Initialize(); var p=demo.Pointer;
        Capture(demo,"closed"); Feed(demo,demo.OpenButton.transform.position,false); Require(demo.Menu.State==BirdMenuPanel.PanelState.Closed,"First sample does not open gate");
        Feed(demo,new Vector3(0,2,-1),false); Feed(demo,demo.OpenButton.transform.position,false); Require(demo.Menu.State==BirdMenuPanel.PanelState.Open,"Reach-through opens map menu"); Capture(demo,"map-open");
        Aim(demo,demo.ZoomButton,false); Aim(demo,demo.ZoomButton,true); Require(demo.ZoomMode && demo.Zoom.ScalingEnabled && !demo.Scroll.enabled,"Zoom action excludes rotation");
        Vector3 origin=demo.View.transform.position,delta=(demo.Volume.transform.position-origin).normalized;
        Feed(demo,origin+delta*4,false); for(int i=0;i<100;i++) Feed(demo,origin+delta*6,false);
        Require(demo.Zoom.Factor>2.1f && demo.Zoom.Factor<=2.3f,"Map grows using ordinary range component"); Capture(demo,"zoomed");
        Feed(demo,origin+delta,false); float held=demo.Zoom.Factor; Feed(demo,origin+delta*6,false); Near(demo.Zoom.Factor,held,1e-5f,"Map clutch reentry has no jump");
        for(int i=0;i<100;i++) Feed(demo,origin+delta*3,false); Require(demo.Zoom.Factor<.65f,"Map shrinks with reach"); Capture(demo,"small");
        Aim(demo,demo.RotateButton,false); Aim(demo,demo.RotateButton,true); Require(!demo.Zoom.ScalingEnabled && demo.Scroll.enabled,"Rotation action excludes zoom");
        Quaternion initial=demo.Rotation.rotation;
        for(int i=0;i<40;i++) { Vector3 normal=Quaternion.AngleAxis(i,Vector3.up)*Vector3.forward; Feed(demo,origin+(demo.Volume.transform.position+normal*.95f-origin)*2,false); }
        Require(Quaternion.Angle(initial,demo.Rotation.rotation)>10,"Existing back-surface rotation drives map"); Capture(demo,"rotated");
        Aim(demo,demo.ResetButton,false); Aim(demo,demo.ResetButton,true); Near(demo.Zoom.Factor,1,.00001f,"Reset button restores map zoom");
        Near(Quaternion.Angle(demo.Rotation.localRotation,Quaternion.Euler(-30,-20,0)),0,.001f,"Reset button restores map orientation"); Capture(demo,"reset");
        Aim(demo,demo.CloseButton,false); Aim(demo,demo.CloseButton,true); Require(demo.Menu.State==BirdMenuPanel.PanelState.Closed && !demo.Zoom.ScalingEnabled,"Close cancels modes");
    }
    static void Prefab()
    {
        using(var f=new Fixture())
        {
            var menu=new GameObject("Event receiver").AddComponent<BirdMenuPanel>(); menu.transform.SetParent(f.root.transform,false);
            var content=new GameObject("Menu content"); content.transform.SetParent(menu.transform,false); menu.Configure(content);
            UnityEventTools.AddPersistentListener(f.scale.Started,menu.Open);
            UnityEventTools.AddVoidPersistentListener(f.scale.Stopped,menu.Close);
            PrefabUtility.SaveAsPrefabAsset(f.root,"Assets/RangeScaleFixture.prefab");
            var clone=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RangeScaleFixture.prefab"));
            var scaler=clone.GetComponent<BirdRangeScale>(); var pointer=clone.transform.Find("Left").GetComponent<BirdPointerInput>(); var receiver=clone.transform.Find("Event receiver").GetComponent<BirdMenuPanel>();
            pointer.Submit(f.origin,Vector3.forward,true,false); scaler.StartScaling(); scaler.Process(.01f);
            Require(receiver.State==BirdMenuPanel.PanelState.Open && receiver.Owner==pointer.UserId,"Serialized Started event survives prefab reload");
            scaler.StopScaling(); Require(receiver.State==BirdMenuPanel.PanelState.Closed,"Serialized Stopped event survives prefab reload"); DestroyImmediate(clone);
        }
    }
    static void Feed(BirdMapPreview demo,Vector3 point,bool pressed)
    {
        demo.Pointer.Submit(demo.View.transform.position,point,true,pressed); demo.Interactor.Process(); demo.Scroll.Process(1f/72); demo.Zoom.Process(1f/72); demo.RefreshVisuals();
    }
    static void Aim(BirdMapPreview demo,BirdMenuElement element,bool pressed) { Feed(demo,demo.View.transform.position+(element.transform.position-demo.View.transform.position)*1.1f,pressed); }
    static void Capture(BirdMapPreview demo,string name)
    {
        demo.RefreshVisuals(); var rt=new RenderTexture(1400,1000,24) { antiAliasing=4 }; var pixels=new Texture2D(1400,1000,TextureFormat.RGB24,false);
        demo.View.targetTexture=rt; demo.View.Render(); RenderTexture.active=rt; pixels.ReadPixels(new Rect(0,0,1400,1000),0,0); pixels.Apply(); File.WriteAllBytes("MapCaptures/"+name+".png",pixels.EncodeToPNG());
        RenderTexture.active=null; demo.View.targetTexture=null; rt.Release(); Destroy(rt); Destroy(pixels);
    }
    static void Require(bool ok,string message) { checks++; if(!ok) throw new Exception("Range/map assertion "+checks+": "+message); }
    static void Near(float a,float b,float tolerance,string message) { Require(Mathf.Abs(a-b)<=tolerance,message+": "+a+" vs "+b); }
}
#endif
