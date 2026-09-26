#if UNITY_EDITOR
using System;
using System.IO;
using Bird3DCursor.UI;
using Bird3DCursor.Manipulation;
using Bird3DCursor.Samples;
using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;

public sealed class UnityHanoiChecks : MonoBehaviour
{
    const string Active="Bird.Hanoi.Checks";
    BirdHanoiPreview demo;
    int checks,captures;
    public static void Run()
    {
        File.WriteAllText("hanoi-result.txt","PENDING");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("Bird paired Hanoi preview").AddComponent<BirdHanoiPreview>();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/HanoiPreview.unity");
        SessionState.SetBool(Active,true); EditorApplication.isPlaying=true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Begin() { if(SessionState.GetBool(Active,false)) new GameObject("Hanoi checks").AddComponent<UnityHanoiChecks>(); }
    void Start()
    {
        try
        {
            demo=FindObjectOfType<BirdHanoiPreview>(); demo.desktopInput=false; demo.Initialize(); demo.Interactor.automatic=false;
            Directory.CreateDirectory("HanoiCaptures");
            Require(typeof(BirdGrabInteractor).Assembly.GetName().Name=="Bird3D.Manipulation","Independent manipulation assembly");
            Require(demo.Buildings.transform.lossyScale.x/demo.Tabletop.transform.lossyScale.x>100,"Same components span >100x scale");
            foreach(var item in demo.GetComponentsInChildren<BirdGrabTarget>())
            {
                var volume=new Bounds(item.Volume.center,item.Volume.size); volume.Expand(.00001f); bool covered=true;
                foreach(var mesh in item.GetComponentsInChildren<MeshFilter>()) for(int corner=0;corner<8;corner++)
                {
                    var sign=new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1);
                    var b=mesh.sharedMesh.bounds;
                    Vector3 local=item.Volume.transform.InverseTransformPoint(mesh.transform.TransformPoint(b.center+Vector3.Scale(sign,b.extents)));
                    covered &= volume.Contains(local);
                }
                Require(covered,"Manipulation volume contains the visible building including projecting windows");
            }
            Capture("paired-start");
            Puzzle(demo.Tabletop,"table"); Puzzle(demo.Buildings,"buildings");
            demo.ResetPuzzles(); Lifecycle(); ShapeAndFrameChecks(); TrajectoryChecks(); HysteresisAndOffset(); ReturnAndFeedback(); Serialization();
            demo.ResetPuzzles(); Capture("paired-final");
            string result="PASS: "+checks+" Unity manipulation assertions; "+captures+" D3D11 captures. Paired-scale seven-move Hanoi solutions, legal/illegal placement, raw-input versus guided readiness, whole-volume bounds, both hands, loss/disable/recovery, callback cancellation, transformed workspaces and prefab events. Synthetic input; not Udon, VRChat-client, headset feel or collision avoidance.";
            File.WriteAllText("hanoi-result.txt",result); Debug.Log(result); SessionState.SetBool(Active,false); EditorApplication.Exit(0);
        }
        catch(Exception e) { File.WriteAllText("hanoi-result.txt","FAIL after "+checks+": "+e); Debug.LogException(e); SessionState.SetBool(Active,false); EditorApplication.Exit(1); }
    }
    void Require(bool value,string description) { checks++; if(!value) throw new Exception(description); }
    void Near(Vector3 a,Vector3 b,float tolerance,string description) { Require(Vector3.Distance(a,b)<=tolerance,description+" expected "+b+" got "+a); }
    void Feed(Vector3 point,bool pressed=false,float dt=1f/72)
    {
        demo.Pointer.Submit(demo.View.transform.position,point,true,pressed); demo.Interactor.Process(dt);
    }
    void Pick(BirdGrabTarget item)
    {
        Physics.SyncTransforms();
        // A short local ray avoids an unrelated foreground puzzle obscuring a distant fixture.
        Vector3 origin=item.transform.position-item.Region.transform.forward*2*Mathf.Abs(item.Region.transform.lossyScale.z);
        demo.Pointer.Submit(origin,item.transform.position,true,false); demo.Interactor.Process(1f/72);
        demo.Pointer.Submit(origin,item.transform.position,true,true); demo.Interactor.Process(1f/72);
        Require(demo.Interactor.ActiveTarget==item,"Press acquires selected top piece");
    }
    void HoldTo(Vector3 point,int frames=80)
    {
        for(int i=0;i<frames;i++) Feed(point,true);
    }
    void SettleReturn() { for(int i=0;i<30;i++) demo.Interactor.Process(1f/72); }
    void Move(BirdHanoiBoard board,int rank,int peg)
    {
        var item=board.Pieces[rank]; Pick(item);
        Vector3 end=board.Slots[peg].transform.position;
        HoldTo(end+board.transform.up*.6f*Mathf.Abs(board.transform.lossyScale.y));
        Require(demo.Interactor.Candidate==board.Slots[peg],"Approach lane acquired");
        Require(!demo.Interactor.ReadyToPlace,"Approach is not final placement");
        HoldTo(end);
        Require(demo.Interactor.ReadyToPlace,"Final capture readiness");
        Feed(end,false);
        Require(demo.Interactor.ActiveTarget==null && board.Location(rank)==peg,"Release commits atomic placement");
        Near(item.transform.position,end,.0005f*Mathf.Abs(board.transform.lossyScale.x),"Firm placement is exact");
    }
    void Puzzle(BirdHanoiBoard board,string label)
    {
        Require(!board.CanGrab(board.Pieces[1]) && !board.CanGrab(board.Pieces[2]),"Covered pieces cannot lift");
        var item=board.Pieces[0]; Vector3 home=item.transform.position; Pick(item);
        Near(item.transform.position,home,1e-5f,"Acquisition preserves grab offset without jumping");
        HoldTo(board.Slots[2].transform.position+board.transform.up*Mathf.Abs(board.transform.lossyScale.y));
        Capture(label+"-approach");
        Require(demo.Interactor.Candidate==board.Slots[2] && !demo.Interactor.ReadyToPlace,"Soft guide is distinct from firm dock");
        // Closing a fist returns Bird to the origin; the held object remains in its own work region.
        Vector3 logicalNear=demo.View.transform.position;
        HoldTo(logicalNear);
        Near(demo.Pointer.Position,logicalNear,0,"Workspace never clamps geometric Bird point");
        Inside(item,"Closed-hand return keeps every volume corner inside workspace");
        if(board==demo.Buildings) Require(item.Volume.bounds.min.z>300,"Full building stays hundreds of meters away");
        Require(!demo.Interactor.ReadyToPlace,"Clamped object is not a valid dock without raw intent");
        Feed(logicalNear,false); Require(demo.Interactor.IsReturning,"Invalid drop begins graceful return");
        SettleReturn(); Near(item.transform.position,home,.001f,"Invalid release rolls back original pose");
        Require(board.Moves==0,"Cancellation does not mutate puzzle model");

        Move(board,0,2); // block an illegal larger-on-smaller drop
        Pick(board.Pieces[1]); Vector3 invalid=board.Slots[2].transform.position;
        HoldTo(invalid); Require(demo.Interactor.Candidate!=board.Slots[2] && !demo.Interactor.ReadyToPlace,"Larger-on-smaller has no magnet or commit");
        Feed(invalid,false); SettleReturn(); Require(board.Location(1)==0 && board.Moves==1,"Illegal drop restores puzzle");
        Move(board,1,1); Move(board,0,1); Move(board,2,2); Move(board,0,0); Move(board,1,2); Move(board,0,2);
        Require(board.Solved && board.Moves==7,"Seven legal moves solve "+label);
        Capture(label+"-solved");
    }
    void Inside(BirdGrabTarget item,string name)
    {
        for(int i=0;i<8;i++)
        {
            Vector3 s=new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1);
            Vector3 p=item.Region.transform.InverseTransformPoint(item.Volume.transform.TransformPoint(item.Volume.center+Vector3.Scale(s,item.Volume.size*.5f)));
            Bounds b=item.Region.LocalBounds; b.Expand(.0001f);
            Require(b.Contains(p),name+" corner "+i);
        }
    }
    void Lifecycle()
    {
        var item=demo.Tabletop.Pieces[0]; Vector3 home=item.transform.position;
        int grabbed=0,cancelled=0,committed=0;
        item.Grabbed.AddListener(()=>grabbed++); item.Cancelled.AddListener(()=>cancelled++); item.Placed.AddListener(()=>committed++);
        Pick(item); HoldTo(home+Vector3.up*.5f);
        demo.OtherPointer.Submit(home-Vector3.forward,home,true,false); demo.Interactor.Process(1f/72);
        demo.OtherPointer.Submit(home-Vector3.forward,home,true,true); demo.Interactor.Process(1f/72);
        Require(demo.Interactor.ActivePointer==demo.Pointer,"Other hand cannot steal active transaction");
        demo.Pointer.Cancel(); demo.Interactor.Process(1f/72);
        Require(demo.Interactor.IsReturning && cancelled==1,"Loss cancels exactly once"); SettleReturn();
        Require(grabbed==1 && committed==0 && item.Owner==null,"Return releases reservation");
        demo.Pointer.Submit(home-Vector3.forward,home,true,true); demo.Interactor.Process(1f/72);
        Require(demo.Interactor.ActiveTarget==null,"Held recovery cannot reacquire");
        demo.OtherPointer.Cancel();
        demo.OtherPointer.Submit(home-Vector3.forward,home,true,false); demo.Interactor.Process(1f/72);
        demo.OtherPointer.Submit(home-Vector3.forward,home,true,true); demo.Interactor.Process(1f/72);
        Require(demo.Interactor.ActiveTarget==item && demo.Interactor.ActivePointer==demo.OtherPointer,"Other hand can start a fresh transaction after release");
        demo.Interactor.CancelImmediately(); demo.OtherPointer.Cancel();
        cancelled=1; // Subsequent lifecycle counter checks start after this independent other-hand transaction.
        Pick(item); HoldTo(home+Vector3.up*.5f); demo.Interactor.enabled=false;
        Near(item.transform.position,home,.0001f,"Disabling driver rolls back immediately"); Require(item.Owner==null && cancelled==2,"Disable clears lease");
        demo.Interactor.enabled=true; demo.Interactor.Process(1f/72); Require(demo.Interactor.ActiveTarget==null,"Enable cannot replay press");
        Pick(item); item.enabled=false; Require(demo.Interactor.ActiveTarget==null,"Disabled target cancels driver immediately"); item.enabled=true;
        Pick(item); demo.Pointer.SetUser("Changed"); demo.Pointer.Submit(Vector3.zero,home,true,true); demo.Interactor.Process(1f/72); SettleReturn();
        Require(demo.Interactor.ActiveTarget==null,"User changes cannot carry grip"); demo.Pointer.SetUser("LocalUser");
        Pick(item); demo.Interactor.Process(.5f); Require(demo.Interactor.IsReturning,"Long pause cancels rather than deriving a jump"); SettleReturn();
        Pick(item); demo.Pointer.Submit(Vector3.zero,new Vector3(float.NaN,0,0),true,true); demo.Interactor.Process(1f/72); SettleReturn();
        Require(demo.Interactor.ActiveTarget==null,"Invalid tracking cancels");
        UnityEngine.Events.UnityAction cancelOnGrab=()=>demo.Interactor.CancelImmediately();
        item.Grabbed.AddListener(cancelOnGrab);
        demo.Pointer.Submit(home-Vector3.forward,home,true,false); demo.Interactor.Process(1f/72);
        demo.Pointer.Submit(home-Vector3.forward,home,true,true); demo.Interactor.Process(1f/72);
        Require(demo.Interactor.ActiveTarget==null && item.Owner==null,"Grab callback may immediately cancel"); item.Grabbed.RemoveListener(cancelOnGrab);
        Near(item.transform.position,home,.0001f,"Callback cancellation restores pose");
        demo.Pointer.Submit(home-Vector3.forward,home,true,false); demo.Interactor.Process(1f/72);
        demo.Pointer.Submit(home-Vector3.forward,home,true,true); demo.Interactor.Cancel(); demo.Interactor.Process(1f/72);
        Require(demo.Interactor.ActiveTarget==null,"Explicit cancel consumes a simultaneous fresh pickup gesture");
    }
    void ShapeAndFrameChecks()
    {
        var board=demo.Tabletop; var item=board.Pieces[0]; var root=board.transform;
        Vector3 position=root.position,scale=root.localScale; Quaternion rotation=root.rotation;
        var random=new System.Random(927);
        for(int frame=0;frame<4;frame++)
        {
            root.rotation=frame==0?Quaternion.identity:frame==1?Quaternion.Euler(180,0,0):Quaternion.Euler(27,39,-61);
            root.localScale=frame==3?new Vector3(-1.2f,.8f,1.6f):Vector3.one*(frame+1)*.65f;
            board.ResetPuzzle(); Physics.SyncTransforms(); Pick(item);
            for(int i=0;i<24;i++)
            {
                Vector3 local=new Vector3((float)random.NextDouble()*16-8,(float)random.NextDouble()*12-6,(float)random.NextDouble()*16-8);
                HoldTo(root.TransformPoint(local),3); Inside(item,"Full transformed volume remains inside");
            }
            demo.Interactor.CancelImmediately();
        }
        root.position=position; root.rotation=rotation; root.localScale=scale; board.ResetPuzzle();
        Pick(item); root.localScale*=1.1f; demo.Interactor.Process(1f/72); Require(demo.Interactor.IsReturning,"Mid-grip frame/scale changes cancel"); SettleReturn(); root.localScale=scale; board.ResetPuzzle();
        Pick(item); var size=item.Volume.size; item.Volume.size*=1.1f; demo.Interactor.Process(1f/72); Require(demo.Interactor.IsReturning,"Volume reconfiguration cancels"); SettleReturn(); item.Volume.size=size; board.ResetPuzzle();
        Pick(item); HoldTo(new Vector3(1e12f,1e12f,1e12f),12); Inside(item,"Astronomical logical input cannot escape workspace"); demo.Interactor.CancelImmediately();
        var body=item.gameObject.AddComponent<Rigidbody>(); body.isKinematic=false; body.useGravity=false;
        Vector3 home=item.transform.position;
        demo.Pointer.Submit(home-Vector3.forward,home,true,false); demo.Interactor.Process(1f/72);
        demo.Pointer.Submit(home-Vector3.forward,home,true,true); demo.Interactor.Process(1f/72);
        Require(demo.Interactor.ActiveTarget==null,"Dynamic physics body rejected"); DestroyImmediate(body);
        var b=item.Region.LocalBounds; item.Region.LocalBounds=new Bounds(Vector3.zero,Vector3.one*.01f);
        demo.Pointer.Submit(home-Vector3.forward,home,true,false); demo.Interactor.Process(1f/72);
        demo.Pointer.Submit(home-Vector3.forward,home,true,true); demo.Interactor.Process(1f/72);
        Require(demo.Interactor.ActiveTarget==null,"Object too large for region cannot be acquired"); item.Region.LocalBounds=b;
    }
    void TrajectoryChecks()
    {
        var item=demo.Tabletop.Pieces[0]; Vector3 start=item.transform.position;
        Vector3[] ends=new Vector3[3]; int[] rates={30,72,120};
        string csv="hz,x,y,z\n";
        for(int k=0;k<rates.Length;k++)
        {
            Pick(item); int hz=rates[k];
            for(int i=1;i<=hz;i++)
            {
                float t=(float)i/hz;
                // No snap lanes on this lateral, raised trajectory. Exact linear filter integration is comparable.
                Vector3 desired=start+item.Region.transform.TransformVector(new Vector3(.4f*t,.5f*t,.7f*t));
                Feed(desired,true,1f/hz);
            }
            ends[k]=item.Region.transform.InverseTransformPoint(item.transform.position);
            csv+=hz+","+ends[k].x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+ends[k].y.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+ends[k].z.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"\n";
            demo.Interactor.CancelImmediately();
        }
        Near(ends[0],ends[2],.01f,"30 versus 120Hz bounded drive"); Near(ends[1],ends[2],.01f,"72 versus 120Hz bounded drive");
        File.WriteAllText("HanoiCaptures/translation-rates.csv",csv);
        Pick(item);
        Vector3 end=demo.Tabletop.Slots[2].transform.position;
        Feed(end,true); Require(!demo.Interactor.ReadyToPlace,"One-frame distant command cannot instantly qualify a visibly distant piece");
        HoldTo(end); Require(demo.Interactor.ReadyToPlace,"Held settling reaches ready state");
        Vector3 offset=item.Region.transform.TransformVector(new Vector3(.28f,0,0));
        HoldTo(end+offset); Require(demo.Interactor.Candidate!=null && !demo.Interactor.ReadyToPlace,"Attraction alone cannot counterfeit final intent");
        demo.Interactor.CancelImmediately();
    }
    void HysteresisAndOffset()
    {
        var root=new GameObject("Generic placement fixture"); root.transform.position=new Vector3(8,0,0);
        var region=root.AddComponent<BirdPlacementRegion>(); region.LocalBounds=new Bounds(new Vector3(0,1.5f,0),new Vector3(10,3,10));
        var go=new GameObject("Offset item"); go.transform.SetParent(root.transform,false); go.transform.localPosition=new Vector3(-2,.6f,0);
        var shapeGo=new GameObject("Rotated offset box"); shapeGo.transform.SetParent(go.transform,false); shapeGo.transform.localPosition=new Vector3(.15f,.12f,0); shapeGo.transform.localRotation=Quaternion.Euler(15,27,13);
        var box=shapeGo.AddComponent<BoxCollider>(); box.size=new Vector3(.3f,.24f,.36f);
        var item=go.AddComponent<BirdGrabTarget>();
        var slots=new BirdSnapTarget[2];
        for(int i=0;i<2;i++)
        {
            var slotGo=new GameObject("Nearby slot "+i); slotGo.transform.SetParent(root.transform,false); slotGo.transform.localPosition=new Vector3(i==0?-.4f:.4f,1,0);
            slots[i]=slotGo.AddComponent<BirdSnapTarget>(); slots[i].influenceRadius=.5f; slots[i].exitMultiplier=1.6f;
        }
        item.Configure(box,region,slots); demo.Interactor.Configure(new[]{demo.Pointer},new[]{item}); Physics.SyncTransforms();
        Vector3 home=go.transform.position,grab=box.bounds.center,origin=grab-Vector3.forward*3;
        demo.Pointer.Submit(origin,grab,true,false); demo.Interactor.Process(1f/72);
        demo.Pointer.Submit(origin,grab,true,true); demo.Interactor.Process(1f/72);
        Require(demo.Interactor.ActiveTarget==item,"Offset child collider is selectable"); Near(go.transform.position,home,1e-6f,"Offset contact does not move pivot on acquisition");
        Action<Vector3,int> aim=(local,frames)=>
        {
            Vector3 point=grab+(root.transform.TransformPoint(local)-home);
            for(int i=0;i<frames;i++) { demo.Pointer.Submit(origin,point,true,true); demo.Interactor.Process(1f/72); }
        };
        aim(new Vector3(-.35f,1.5f,0),40); Require(demo.Interactor.Candidate==slots[0],"First approach lane acquired");
        aim(new Vector3(.15f,1.5f,0),40); Require(demo.Interactor.Candidate==slots[0],"Hysteresis retains lane even when neighbor becomes closer");
        aim(new Vector3(.85f,1.5f,0),40); Require(demo.Interactor.Candidate==slots[1],"Crossing release margin permits lane change");
        Inside(item,"Offset rotated volume is bounded using all corners");
        // A tiny positive timestep must not produce cancellation or an invalid transform.
        demo.Pointer.Submit(origin,grab+new Vector3(2,1,0),true,true); demo.Interactor.Process(1e-8f);
        Require(BirdPlacementRegion.Finite(item.transform.position) && demo.Interactor.ActiveTarget==item,"Tiny step keeps response finite");
        slots[1].influenceRadius=float.NaN; demo.Interactor.Process(1f/72); Require(demo.Interactor.Candidate!=slots[1],"Invalid slot parameters remove attraction");
        demo.Interactor.CancelImmediately();
        Vector3 far=grab+Vector3.forward*1000000;
        demo.Pointer.Submit(origin,far,true,false); demo.Interactor.Process(1f/72);
        demo.Pointer.Submit(origin,far,true,true); demo.Interactor.Process(1f/72);
        Require(demo.Interactor.ActiveTarget==item,"Point-through acquisition has no short ray cap"); Near(item.transform.position,home,.0001f,"Far endpoint retains hit offset on pickup");
        for(int i=0;i<50;i++) { demo.Pointer.Submit(origin,far+Vector3.up*.25f,true,true); demo.Interactor.Process(1f/72); }
        Near(item.transform.position,home+Vector3.up*.25f,.0001f,"Relative displacement preserves off-object grab offset");
        demo.Interactor.CancelImmediately();
        // A point inside a rear target must not outrank an earlier ray intersection.
        var rearGo=new GameObject("Rear item"); rearGo.transform.SetParent(root.transform,false); rearGo.transform.position=grab+Vector3.forward;
        var rearBox=rearGo.AddComponent<BoxCollider>(); rearBox.size=Vector3.one*.2f; var rear=rearGo.AddComponent<BirdGrabTarget>(); rear.Configure(rearBox,region,slots);
        demo.Interactor.Configure(new[]{demo.Pointer},new[]{rear,item}); Physics.SyncTransforms();
        demo.Pointer.Submit(origin,rearGo.transform.position,true,false); demo.Interactor.Process(1f/72);
        demo.Pointer.Submit(origin,rearGo.transform.position,true,true); demo.Interactor.Process(1f/72);
        Require(demo.Interactor.ActiveTarget==item,"Nearest hit wins even if endpoint is inside farther volume");
        demo.Interactor.CancelImmediately(); DestroyImmediate(root);
        var all=new System.Collections.Generic.List<BirdGrabTarget>(); all.AddRange(demo.Tabletop.Pieces); all.AddRange(demo.Buildings.Pieces);
        demo.Interactor.Configure(new[]{demo.Pointer,demo.OtherPointer},all.ToArray());
    }
    void ReturnAndFeedback()
    {
        demo.ResetPuzzles(); Move(demo.Tabletop,0,2);
        var item=demo.Tabletop.Pieces[0]; Vector3 home=item.transform.position;
        Pick(item); HoldTo(home+new Vector3(-.5f,.8f,.4f));
        demo.Interactor.Cancel(); int moves=demo.Tabletop.Moves; demo.Tabletop.ResetPuzzle();
        Require(demo.Tabletop.Moves==moves && demo.Tabletop.Location(0)==2,"Board cannot reset underneath a returning piece");
        float previous=Vector3.Distance(item.transform.position,home);
        for(int i=0;i<25;i++)
        {
            demo.Interactor.Process(1f/72); float distance=Vector3.Distance(item.transform.position,home);
            Require(distance<=previous+.00001f,"Rollback converges without overshooting"); Inside(item,"Return path remains bounded"); previous=distance;
        }
        Near(item.transform.position,home,.00001f,"Rollback reaches committed dock exactly");
        var feedback=item.GetComponent<BirdGrabFeedback>(); var visual=feedback.visual; feedback.enabled=false;
        var original=new MaterialPropertyBlock(); original.SetFloat("_BirdFixture",.37f); original.SetColor("_Color",Color.magenta); visual.SetPropertyBlock(original);
        feedback.enabled=true; feedback.Refresh(); feedback.enabled=false;
        var restored=new MaterialPropertyBlock(); visual.GetPropertyBlock(restored);
        Require(restored.GetColor("_Color")==Color.magenta && Mathf.Abs(restored.GetFloat("_BirdFixture")-.37f)<.0001f,"Feedback restores preexisting renderer properties");
        feedback.Refresh(); visual.GetPropertyBlock(restored); Require(restored.GetColor("_Color")==Color.magenta,"Disabled feedback cannot be reapplied manually");
        visual.SetPropertyBlock(null); feedback.enabled=true; feedback.Refresh();
        var substitute=demo.Tabletop.Pieces[1].transform.Find("Building volume").GetComponent<Renderer>(); feedback.visual=substitute; feedback.Refresh();
        visual.GetPropertyBlock(restored); Require(restored.isEmpty,"Changing renderer restores previously bound visual");
        feedback.enabled=false; feedback.visual=visual; feedback.enabled=true;
        demo.ResetPuzzles();
    }
    void Serialization()
    {
        var holder=new GameObject("Serialized interaction");
        var region=holder.AddComponent<BirdPlacementRegion>();
        var item=new GameObject("Item"); item.transform.SetParent(holder.transform,false); item.transform.localPosition=Vector3.up;
        var shape=item.AddComponent<BoxCollider>(); shape.size=Vector3.one*.1f;
        var target=item.AddComponent<BirdGrabTarget>(); var slot=new GameObject("Slot").AddComponent<BirdSnapTarget>(); slot.transform.SetParent(holder.transform,false); slot.transform.localPosition=Vector3.up;
        target.Configure(shape,region,new[]{slot});
        var receiver=holder.AddComponent<UnityHanoiEventReceiver>();
        UnityEventTools.AddPersistentListener(target.Placed,receiver.Record);
        PrefabUtility.SaveAsPrefabAsset(holder,"Assets/HanoiAuthoring.prefab"); DestroyImmediate(holder);
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HanoiAuthoring.prefab"));
        var restored=instance.GetComponentInChildren<BirdGrabTarget>();
        Require(restored.Region==instance.GetComponent<BirdPlacementRegion>() && restored.Destinations.Length==1,"Prefab restores component references");
        var pointer=instance.AddComponent<BirdPointerInput>(); var driver=instance.AddComponent<BirdGrabInteractor>(); driver.automatic=false;
        driver.Configure(new[]{pointer},new[]{restored}); Physics.SyncTransforms();
        Vector3 point=restored.transform.position,origin=point-Vector3.forward;
        pointer.Submit(origin,point,true,false); driver.Process(1f/72);
        pointer.Submit(origin,point,true,true); driver.Process(1f/72); driver.Process(1f/72);
        pointer.Submit(origin,point,true,false); driver.Process(1f/72); driver.Process(1f/72);
        Require(instance.GetComponent<UnityHanoiEventReceiver>().count==1,"Real grip/drop invokes remapped Inspector UnityEvent once after prefab roundtrip"); DestroyImmediate(instance);
    }
    void Capture(string name)
    {
        demo.UpdateFeedback();
        var rt=new RenderTexture(1440,1000,24){antiAliasing=4}; var pixels=new Texture2D(1440,1000,TextureFormat.RGB24,false);
        var old=RenderTexture.active; var oldTarget=demo.View.targetTexture;
        try
        {
            demo.View.targetTexture=rt; demo.View.Render(); RenderTexture.active=rt;
            pixels.ReadPixels(new Rect(0,0,1440,1000),0,0); pixels.Apply();
            int colorful=0; foreach(var p in pixels.GetPixels32()) if(p.r>180 && p.g>70 && p.b<130) colorful++;
            Require(colorful>50,"Camera capture contains visible orange tower pixels");
            File.WriteAllBytes("HanoiCaptures/"+name+".png",pixels.EncodeToPNG()); captures++;
        }
        finally { demo.View.targetTexture=oldTarget; RenderTexture.active=old; rt.Release(); Destroy(rt); Destroy(pixels); }
    }
}
#endif
