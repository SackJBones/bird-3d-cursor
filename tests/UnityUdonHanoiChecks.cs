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

// Configure proxies only during authoring; runtime checks drive compiled backing VMs.
public class UnityUdonHanoiChecks : MonoBehaviour
{
    const string ScenePath="Assets/BirdWorld/Scenes/BirdHanoiDemo.unity";
    const string Active="Bird.Udon.Hanoi.Checks";
    static readonly string[] Names={"BirdObjectRegion","BirdObjectSnapTarget","BirdObjectPolicy","BirdObjectTarget","BirdObjectPlayArea","BirdHanoiBoard","BirdObjectGrip","BirdObjectFeedback","BirdHanoiStation"};
    static int checks;
    static double deadline;
    static UdonBehaviour left,right,grip,router,gate,station;
    static readonly Vector3 Origin=new Vector3(0,1.65f,0);
    int stage;
    float started;
    Vector3 frameHome,framePoint;
    bool ran;
    public static void Run()
    {
        File.WriteAllText("udon-hanoi-result.txt","PENDING"); Compile(); EditorSceneManager.OpenScene(ScenePath);
        SessionState.SetBool(Active,true); EditorApplication.isPlaying=true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Begin() { if(SessionState.GetBool(Active,false)) new GameObject("Hanoi VM checks").AddComponent<UnityUdonHanoiChecks>(); }
    void Update()
    {
        if(ran || !SessionState.GetBool(Active,false)) return;
        if(deadline==0) deadline=EditorApplication.timeSinceStartup+120;
        if(EditorApplication.timeSinceStartup>deadline) { Finish(false,"ClientSim startup/frame sequence timeout"); return; }
        if(!Utilities.IsValid(Networking.LocalPlayer) || Time.timeSinceLevelLoad<2) return;
        try
        {
            if(!FrameSequence()) return;
            ran=true; Check();
            Finish(true,checks+" compiled-Udon assertions; saved-scene normal Update/LateUpdate grip, drop, distant closure and loss recovery; both seven-move solutions, policy, menu arbitration, player-area and volume/lifecycle checks. Synthetic logical input, not actual VR hands or multiplayer validation.");
        }
        catch(Exception e) { Finish(false,e.ToString()); }
    }
    bool FrameSequence()
    {
        if(stage==0)
        {
            left=Vm<BirdUiPointer>("UI Left"); right=Vm<BirdUiPointer>("UI Right"); grip=Vm<BirdObjectGrip>("Hanoi Grip");
            router=Vm<BirdUiRouter>("UI Router"); gate=Vm<BirdObjectPlayArea>("Hanoi Play Area"); station=Vm<BirdHanoiStation>("Hanoi Station");
            Vm<BirdUiDesktopInput>("UI Desktop Input").enabled=false;
            station.SendCustomEvent("ResetPuzzles"); left.SendCustomEvent("Cancel");
            frameHome=Piece("Table",0).transform.position; framePoint=frameHome;
            Feed(left,framePoint,false); stage++; return false;
        }
        if(stage==1) { Feed(left,framePoint,true); stage++; return false; }
        if(stage==2)
        {
            Assert(Ref(grip,"ActiveTarget")==Piece("Table",0),"Normal LateUpdate picks tabletop piece; gate="+Flag(gate,"allowed")+" player="+gate.GetProgramVariable("samplePosition")+" tracked="+Flag(left,"tracked")+" edge="+Flag(left,"pressedThisSample")+" consumed="+Flag(left,"uiConsumed")+" hovered="+Ref(grip,"HoveredTarget")+" policy="+Vm<BirdObjectPolicy>("Hanoi Table").GetProgramVariable("allowed")+" operation="+Vm<BirdObjectPolicy>("Hanoi Table").GetProgramVariable("operation"));
            started=Time.unscaledTime; stage++;
        }
        if(stage==3)
        {
            Feed(left,Slot("Table",2).transform.position,true);
            if(Time.unscaledTime-started<.8f) return false;
            Assert(Flag(grip,"ReadyToPlace"),"Normal LateUpdate reaches final placement"); Capture("frame-table-ready");
            Feed(left,Slot("Table",2).transform.position,false); stage++; return false;
        }
        if(stage==4)
        {
            Assert(Number(BoardVm("Table"),"moves")==1 && Ref(grip,"ActiveTarget")==null,"Normal release commits tabletop");
            frameHome=Piece("Buildings",0).transform.position; Feed(left,frameHome,false); stage++; return false;
        }
        if(stage==5) { Feed(left,frameHome,true); stage++; return false; }
        if(stage==6)
        {
            Assert(Ref(grip,"ActiveTarget")==Piece("Buildings",0),"Normal frame grabs full building");
            Feed(left,Origin,true); started=Time.unscaledTime; stage++; return false;
        }
        if(stage==7)
        {
            Feed(left,Origin,true);
            if(Time.unscaledTime-started<.4f) return false;
            Assert(Piece("Buildings",0).GetComponent<BoxCollider>().bounds.min.z>300,"Normal closed hand keeps whole building distant");
            Capture("frame-building-bounded"); left.SendCustomEvent("Cancel"); started=Time.unscaledTime; stage++; return false;
        }
        if(stage==8)
        {
            if(Time.unscaledTime-started<.45f) return false;
            Assert(Ref(grip,"ActiveTarget")==null && Vector3.Distance(Piece("Buildings",0).transform.position,frameHome)<.002f,"Normal tracking loss returns to committed pose");
            Capture("paired-puzzles"); stage++; return true;
        }
        return true;
    }
    static void Check()
    {
        grip.SetProgramVariable("automatic",false); router.SetProgramVariable("automatic",false); gate.SetProgramVariable("automatic",false);
        Vm<BirdUiSphericalScroll>("UI Sphere").SetProgramVariable("automatic",false);
        Reset();
        SceneContracts();
        foreach(string board in new[]{"Table","Buildings"})
        {
            Grab(board,2,false); Assert(Ref(grip,"ActiveTarget")==null,"Only exposed top piece is eligible: "+board);
            int[,] moves={{0,2},{1,1},{0,1},{2,2},{0,0},{1,2},{0,2}};
            for(int i=0;i<7;i++) MovePiece(board,moves[i,0],moves[i,1]);
            Assert(Flag(BoardVm(board),"solved") && Number(BoardVm(board),"moves")==7,"Seven moves solve "+board);
        }
        Assert(Number(station,"placedCount")>=15,"Serialized local placement callback runs");
        Feed(left,Origin,false); Step(); Feed(left,Origin+(Vm<BirdUiElement>("UI Open").transform.position-Origin)*1.1f,false); Step();
        Vector3 resetPoint=Vm<BirdUiElement>("Hanoi Reset").transform.position;
        Feed(left,resetPoint,false); Step(); Feed(left,resetPoint,true); Step();
        Assert(Number(BoardVm("Table"),"moves")==0 && Number(BoardVm("Buildings"),"moves")==0 && !Flag(BoardVm("Table"),"solved") && !Flag(BoardVm("Buildings"),"solved"),"Authored menu reset dispatches station event and resets both solved puzzles");
        Reset(); MovePiece("Table",0,2); Grab("Table",1);
        Vector3 illegal=Slot("Table",2).transform.position;
        Hold(left,illegal,45); Assert(!Flag(grip,"ReadyToPlace"),"Larger-on-smaller drop rejected before release");
        Feed(left,illegal,false); Step(); Assert(Flag(grip,"IsReturning"),"Illegal release begins graceful return");
        Return(); Assert(Number(BoardVm("Table"),"moves")==1,"Cancelled illegal drop leaves model intact");
        Reset();
        Vector3 home=Piece("Buildings",0).transform.position;
        Vector3 far=Origin+(home-Origin)*10000;
        Feed(left,far,false); Step(); Feed(left,far,true); Step();
        Assert(Ref(grip,"ActiveTarget")==Piece("Buildings",0) && Vector3.Distance(home,Piece("Buildings",0).transform.position)<.001f,"Point-through acquisition preserves far-endpoint offset");
        Hold(left,far,10); Assert(Vector3.Distance(home,Piece("Buildings",0).transform.position)<.01f,"Stationary distant point never teleports object");
        Hold(left,new Vector3(1e12f,-1e12f,1e12f),30); Inside("Buildings",0);
        Assert((Vector3)left.GetProgramVariable("position")==new Vector3(1e12f,-1e12f,1e12f),"Workspace does not modify logical Bird point");
        Feed(left,Origin,false); Step(); Return();
        Reset(); Grab("Table",0);
        Vector3 end=Slot("Table",2).transform.position;
        Vector3 regionRight=BoardVm("Table").transform.TransformVector(Vector3.right);
        Hold(left,end+regionRight*.3f,60);
        Assert(Ref(grip,"Candidate")==Slot("Table",2) && !Flag(grip,"ReadyToPlace"),"Guidance cannot manufacture raw release intent");
        Capture("table-approach"); Feed(left,end+regionRight*.3f,false); Step(); Return();
        Lifecycle(); MenuArbitration(); PlayerArea(); TransformedVolumes(); GuidanceAndRates();
        Reset(); Capture("verified-final");
        Assert(Number(station,"cancelledCount")>5 && Number(station,"grabbedCount")>20,"Authored grab/cancel events execute through Udon");
    }
    static void Lifecycle()
    {
        Reset(); Grab("Buildings",0); Hold(left,Origin,40); Inside("Buildings",0);
        Assert(Piece("Buildings",0).GetComponent<BoxCollider>().bounds.min.z>300,"Closed-hand full shape remains outside viewing area");
        left.SendCustomEvent("Cancel"); Step(); Assert(Flag(grip,"IsReturning"),"Tracking loss cancels transaction"); Return();
        Feed(left,Piece("Buildings",0).transform.position,true); Step(); Assert(Ref(grip,"ActiveTarget")==null,"Recovered held input cannot reacquire");
        Reset(); Grab("Table",0); left.SetProgramVariable("userId","Other"); Feed(left,Origin,true); Step(); Assert(Flag(grip,"IsReturning"),"Owner change cancels"); Return(); left.SetProgramVariable("userId","LocalUser");
        Reset(); Grab("Table",0); grip.SetProgramVariable("stepDelta",.3f); grip.SendCustomEvent("Process"); Assert(Flag(grip,"IsReturning"),"Long frame pause cancels"); Return();
        Reset(); Grab("Table",0); var board=BoardVm("Table"); board.enabled=false;
        Assert(Ref(grip,"ActiveTarget")==null,"Disabling rule evaluator rolls back immediately"); board.enabled=true;
        Grab("Table",0); Assert(Ref(grip,"ActiveTarget")==Piece("Table",0),"Reenabled evaluator released reservation");
        Piece("Table",0).enabled=false; Assert(Ref(grip,"ActiveTarget")==null,"Disabled item releases transaction"); Piece("Table",0).enabled=true;
        Reset(); Vector3 point=Piece("Table",0).transform.position; Feed(left,point,false); Step(); Feed(left,point,true); grip.SendCustomEvent("Cancel"); Step();
        Assert(Ref(grip,"ActiveTarget")==null,"Same-frame explicit cancel consumes pickup edge");
        Feed(right,point,false); Step(); Feed(right,point,true); Step(); Assert(Ref(grip,"ActivePointer")==right,"Fresh other hand acquires normally");
        Feed(left,point,false); Step(); Feed(left,point,true); Step(); Assert(Ref(grip,"ActivePointer")==right,"Other hand cannot steal transaction");
        grip.enabled=false; Assert(Ref(grip,"ActiveTarget")==null,"Disabled grip rolls back"); grip.enabled=true;
        Reset(); Grab("Table",0); grip.SetProgramVariable("stepDelta",float.NaN); grip.SendCustomEvent("Process"); Assert(Ref(grip,"ActiveTarget")==null,"Invalid elapsed time rolls back immediately");
        Reset(); Grab("Table",0); var visual=Piece("Table",0).GetComponentInChildren<Renderer>();
        Vm<BirdObjectFeedback>("Hanoi Table Piece 0").SendCustomEvent("Refresh"); var block=new MaterialPropertyBlock(); visual.GetPropertyBlock(block);
        Assert(block.GetColor("_Color").g>visual.sharedMaterial.color.g,"Held renderer receives feedback tint");
        Vm<BirdObjectFeedback>("Hanoi Table Piece 0").enabled=false; visual.GetPropertyBlock(block); Assert(block.isEmpty,"Disabled feedback restores original property block"); Vm<BirdObjectFeedback>("Hanoi Table Piece 0").enabled=true;
        Reset();
    }
    static void GuidanceAndRates()
    {
        Reset(); Grab("Table",0); var frame=BoardVm("Table").transform; var item=Piece("Table",0);
        Vector3 home=item.transform.position;
        Hold(left,home+frame.TransformVector(Vector3.right*.4f),20); Assert(Ref(grip,"Candidate")==Slot("Table",0),"Approach selects eligible lane");
        Hold(left,home+frame.TransformVector(Vector3.right*.55f),20); Assert(Ref(grip,"Candidate")==Slot("Table",0),"Hysteresis retains lane outside attraction radius");
        Hold(left,home+frame.TransformVector(Vector3.right*.7f),20); Assert(Ref(grip,"Candidate")==null,"Lane exits beyond hysteresis radius");
        Reset(); Grab("Table",0); var volume=item.GetComponent<BoxCollider>(); Vector3 oldCenter=volume.center; volume.center+=Vector3.right*.02f; Physics.SyncTransforms(); Step();
        Assert(Flag(grip,"IsReturning"),"Changed shape cancels cached workspace transaction"); volume.center=oldCenter; Return();
        Reset(); var originalEvent=Ref(item,"eventTarget"); string originalGrab=(string)item.GetProgramVariable("grabbedEvent");
        item.SetProgramVariable("eventTarget",grip); item.SetProgramVariable("grabbedEvent","Cancel"); Grab("Table",0,false); Assert(Flag(grip,"IsReturning"),"Grab notification can cancel without recursion or stale ownership"); Return();
        item.SetProgramVariable("eventTarget",originalEvent); item.SetProgramVariable("grabbedEvent",originalGrab);
        var originalSlots=item.GetProgramVariable("destinations");
        var results=new List<Vector3>(); var csv=new List<string>{"hz,final_x,final_y,final_z"};
        foreach(int hz in new[]{30,72,120})
        {
            Reset(); item.SetProgramVariable("destinations",new UdonBehaviour[0]); Grab("Table",0); home=item.transform.position;
            for(int i=1;i<=hz;i++)
            {
                Feed(left,home+frame.TransformVector(new Vector3(.7f,.5f,.1f)*(i/(float)hz)),true);
                grip.SetProgramVariable("stepDelta",1f/hz); grip.SendCustomEvent("Process");
            }
            Vector3 p=frame.InverseTransformPoint(item.transform.position); results.Add(p);
            csv.Add(hz+","+p.x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+p.y.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+p.z.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
            Inside("Table",0);
        }
        Assert(Vector3.Distance(results[0],results[2])<.00002f && Vector3.Distance(results[1],results[2])<.00002f,"Compiled follower agrees across 30/72/120 Hz linear target intervals");
        Directory.CreateDirectory("../Validation/UdonHanoi"); File.WriteAllLines("../Validation/UdonHanoi/translation-rates.csv",csv);
        item.SetProgramVariable("destinations",originalSlots); Reset();
    }
    static void SceneContracts()
    {
        Assert(Vector3.Distance(GameObject.Find("Feasibility floor").GetComponent<Renderer>().bounds.size,new Vector3(10,.3f,10))<.001f,"Authored viewing deck has explicit ten-meter dimensions");
        float tableTop=GameObject.Find("Table").GetComponent<Renderer>().bounds.max.y;
        foreach(string board in new[]{"Table","Buildings"})
        {
            for(int rank=0;rank<3;rank++)
            {
                var piece=Piece(board,rank); var volume=piece.GetComponent<BoxCollider>(); Bounds local=new Bounds(volume.center,volume.size); local.Expand(.0002f); bool enclosed=true;
                foreach(var mesh in piece.GetComponentsInChildren<MeshFilter>()) for(int i=0;i<8;i++)
                {
                    Bounds b=mesh.sharedMesh.bounds; Vector3 sign=new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1);
                    Vector3 p=volume.transform.InverseTransformPoint(mesh.transform.TransformPoint(b.center+Vector3.Scale(b.extents,sign))); enclosed &= local.Contains(p);
                }
                Assert(enclosed,"Body and projecting windows fit the declared volume: "+board+" "+rank);
            }
        }
        foreach(Transform child in BoardVm("Table").transform) if(child.name=="Landing pad")
            Assert(child.GetComponent<Renderer>().bounds.max.y-tableTop>.005f,"Table pad top is separated from tabletop face");
    }
    static void MenuArbitration()
    {
        Reset(); var panel=Vm<BirdUiPanel>("UI Root Panel");
        var open=Vm<BirdUiElement>("UI Open"); Vector3 openPoint=open.transform.position;
        Feed(left,Origin,false); Step(); Feed(left,Origin+(openPoint-Origin)*1.1f,false); Step();
        Assert(Number(panel,"state")==1,"Saved reach-through menu opens alongside puzzle");
        Capture("menu-owns-input");
        Grab("Table",0,false); Assert(Ref(grip,"ActiveTarget")==null,"Foreground menu blocks world pickup even off its controls");
        panel.SendCustomEvent("CloseAll"); Reset(); Grab("Table",0);
        panel.SetProgramVariable("owner","LocalUser"); panel.SetProgramVariable("state",1); Step(); Assert(Flag(grip,"IsReturning"),"Opening menu cancels active world grip"); Return(); panel.SendCustomEvent("CloseAll");
        Reset();
        // Place the close control directly before the top piece, then close on the same press.
        var close=Vm<BirdUiElement>("UI Close"); Vector3 saved=close.transform.position;
        panel.SetProgramVariable("owner","LocalUser"); panel.SetProgramVariable("state",1);
        ((GameObject)panel.GetProgramVariable("content")).SetActive(true);
        Vector3 point=Piece("Table",0).transform.position; close.transform.position=Vector3.Lerp(Origin,point,.75f); Physics.SyncTransforms();
        Feed(left,point,false); Step(); Feed(left,point,true); Step();
        Assert(Number(panel,"state")==0 && Flag(left,"uiConsumed"),"Closing action retains sample consumption after panel disappears");
        Assert(Ref(grip,"ActiveTarget")==null,"Close cannot also pick up object behind it");
        close.transform.position=saved; Physics.SyncTransforms();
        // An accepted inert UI surface also reserves the ray; a new neutral sample releases it.
        var oldAction=Ref(open,"actionTarget"); var oldActivation=Number(open,"activation"); Vector3 oldOpen=open.transform.position;
        open.SetProgramVariable("activation",(int)BirdUiActivation.SelectThrough); open.SetProgramVariable("actionTarget",null); open.transform.position=Vector3.Lerp(Origin,point,.75f); Physics.SyncTransforms();
        Feed(left,point,false); Step(); Feed(left,point,true); Step(); Assert(Flag(left,"uiConsumed") && Ref(grip,"ActiveTarget")==null,"Inert foreground UI contact still reserves input");
        open.transform.position=oldOpen; open.SetProgramVariable("activation",oldActivation); open.SetProgramVariable("actionTarget",oldAction); Physics.SyncTransforms();
        Feed(left,point,false); Step(); Feed(left,point,true); Step(); Assert(Ref(grip,"ActiveTarget")==Piece("Table",0),"Fresh unconsumed press restores world input");
        Reset();
    }
    static void PlayerArea()
    {
        Reset(); gate.SetProgramVariable("samplePosition",new Vector3(100,0,0)); gate.SendCustomEvent("Evaluate");
        Grab("Buildings",0,false); Assert(!Flag(gate,"allowed") && Ref(grip,"ActiveTarget")==null,"Outside viewing area blocks acquisition");
        SetPlayer(Vector3.zero); Grab("Buildings",0);
        SetPlayer(new Vector3(100,0,0)); Step(); Assert(Flag(grip,"IsReturning"),"Leaving viewing area cancels held building"); Return();
        SetPlayer(Vector3.zero); var frame=BoardVm("Buildings").transform; Vector3 old=frame.position; frame.position=Vector3.zero; Physics.SyncTransforms(); gate.SendCustomEvent("Evaluate");
        Assert(!Flag(gate,"allowed"),"Workspace overlapping viewing area fails closed"); frame.position=old; Physics.SyncTransforms(); SetPlayer(Vector3.zero);
        gate.SetProgramVariable("hasPlayer",false); gate.SendCustomEvent("Evaluate"); Assert(!Flag(gate,"allowed"),"Missing local player fails closed");
        SetPlayer(new Vector3(float.NaN,0,0)); Assert(!Flag(gate,"allowed"),"Invalid player sample fails closed"); Reset();
        var region=Vm<BirdObjectRegion>("Hanoi Buildings"); Bounds saved=(Bounds)region.GetProgramVariable("localBounds"); region.SetProgramVariable("localBounds",new Bounds(Vector3.zero,Vector3.zero)); gate.SendCustomEvent("Evaluate");
        Assert(!Flag(gate,"allowed"),"Invalid protected workspace fails closed"); region.SetProgramVariable("localBounds",saved); Reset();
    }
    static void TransformedVolumes()
    {
        Reset(); var target=Piece("Table",0); var frame=BoardVm("Table").transform; Quaternion oldRotation=frame.rotation; Vector3 oldScale=frame.localScale;
        // Disable demo menu/player policy for arbitrary mathematical fixture rotations.
        var oldGate=Ref(grip,"playArea"); grip.SetProgramVariable("playArea",null); router.SetProgramVariable("elements",new UdonBehaviour[0]);
        foreach(Quaternion rotation in new[]{Quaternion.identity,Quaternion.Euler(0,35,180),Quaternion.Euler(21,33,8)})
        foreach(Vector3 scale in new[]{Vector3.one*.2f,new Vector3(-.3f,.15f,.25f)})
        {
            frame.rotation=rotation; frame.localScale=scale; Physics.SyncTransforms();
            // Origin immediately outside the chosen top volume avoids unrelated foreground geometry.
            Vector3 point=target.transform.position,origin=point-frame.forward*.5f;
            Feed(left,point,false,origin); Step(false); Feed(left,point,true,origin); Step(false);
            Assert(Ref(grip,"ActiveTarget")==target,"Transformed volume acquired");
            for(int i=0;i<12;i++) { Feed(left,point+new Vector3(5,-4,6)*(i/11f),true,origin); Step(false); Inside("Table",0); }
            grip.SendCustomEvent("CancelImmediately"); left.SendCustomEvent("Cancel");
        }
        frame.rotation=oldRotation; frame.localScale=oldScale; Physics.SyncTransforms(); grip.SetProgramVariable("playArea",oldGate);
        // Restore saved authored router registrations without invoking a runtime C# method.
        var elements=new List<UdonBehaviour>(); foreach(var proxy in FindObjectsOfType<BirdUiElement>(true)) elements.Add(UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy));
        router.SetProgramVariable("elements",elements.ToArray()); router.SendCustomEvent("Initialize"); Reset();
    }
    static void Reset()
    {
        station.SendCustomEvent("ResetPuzzles"); Vm<BirdUiPanel>("UI Root Panel").SendCustomEvent("CloseAll"); left.SendCustomEvent("Cancel"); right.SendCustomEvent("Cancel");
        SetPlayer(Vector3.zero); grip.SetProgramVariable("stepDelta",1f/72); grip.SendCustomEvent("Initialize"); router.SendCustomEvent("Initialize"); Physics.SyncTransforms();
    }
    static void SetPlayer(Vector3 position) { gate.SetProgramVariable("hasPlayer",true); gate.SetProgramVariable("samplePosition",position); gate.SendCustomEvent("Evaluate"); }
    static void Grab(string board,int rank,bool expected=true)
    {
        Vector3 point=Piece(board,rank).transform.position; Feed(left,point,false); Step(); Feed(left,point,true); Step();
        if(expected) Assert(Ref(grip,"ActiveTarget")==Piece(board,rank),"Acquire "+board+" piece "+rank);
    }
    static void MovePiece(string board,int rank,int peg)
    {
        Grab(board,rank); Vector3 end=Slot(board,peg).transform.position; Hold(left,end,65); Inside(board,rank);
        Assert(Flag(grip,"ReadyToPlace"),"Valid slot ready "+board+" "+rank+" to "+peg);
        if(board=="Buildings" && rank==0 && peg==2 && Number(BoardVm(board),"moves")==0) Capture("building-ready");
        Feed(left,end,false); Step();
        Assert(Ref(grip,"ActiveTarget")==null && Vector3.Distance(Piece(board,rank).transform.position,end)<.002f,"Exact atomic placement "+board);
    }
    static void Hold(UdonBehaviour pointer,Vector3 point,int frames) { for(int i=0;i<frames;i++) { Feed(pointer,point,true); Step(); } }
    static void Return() { for(int i=0;i<24;i++) Step(); Assert(Ref(grip,"ActiveTarget")==null,"Return completes and releases reservation"); }
    static void Inside(string board,int rank)
    {
        var volume=Piece(board,rank).GetComponent<BoxCollider>(); var region=Vm<BirdObjectRegion>("Hanoi "+board); Bounds bounds=(Bounds)region.GetProgramVariable("localBounds"); bounds.Expand(.0002f);
        for(int i=0;i<8;i++)
        {
            Vector3 sign=new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1);
            Vector3 point=region.transform.InverseTransformPoint(volume.transform.TransformPoint(volume.center+Vector3.Scale(volume.size*.5f,sign)));
            Assert(bounds.Contains(point),"Whole configured volume inside "+board+" workspace");
        }
    }
    static void Step(bool withUi=true) { grip.SetProgramVariable("stepDelta",1f/72); if(withUi) router.SendCustomEvent("Process"); grip.SendCustomEvent("Process"); }
    static void Feed(UdonBehaviour pointer,Vector3 position,bool pressed,Vector3? origin=null)
    {
        pointer.SetProgramVariable("sampleOrigin",origin??Origin); pointer.SetProgramVariable("samplePosition",position); pointer.SetProgramVariable("sampleTracked",true); pointer.SetProgramVariable("samplePressed",pressed); pointer.SendCustomEvent("Submit");
    }
    static UdonBehaviour Piece(string board,int rank) { return Vm<BirdObjectTarget>("Hanoi "+board+" Piece "+rank); }
    static UdonBehaviour Slot(string board,int peg) { return Vm<BirdObjectSnapTarget>("Hanoi "+board+" Slot "+peg); }
    static UdonBehaviour BoardVm(string board) { return Vm<BirdHanoiBoard>("Hanoi "+board); }
    static UdonBehaviour Vm<T>(string name) where T:UdonSharpBehaviour { var go=GameObject.Find(name); if(go==null) { foreach(var proxy in Resources.FindObjectsOfTypeAll<T>()) if(proxy.gameObject.scene.IsValid() && proxy.name==name) return UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy); throw new Exception("Missing "+name); } return UdonSharpEditorUtility.GetBackingUdonBehaviour(go.GetComponent<T>()); }
    static UdonBehaviour Ref(UdonBehaviour vm,string key) { return (UdonBehaviour)vm.GetProgramVariable(key); }
    static bool Flag(UdonBehaviour vm,string key) { return (bool)vm.GetProgramVariable(key); }
    static int Number(UdonBehaviour vm,string key) { return (int)vm.GetProgramVariable(key); }
    static void Assert(bool ok,string message) { checks++; if(!ok) throw new Exception("Assertion "+checks+": "+message); }
    static void Finish(bool ok,string result) { SessionState.SetBool(Active,false); File.WriteAllText("udon-hanoi-result.txt",(ok?"PASS: ":"FAIL: ")+result); EditorApplication.Exit(ok?0:1); }
    static void Capture(string name)
    {
        station.SendCustomEvent("Refresh"); Directory.CreateDirectory("../Validation/UdonHanoi");
        var layers=new Dictionary<Transform,int>();
        foreach(string rootName in new[]{"Hanoi environment","Hanoi Table","Hanoi Buildings","Hanoi Menu Offset","Feasibility floor"})
            foreach(Transform item in GameObject.Find(rootName).GetComponentsInChildren<Transform>(true)) { if(!layers.ContainsKey(item)) layers[item]=item.gameObject.layer; item.gameObject.layer=30; }
        var camera=new GameObject("Hanoi evidence camera").AddComponent<Camera>(); camera.transform.position=new Vector3(0,1.65f,0); camera.transform.LookAt(new Vector3(0,1.4f,4)); camera.fieldOfView=64; camera.farClipPlane=2500; camera.nearClipPlane=.05f;
        camera.cullingMask=1<<30; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.07f,.12f,.2f);
        var texture=new RenderTexture(1600,1100,24) { antiAliasing=4 }; var pixels=new Texture2D(1600,1100,TextureFormat.RGB24,false);
        camera.targetTexture=texture; camera.Render(); RenderTexture.active=texture; pixels.ReadPixels(new Rect(0,0,1600,1100),0,0); pixels.Apply(); File.WriteAllBytes("../Validation/UdonHanoi/"+name+".png",pixels.EncodeToPNG());
        RenderTexture.active=null; camera.targetTexture=null; texture.Release(); Destroy(camera.gameObject); Destroy(texture); Destroy(pixels);
        foreach(var pair in layers) pair.Key.gameObject.layer=pair.Value;
    }
    public static void Generate()
    {
        if(File.Exists(ScenePath)) throw new Exception("Refusing to overwrite authored Hanoi scene.");
        Compile();
        var scene=EditorSceneManager.OpenScene("Assets/BirdWorld/Scenes/BirdUiDemo.unity");
        var menu=new GameObject("Hanoi Menu Offset").transform;
        var roots=new HashSet<Transform>();
        foreach(var proxy in FindObjectsOfType<UdonSharpBehaviour>(true))
            if(proxy.GetType().Name.StartsWith("BirdUi")) roots.Add(proxy.transform.root);
        foreach(string name in new[]{"Bird UI station","UI title","Desktop instructions"}) roots.Add(GameObject.Find(name).transform.root);
        foreach(var root in roots) root.SetParent(menu,true);
        menu.position=new Vector3(-2.3f,0,0);
        GameObject.Find("UI title").GetComponentInChildren<Text>(true).text="MENU / COLORS";
        GameObject.Find("UI Open").transform.position=new Vector3(-1.1f,.8f,1.8f);
        GameObject.Find("Desktop instructions").SetActive(false);
        var world=new GameObject("Hanoi environment").transform;
        var floor=GameObject.Find("Feasibility floor");
        floor.transform.position=new Vector3(0,-.15f,0); floor.transform.localScale=new Vector3(10,.3f,10);
        var terrace=Material("HanoiTerrace",new Color(.22f,.3f,.35f));
        floor.GetComponent<Renderer>().sharedMaterial=terrace;
        foreach(Vector3 p in new[]{new Vector3(-5,.65f,0),new Vector3(5,.65f,0),new Vector3(0,.65f,5),new Vector3(0,.65f,-5)})
        {
            bool side=Mathf.Abs(p.x)>1;
            Shape("Terrace boundary",world,p,side?new Vector3(.15f,1.3f,10):new Vector3(10,1.3f,.15f),terrace,true);
        }
        Shape("Table",world,new Vector3(0,.72f,2.05f),new Vector3(1.08f,.12f,.72f),terrace,true);
        Shape("Valley",world,new Vector3(0,-31,600),new Vector3(1500,6,1450),Material("HanoiValley",new Color(.13f,.23f,.23f)),false);
        Shape("Building foundation",world,new Vector3(0,-18.4f,420),new Vector3(350,12,190),terrace,false);
        for(int i=0;i<9;i++)
            Shape("Distant scale reference",world,new Vector3((i-4)*150,-12,800+Mathf.Abs(i-4)*70),new Vector3(30,32+(i%3)*24,35),Material("HanoiHorizon",new Color(.22f,.3f,.4f)),false);
        var grip=new GameObject("Hanoi Grip").AddUdonSharpComponent<BirdObjectGrip>();
        grip.pointers=new[]{GameObject.Find("UI Left").GetComponent<BirdUiPointer>(),GameObject.Find("UI Right").GetComponent<BirdUiPointer>()};
        grip.menuPanels=new[]{GameObject.Find("UI Root Panel").GetComponent<BirdUiPanel>()};
        var gate=new GameObject("Hanoi Play Area").AddUdonSharpComponent<BirdObjectPlayArea>();
        gate.playerArea=gate.gameObject.AddComponent<BoxCollider>(); gate.playerArea.center=new Vector3(0,2,0); gate.playerArea.size=new Vector3(9.6f,6,9.6f); gate.playerArea.isTrigger=true; grip.playArea=gate;
        var station=new GameObject("Hanoi Station").AddUdonSharpComponent<BirdHanoiStation>(); station.grip=grip;
        var targets=new List<BirdObjectTarget>(); var feedbacks=new List<BirdObjectFeedback>();
        station.tabletop=Board("Table",new Vector3(0,.78f,2.05f),.2f,station,targets,feedbacks);
        station.buildings=Board("Buildings",new Vector3(0,-12,420),75,station,targets,feedbacks);
        grip.targets=targets.ToArray(); station.feedbacks=feedbacks.ToArray();
        gate.protectedRegions=new[]{station.buildings.GetComponent<BirdObjectRegion>()};
        station.footprint=Shape("Placement footprint",world,Vector3.zero,Vector3.one,Material("HanoiFootprint",new Color(.1f,.85f,.78f)),false).transform;
        station.footprint.gameObject.SetActive(false);
        station.approach=new GameObject("Placement approach").AddComponent<LineRenderer>(); station.approach.transform.SetParent(world);
        station.approach.positionCount=2; station.approach.enabled=false; station.approach.sharedMaterial=Material("HanoiGuide",new Color(.25f,.9f,.75f));
        station.label=Label("Hanoi instructions",new Vector3(1.05f,1.15f,2.4f),"POINT / HOLD TO PICK UP",world,530,450,.0015f);
        var desktop=GameObject.Find("UI Desktop Input").GetComponent<BirdUiDesktopInput>();
        desktop.label=null; desktop.maximumRange=1500; desktop.exponentialRange=true;
        desktop.cancelTarget=UdonSharpEditorUtility.GetBackingUdonBehaviour(station); desktop.cancelEvent="CancelGrip";
        desktop.instructions="Local desktop input / look, wheel, hold to move; X cancels";
        var reset=Shape("Hanoi Reset",grip.menuPanels[0].content.transform,new Vector3(-2.3f,.55f,2.8f),new Vector3(.9f,.22f,.08f),Material("HanoiReset",new Color(.2f,.3f,.45f)),true);
        var element=reset.AddUdonSharpComponent<BirdUiElement>(); element.panel=grip.menuPanels[0]; element.target=reset.GetComponent<BoxCollider>(); element.target.isTrigger=true; element.feedback=reset.GetComponent<Renderer>();
        var action=reset.AddUdonSharpComponent<BirdUiAction>(); action.element=element; action.action=BirdUiActionKind.SendEvent; action.callback=UdonSharpEditorUtility.GetBackingUdonBehaviour(station); action.callbackEvent="ResetPuzzles";
        element.actionTarget=UdonSharpEditorUtility.GetBackingUdonBehaviour(action);
        Label("Reset puzzles label",new Vector3(-2.3f,.55f,2.75f),"RESET BOTH PUZZLES",reset.transform,800,90,.001f);
        var router=GameObject.Find("UI Router").GetComponent<BirdUiRouter>(); var elements=new List<BirdUiElement>(router.elements); elements.Add(element); router.elements=elements.ToArray();
        foreach(var proxy in FindObjectsOfType<UdonSharpBehaviour>(true)) UdonSharpEditorUtility.CopyProxyToUdon(proxy);
        RefineVisuals();
        Physics.SyncTransforms();
        if(!EditorSceneManager.SaveScene(scene,ScenePath)) throw new Exception("Scene save failed");
        AssetDatabase.SaveAssets();
        File.WriteAllText("udon-hanoi-generate-result.txt","PASS: authored paired-scale Hanoi, existing menu/color selector, shared local manipulation and viewing-area gate."); EditorApplication.Exit(0);
    }
    public static void RefineLayout()
    {
        var scene=EditorSceneManager.OpenScene(ScenePath); RefineVisuals(); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        File.WriteAllText("udon-hanoi-layout-result.txt","PASS: viewing deck, table contrast, menu title/result arrangement refined."); EditorApplication.Exit(0);
    }
    static void RefineVisuals()
    {
        var world=GameObject.Find("Hanoi environment").transform;
        // The inherited feasibility surface is a 10-unit Plane mesh, not a unit cube.
        // Replace it in this new scene with an explicitly dimensioned deck.
        DestroyImmediate(GameObject.Find("Feasibility floor"));
        Shape("Feasibility floor",world,new Vector3(0,-.15f,0),new Vector3(10,.3f,10),Material("HanoiDeck",new Color(.12f,.19f,.23f)),true);
        GameObject.Find("Table").GetComponent<Renderer>().sharedMaterial=Material("HanoiTableSurface",new Color(.45f,.32f,.23f));
        GameObject.Find("Table").transform.position=new Vector3(0,.71f,2.05f);
        var title=GameObject.Find("UI title"); title.transform.position=new Vector3(-2.3f,2.65f,3.8f); title.transform.localScale=Vector3.one*.002f;
        GameObject.Find("UI Result").transform.position=new Vector3(-3.8f,1.85f,3.8f);
        var label=FindScene("Reset puzzles label").transform; label.SetParent(null,true); label.localScale=Vector3.one*.001f; label.SetParent(FindScene("Hanoi Reset").transform,true);
    }
    static GameObject FindScene(string name) { foreach(var value in Resources.FindObjectsOfTypeAll<GameObject>()) if(value.scene.IsValid() && value.name==name) return value; throw new Exception("Missing authored object "+name); }
    static BirdHanoiBoard Board(string name,Vector3 position,float unit,BirdHanoiStation station,List<BirdObjectTarget> targets,List<BirdObjectFeedback> feedbacks)
    {
        var root=new GameObject("Hanoi "+name).transform; root.position=position; root.localScale=Vector3.one*unit;
        var board=root.gameObject.AddUdonSharpComponent<BirdHanoiBoard>();
        var region=root.gameObject.AddUdonSharpComponent<BirdObjectRegion>(); region.localBounds=new Bounds(new Vector3(0,1.7f,0),new Vector3(4.5f,3.4f,2.6f));
        var policy=root.gameObject.AddUdonSharpComponent<BirdObjectPolicy>(); policy.evaluator=UdonSharpEditorUtility.GetBackingUdonBehaviour(board); board.policy=policy;
        board.pegs=new Transform[3]; board.slots=new BirdObjectSnapTarget[3]; board.pieces=new BirdObjectTarget[3]; board.heights=new[]{.34f,.42f,.5f};
        for(int i=0;i<3;i++)
        {
            var peg=new GameObject("Hanoi "+name+" Peg "+i).transform; peg.SetParent(root,false); peg.localPosition=new Vector3((i-1)*1.4f,0,0); board.pegs[i]=peg;
            var pad=Shape("Landing pad",root,peg.position-root.up*unit*.025f,new Vector3(1.2f,.05f,1.1f)*unit,Material("HanoiPad",new Color(.32f,.39f,.42f)),false); pad.transform.rotation=root.rotation;
            var slot=new GameObject("Hanoi "+name+" Slot "+i).AddUdonSharpComponent<BirdObjectSnapTarget>(); slot.transform.SetParent(root,false); slot.transform.position=peg.position;
            slot.approachLength=1.35f; slot.influenceRadius=.48f; slot.captureRadius=.23f; board.slots[i]=slot;
        }
        Color[] colors={new Color(1,.63f,.2f),new Color(.17f,.8f,.85f),new Color(.6f,.43f,.94f)};
        float[] widths={.62f,.87f,1.12f}; float baseHeight=0;
        for(int rank=2;rank>=0;rank--)
        {
            float h=board.heights[rank],w=widths[rank],d=w*.83f;
            var item=new GameObject("Hanoi "+name+" Piece "+rank); item.transform.SetParent(root,false); item.transform.localPosition=new Vector3(-1.4f,baseHeight+h*.5f,0); baseHeight+=h;
            var body=Shape("Tower body",item.transform,item.transform.position,new Vector3(w,h,d)*unit,Material("HanoiPiece"+rank,colors[rank]),false);
            var volume=item.AddComponent<BoxCollider>(); volume.size=new Vector3(w,h,d+.007f); volume.center=new Vector3(0,0,-.0035f); volume.isTrigger=true;
            var target=item.AddUdonSharpComponent<BirdObjectTarget>(); target.volume=volume; target.region=region; target.policy=policy; target.destinations=board.slots;
            target.eventTarget=UdonSharpEditorUtility.GetBackingUdonBehaviour(station); target.grabbedEvent="RecordGrab"; target.placedEvent="RecordPlace"; target.cancelledEvent="RecordCancel";
            board.pieces[rank]=target; targets.Add(target);
            var feedback=item.AddUdonSharpComponent<BirdObjectFeedback>(); feedback.grip=station.grip; feedback.target=target; feedback.visual=body.GetComponent<Renderer>(); feedbacks.Add(feedback);
            int floors=8+rank*2;
            for(int row=0;row<floors;row++) for(int col=0;col<6;col++)
            {
                var window=Shape("Window",item.transform,Vector3.zero,Vector3.one,Material("HanoiWindow",new Color(.06f,.13f,.2f)),false);
                window.transform.localPosition=new Vector3((col-2.5f)*w/7,(row-(floors-1)*.5f)*h/(floors+1),-d*.5f-.005f);
                window.transform.localScale=new Vector3(w/13,h/(floors+1)*.5f,.004f);
            }
        }
        return board;
    }
    static Material Material(string name,Color color)
    {
        string path="Assets/BirdWorld/Materials/"+name+".mat";
        var value=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(value==null)
        {
            var shader=Shader.Find("Bird/Samples/Hanoi Surface"); if(shader==null) throw new Exception("Missing Hanoi sample shader");
            value=new Material(shader); value.color=color; AssetDatabase.CreateAsset(value,path);
        }
        return value;
    }
    static GameObject Shape(string name,Transform parent,Vector3 position,Vector3 worldSize,Material material,bool collider)
    {
        var value=GameObject.CreatePrimitive(PrimitiveType.Cube); value.name=name; value.transform.position=position; value.transform.localScale=worldSize; value.transform.SetParent(parent,true);
        value.GetComponent<Renderer>().sharedMaterial=material; if(!collider) DestroyImmediate(value.GetComponent<Collider>()); return value;
    }
    static Text Label(string name,Vector3 position,string text,Transform parent,float width,float height,float scale)
    {
        var go=new GameObject(name); go.transform.SetParent(parent,true); go.transform.position=position; go.transform.localScale=Vector3.one*scale;
        var canvas=go.AddComponent<Canvas>(); canvas.renderMode=RenderMode.WorldSpace;
        var rt=go.GetComponent<RectTransform>(); rt.sizeDelta=new Vector2(width,height);
        var child=new GameObject("Text"); child.transform.SetParent(go.transform,false); var value=child.AddComponent<Text>();
        value.rectTransform.sizeDelta=rt.sizeDelta; value.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); value.fontSize=30; value.alignment=TextAnchor.MiddleCenter; value.color=Color.white; value.text=text;
        return value;
    }
    public static void CompileOnly()
    {
        Compile(); File.WriteAllText("udon-hanoi-compile-result.txt","PASS: compiled nine object/Hanoi Udon programs."); EditorApplication.Exit(0);
    }
    static void Compile()
    {
        foreach(string name in Names)
        {
            string path="Assets/BirdWorld/Programs/"+name+".asset";
            var source=AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/BirdGenerated/Runtime/"+name+".cs");
            if(source==null) throw new Exception("Missing source "+name);
            var program=AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path);
            if(program==null) { program=ScriptableObject.CreateInstance<UdonSharpProgramAsset>(); program.sourceCsScript=source; AssetDatabase.CreateAsset(program,path); }
            else if(program.sourceCsScript!=source) throw new Exception("Mismatched program source "+name);
        }
        AssetDatabase.SaveAssets(); UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
        if(UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) throw new Exception("Udon compilation failed");
    }
}
#endif
