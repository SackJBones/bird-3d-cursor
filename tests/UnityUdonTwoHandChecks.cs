#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UdonSharp;
using UdonSharpEditor;
using VRC.SDKBase;
using VRC.Udon;

// Author proxies in edit mode; all interaction assertions execute compiled backing VMs.
public class UnityUdonTwoHandChecks : MonoBehaviour
{
    const string ScenePath="Assets/BirdWorld/Scenes/BirdPoseDemo.unity", Active="Bird.Udon.TwoHand.Checks";
    static int checks;
    static double deadline;
    Fixture f;
    int stage, phase;
    float started;
    Quaternion releasedTurn;
    Vector3 releasedScale;
    public static void Run()
    {
        File.WriteAllText("udon-two-hand-result.txt","PENDING"); Compile(); EditorSceneManager.OpenScene(ScenePath);
        SessionState.SetBool(Active,true); EditorApplication.isPlaying=true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Begin() { if(SessionState.GetBool(Active,false)) new GameObject("Two hand VM checks").AddComponent<UnityUdonTwoHandChecks>(); }
    void Update()
    {
        if(!SessionState.GetBool(Active,false)) return;
        if(deadline==0) deadline=EditorApplication.timeSinceStartup+150;
        if(EditorApplication.timeSinceStartup>deadline) { Finish(false,"ClientSim/frame sequence timeout"); return; }
        if(!Utilities.IsValid(Networking.LocalPlayer) || Time.timeSinceLevelLoad<2) return;
        try
        {
            if(!Frames()) return;
            Contracts(); Finish(true,checks+" compiled-Udon two-hand assertions; normal LateUpdate and PostLateUpdate pickup, clutch, turn/size, secondary release and exact dock; cancellation, bounds, ownership, lifecycle and cadence contracts. Synthetic hand origins, not physical click, client or multiplayer validation.");
        }
        catch(Exception e) { Finish(false,e.ToString()); }
    }
    bool Frames()
    {
        if(stage==0)
        {
            f=new Fixture(phase==1?"Buildings":"Table"); f.SetPhase(phase==1);
            Set(f.grip,"automatic",true); Set(f.gesture,"automatic",true);
            f.Feed(false); stage++; return false;
        }
        if(stage==1) { f.Feed(); stage++; return false; }
        if(stage==2)
        {
            Assert(Ref(f.grip,"ActiveTarget")==f.item,"Normal phase acquires primary "+phase);
            f.secondaryPressed=true; f.Feed(); stage++; return false;
        }
        if(stage==3)
        {
            Assert(Flag(f.gesture,"IsEngaged"),"Normal phase accepts second-hand press "+phase);
            f.span=Quaternion.Euler(0,90,0)*Vector3.right*.5f;
            f.point=f.anchor+f.slot.transform.position-f.home; started=Time.unscaledTime; stage++;
        }
        if(stage==4)
        {
            f.Feed(); f.Inside(); if(Time.unscaledTime-started<.8f) return false;
            Assert(Flag(f.grip,"ReadyToPlace"),"Normal two-hand pose matches exact dock "+phase);
            f.secondaryPressed=false; f.Feed(); stage++; return false;
        }
        if(stage==5)
        {
            Assert(!Flag(f.gesture,"IsEngaged"),"Normal secondary release disengages "+phase);
            releasedTurn=f.item.transform.localRotation; releasedScale=f.item.transform.localScale;
            f.span=Vector3.up*.7f; f.Feed(); stage++; return false;
        }
        if(stage==6)
        {
            Assert(f.item.transform.localRotation==releasedTurn && f.item.transform.localScale==releasedScale,"Normal release freezes displayed pose "+phase);
            Assert(Flag(f.grip,"ReadyToPlace"),"Secondary release retains valid dock: limited="+Flag(f.grip,"PoseLimited")+", requested="+Get<float>(f.grip,"RequestedScaleFactor")+", candidate="+Ref(f.grip,"Candidate"));
            f.Feed(false); stage++; return false;
        }
        if(stage==7)
        {
            Assert(Ref(f.grip,"ActiveTarget")==null,"Normal final drop releases ownership "+phase+"; returning="+Flag(f.grip,"IsReturning")+", limited="+Flag(f.grip,"PoseLimited"));
            Near(Vector3.Distance(f.item.transform.position,f.slot.transform.position),0,.002f,"Normal exact dock position");
            Near(Quaternion.Angle(f.item.transform.localRotation,Quaternion.Euler(0,90,0)),0,.02f,"Normal exact dock orientation");
            Near(f.item.transform.localScale.x,1.25f,.00001f,"Normal exact dock scale");
            // Synchronous GPU readback/PNG writing can exceed the production 250 ms
            // stale-input cutoff. Capture only after the transaction has finished.
            Capture(phase==0?"table-two-hand-placed":"building-two-hand-placed");
            f.Dispose(); if(++phase<2) { stage=0; return false; }
            return true;
        }
        return false;
    }
    static void Contracts()
    {
        using(var f=new Fixture())
        {
            f.Pick(); f.Engage(); Near(Quaternion.Angle(f.item.transform.localRotation,Quaternion.identity),0,.001f,"No jump at clutch");
            f.span=Quaternion.Euler(0,90,0)*Vector3.right*.5f;
            for(int i=0;i<70;i++) f.Sample();
            Near(Quaternion.Angle(f.item.transform.localRotation,Quaternion.Euler(0,90,0)),0,.03f,"Quarter turn"); Near(Get<float>(f.grip,"CurrentScaleFactor"),1.25f,.00001f,"Hand separation controls size");
            f.secondaryPressed=false; f.Sample(); var turn=f.item.transform.localRotation; var size=f.item.transform.localScale;
            f.span=Vector3.up*.7f; for(int i=0;i<25;i++) f.Sample();
            Assert(!Flag(f.gesture,"IsEngaged") && Ref(f.grip,"ActiveTarget")==f.item && f.item.transform.localRotation==turn && f.item.transform.localScale==size,"Secondary release freezes while primary translates");
            f.Engage(); f.Sample(); Near(Quaternion.Angle(turn,f.item.transform.localRotation),0,.001f,"Re-clutch rotation has no jump"); Near(Get<float>(f.grip,"CurrentScaleFactor"),1.25f,.00001f,"Re-clutch uses displayed size");
            f.gesture.SendCustomEvent("Cancel"); f.Sample(); Assert(!Flag(f.gesture,"IsEngaged"),"Cancel consumes held edge");
            f.Engage(); Set(f.grip,"followRate",2f); f.span=Vector3.right*.8f; f.Sample(); f.secondaryPressed=false; f.Sample(); turn=f.item.transform.localRotation; size=f.item.transform.localScale;
            for(int i=0;i<30;i++) f.Sample(); Assert(turn==f.item.transform.localRotation && size==f.item.transform.localScale,"Release stops pose lag immediately");
        }
        using(var f=new Fixture())
        {
            f.secondaryPressed=true; f.Pick(); f.Sample(); Assert(!Flag(f.gesture,"IsEngaged"),"Already held secondary cannot join pickup");
            f.Engage(); f.span=Vector3.right*1.2f; f.Sample(); Assert(Flag(f.grip,"PoseLimited") && Flag(f.gesture,"IsEngaged"),"Rejected size permits adjustment but blocks stale drop");
            f.secondaryPressed=false; f.Sample(); Assert(Flag(f.grip,"PoseLimited"),"Release preserves rejected intent");
            f.span=Vector3.right*.4f; f.Engage(); Assert(!Flag(f.grip,"PoseLimited"),"Fresh valid clutch restores eligibility");
            f.span=Vector3.zero; f.Sample(); Assert(!Flag(f.gesture,"IsEngaged") && Flag(f.grip,"PoseLimited"),"Coincident roots end clutch");
            f.span=Vector3.right*.4f; f.Engage(); f.span=Vector3.left*.4f; f.Sample(); Assert(!Flag(f.gesture,"IsEngaged") && Flag(f.grip,"PoseLimited"),"Antipodal span cannot flip");
            f.span=Vector3.right*.4f; f.Sample(); Assert(!Flag(f.gesture,"IsEngaged"),"Held recovery cannot reacquire");
        }
        using(var f=new Fixture())
        {
            f.Pick(); Set(f.right,"userId","OtherUser"); f.secondaryPressed=true; f.Sample(); Assert(!Flag(f.gesture,"IsEngaged"),"Different user cannot join"); Set(f.right,"userId","LocalUser");
            f.Engage(); f.right.SendCustomEvent("Cancel"); f.Step(); Assert(!Flag(f.gesture,"IsEngaged") && Ref(f.grip,"ActiveTarget")==f.item && Flag(f.grip,"PoseLimited"),"Secondary loss freezes and blocks stale placement");
            f.Sample(); Assert(!Flag(f.gesture,"IsEngaged"),"Recovery while pressed needs new edge");
            f.Engage(); f.left.SendCustomEvent("Cancel"); f.Step(); Assert(Flag(f.grip,"IsReturning"),"Primary loss starts rollback");
            for(int i=0;i<30;i++) f.Step(); Assert(Ref(f.grip,"ActiveTarget")==null,"Primary loss completes return");
        }
        using(var f=new Fixture())
        {
            f.Pick(); Submit(f.right,f.origin+f.span,Vector3.one*100,false); f.Step(); Submit(f.right,f.origin+f.span,Vector3.one*100,true); f.Step(); Assert(!Flag(f.gesture,"IsEngaged"),"Secondary must point through held volume");
            f.Engage(); Submit(f.right,f.origin+Vector3.forward*.5f,f.point,true); f.Step(); Near(Get<float>(f.grip,"RequestedScaleFactor"),1,.00001f,"Unpaired sample waits");
            Submit(f.left,f.origin,f.point,true); f.Step(); Near(Get<float>(f.grip,"RequestedScaleFactor"),1.25f,.00001f,"Other fresh sample completes pair");
            for(int i=0;i<25;i++) { Submit(f.left,f.origin,f.point,true); f.Step(); }
            Assert(!Flag(f.gesture,"IsEngaged") && Ref(f.grip,"ActiveTarget")==f.item && Flag(f.grip,"PoseLimited"),"Stale secondary ends clutch without dropping primary");
            f.Engage(); f.gesture.enabled=false; var turn=f.item.transform.localRotation; f.Sample(); Assert(!Flag(f.gesture,"IsEngaged") && f.item.transform.localRotation==turn,"Disable freezes clutch");
            f.gesture.enabled=true; f.Sample(); Assert(!Flag(f.gesture,"IsEngaged"),"Enable cannot replay held edge");
            f.Engage(); Request(f,Quaternion.Euler(0,25,0),1.1f); f.Sample(); Assert(!Flag(f.gesture,"IsEngaged") && Mathf.Abs(Get<float>(f.grip,"RequestedScaleFactor")-1.1f)<1e-6f,"Explicit pose command takes over");
            Set(f.gesture,"eventTarget",f.gesture); Set(f.gesture,"engagedEvent","Cancel");
            f.secondaryPressed=false; f.Sample(); f.secondaryPressed=true; f.Sample(); Assert(!Flag(f.gesture,"IsEngaged"),"Engaged event can cancel reentrantly");
        }
        foreach(string cause in new[]{"secondary-ui","primary-ui","menu","area","rebind","owner","gap","parent","mode"}) using(var f=new Fixture())
        {
            f.Pick(); f.Engage(); f.Feed();
            var parent=f.item.transform.parent; var oldPosition=parent.position;
            if(cause=="secondary-ui") Set(f.right,"uiConsumed",true);
            if(cause=="primary-ui") Set(f.left,"uiConsumed",true);
            if(cause=="menu") Set(Vm<BirdUiPanel>("UI Root Panel"),"state",1);
            if(cause=="area") Set(f.gate,"allowed",false);
            if(cause=="rebind") Set(f.gesture,"second",null);
            if(cause=="owner") Set(f.right,"userId","OtherUser");
            if(cause=="parent") parent.position+=Vector3.up*.01f;
            if(cause=="mode") f.gesture.gameObject.SetActive(false);
            f.Step(cause=="gap"?.3f:1f/72);
            Assert(!Flag(f.gesture,"IsEngaged"),"Ownership discontinuity ends clutch: "+cause);
            if(cause=="secondary-ui") Assert(Flag(f.grip,"PoseLimited") && Ref(f.grip,"ActiveTarget")==f.item,"UI-owned secondary cannot drop or keep driving target");
            parent.position=oldPosition; if(cause=="mode") f.gesture.gameObject.SetActive(true);
            f.Sample(); Assert(!Flag(f.gesture,"IsEngaged"),"Discontinuity cannot replay held press: "+cause);
        }
        foreach(string cause in new[]{"consumed","unregistered","same-hand","outside","phase"}) using(var f=new Fixture())
        {
            f.Pick(); f.secondaryPressed=true; f.Feed();
            if(cause=="consumed") Set(f.right,"uiConsumed",true);
            if(cause=="unregistered") Set(f.grip,"pointers",new[]{f.left});
            if(cause=="same-hand") Set(f.gesture,"second",f.left);
            if(cause=="outside") Submit(f.right,f.origin+f.span,Vector3.one*100,true);
            if(cause=="phase") { Set(f.grip,"automatic",true); Set(f.gesture,"automatic",true); Set(f.grip,"postLateUpdate",true); }
            f.Step(); Assert(!Flag(f.gesture,"IsEngaged"),"Invalid secondary cannot acquire: "+cause);
            f.Sample(); Assert(!Flag(f.gesture,"IsEngaged"),"Held invalid attempt is not replayed: "+cause);
        }
        using(var f=new Fixture())
        {
            f.Pick(); f.Engage(); Set(f.grip,"followRate",1f); f.span=Quaternion.Euler(0,90,0)*Vector3.right*.4f; f.Sample();
            Assert(Quaternion.Angle(f.item.transform.localRotation,Quaternion.identity)<12 && !Flag(f.grip,"ReadyToPlace"),"Displayed home pose cannot conceal contrary gesture intent");
            Submit(f.left,f.origin,f.point,false); f.right.SendCustomEvent("Cancel"); f.Step(); Assert(Flag(f.grip,"IsReturning"),"Secondary loss on primary release cannot manufacture a matching drop");
        }
        using(var f=new Fixture())
        {
            f.Pick(); Submit(f.right,f.origin+f.span,f.point,false); f.Step();
            var far=f.origin+f.span+(f.point-f.origin-f.span).normalized*1e6f;
            Submit(f.right,f.origin+f.span,far,true); f.Step(); Assert(Flag(f.gesture,"IsEngaged"),"Million-metre logical ray can join"); Assert(Get<Vector3>(f.right,"position")==far,"Gesture never rewrites logical Bird point");
        }
        using(var f=new Fixture())
        {
            f.Sample(false); f.secondaryPressed=true; f.Sample(false); Assert(Ref(f.grip,"ActivePointer")==f.right,"Either hand can be primary");
            f.Sample(); Assert(Ref(f.gesture,"ActiveSecondary")==f.left,"Opposite hand joins");
            f.span=Quaternion.Euler(0,90,0)*Vector3.right*.4f;
            for(int i=0;i<70;i++) f.Sample(); Near(Quaternion.Angle(f.item.transform.localRotation,Quaternion.Euler(0,90,0)),0,.03f,"Reversed roles preserve turn direction");
        }
        foreach(bool turnOnly in new[]{true,false}) using(var f=new Fixture())
        {
            Set(f.item,"allowRotation",turnOnly); Set(f.item,"allowScaling",!turnOnly); f.Pick(); f.Engage(); f.span=Quaternion.Euler(0,90,0)*Vector3.right*.5f;
            for(int i=0;i<70;i++) f.Sample();
            Near(Quaternion.Angle(f.item.transform.localRotation,turnOnly?Quaternion.Euler(0,90,0):Quaternion.identity),0,.03f,"Rotation permission");
            Near(Get<float>(f.grip,"CurrentScaleFactor"),turnOnly?1:1.25f,.00001f,"Scale permission"); Assert(!Flag(f.grip,"PoseLimited"),"Disabled axis does not reject permitted axis");
        }
        foreach(float dt in new[]{0f,-1f,float.NaN,float.PositiveInfinity,.26f}) using(var f=new Fixture())
        {
            f.Pick(); f.Engage(); f.Sample(true,dt); Assert(!Flag(f.gesture,"IsEngaged"),"Invalid step stops clutch");
        }
        using(var f=new Fixture())
        {
            f.Pick(); f.Engage(); Set(f.gesture,"maximumTurnDegrees",180f); f.Sample(); Assert(!Flag(f.gesture,"IsEngaged") && Flag(f.grip,"PoseLimited"),"Unsafe settings fail closed");
        }
        using(var f=new Fixture())
        {
            f.Pick(); Set(f.left,"revision",int.MaxValue); Set(f.right,"revision",int.MaxValue); f.Step();
            f.Engage(); Assert(Flag(f.gesture,"IsEngaged"),"Revision wrap preserves fresh press");
        }
        foreach(bool mirrored in new[]{false,true}) using(var f=new Fixture())
        {
            var frame=f.region.transform; var oldRotation=frame.localRotation; var oldScale=frame.localScale;
            frame.localRotation=Quaternion.Euler(20,35,-15); frame.localScale=Vector3.Scale(oldScale,new Vector3(mirrored?-1:1,1.5f,.7f));
            f.point=f.box.transform.TransformPoint(f.box.center); f.Pick(); f.Engage();
            Quaternion worldTurn=Quaternion.Euler(0,70,0); f.span=worldTurn*Vector3.right*.5f;
            for(int i=0;i<70;i++) f.Sample();
            Quaternion expected=Quaternion.Inverse(frame.localRotation)*worldTurn*frame.localRotation;
            Near(Quaternion.Angle(f.item.transform.localRotation,expected),0,.03f,"Authored frame handles nonuniform/mirrored parent "+mirrored);
            Near(Get<float>(f.grip,"CurrentScaleFactor"),1.25f,.00001f,"World hand span remains metric under parent scale");
            f.Dispose(); frame.localRotation=oldRotation; frame.localScale=oldScale;
        }
        using(var f=new Fixture("Buildings"))
        {
            f.Pick(); f.Engage(); f.span=Quaternion.Euler(0,90,0)*Vector3.right*.5f; f.point=f.origin;
            for(int i=0;i<60;i++) f.Sample();
            Assert(f.box.bounds.min.z>300,"Hand closure cannot bring any building corner into the viewing area");
            f.Sample(false); Assert(Flag(f.grip,"IsReturning"),"Out-of-dock release cancels whole pose");
            for(int i=0;i<30;i++) f.Step();
            Near(Vector3.Distance(f.item.transform.position,f.home),0,.002f,"Cancelled building returns home");
            Near(Quaternion.Angle(f.item.transform.localRotation,Quaternion.identity),0,.001f,"Cancelled building restores rotation");
            Near(f.item.transform.localScale.x,1,.00001f,"Cancelled building restores size");
            Capture("building-closure-cancelled");
        }
        using(var f=new Fixture())
        {
            f.SetPhase(true); f.Pick(); f.Engage(); f.span=Quaternion.Euler(0,90,0)*Vector3.right*.5f; f.Feed();
            Set(f.grip,"automatic",true); Set(f.gesture,"automatic",true);
            f.gesture.SendCustomEvent("PostLateUpdate"); f.grip.SendCustomEvent("PostLateUpdate");
            var turn=f.item.transform.localRotation; var size=f.item.transform.localScale;
            for(int i=0;i<4;i++) { f.gesture.SendCustomEvent("PostLateUpdate"); f.grip.SendCustomEvent("PostLateUpdate"); }
            Assert(f.item.transform.localRotation==turn && f.item.transform.localScale==size,"Repeated automatic dispatch in one frame integrates once");
            f.gesture.enabled=false; f.grip.enabled=false;
            f.gesture.SendCustomEvent("PostLateUpdate"); f.grip.SendCustomEvent("PostLateUpdate");
            Assert(!Flag(f.gesture,"IsEngaged") && Ref(f.grip,"ActiveTarget")==null,"Queued disabled callbacks cannot revive gesture/grip");
        }
        Rates();
    }
    static void Request(Fixture f,Quaternion rotation,float factor)
    {
        Set(f.grip,"poseRequestRotation",rotation); Set(f.grip,"poseRequestFactor",factor); f.grip.SendCustomEvent("RequestPose"); Assert(Flag(f.grip,"poseRequestAccepted"),"Explicit pose request accepted");
    }
    static void Rates()
    {
        var rows=new List<string>{"hz,angle_degrees,reference_factor"}; float angle=0,factor=0;
        foreach(int hz in new[]{30,72,120}) using(var f=new Fixture())
        {
            Set(f.grip,"followRate",10f); f.Pick(); f.Engage(); f.span=Quaternion.Euler(0,90,0)*Vector3.right*.6f;
            for(int i=0;i<hz/2;i++) f.Sample(true,1f/hz);
            float a=Quaternion.Angle(Quaternion.identity,f.item.transform.localRotation),b=Get<float>(f.grip,"CurrentScaleFactor");
            if(hz!=30) { Near(a,angle,.002f,"Constant request rotation cadence"); Near(b,factor,.000002f,"Constant request scale cadence"); }
            angle=a; factor=b; rows.Add(hz+","+a.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+b.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
        }
        Directory.CreateDirectory("../Validation/UdonTwoHand"); File.WriteAllLines("../Validation/UdonTwoHand/two-hand-rates.csv",rows);
    }

    sealed class Fixture : IDisposable
    {
        public UdonBehaviour left,right,grip,gesture,item,region,slot,router,gate;
        public BoxCollider box;
        public Vector3 origin,span=Vector3.right*.4f,point,anchor,home;
        public bool secondaryPressed;
        public Fixture(string name="Table")
        {
            left=Vm<BirdUiPointer>("UI Left"); right=Vm<BirdUiPointer>("UI Right"); grip=Vm<BirdObjectGrip>("Hanoi Grip");
            gesture=Vm<BirdObjectTwoHandPose>("Two Hand Pose"); router=Vm<BirdUiRouter>("UI Router"); gate=Vm<BirdObjectPlayArea>("Hanoi Play Area");
            Vm<BirdUiDesktopInput>("UI Desktop Input").enabled=false; Set(Vm<BirdObjectPoseControls>("Pose Controls"),"desktopInput",false);
            item=Vm<BirdObjectTarget>("Pose "+name+" Piece"); region=Vm<BirdObjectRegion>("Pose "+name); slot=Vm<BirdObjectSnapTarget>("Pose "+name+" Dock 1");
            grip.SendCustomEvent("CancelImmediately"); gesture.SendCustomEvent("Cancel");
            grip.enabled=gesture.enabled=true; Set(grip,"automatic",false); Set(gesture,"automatic",false); Set(router,"automatic",false); Set(gate,"automatic",false); Set(gate,"allowed",true);
            Set(grip,"postLateUpdate",false); Set(gesture,"postLateUpdate",false); Set(grip,"followRate",18f);
            Set(gesture,"minimumSpan",.06f); Set(gesture,"maximumSpan",3f); Set(gesture,"maximumSampleGap",.25f); Set(gesture,"maximumTurnDegrees",160f);
            Set(gesture,"eventTarget",null); Set(gesture,"engagedEvent",""); Set(gesture,"disengagedEvent","");
            Set(gesture,"first",left); Set(gesture,"second",right); Set(gesture,"grip",grip);
            Set(grip,"pointers",new[]{left,right}); Set(grip,"targets",new[]{item});
            foreach(var p in new[]{left,right}) { p.enabled=true; Set(p,"userId","LocalUser"); p.SendCustomEvent("Cancel"); }
            foreach(var panel in Resources.FindObjectsOfTypeAll<BirdUiPanel>()) if(panel.gameObject.scene.IsValid()) UdonSharpEditorUtility.GetBackingUdonBehaviour(panel).SendCustomEvent("Close");
            Set(item,"allowRotation",true); Set(item,"allowScaling",true); item.transform.localPosition=new Vector3(-1.35f,0,0); item.transform.localRotation=Quaternion.identity; item.transform.localScale=Vector3.one;
            box=(BoxCollider)item.GetProgramVariable("volume"); anchor=box.transform.TransformPoint(box.center); home=item.transform.position;
            point=anchor; origin=new Vector3(-.2f,1.65f,0); Physics.SyncTransforms();
            grip.SendCustomEvent("Initialize"); gesture.SendCustomEvent("Cancel");
        }
        public void SetPhase(bool value)
        {
            grip.enabled=false; gesture.enabled=false; Set(grip,"postLateUpdate",value); Set(gesture,"postLateUpdate",value); grip.enabled=true; gesture.enabled=true;
        }
        public void Feed(bool pressed=true)
        {
            Submit(left,origin,point,pressed); Submit(right,origin+span,box.transform.TransformPoint(box.center),secondaryPressed);
        }
        public void Sample(bool pressed=true,float dt=1f/72) { Feed(pressed); Step(dt); }
        public void Step(float dt=1f/72)
        {
            Physics.SyncTransforms(); Set(gesture,"stepDelta",dt); gesture.SendCustomEvent("Process"); Set(grip,"stepDelta",dt); grip.SendCustomEvent("Process"); Inside();
        }
        public void Pick() { Sample(false); Sample(); Assert(Ref(grip,"ActiveTarget")==item,"Primary acquisition"); }
        public void Engage() { secondaryPressed=false; Sample(); secondaryPressed=true; Sample(); Assert(Ref(gesture,"ActiveSecondary")==right,"Fresh secondary acquisition"); }
        public void Inside()
        {
            Bounds bounds=(Bounds)region.GetProgramVariable("localBounds"); bounds.Expand(.0003f);
            for(int i=0;i<8;i++)
            {
                var sign=new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1);
                Assert(bounds.Contains(region.transform.InverseTransformPoint(box.transform.TransformPoint(box.center+Vector3.Scale(box.size*.5f,sign)))),"Whole box remains in workspace");
            }
        }
        public void Dispose()
        {
            gesture.SendCustomEvent("Cancel"); grip.SendCustomEvent("CancelImmediately");
            Set(grip,"automatic",false); Set(gesture,"automatic",false);
        }
    }
    static void Submit(UdonBehaviour pointer,Vector3 origin,Vector3 point,bool pressed)
    {
        Set(pointer,"sampleOrigin",origin); Set(pointer,"samplePosition",point); Set(pointer,"sampleTracked",true); Set(pointer,"samplePressed",pressed); pointer.SendCustomEvent("Submit");
    }
    static void Set(UdonBehaviour vm,string key,object value) { vm.SetProgramVariable(key,value); }
    static T Get<T>(UdonBehaviour vm,string key) { return (T)vm.GetProgramVariable(key); }
    static UdonBehaviour Ref(UdonBehaviour vm,string key) { return Get<UdonBehaviour>(vm,key); }
    static bool Flag(UdonBehaviour vm,string key) { return Get<bool>(vm,key); }
    static UdonBehaviour Vm<T>(string name) where T:UdonSharpBehaviour
    {
        foreach(var proxy in Resources.FindObjectsOfTypeAll<T>()) if(proxy.gameObject.scene.IsValid() && proxy.name==name) return UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy);
        throw new Exception("Missing "+name);
    }
    static void Assert(bool ok,string message) { checks++; if(!ok) throw new Exception("Assertion "+checks+": "+message); }
    static void Near(float a,float b,float tolerance,string message) { Assert(Mathf.Abs(a-b)<=tolerance,message+": "+a+" / "+b); }
    static void Finish(bool ok,string text) { SessionState.SetBool(Active,false); File.WriteAllText("udon-two-hand-result.txt",(ok?"PASS: ":"FAIL: ")+text); EditorApplication.Exit(ok?0:1); }
    static void Capture(string name)
    {
        Directory.CreateDirectory("../Validation/UdonTwoHand"); var layers=new Dictionary<Transform,int>();
        foreach(string rootName in new[]{"Hanoi environment","Pose environment","Pose Table","Pose Buildings"})
            foreach(var child in GameObject.Find(rootName).GetComponentsInChildren<Transform>(true)) { layers[child]=child.gameObject.layer; child.gameObject.layer=30; }
        var camera=new GameObject("Two hand evidence camera").AddComponent<Camera>(); camera.transform.position=new Vector3(0,1.65f,0);
        camera.transform.LookAt(new Vector3(2,1.4f,3)); camera.fieldOfView=70; camera.nearClipPlane=.05f; camera.farClipPlane=2500;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.08f,.15f,.22f); camera.cullingMask=1<<30;
        var texture=new RenderTexture(1600,1100,24) { antiAliasing=4 }; var pixels=new Texture2D(1600,1100,TextureFormat.RGB24,false);
        camera.targetTexture=texture; camera.Render(); RenderTexture.active=texture; pixels.ReadPixels(new Rect(0,0,1600,1100),0,0); pixels.Apply(); File.WriteAllBytes("../Validation/UdonTwoHand/"+name+".png",pixels.EncodeToPNG());
        RenderTexture.active=null; camera.targetTexture=null; texture.Release(); Destroy(camera.gameObject); Destroy(texture); Destroy(pixels);
        foreach(var pair in layers) pair.Key.gameObject.layer=pair.Value;
    }
    static void Compile()
    {
        string name="BirdObjectTwoHandPose",path="Assets/BirdWorld/Programs/"+name+".asset";
        var source=AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/BirdGenerated/Runtime/"+name+".cs"); if(source==null) throw new Exception("Missing source "+name);
        var program=AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path);
        if(program==null) { program=ScriptableObject.CreateInstance<UdonSharpProgramAsset>(); program.sourceCsScript=source; AssetDatabase.CreateAsset(program,path); }
        else if(program.sourceCsScript!=source) throw new Exception("Mismatched program source");
        AssetDatabase.SaveAssets(); UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync(); if(UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) throw new Exception("Udon compile failure");
    }
    public static void Author()
    {
        Compile(); var scene=EditorSceneManager.OpenScene(ScenePath);
        if(GameObject.Find("Two Hand Pose")!=null) throw new Exception("Refusing to replace authored gesture");
        var gesture=new GameObject("Two Hand Pose").AddUdonSharpComponent<BirdObjectTwoHandPose>();
        gesture.grip=GameObject.Find("Hanoi Grip").GetComponent<BirdObjectGrip>(); gesture.first=GameObject.Find("UI Left").GetComponent<BirdUiPointer>(); gesture.second=GameObject.Find("UI Right").GetComponent<BirdUiPointer>();
        UdonSharpEditorUtility.CopyProxyToUdon(gesture);
        if(!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed"); AssetDatabase.SaveAssets();
        File.WriteAllText("udon-two-hand-author-result.txt","PASS: optional two-hand adapter bound to the existing local grip and two pointers; no avatar click source added."); EditorApplication.Exit(0);
    }
}
#endif
