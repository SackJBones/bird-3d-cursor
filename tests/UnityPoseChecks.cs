#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Bird3DCursor.UI;
using Bird3DCursor.Manipulation;
using Bird3DCursor.Samples;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class UnityPoseChecks : MonoBehaviour
{
    const string Active="Bird.Pose.Checks";
    static int checks;
    int wait;
    sealed class Fixture:IDisposable
    {
        public GameObject root=new GameObject("Pose fixture");
        public BirdPlacementRegion region;
        public BirdGrabTarget item;
        public BirdSnapTarget slot;
        public BirdPointerInput pointer;
        public BirdGrabInteractor grip;
        public BirdHeldPoseControls commands;
        public Vector3 home,anchor,origin;
        public Quaternion homeRotation;
        public Vector3 homeScale;
        public Fixture()
        {
            region=root.AddComponent<BirdPlacementRegion>(); region.LocalBounds=new Bounds(new Vector3(0,2,0),new Vector3(8,4,6));
            var go=new GameObject("Item"); go.transform.SetParent(root.transform,false); go.transform.localPosition=new Vector3(-2,1,0);
            var box=go.AddComponent<BoxCollider>(); box.center=Vector3.up*.2f; box.size=new Vector3(1,.8f,.6f);
            item=go.AddComponent<BirdGrabTarget>();
            slot=new GameObject("Dock").AddComponent<BirdSnapTarget>(); slot.transform.SetParent(root.transform,false); slot.transform.localPosition=new Vector3(2,1,0);
            slot.transform.localRotation=Quaternion.Euler(0,90,0); slot.matchRotation=slot.matchScale=true; slot.scaleFactor=1.25f;
            item.Configure(box,region,new[]{slot}); item.ConfigurePose(true,true,.5f,2);
            pointer=root.AddComponent<BirdPointerInput>(); grip=root.AddComponent<BirdGrabInteractor>(); grip.automatic=false; grip.Configure(new[]{pointer},new[]{item});
            commands=root.AddComponent<BirdHeldPoseControls>(); commands.interactor=grip;
        }
        public void Pick()
        {
            Physics.SyncTransforms(); home=region.transform.InverseTransformPoint(item.transform.position); homeRotation=item.transform.localRotation; homeScale=item.transform.localScale;
            anchor=item.Volume.transform.TransformPoint(item.Volume.center); origin=anchor-root.transform.forward*5;
            pointer.Submit(origin,anchor,true,false); grip.Process(1f/72); pointer.Submit(origin,anchor,true,true); grip.Process(1f/72);
            Assert(grip.ActiveTarget==item,"Fresh grip acquires pose-enabled item");
        }
        public void Feed(Vector3 localPivot,bool pressed=true,float dt=1f/72)
        { pointer.Submit(origin,anchor+region.transform.TransformVector(localPivot-home),true,pressed); grip.Process(dt); }
        public void Hold(Vector3 localPivot,int frames=90) { for(int i=0;i<frames;i++) Feed(localPivot); }
        public void Return() { for(int i=0;i<30;i++) { grip.Process(1f/72); Inside(item); } }
        public void Dispose() { DestroyImmediate(root); }
    }
    public static void Run()
    {
        File.WriteAllText("pose-result.txt","PENDING"); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("Pose docking preview").AddComponent<BirdPosePreview>(); EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/PosePreview.unity");
        SessionState.SetBool(Active,true); EditorApplication.isPlaying=true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Begin() { if(SessionState.GetBool(Active,false)) new GameObject("Pose checks").AddComponent<UnityPoseChecks>(); }
    void Update()
    {
        if(wait++<2) return; enabled=false;
        try
        {
            BoundsWithoutMutation(); Placement(); Boundaries(); TransformedTransactions(); MirroredDocks(); Lifecycle(); Rates(); Serialization(); Preview();
            File.WriteAllText("pose-result.txt","PASS: "+checks+" actual Unity pose assertions, four rendered views, two-scale docking, whole-box bounds, pose-intent gates, full-pose rollback and prefab event. Synthetic input; no Udon/client/headset claim.");
            SessionState.SetBool(Active,false); EditorApplication.Exit(0);
        }
        catch(Exception e) { File.WriteAllText("pose-result.txt","FAIL after "+checks+": "+e); Debug.LogException(e); SessionState.SetBool(Active,false); EditorApplication.Exit(1); }
    }
    static void BoundsWithoutMutation()
    {
        using(var f=new Fixture())
        {
            var child=new GameObject("Offset shape").transform; child.SetParent(f.item.transform,false); child.localPosition=new Vector3(.2f,.1f,-.15f); child.localRotation=Quaternion.Euler(15,30,12); child.localScale=new Vector3(.8f,.6f,1.1f);
            var box=child.gameObject.AddComponent<BoxCollider>(); box.center=new Vector3(.13f,-.05f,.02f); box.size=new Vector3(1,.7f,.5f);
            for(int frame=0;frame<4;frame++)
            {
                f.root.transform.rotation=frame==0?Quaternion.identity:frame==1?Quaternion.Euler(180,0,0):Quaternion.Euler(23,48,-17);
                f.root.transform.localScale=frame==3?new Vector3(-1.3f,.7f,1.6f):Vector3.one*(frame+1);
                f.item.transform.localScale=new Vector3(-.8f,1.1f,.7f);
                foreach(float factor in new[]{.5f,1f,1.8f})
                {
                    var orientation=Quaternion.Euler(27,61,13); var scale=f.item.transform.localScale*factor;
                    Vector3 beforePosition=f.item.transform.position,beforeScale=f.item.transform.localScale; Quaternion beforeRotation=f.item.transform.localRotation;
                    Bounds predicted; Assert(f.region.TryPivotBounds(f.item.transform,box,orientation,scale,out predicted),"Hypothetical transformed pose fits");
                    Assert(f.item.transform.position==beforePosition && f.item.transform.localRotation==beforeRotation && f.item.transform.localScale==beforeScale,"Bounds query never mutates scene");
                    f.item.transform.localRotation=orientation; f.item.transform.localScale=scale;
                    // Independent actual Transform corner bounds, including offset and rotated child geometry.
                    Vector3 pivot=f.region.transform.InverseTransformPoint(f.item.transform.position),min=Vector3.one*float.PositiveInfinity,max=-min;
                    for(int i=0;i<8;i++) { Vector3 corner=Corner(f.item,box,i)-pivot; min=Vector3.Min(min,corner); max=Vector3.Max(max,corner); }
                    Near(predicted.min,f.region.LocalBounds.min-min,.00002f,"Predicted minimum agrees with actual transformed corners");
                    Near(predicted.max,f.region.LocalBounds.max-max,.00002f,"Predicted maximum agrees with actual transformed corners");
                    f.item.transform.localRotation=beforeRotation; f.item.transform.localScale=beforeScale;
                }
            }
        }
    }
    static void Placement()
    {
        using(var f=new Fixture())
        {
            int placed=0; f.item.Placed.AddListener(()=>placed++); f.Pick(); Vector3 end=f.slot.transform.localPosition;
            f.Hold(end); Assert(!f.grip.ReadyToPlace,"Position alone cannot qualify misoriented/wrong-size dock");
            Assert(f.grip.TrySetHeldPose(Quaternion.Euler(0,90,0),1.25f),"Valid pose request accepted");
            Assert(f.item.transform.localRotation==f.homeRotation && f.item.transform.localScale==f.homeScale,"Request queues pose without mutating target");
            f.Hold(end); Assert(f.grip.ReadyToPlace,"Settled position, orientation and size qualify");
            Assert(!f.grip.TrySetHeldPose(Quaternion.Euler(0,90,0),20),"Beyond-limit pose request rejected"); f.Feed(end);
            Assert(f.grip.PoseLimited && !f.grip.ReadyToPlace,"Rejected raw pose request cannot reuse stale matching intent");
            f.grip.TrySetHeldPose(Quaternion.Euler(0,90,0),1.25f); f.Hold(end); Assert(f.grip.ReadyToPlace,"Valid pose request restores placement eligibility");
            Assert(f.grip.TrySetHeldPose(Quaternion.identity,1.25f),"Raw rotation request moves away"); f.Feed(end);
            Assert(!f.grip.ReadyToPlace,"Current near-matching pose cannot counterfeit contrary raw rotation intent");
            f.grip.TrySetHeldPose(Quaternion.Euler(0,90,0),1.25f); f.Hold(end);
            f.grip.TrySetHeldPose(Quaternion.Euler(0,90,0),1.8f); f.Feed(end,true,.001f);
            Assert(!f.grip.ReadyToPlace,"Current near-matching size cannot counterfeit contrary raw scale intent");
            f.grip.TrySetHeldPose(Quaternion.Euler(0,90,0),1.25f); f.Hold(end); f.Feed(end,false);
            Assert(placed==1 && f.grip.ActiveTarget==null,"Exact pose commits once"); Near(f.item.transform.localPosition,end,1e-6f,"Exact pivot commit");
            Assert(Quaternion.Angle(f.item.transform.localRotation,f.slot.transform.localRotation)<.001f,"Exact orientation commit"); Near(f.item.transform.localScale,Vector3.one*1.25f,1e-6f,"Exact size commit");
            f.Pick(); Assert(Mathf.Abs(f.grip.CurrentScaleFactor-1.25f)<1e-6f,"New grip retains authored reference rather than compounding scale");
            f.slot.transform.localPosition=new Vector3(-2,1,0); f.slot.transform.localRotation=Quaternion.identity; f.slot.scaleFactor=1;
            f.grip.TrySetHeldPose(Quaternion.identity,1); f.Hold(f.slot.transform.localPosition); f.Feed(f.slot.transform.localPosition,false);
            Assert(placed==2 && f.grip.ActiveTarget==null,"Second transaction can restore original dock size"); Near(f.item.transform.localScale,Vector3.one,1e-6f,"Authored size stable across placements");
        }
    }
    static void Boundaries()
    {
        using(var f=new Fixture())
        {
            f.Pick(); Assert(!f.grip.TrySetHeldPose(Quaternion.identity,3),"Authored scale limit rejects oversized request");
            Assert(!f.grip.TrySetHeldPose(new Quaternion(0,0,0,0),1) && !f.grip.TrySetHeldPose(new Quaternion(float.NaN,0,0,1),1),"Invalid orientations rejected");
            Assert(!f.grip.TrySetHeldPose(Quaternion.identity,float.PositiveInfinity),"Invalid factor rejected");
            for(int i=0;i<80;i++)
            {
                f.grip.TrySetHeldPose(Quaternion.Euler(i*3,i*7,i*2),.6f+(i%15)*.08f);
                f.Feed(new Vector3(i%2==0?100:-100,i%3==0?100:-100,50)); Inside(f.item);
            }
            Vector3 logical=f.pointer.Position; Assert((logical-f.item.transform.position).magnitude>50,"Workspace restricts full object while logical point stays distant");
            f.grip.Cancel(); f.Return(); Near(f.item.transform.localScale,Vector3.one,1e-6f,"Return restores scale");
            Assert(Quaternion.Angle(f.item.transform.localRotation,f.homeRotation)<.001f,"Return restores rotation"); Near(f.item.transform.localPosition,f.home,1e-6f,"Return restores home pivot");
        }
        using(var f=new Fixture())
        {
            f.region.LocalBounds=new Bounds(new Vector3(0,1,0),new Vector3(1.5f,2,1.5f)); f.item.transform.localPosition=Vector3.up;
            f.item.Volume.center=Vector3.zero; f.item.Volume.size=new Vector3(1.4f,.5f,1.4f); f.Pick();
            Assert(f.grip.TrySetHeldPose(Quaternion.Euler(0,90,0),1),"Fitting endpoint accepted in narrow region");
            for(int i=0;i<30;i++) { f.Feed(f.home,true,1f/120); Inside(f.item); }
            Assert(f.grip.PoseLimited,"An oversized intermediate box stops rotation at a valid sampled pose");
            f.slot.transform.localPosition=f.home; f.slot.transform.localRotation=f.item.transform.localRotation; f.slot.scaleFactor=1; f.Feed(f.home);
            Assert(!f.grip.ReadyToPlace,"Stopped intermediate pose cannot manufacture dock intent");
            f.grip.Cancel(); f.Return();
            f.Pick(); f.grip.followRate=100; f.grip.TrySetHeldPose(Quaternion.Euler(0,90,0),1); f.Feed(f.home,true,.2f);
            Assert(Quaternion.Angle(f.item.transform.localRotation,Quaternion.Euler(0,90,0))<.01f,"Coarse pose step reaches another feasible orientation");
            f.grip.Cancel(); f.grip.Process(.125f);
            Assert(f.grip.ActiveTarget==null && Quaternion.Angle(f.item.transform.localRotation,Quaternion.identity)<.001f,"Impossible return intermediate falls back to committed pose"); Inside(f.item);
        }
        using(var f=new Fixture())
        {
            f.region.LocalBounds=new Bounds(Vector3.up,new Vector3(1.8f,2,3)); f.item.transform.localPosition=Vector3.up;
            f.item.Volume.center=Vector3.zero; f.item.Volume.size=new Vector3(1,.5f,.5f); f.slot.transform.localPosition=Vector3.up; f.slot.matchRotation=false; f.slot.scaleFactor=1.9f;
            f.Pick(); Assert(f.grip.TrySetHeldPose(Quaternion.identity,1.79f),"Nearly filling pose fits"); f.Hold(f.home);
            Assert(!f.grip.ReadyToPlace,"Close size intent cannot commit an exact dock pose that does not fit");
            f.slot.scaleFactor=float.NaN; f.Feed(f.home); Assert(!f.grip.ReadyToPlace,"Nonfinite dock scale rejected");
            f.item.Volume.transform.localScale=Vector3.zero; Bounds ignored; Assert(!f.region.TryPivotBounds(f.item.transform,f.item.Volume,out ignored),"Collapsed shape rejected");
        }
    }
    static void Lifecycle()
    {
        using(var f=new Fixture())
        {
            f.item.ConfigurePose(false,false); f.Pick(); Assert(!f.grip.TrySetHeldPose(Quaternion.Euler(0,30,0),1) && !f.grip.TrySetHeldPose(Quaternion.identity,1.25f),"Default translation permissions cannot be bypassed"); f.grip.CancelImmediately();
            f.item.ConfigurePose(true,true); f.Pick(); f.commands.RotateAroundWorkspaceUp(45); f.commands.ResizeBy(1.25f); f.Hold(Vector3.up*2);
            f.pointer.Cancel(); f.grip.Process(1f/72); Assert(f.grip.IsReturning,"Tracking loss returns all pose channels"); f.Return(); Near(f.item.transform.localScale,Vector3.one,1e-6f,"Loss restores size");
            f.Pick(); f.commands.RotateAroundWorkspaceUp(45); f.commands.ResizeBy(1.25f); f.Hold(Vector3.up*2); f.grip.enabled=false;
            Assert(f.grip.ActiveTarget==null && f.item.Owner==null,"Disable releases reservation"); Near(f.item.transform.localScale,Vector3.one,1e-6f,"Disable restores size immediately"); Assert(Quaternion.Angle(f.item.transform.localRotation,Quaternion.identity)<.001f,"Disable restores orientation immediately"); f.grip.enabled=true;
            f.Pick(); f.commands.ResizeBy(1.25f); f.Hold(Vector3.up*2); f.item.enabled=false;
            Assert(f.grip.ActiveTarget==null && f.item.Owner==null,"Target disable rolls back complete pose"); Near(f.item.transform.localScale,Vector3.one,1e-6f,"Target disable restores original scale"); f.item.enabled=true;
            f.Pick(); f.commands.ResizeBy(1.25f); f.Hold(Vector3.up*2); f.grip.Process(.3f); Assert(f.grip.IsReturning,"Frame pause cancels pose transaction"); f.Return();
            f.Pick(); f.commands.ResizeBy(1.25f); f.Hold(Vector3.up*2);
            var serial=new SerializedObject(f.item); serial.FindProperty("minimumScaleFactor").floatValue=.6f; serial.ApplyModifiedPropertiesWithoutUndo(); f.grip.Process(1f/72);
            Assert(f.grip.IsReturning,"Mid-grip pose policy change cancels"); f.Return();
        }
        using(var f=new Fixture())
        {
            var parent=new GameObject("Independent moving parent").transform; parent.SetParent(f.root.transform,false); f.item.transform.SetParent(parent,true); f.Pick();
            parent.localPosition=Vector3.right*.1f; f.grip.Process(1f/72); Assert(f.grip.IsReturning,"Parent frame translation cannot silently change a held transaction"); f.Return();
        }
        using(var f=new Fixture())
        {
            f.item.ConfigurePose(false,false); f.item.transform.localScale=Vector3.one*1.25f; f.slot.matchRotation=false;
            f.Pick(); f.Hold(f.slot.transform.localPosition); Assert(f.grip.ReadyToPlace,"Fixed-size target can match its authored-reference scale without permitting resizing");
            f.grip.CancelImmediately(); f.item.transform.localScale=new Vector3(1.1f,1.2f,1.3f); f.Pick(); f.Hold(f.slot.transform.localPosition);
            Assert(!f.grip.ReadyToPlace,"Nonproportional fixed scale cannot pretend to match uniform factor");
        }
    }
    static void TransformedTransactions()
    {
        for(int frame=0;frame<4;frame++) using(var f=new Fixture())
        {
            f.root.transform.rotation=frame==0?Quaternion.identity:frame==1?Quaternion.Euler(180,0,0):Quaternion.Euler(23,48,-17);
            f.root.transform.localScale=frame==3?new Vector3(-1.3f,.7f,1.6f):Vector3.one*(frame+1);
            f.item.transform.localScale=new Vector3(-.8f,1.1f,.7f); f.item.ConfigurePose(true,true); f.Pick();
            for(int i=0;i<24;i++)
            {
                Assert(f.grip.TrySetHeldPose(Quaternion.Euler(i*3,i*7,i*2),.8f+(i%9)*.1f),"Transformed pose request fits");
                f.Feed(new Vector3(i%2==0?30:-30,i%3==0?30:-30,20));
                Assert(f.grip.ActiveTarget==f.item && !f.grip.IsReturning,"Transform frame does not cause spurious cancellation"); Inside(f.item);
            }
            f.grip.Cancel(); f.Return(); Near(f.item.transform.localScale,f.homeScale,.00001f,"Mirrored/nonuniform reference restored");
            Near(f.item.transform.localPosition,f.home,.00001f,"Transformed rollback pivot restored");
        }
    }
    static void Rates()
    {
        var rows=new List<string>{"hz,yaw_degrees,scale_factor,iterative_slerp_yaw_degrees"};
        foreach(int hz in new[]{30,72,120}) using(var f=new Fixture())
        {
            f.Pick(); f.grip.followRate=10; f.grip.TrySetHeldPose(Quaternion.Euler(0,90,0),1.8f);
            Quaternion iterative=Quaternion.identity;
            for(int i=0;i<hz/2;i++) { f.Feed(f.home,true,1f/hz); iterative=Quaternion.Slerp(iterative,Quaternion.Euler(0,90,0),1-Mathf.Exp(-10f/hz)); }
            float weight=1-Mathf.Exp(-5); float angle=Quaternion.Angle(Quaternion.identity,f.item.transform.localRotation);
            Assert(f.grip.ActiveTarget==f.item && !f.grip.PoseLimited,"Rate fixture remains freely held");
            Assert(Mathf.Abs(angle-90*weight)<.003f,"Held orientation matches analytic stationary-request response at "+hz+": "+angle+" vs "+(90*weight));
            Assert(Mathf.Abs(f.grip.CurrentScaleFactor-Mathf.Exp(Mathf.Log(1.8f)*weight))<.00001f,"Held log-scale matches analytic stationary-request response at "+hz);
            rows.Add(hz+","+angle.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+f.grip.CurrentScaleFactor.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+Quaternion.Angle(Quaternion.identity,iterative).ToString("R",System.Globalization.CultureInfo.InvariantCulture));
        }
        Directory.CreateDirectory("PoseCaptures"); File.WriteAllLines("PoseCaptures/pose-rates.csv",rows);
    }
    static void MirroredDocks()
    {
        foreach(bool nested in new[]{false,true}) using(var f=new Fixture())
        {
            f.root.transform.rotation=Quaternion.Euler(23,62,34); f.root.transform.localScale=new Vector3(-1.3f,.7f,1.6f);
            if(nested)
            {
                var frame=new GameObject("Independent authored parent").transform; frame.SetParent(f.root.transform,false); frame.localRotation=Quaternion.Euler(0,15,0);
                f.item.transform.SetParent(frame,false); f.item.transform.localRotation=Quaternion.Euler(0,-15,0);
            }
            f.item.transform.localScale=new Vector3(-1,.8f,1.1f); f.item.ConfigurePose(true,true); f.Pick();
            for(int i=0;i<6;i++) f.commands.RotateAroundWorkspaceUp(15); f.commands.ResizeBy(1.25f);
            Quaternion expected=Quaternion.Euler(0,nested?75:90,0);
            Assert(Quaternion.Angle(f.grip.RequestedLocalRotation,expected)<.02f,"Pose commands use authored axes through mirrored hierarchy");
            f.Hold(f.slot.transform.localPosition); Assert(f.grip.ReadyToPlace,"Matching authored dock works with mirrored/nonuniform parent and reference");
            f.Feed(f.slot.transform.localPosition,false);
            Assert(f.grip.ActiveTarget==null && Quaternion.Angle(f.item.transform.localRotation,expected)<.02f,"Mirrored dock commits exact local orientation");
            Near(f.item.transform.localScale,new Vector3(-1,.8f,1.1f)*1.25f,.00001f,"Mirrored dock preserves reference proportions"); Inside(f.item);
        }
    }
    static void Serialization()
    {
        using(var f=new Fixture())
        {
            UnityEventTools.AddFloatPersistentListener(f.item.Grabbed,f.commands.ResizeBy,1.25f);
            PrefabUtility.SaveAsPrefabAsset(f.root,"Assets/PoseAuthoring.prefab");
            var loaded=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PoseAuthoring.prefab")); loaded.transform.position=Vector3.right*20;
            var item=loaded.GetComponentInChildren<BirdGrabTarget>(); var pointer=loaded.GetComponent<BirdPointerInput>(); var grip=loaded.GetComponent<BirdGrabInteractor>(); var point=item.transform.TransformPoint(item.Volume.center); Physics.SyncTransforms();
            pointer.Submit(point-Vector3.forward*3,point,true,false); grip.Process(1f/72); pointer.Submit(point-Vector3.forward*3,point,true,true); grip.Process(1f/72);
            Assert(grip.ActiveTarget==item && Mathf.Abs(grip.RequestedScaleFactor-1.25f)<1e-6f,"Prefab remaps persistent pose command to its own transaction");
            Assert(item.AllowRotation && item.AllowScaling && item.Destinations[0].matchRotation && item.Destinations[0].scaleFactor==1.25f,"Pose permissions and dock fields round trip"); DestroyImmediate(loaded);
        }
    }
    static void Preview()
    {
        var demo=FindObjectOfType<BirdPosePreview>(); demo.desktopInput=false; demo.Interactor.automatic=false; Capture(demo,"start");
        foreach(var item in demo.Items)
        {
            Vector3 start=item.transform.position,anchor=item.Volume.transform.TransformPoint(item.Volume.center),origin=anchor-item.Region.transform.forward*5*Mathf.Abs(item.Region.transform.lossyScale.z);
            demo.Pointer.Submit(origin,anchor,true,false); demo.Interactor.Process(1f/72); demo.Pointer.Submit(origin,anchor,true,true); demo.Interactor.Process(1f/72);
            Assert(demo.Interactor.ActiveTarget==item,"Preview pick at both scales"); var slot=item.Destinations[1];
            for(int i=0;i<6;i++) demo.TurnRight(); demo.Grow();
            Vector3 end=anchor+(slot.transform.position-start);
            for(int i=0;i<100;i++) { demo.Pointer.Submit(origin,end,true,true); demo.Interactor.Process(1f/72); Inside(item); }
            Assert(demo.Interactor.ReadyToPlace,"Preview required pose matches at both scales"); Capture(demo,item==demo.Items[0]?"table-ready":"building-ready");
            demo.Pointer.Submit(origin,end,true,false); demo.Interactor.Process(1f/72); Assert(demo.Interactor.ActiveTarget==null,"Preview exact pose drop");
        }
        Capture(demo,"both-placed");
    }
    static Vector3 Corner(BirdGrabTarget item,BoxCollider box,int i)
    { return item.Region.transform.InverseTransformPoint(box.transform.TransformPoint(box.center+Vector3.Scale(box.size*.5f,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)))); }
    static void Inside(BirdGrabTarget item)
    { Bounds b=item.Region.LocalBounds; b.Expand(.0001f); for(int i=0;i<8;i++) Assert(b.Contains(Corner(item,item.Volume,i)),"Whole configured volume stays in region"); }
    static void Near(Vector3 a,Vector3 b,float epsilon,string why) { Assert(Vector3.Distance(a,b)<=epsilon,why+" "+a+" vs "+b); }
    static void Assert(bool ok,string message) { checks++; if(!ok) throw new Exception("Assertion "+checks+": "+message); }
    static void Capture(BirdPosePreview demo,string name)
    {
        demo.UpdateFeedback(); var rt=new RenderTexture(1440,1000,24){antiAliasing=4}; var pixels=new Texture2D(1440,1000,TextureFormat.RGB24,false); demo.View.targetTexture=rt; demo.View.Render(); RenderTexture.active=rt;
        pixels.ReadPixels(new Rect(0,0,1440,1000),0,0); pixels.Apply(); File.WriteAllBytes("PoseCaptures/"+name+".png",pixels.EncodeToPNG()); demo.View.targetTexture=null; RenderTexture.active=null; rt.Release(); Destroy(rt); Destroy(pixels);
    }
}
#endif
