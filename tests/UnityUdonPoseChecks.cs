#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UdonSharp;
using UdonSharpEditor;
using VRC.SDKBase;
using VRC.Udon;

// Configure proxies only while authoring. All runtime interaction uses compiled backing VMs.
public class UnityUdonPoseChecks : MonoBehaviour
{
    const string ScenePath="Assets/BirdWorld/Scenes/BirdPoseDemo.unity", Active="Bird.Udon.Pose.Checks";
    static UdonBehaviour grip,left,router,controls,gate,item,region,slot;
    static readonly Vector3 Origin=new Vector3(0,1.65f,0);
    static Vector3 anchor,home;
    static int checks;
    static double deadline;
    int stage;
    float started;
    public static void Run()
    {
        File.WriteAllText("udon-pose-result.txt","PENDING"); Compile(); EditorSceneManager.OpenScene(ScenePath);
        SessionState.SetBool(Active,true); EditorApplication.isPlaying=true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Begin() { if(SessionState.GetBool(Active,false)) new GameObject("Pose VM checks").AddComponent<UnityUdonPoseChecks>(); }
    void Update()
    {
        if(!SessionState.GetBool(Active,false)) return;
        if(deadline==0) deadline=EditorApplication.timeSinceStartup+120;
        if(EditorApplication.timeSinceStartup>deadline) { Finish(false,"ClientSim/frame sequence timeout"); return; }
        if(!Utilities.IsValid(Networking.LocalPlayer) || Time.timeSinceLevelLoad<2) return;
        try
        {
            if(!Frames()) return;
            Contracts(); Finish(true,checks+" compiled-Udon pose assertions; saved-scene normal-frame pickup, command rotation/resize, full-pose docking and loss return; limits, intent, whole-box containment, frames, callbacks, lifecycle and cadence. Four rendered captures. Synthetic logical input, not physical hands, VRChat client or multiplayer validation.");
        }
        catch(Exception e) { Finish(false,e.ToString()); }
    }
    bool Frames()
    {
        if(stage==0)
        {
            grip=Vm<BirdObjectGrip>("Hanoi Grip"); left=Vm<BirdUiPointer>("UI Left"); router=Vm<BirdUiRouter>("UI Router");
            controls=Vm<BirdObjectPoseControls>("Pose Controls"); gate=Vm<BirdObjectPlayArea>("Hanoi Play Area");
            Vm<BirdUiDesktopInput>("UI Desktop Input").enabled=false; controls.SetProgramVariable("desktopInput",false);
            Select("Table"); anchor=Volume().transform.TransformPoint(Volume().center); home=item.transform.position; Feed(anchor); stage++; return false;
        }
        if(stage==1) { Feed(anchor,true); stage++; return false; }
        if(stage==2)
        {
            Assert(Ref(grip,"ActiveTarget")==item,"Normal LateUpdate acquires authored pose item");
            for(int i=0;i<6;i++) controls.SendCustomEvent("RotateRight"); controls.SendCustomEvent("Grow");
            started=Time.unscaledTime; stage++; return false;
        }
        if(stage==3)
        {
            Feed(anchor+slot.transform.position-home,true);
            if(Time.unscaledTime-started<.8f) return false;
            Assert(Flag(grip,"ReadyToPlace"),"Normal frames reach matching rotation/scale dock"); Inside(); Capture("frame-table-ready");
            Feed(anchor+slot.transform.position-home); stage++; return false;
        }
        if(stage==4)
        {
            Assert(Ref(grip,"ActiveTarget")==null,"Normal release commits"); Exact();
            Select("Buildings"); anchor=Volume().transform.TransformPoint(Volume().center); home=item.transform.position; Feed(anchor); stage++; return false;
        }
        if(stage==5) { Feed(anchor,true); stage++; return false; }
        if(stage==6)
        {
            Assert(Ref(grip,"ActiveTarget")==item,"Normal distant pickup"); controls.SendCustomEvent("RotateRight"); controls.SendCustomEvent("Grow");
            started=Time.unscaledTime; stage++; return false;
        }
        if(stage==7)
        {
            Feed(Origin,true); if(Time.unscaledTime-started<.5f) return false;
            Inside(); Assert(Volume().bounds.min.z>300,"Rotated/resized whole building remains distant on hand closure");
            Capture("frame-building-bounded"); left.SendCustomEvent("Cancel"); started=Time.unscaledTime; stage++; return false;
        }
        if(stage==8)
        {
            if(Time.unscaledTime-started<.45f) return false;
            Assert(Ref(grip,"ActiveTarget")==null && Vector3.Distance(item.transform.position,home)<.002f && Quaternion.Angle(item.transform.localRotation,Quaternion.identity)<.01f && (item.transform.localScale-Vector3.one).magnitude<.0001f,"Normal loss restores full acquisition pose");
            Capture("paired-pose-stations"); return true;
        }
        return false;
    }
    static void Contracts()
    {
        grip.SetProgramVariable("automatic",false); router.SetProgramVariable("automatic",false); gate.SetProgramVariable("automatic",false);
        foreach(string scale in new[]{"Table","Buildings"})
        {
            Select(scale); Reset(); Grab();
            Hold(slot.transform.position,60); Assert(!Flag(grip,"ReadyToPlace"),"Position alone cannot match a pose dock: "+scale);
            Request(Quaternion.Euler(0,90,0),1.25f); Hold(slot.transform.position,60);
            Assert(Flag(grip,"ReadyToPlace"),"Full intent and display match: "+scale);
            Request(Quaternion.Euler(0,150,0),1.25f); Step(); Assert(!Flag(grip,"ReadyToPlace"),"Matching display cannot override contrary orientation intent");
            Request(Quaternion.Euler(0,90,0),1.25f); Hold(slot.transform.position,60);
            Request(Quaternion.Euler(0,90,0),1.8f); Step(); Assert(!Flag(grip,"ReadyToPlace"),"Matching display cannot override contrary scale intent");
            Request(Quaternion.Euler(0,90,0),1.25f); Hold(slot.transform.position,60);
            Request(Quaternion.Euler(0,90,0),20,false); Step(); Assert(Flag(grip,"PoseLimited") && !Flag(grip,"ReadyToPlace"),"Rejected request blocks stale matching intent");
            controls.SendCustomEvent("Refresh"); Assert(((Text)controls.GetProgramVariable("status")).text.StartsWith("POSE LIMIT"),"Authored limit feedback follows VM state");
            Request(Quaternion.Euler(0,90,0),1.25f); Hold(slot.transform.position,60); Release(slot.transform.position); Exact();
            Grab(); Near((float)grip.GetProgramVariable("CurrentScaleFactor"),1.25f,.00001f,"Re-grip uses authored reference");
            controls.SendCustomEvent("Shrink"); Hold(item.transform.position,50); Near((float)grip.GetProgramVariable("CurrentScaleFactor"),1,.0001f,"Shrink command uses current requested factor");
            controls.SendCustomEvent("ResetPose"); Hold(item.transform.position,50); Near((float)grip.GetProgramVariable("CurrentScaleFactor"),1.25f,.0001f,"Reset uses acquisition factor");
            grip.SendCustomEvent("CancelImmediately");
        }
        Select("Table"); Reset(); Grab();
        foreach(float bad in new[]{0f,-1f,float.NaN,float.PositiveInfinity,.49f,2.01f}) Request(Quaternion.identity,bad,false);
        Request(new Quaternion(0,0,0,0),1,false); Request(new Quaternion(float.NaN,0,0,1),1,false);
        Request(Quaternion.identity,1); Assert(!Flag(grip,"PoseLimited"),"Valid request clears limit");
        var random=new System.Random(73);
        for(int n=0;n<40;n++)
        {
            Request(Quaternion.Euler(0,(float)random.NextDouble()*360,0),.55f+(float)random.NextDouble()*1.1f);
            Hold(region.transform.TransformPoint(new Vector3(n%2==0?10:-10,5,10)),3); Inside();
        }
        grip.SendCustomEvent("Cancel"); for(int n=0;n<30;n++) { Step(); Inside(); }
        Assert(Ref(grip,"ActiveTarget")==null,"Full pose cancellation finishes");
        Lifecycle(); FramesAndVolumes(); Narrow(); Rates();
        Select("Table"); Reset(); Select("Buildings"); Reset(); Capture("verified-final");
    }
    static void Lifecycle()
    {
        Reset(); item.SetProgramVariable("allowRotation",false); item.SetProgramVariable("allowScaling",false); Grab();
        Request(Quaternion.Euler(0,15,0),1,false); Request(Quaternion.identity,1.2f,false); grip.SendCustomEvent("CancelImmediately");
        item.SetProgramVariable("allowRotation",true); item.SetProgramVariable("allowScaling",true);
        foreach(string cause in new[]{"loss","pause","target","grip","policy","parent","menu","consumed"})
        {
            Reset(); Grab(); Request(Quaternion.Euler(0,60,0),1.4f); Hold(region.transform.TransformPoint(new Vector3(0,.4f,0)),40);
            var root=Vm<BirdUiPanel>("UI Root Panel"); var frame=item.transform.parent; Vector3 oldPosition=frame.position;
            if(cause=="loss") left.SendCustomEvent("Cancel");
            if(cause=="pause") grip.SetProgramVariable("stepDelta",.3f);
            if(cause=="target") item.enabled=false;
            if(cause=="grip") grip.enabled=false;
            if(cause=="policy") item.SetProgramVariable("maximumScaleFactor",1.6f);
            if(cause=="parent") frame.position+=Vector3.right*.1f;
            if(cause=="menu") root.SetProgramVariable("state",1);
            if(cause=="consumed") left.SetProgramVariable("uiConsumed",true);
            grip.SendCustomEvent("Process");
            Assert(Ref(grip,"ActiveTarget")==null || Flag(grip,"IsReturning"),"Cancellation trigger: "+cause);
            item.enabled=true; grip.enabled=true; item.SetProgramVariable("maximumScaleFactor",2f); frame.position=oldPosition;
            root.SetProgramVariable("state",0); left.SetProgramVariable("uiConsumed",false);
            for(int i=0;i<30;i++) Step();
            Assert(Ref(grip,"ActiveTarget")==null && Quaternion.Angle(item.transform.localRotation,Quaternion.identity)<.01f && (item.transform.localScale-Vector3.one).sqrMagnitude<1e-8f,"Restored acquisition pose: "+cause);
        }
        Reset(); item.SetProgramVariable("eventTarget",controls); item.SetProgramVariable("grabbedEvent","Grow"); Grab();
        Near((float)grip.GetProgramVariable("RequestedScaleFactor"),1.25f,.00001f,"Serialized cross-program grabbed callback can request pose");
        grip.SendCustomEvent("CancelImmediately"); item.SetProgramVariable("eventTarget",null); item.SetProgramVariable("grabbedEvent","");
    }
    static void FramesAndVolumes()
    {
        var frame=region.transform; Quaternion frameRotation=frame.rotation; Vector3 frameScale=frame.localScale;
        var volume=Volume(); Quaternion childRotation=volume.transform.localRotation; Vector3 childScale=volume.transform.localScale;
        Vector3 center=volume.center,size=volume.size;
        object oldGate=grip.GetProgramVariable("playArea"); grip.SetProgramVariable("playArea",null);
        foreach(int mode in new[]{0,1,2})
        {
            Reset(); frame.rotation=Quaternion.Euler(mode==0?180:23,mode*31,mode*17); frame.localScale=Vector3.Scale(frameScale,mode==2?new Vector3(-1,1.3f,.8f):Vector3.one);
            volume.transform.localRotation=Quaternion.Euler(0,17,0); volume.transform.localScale=new Vector3(.8f,1,.9f); volume.center=new Vector3(.05f,.46f,0);
            Vector3 reference=mode==2?new Vector3(-1,.8f,1.1f):Vector3.one; item.transform.localScale=reference; item.SetProgramVariable("poseReferenceScale",reference);
            Physics.SyncTransforms(); Grab(); Request(Quaternion.Euler(0,90,0),1.25f); Hold(slot.transform.position,70); Inside();
            Assert(Flag(grip,"ReadyToPlace"),"Authored orientation docks through mirrored/nonuniform frame and offset rotated child box: "+mode); Release(slot.transform.position); Exact(reference);
        }
        grip.SendCustomEvent("CancelImmediately"); frame.rotation=frameRotation; frame.localScale=frameScale;
        volume.transform.localRotation=childRotation; volume.transform.localScale=childScale; volume.center=center; volume.size=size;
        item.SetProgramVariable("poseReferenceScale",Vector3.one); grip.SetProgramVariable("playArea",oldGate); Reset();
        volume.transform.localScale=Vector3.zero; Feed(item.transform.position); Step(); Feed(item.transform.position,true); Step(); Assert(Ref(grip,"ActiveTarget")==null,"Degenerate child volume rejects pickup"); volume.transform.localScale=childScale;
    }
    static void Narrow()
    {
        Reset(); var volume=Volume(); Vector3 center=volume.center,size=volume.size; var bounds=(Bounds)region.GetProgramVariable("localBounds");
        Vector3 originalSlot=slot.transform.localPosition;
        region.SetProgramVariable("localBounds",new Bounds(Vector3.up,new Vector3(1.5f,2,1.5f))); volume.size=new Vector3(1.4f,.5f,1.4f); volume.center=new Vector3(0,.25f,0);
        item.transform.localPosition=Vector3.zero; slot.transform.localPosition=Vector3.zero; slot.SetProgramVariable("scaleFactor",1f); Physics.SyncTransforms();
        Grab(); Request(Quaternion.Euler(0,90,0),1); Hold(home,30);
        Assert(Flag(grip,"PoseLimited") && !Flag(grip,"ReadyToPlace"),"Impossible sampled intermediate stops pose and cannot manufacture dock intent"); Inside(); grip.SendCustomEvent("CancelImmediately");
        grip.SetProgramVariable("followRate",10000f); Grab(); Request(Quaternion.Euler(0,90,0),1); Hold(home,1);
        Near(Quaternion.Angle(item.transform.localRotation,Quaternion.Euler(0,90,0)),0,.01f,"Coarse sample reaches fitting endpoint");
        grip.SendCustomEvent("Cancel"); Step(.125f); Assert(Ref(grip,"ActiveTarget")==null && Quaternion.Angle(item.transform.localRotation,Quaternion.identity)<.01f,"Impossible midpoint during return restores committed pose"); Inside();
        grip.SetProgramVariable("followRate",18f);
        // Final pose must fit even when the requested/displayed size is within capture tolerance.
        region.SetProgramVariable("localBounds",new Bounds(Vector3.up,new Vector3(1.8f,2,1.8f))); volume.size=new Vector3(1,.5f,1); slot.SetProgramVariable("scaleFactor",1.9f);
        Grab(); Request(Quaternion.Euler(0,90,0),1.79f); Hold(home,70); Assert(!Flag(grip,"ReadyToPlace"),"Exact dock shape cannot exceed workspace");
        grip.SendCustomEvent("CancelImmediately"); region.SetProgramVariable("localBounds",bounds); volume.center=center; volume.size=size; slot.transform.localPosition=originalSlot; slot.SetProgramVariable("scaleFactor",1.25f); Reset();
    }
    static void Rates()
    {
        var rows=new List<string>{"hz,angle_degrees,reference_factor"}; float[] angles=new float[3],factors=new float[3]; int index=0;
        grip.SetProgramVariable("followRate",10f);
        foreach(int hz in new[]{30,72,120})
        {
            Reset(); Grab(); Request(Quaternion.Euler(0,90,0),1.8f);
            for(int n=0;n<hz/2;n++) { Feed(anchor,true); Step(1f/hz); }
            angles[index]=Quaternion.Angle(Quaternion.identity,item.transform.localRotation); factors[index]=(float)grip.GetProgramVariable("CurrentScaleFactor");
            rows.Add(hz+","+angles[index].ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+factors[index].ToString("R",System.Globalization.CultureInfo.InvariantCulture)); index++;
        }
        Near(angles[0],angles[2],.002f,"30/120 Hz constant-request rotation agreement"); Near(angles[1],angles[2],.002f,"72/120 Hz rotation agreement");
        Near(factors[0],factors[2],.000002f,"30/120 Hz log-scale agreement"); Near(factors[1],factors[2],.000002f,"72/120 Hz log-scale agreement");
        File.WriteAllLines("../Validation/UdonPose/pose-rates.csv",rows); grip.SendCustomEvent("CancelImmediately"); grip.SetProgramVariable("followRate",18f);
    }
    static void Select(string name) { item=Vm<BirdObjectTarget>("Pose "+name+" Piece"); region=Vm<BirdObjectRegion>("Pose "+name); slot=Vm<BirdObjectSnapTarget>("Pose "+name+" Dock 1"); }
    static void Reset()
    {
        grip.SendCustomEvent("CancelImmediately"); item.transform.localPosition=new Vector3(-1.35f,0,0); item.transform.localRotation=Quaternion.identity;
        item.transform.localScale=(Vector3)item.GetProgramVariable("poseReferenceScale"); left.SendCustomEvent("Cancel"); left.SetProgramVariable("uiConsumed",false);
        Physics.SyncTransforms(); grip.SendCustomEvent("Initialize");
    }
    static void Grab()
    {
        anchor=Volume().transform.TransformPoint(Volume().center); home=item.transform.position;
        Feed(anchor); Step(); Feed(anchor,true); Step(); Assert(Ref(grip,"ActiveTarget")==item,"Acquire selected pose item");
    }
    static void Request(Quaternion turn,float factor,bool expected=true)
    {
        Quaternion before=item.transform.localRotation; Vector3 size=item.transform.localScale;
        grip.SetProgramVariable("poseRequestRotation",turn); grip.SetProgramVariable("poseRequestFactor",factor); grip.SendCustomEvent("RequestPose");
        Assert(Flag(grip,"poseRequestAccepted")==expected,"Request acceptance: "+factor);
        Assert(item.transform.localRotation==before && item.transform.localScale==size,"Hypothetical pose request does not mutate transform");
    }
    static void Hold(Vector3 pivot,int count) { for(int n=0;n<count;n++) { Feed(anchor+pivot-home,true); Step(); Inside(); } }
    static void Release(Vector3 pivot) { Feed(anchor+pivot-home); Step(); Assert(Ref(grip,"ActiveTarget")==null,"Exact drop releases ownership"); }
    static void Exact(Vector3? reference=null)
    {
        Assert(Vector3.Distance(item.transform.position,slot.transform.position)<.002f && Quaternion.Angle(item.transform.rotation,slot.transform.rotation)<.02f,"Exact destination position/orientation");
        Assert((item.transform.localScale-(reference??Vector3.one)*1.25f).magnitude<.0001f,"Exact destination reference size"); Inside();
    }
    static BoxCollider Volume() { return (BoxCollider)item.GetProgramVariable("volume"); }
    static void Inside()
    {
        var bounds=(Bounds)region.GetProgramVariable("localBounds"); bounds.Expand(.0003f); var box=Volume();
        for(int i=0;i<8;i++) Assert(bounds.Contains(region.transform.InverseTransformPoint(box.transform.TransformPoint(box.center+Vector3.Scale(box.size*.5f,Sign(i))))),"Whole configured box inside workspace");
    }
    static Vector3 Sign(int i) { return new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1); }
    static void Feed(Vector3 point,bool pressed=false) { left.SetProgramVariable("sampleOrigin",Origin); left.SetProgramVariable("samplePosition",point); left.SetProgramVariable("sampleTracked",true); left.SetProgramVariable("samplePressed",pressed); left.SendCustomEvent("Submit"); }
    static void Step(float dt=1f/72) { grip.SetProgramVariable("stepDelta",dt); grip.SendCustomEvent("Process"); }
    static UdonBehaviour Vm<T>(string name) where T:UdonSharpBehaviour { foreach(var proxy in Resources.FindObjectsOfTypeAll<T>()) if(proxy.gameObject.scene.IsValid() && proxy.name==name) return UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy); throw new Exception("Missing "+name); }
    static UdonBehaviour Ref(UdonBehaviour vm,string key) { return (UdonBehaviour)vm.GetProgramVariable(key); }
    static bool Flag(UdonBehaviour vm,string key) { return (bool)vm.GetProgramVariable(key); }
    static void Assert(bool ok,string message) { checks++; if(!ok) throw new Exception("Assertion "+checks+": "+message); }
    static void Near(float a,float b,float tolerance,string message) { Assert(Mathf.Abs(a-b)<=tolerance,message+": "+a+" / "+b); }
    static void Finish(bool ok,string text) { SessionState.SetBool(Active,false); File.WriteAllText("udon-pose-result.txt",(ok?"PASS: ":"FAIL: ")+text); EditorApplication.Exit(ok?0:1); }
    static void Capture(string name)
    {
        Directory.CreateDirectory("../Validation/UdonPose"); var layers=new Dictionary<Transform,int>();
        foreach(string rootName in new[]{"Hanoi environment","Pose environment","Pose Table","Pose Buildings","Hanoi Table","Hanoi Buildings"})
            foreach(var child in GameObject.Find(rootName).GetComponentsInChildren<Transform>(true)) { layers[child]=child.gameObject.layer; child.gameObject.layer=30; }
        var camera=new GameObject("Pose evidence camera").AddComponent<Camera>(); camera.transform.position=Origin;
        camera.transform.LookAt(new Vector3(2,1.4f,3)); camera.fieldOfView=70; camera.nearClipPlane=.05f; camera.farClipPlane=2500;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.08f,.15f,.22f); camera.cullingMask=1<<30;
        var texture=new RenderTexture(1600,1100,24) { antiAliasing=4 }; var pixels=new Texture2D(1600,1100,TextureFormat.RGB24,false);
        camera.targetTexture=texture; camera.Render(); RenderTexture.active=texture; pixels.ReadPixels(new Rect(0,0,1600,1100),0,0); pixels.Apply(); File.WriteAllBytes("../Validation/UdonPose/"+name+".png",pixels.EncodeToPNG());
        RenderTexture.active=null; camera.targetTexture=null; texture.Release(); Destroy(camera.gameObject); Destroy(texture); Destroy(pixels);
        foreach(var pair in layers) pair.Key.gameObject.layer=pair.Value;
    }
    public static void Generate()
    {
        if(File.Exists(ScenePath)) throw new Exception("Refusing to overwrite authored pose scene."); Compile();
        var scene=EditorSceneManager.OpenScene("Assets/BirdWorld/Scenes/BirdMapDemo.unity");
        var controller=GameObject.Find("Hanoi Grip").GetComponent<BirdObjectGrip>();
        var command=new GameObject("Pose Controls").AddUdonSharpComponent<BirdObjectPoseControls>(); command.grip=controller; command.desktopInput=true;
        var environment=new GameObject("Pose environment").transform;
        Box("Pose table",environment,new Vector3(2,.7f,2.4f),new Vector3(1.7f,.16f,1),Mat("PoseTable",new Color(.24f,.3f,.33f)));
        Box("Pose foundation",environment,new Vector3(400,-18,500),new Vector3(410,12,240),Mat("PoseFoundation",new Color(.2f,.28f,.3f)));
        var targets=new List<BirdObjectTarget>(controller.targets);
        targets.Add(Station("Table",new Vector3(2,.78f,2.4f),.3f,controller)); targets.Add(Station("Buildings",new Vector3(400,-12,500),75,controller)); controller.targets=targets.ToArray();
        var protectedRegions=new List<BirdObjectRegion>(controller.playArea.protectedRegions); protectedRegions.Add(targets[targets.Count-1].region); controller.playArea.protectedRegions=protectedRegions.ToArray();
        command.status=Label("Pose instructions",environment,new Vector3(3.8f,1.4f,2.7f),"MATCH THE OUTLINE",.0024f,640,530);
        foreach(var proxy in FindObjectsOfType<UdonSharpBehaviour>(true)) UdonSharpEditorUtility.CopyProxyToUdon(proxy);
        Physics.SyncTransforms(); if(!EditorSceneManager.SaveScene(scene,ScenePath)) throw new Exception("Scene save failed"); AssetDatabase.SaveAssets();
        File.WriteAllText("udon-pose-generate-result.txt","PASS: authored paired pose stations, shared grip, protected distant workspace, prior map/menu/Hanoi retained."); EditorApplication.Exit(0);
    }
    static BirdObjectTarget Station(string name,Vector3 position,float unit,BirdObjectGrip controller)
    {
        var root=new GameObject("Pose "+name).transform; root.position=position; root.localScale=Vector3.one*unit;
        var workspace=root.gameObject.AddUdonSharpComponent<BirdObjectRegion>(); workspace.localBounds=new Bounds(new Vector3(0,1.6f,0),new Vector3(5,3.2f,3));
        var slots=new BirdObjectSnapTarget[2];
        for(int i=0;i<2;i++)
        {
            var dock=new GameObject("Pose "+name+" Dock "+i).transform; dock.SetParent(root,false); dock.localPosition=new Vector3(i==0?-1.35f:1.3f,0,0); dock.localRotation=Quaternion.Euler(0,i*90,0);
            var target=dock.gameObject.AddUdonSharpComponent<BirdObjectSnapTarget>(); slots[i]=target; target.matchRotation=target.matchScale=true;
            target.scaleFactor=i==0?1:1.25f; target.captureRadius=.2f; target.influenceRadius=.55f; target.approachLength=1.4f;
            Box("Pose pad",root,dock.localPosition+Vector3.down*.03f,new Vector3(1.3f,.06f,1.3f),Mat("PosePad",new Color(.3f,.39f,.44f)));
            Vector3 center=new Vector3(0,.46f,0)*target.scaleFactor,half=new Vector3(.95f,.92f,.7f)*(.5f*target.scaleFactor);
            for(int a=0;a<8;a++) for(int bit=0;bit<3;bit++)
            {
                int b=a^(1<<bit); if(b<a) continue;
                var line=new GameObject("Required pose outline").AddComponent<LineRenderer>(); line.transform.SetParent(dock,false); line.useWorldSpace=false; line.positionCount=2;
                line.SetPosition(0,center+Vector3.Scale(half,Sign(a))); line.SetPosition(1,center+Vector3.Scale(half,Sign(b))); line.widthMultiplier=.012f*unit; line.sharedMaterial=Mat("PoseOutline",new Color(.25f,.88f,.72f));
            }
            Label("Pose dock label",root,dock.localPosition+new Vector3(0,.08f,-.85f),i==0?"0 deg / 1x":"90 deg / 1.25x",.0015f,1100,100);
        }
        var piece=new GameObject("Pose "+name+" Piece").transform; piece.SetParent(root,false); piece.localPosition=slots[0].transform.localPosition;
        var shape=new GameObject("Pose "+name+" Volume").transform; shape.SetParent(piece,false);
        var volume=shape.gameObject.AddComponent<BoxCollider>(); volume.isTrigger=true; volume.center=new Vector3(0,.46f,0); volume.size=new Vector3(.95f,.92f,.7f);
        var item=piece.gameObject.AddUdonSharpComponent<BirdObjectTarget>(); item.volume=volume; item.region=workspace; item.destinations=slots; item.allowRotation=item.allowScaling=true;
        var body=Box("Pose body",piece,new Vector3(0,.4f,0),new Vector3(.8f,.8f,.55f),Mat("PoseBody",new Color(.16f,.35f,.35f)));
        Box("Pose roof",piece,new Vector3(0,.86f,0),new Vector3(.95f,.12f,.7f),Mat("PoseRoof",new Color(.84f,.5f,.2f)));
        for(int row=0;row<6;row++) for(int col=0;col<4;col++) Box("Pose window",piece,new Vector3((col-1.5f)*.16f,.08f+row*.12f,-.282f),new Vector3(.08f,.06f,.008f),Mat("PoseWindow",new Color(.06f,.16f,.2f)));
        var feedback=piece.gameObject.AddUdonSharpComponent<BirdObjectFeedback>(); feedback.grip=controller; feedback.target=item; feedback.visual=body.GetComponent<Renderer>(); return item;
    }
    static GameObject Box(string name,Transform parent,Vector3 position,Vector3 scale,Material material)
    {
        var result=GameObject.CreatePrimitive(PrimitiveType.Cube); result.name=name; result.transform.SetParent(parent,false); result.transform.localPosition=position; result.transform.localScale=scale;
        DestroyImmediate(result.GetComponent<Collider>()); result.GetComponent<Renderer>().sharedMaterial=material; return result;
    }
    static Material Mat(string name,Color color)
    {
        string path="Assets/BirdWorld/Materials/"+name+".mat"; var value=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(value==null) { value=new Material(Shader.Find("Bird/Samples/Hanoi Surface")) { color=color }; AssetDatabase.CreateAsset(value,path); } return value;
    }
    static Text Label(string name,Transform parent,Vector3 position,string text,float scale,float width,float height)
    {
        var go=new GameObject(name); go.transform.SetParent(parent,false); go.transform.localPosition=position; go.transform.localScale=Vector3.one*scale;
        var canvas=go.AddComponent<Canvas>(); canvas.renderMode=RenderMode.WorldSpace; var rt=go.GetComponent<RectTransform>(); rt.sizeDelta=new Vector2(width,height);
        var child=new GameObject("Text"); child.transform.SetParent(go.transform,false); var label=child.AddComponent<Text>(); label.rectTransform.sizeDelta=rt.sizeDelta;
        label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize=30; label.alignment=TextAnchor.MiddleCenter; label.color=Color.white; label.text=text; return label;
    }
    static void Compile()
    {
        string name="BirdObjectPoseControls", path="Assets/BirdWorld/Programs/"+name+".asset";
        var source=AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/BirdGenerated/Runtime/"+name+".cs"); if(source==null) throw new Exception("Missing source "+name);
        var program=AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path);
        if(program==null) { program=ScriptableObject.CreateInstance<UdonSharpProgramAsset>(); program.sourceCsScript=source; AssetDatabase.CreateAsset(program,path); }
        else if(program.sourceCsScript!=source) throw new Exception("Mismatched program source "+name);
        AssetDatabase.SaveAssets(); UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync(); if(UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) throw new Exception("Udon compile failure");
    }
    public static void RefineLayout()
    {
        var scene=EditorSceneManager.OpenScene(ScenePath);
        var text=GameObject.Find("Pose instructions").transform; text.localPosition=new Vector3(3.8f,1.4f,2.7f); text.localScale=Vector3.one*.0024f;
        foreach(var label in FindObjectsOfType<Canvas>()) if(label.name=="Pose dock label")
        { var position=label.transform.localPosition; position.y=.08f; label.transform.localPosition=position; label.transform.localScale=Vector3.one*.0015f; }
        EditorSceneManager.SaveScene(scene); File.WriteAllText("udon-pose-layout-result.txt","PASS: pose command text enlarged and dock labels moved above surfaces."); EditorApplication.Exit(0);
    }
}
#endif
