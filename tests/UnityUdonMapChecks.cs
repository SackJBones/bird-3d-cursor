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

// Proxy configuration is confined to authoring; play-mode checks use backing VMs.
public class UnityUdonMapChecks : MonoBehaviour
{
    const string ScenePath="Assets/BirdWorld/Scenes/BirdMapDemo.unity", Active="Bird.Udon.Map.Checks";
    static UdonBehaviour left,right,router,zoom,scroll,station,panel,root;
    static readonly Vector3 Origin=new Vector3(0,1.65f,0), Center=new Vector3(-2.3f,1.75f,4);
    static int checks;
    static double deadline;
    int stage;
    float started,held;
    Quaternion released;
    public static void Run()
    {
        File.WriteAllText("udon-map-result.txt","PENDING"); Compile(); EditorSceneManager.OpenScene(ScenePath);
        SessionState.SetBool(Active,true); EditorApplication.isPlaying=true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Begin() { if(SessionState.GetBool(Active,false)) new GameObject("Map VM checks").AddComponent<UnityUdonMapChecks>(); }
    void Update()
    {
        if(!SessionState.GetBool(Active,false)) return;
        if(deadline==0) deadline=EditorApplication.timeSinceStartup+90;
        if(EditorApplication.timeSinceStartup>deadline) { Finish(false,"ClientSim/frame-sequence timeout"); return; }
        if(!Utilities.IsValid(Networking.LocalPlayer) || Time.timeSinceLevelLoad<2) return;
        try
        {
            if(!Frames()) return;
            Contracts(); Finish(true,checks+" compiled-Udon map assertions; normal-frame menu/zoom/clutch/rotate/reset/back/loss and range-law, rates, ownership, frame, volume and callback contracts; five captures. Synthetic input, not client/headset or multiplayer validation.");
        }
        catch(Exception e) { Finish(false,e.ToString()); }
    }
    bool Frames()
    {
        if(stage==0)
        {
            left=Vm<BirdUiPointer>("UI Left"); right=Vm<BirdUiPointer>("UI Right"); router=Vm<BirdUiRouter>("UI Router");
            zoom=Vm<BirdUiRangeScale>("Map Sphere"); scroll=Vm<BirdUiSphericalScroll>("Map Sphere"); station=Vm<BirdMapStation>("Map Station");
            panel=Vm<BirdUiPanel>("Map Panel"); root=Vm<BirdUiPanel>("UI Root Panel");
            Vm<BirdUiDesktopInput>("UI Desktop Input").enabled=false;
            Feed(Origin); stage++; return false;
        }
        if(stage==1) { Aim("UI Open"); stage++; return false; }
        if(stage==2) { Assert(Number(root,"state")==1,"Normal reach-through opens root"); Aim("UI Map"); stage++; return false; }
        if(stage==3) { Aim("UI Map",true); stage++; return false; }
        if(stage==4)
        {
            Assert(Number(panel,"state")==1 && Number(root,"state")==2,"Map branch receives foreground focus");
            foreach(string name in new[]{"UI Map","Map Rotate","Map Zoom","Map Reset","Map Back"})
            {
                Transform label=FindScene(name+" label").transform;
                var visual=FindScene(name).GetComponent<BirdUiVisual>();
                Transform artwork=visual==null?FindScene(name).transform:(Transform)UdonSharpEditorUtility.GetBackingUdonBehaviour(visual).GetProgramVariable("visualRoot");
                Assert(label.parent==artwork && Vector3.Distance(label.position,artwork.position)<.1f,"Reloaded world-space label remains on its assigned artwork: "+name);
            }
            Aim("Map Zoom"); stage++; return false;
        }
        if(stage==5) { Aim("Map Zoom",true); stage++; return false; }
        if(stage==6) { Assert(Flag(station,"zoomMode") && Flag(zoom,"scalingEnabled") && !scroll.enabled,"Serialized zoom action selects exclusive mode: mode="+Flag(station,"zoomMode")+" armed="+Flag(zoom,"scalingEnabled")+" rotation="+scroll.enabled+" calls="+Number(Vm<BirdUiAction>("Map Zoom"),"invocationCount")+" dt="+zoom.GetProgramVariable("stepDelta")); Feed(RangePoint(6)); stage++; return false; }
        if(stage==7) { Assert(Ref(zoom,"activePointer")==left,"Normal frame acquires zoom"); started=Time.unscaledTime; stage++; }
        if(stage==8)
        {
            float t=Mathf.Min(1,Time.unscaledTime-started); Feed(RangePoint(6*Mathf.Exp(.7f*t)));
            if(t<1) return false;
            held=Factor(); Assert(held>2.1f && held<=2.3001f,"Normal exponential reach enlarges bounded map"); Feed(Origin); stage++; return false;
        }
        if(stage==9) { Assert(Ref(zoom,"activePointer")==null && Mathf.Abs(Factor()-held)<1e-5f,"Withdrawal immediately freezes size"); Feed(RangePoint(7)); stage++; return false; }
        if(stage==10) { Near(Factor(),held,1e-5f,"Reentry rebases without jump"); Aim("Map Reset"); stage++; return false; }
        if(stage==11) { Aim("Map Reset",true); stage++; return false; }
        if(stage==12) { Near(Factor(),1,1e-5f,"Authored Reset restores size"); Assert(!Flag(station,"zoomMode") && scroll.enabled,"Reset returns to rotation"); Feed(Center); stage++; return false; }
        if(stage==13) { Assert(Ref(scroll,"activePointer")==null,"Map interior does not drive rotation"); Feed(RangePoint(7)); started=Time.unscaledTime; stage++; return false; }
        if(stage==14)
        {
            float t=Mathf.Min(.6f,Time.unscaledTime-started); Feed(Origin+(Center+Vector3.right*t-Origin).normalized*7);
            if(t<.6f) return false;
            Assert(((Vector3)scroll.GetProgramVariable("angularVelocity")).magnitude>1,"Far-side flick builds momentum");
            Feed(Center); released=Target().parent.localRotation; started=Time.unscaledTime; stage++; return false;
        }
        if(stage==15)
        {
            Feed(Center); if(Time.unscaledTime-started<.25f) return false;
            Assert(Quaternion.Angle(released,Target().parent.localRotation)>.5f && Ref(scroll,"activePointer")==null,"Withdrawal inside coasts without driving"); Aim("Map Back"); stage++; return false;
        }
        if(stage==16) { Aim("Map Back",true); stage++; return false; }
        if(stage==17) { Assert(Number(panel,"state")==0 && Number(root,"state")==1,"Back closes map and restores root"); Assert(!Flag(zoom,"scalingEnabled") && ((Vector3)scroll.GetProgramVariable("angularVelocity")).sqrMagnitude==0,"Closed map stops both modes"); left.SendCustomEvent("Cancel"); stage++; return false; }
        if(stage==18) { Assert(Number(root,"state")==0,"Tracking loss closes owned menu"); return true; }
        return false;
    }
    static void Contracts()
    {
        router.SetProgramVariable("automatic",false); zoom.SetProgramVariable("automatic",false); scroll.SetProgramVariable("automatic",false);
        Vm<BirdObjectGrip>("Hanoi Grip").SetProgramVariable("automatic",false);
        root.SetProgramVariable("owner","LocalUser"); root.SetProgramVariable("state",2); root.SetProgramVariable("child",panel); panel.SetProgramVariable("owner","LocalUser"); panel.SetProgramVariable("state",1);
        ((GameObject)root.GetProgramVariable("content")).SetActive(true);
        ((GameObject)panel.GetProgramVariable("content")).SetActive(true); station.SendCustomEvent("ZoomMap");
        var grip=Vm<BirdObjectGrip>("Hanoi Grip"); Vector3 piece=Vm<BirdObjectTarget>("Hanoi Table Piece 0").transform.position;
        Feed(piece); grip.SendCustomEvent("Process"); Feed(piece,true); grip.SendCustomEvent("Process");
        Assert(Ref(grip,"ActiveTarget")==null,"Foreground map blocks world gripping off its controls");
        zoom.SetProgramVariable("minimumFactor",.1f); zoom.SetProgramVariable("maximumFactor",16f); zoom.SetProgramVariable("response",0f);
        ResetZoom(); Sample(6); foreach(float ratio in new[]{1f,1.2f,2f,3f,.8f}) { Sample(6*ratio); Near(Factor(),ratio*ratio,.00003f,"Compiled legacy squared law"); }
        float held=Factor(); Sample(2); Assert(Ref(zoom,"activePointer")==null && Factor()==held,"Finite segment cannot engage before sphere");
        Sample(7); Near(Factor(),held,.00001f,"Compiled clutch no jump"); Sample(14); Near(Factor(),held*4,.00005f,"Clutch uses displayed size");
        Sample(1e12f); Near(Factor(),16,.00001f,"Huge finite point bounds view only"); Assert(((Vector3)left.GetProgramVariable("position")-Origin).magnitude>1e11f,"Logical point unchanged");
        left.SendCustomEvent("Cancel"); Step(); held=Factor(); Assert(Ref(zoom,"activePointer")==null,"Loss releases owner");
        Sample(6); Step(); Step(); Near(Factor(),held,.00001f,"Repeated render of recovery sample does not repeatedly clutch"); Sample(5); Assert(Factor()<held,"Recovered sample can drive later movement");
        Feed(RangePoint(7),false,right); Step(); Assert(Ref(zoom,"activePointer")==left,"Second hand cannot steal");
        left.SendCustomEvent("Cancel"); Step(); Step(); Assert(Ref(zoom,"activePointer")==right,"Other hand acquires after owner loss");
        right.SendCustomEvent("Cancel"); left.SetProgramVariable("userId","Other"); Sample(6); Assert(Ref(zoom,"activePointer")==null,"Other user rejected by focus gate"); left.SetProgramVariable("userId","LocalUser");
        ResetZoom(); Sample(6); zoom.SetProgramVariable("stepDelta",.3f); zoom.SendCustomEvent("Process"); Assert(!Flag(zoom,"scalingEnabled"),"Long frame disarms");
        ResetZoom(); Sample(6); zoom.enabled=false; Assert(Ref(zoom,"activePointer")==null && !Flag(zoom,"scalingEnabled"),"Disable cancels immediately"); zoom.enabled=true;
        ResetZoom(); Sample(6); Vector3 rest=Target().localScale; Target().localScale*=2; Step(); Assert(!Flag(zoom,"scalingEnabled"),"External scale writer disarms"); Target().localScale=rest;
        ResetZoom(); Sample(6); var rotation=Target().parent; Quaternion old=rotation.localRotation; rotation.localRotation*=Quaternion.Euler(0,10,0); Step(); Assert(Ref(zoom,"activePointer")==null,"Changed parent frame releases cached gesture"); rotation.localRotation=old;
        ResetZoom(); Sample(6); panel.SetProgramVariable("state",2); Step(); Assert(Ref(zoom,"activePointer")==null,"Background map cannot drive"); panel.SetProgramVariable("state",1);
        ResetZoom(); zoom.SetProgramVariable("eventTarget",zoom); zoom.SetProgramVariable("startedEvent","Cancel"); Sample(6); Assert(!Flag(zoom,"scalingEnabled") && Ref(zoom,"activePointer")==null,"Started callback can cancel reentrantly"); zoom.SetProgramVariable("eventTarget",null); zoom.SetProgramVariable("startedEvent","");
        ResetZoom(); Sample(6); zoom.SetProgramVariable("eventTarget",zoom); zoom.SetProgramVariable("changedEvent","StopScaling"); Sample(12); Assert(!Flag(zoom,"scalingEnabled") && Ref(zoom,"activePointer")==null,"Changed callback can stop safely"); zoom.SetProgramVariable("eventTarget",null); zoom.SetProgramVariable("changedEvent","");
        ResetZoom(); Sample(6); Step(float.NaN); Assert(!Flag(zoom,"scalingEnabled"),"Nonfinite time disarms");
        ResetZoom(); Sample(6); Feed(new Vector3(float.NaN,0,0)); Step(); Assert(Ref(zoom,"activePointer")==null,"Invalid sample cancels contact");
        zoom.SetProgramVariable("minimumFactor",0f); zoom.SendCustomEvent("ResetScale"); zoom.SendCustomEvent("StartScaling"); Assert(!Flag(zoom,"scalingEnabled") && !float.IsNaN(Factor()),"Invalid limits cannot arm or corrupt reset"); zoom.SetProgramVariable("minimumFactor",.1f);
        var collider=(Collider)zoom.GetProgramVariable("engagementVolume"); collider.enabled=false; ResetZoom(); Sample(6); Assert(Ref(zoom,"activePointer")==null,"Disabled engagement shape rejects input"); collider.enabled=true;
        Transform savedParent=collider.transform.parent; collider.transform.SetParent(Target(),true); ResetZoom(); Sample(6); Assert(Ref(zoom,"activePointer")==null,"Scaled-target-owned gate rejected"); collider.transform.SetParent(savedParent,true);
        Rates(); TransformedFrames();
        zoom.SendCustomEvent("Cancel"); zoom.SetProgramVariable("minimumFactor",.3f); zoom.SetProgramVariable("maximumFactor",2.3f); zoom.SetProgramVariable("response",12f); station.SendCustomEvent("ResetMap");
        Capture("map-verified"); CaptureStates();
    }
    static void Rates()
    {
        var factors=new List<float>(); var csv=new List<string>{"hz,factor"}; zoom.SetProgramVariable("response",12f);
        foreach(int hz in new[]{30,72,120})
        {
            ResetZoom(); Sample(6,1f/hz);
            for(int i=1;i<=hz;i++) Sample(6*Mathf.Exp(.6f*i/hz),1f/hz);
            float expected=Mathf.Exp(1.2f*(1-(1-Mathf.Exp(-12))/12)); Near(Factor(),expected,.0001f,"Compiled analytic log follower at "+hz+" Hz");
            factors.Add(Factor()); csv.Add(hz+","+Factor().ToString("R",System.Globalization.CultureInfo.InvariantCulture));
        }
        Near(factors[0],factors[2],.00002f,"30/120 Hz equivalence"); Near(factors[1],factors[2],.00002f,"72/120 Hz equivalence");
        Directory.CreateDirectory("../Validation/UdonMap"); File.WriteAllLines("../Validation/UdonMap/range-scale-rates.csv",csv);
        zoom.SetProgramVariable("response",0f);
        Transform target=Target();
        zoom.SendCustomEvent("Cancel"); target.localScale=new Vector3(-.01f,.02f,.03f); zoom.SendCustomEvent("Initialize"); zoom.SendCustomEvent("StartScaling"); Sample(6); Sample(12);
        Assert(Vector3.Distance(target.localScale,new Vector3(-.04f,.08f,.12f))<1e-6f,"Compiled factor preserves mirrored nonuniform baseline");
        zoom.SendCustomEvent("Cancel"); target.localScale=Vector3.one; zoom.SendCustomEvent("Initialize");
    }
    static void ResetZoom() { right.SendCustomEvent("Cancel"); zoom.SendCustomEvent("ResetScale"); zoom.SendCustomEvent("StartScaling"); }
    static void TransformedFrames()
    {
        Transform frame=FindScene("Hanoi Menu Offset").transform; Vector3 position=frame.position,scale=frame.localScale; Quaternion rotation=frame.rotation;
        foreach(Quaternion turn in new[]{Quaternion.identity,Quaternion.Euler(0,0,180),Quaternion.Euler(17,65,31)})
        foreach(Vector3 size in new[]{Vector3.one,new Vector3(-2,.5f,1.3f)})
        {
            zoom.SendCustomEvent("Cancel"); frame.position=new Vector3(3,-2,7); frame.rotation=turn; frame.localScale=size; Physics.SyncTransforms(); ResetZoom();
            Vector3 center=zoom.transform.position,axis=frame.forward,origin=center-axis*6;
            left.SetProgramVariable("sampleOrigin",origin); left.SetProgramVariable("samplePosition",center+axis*2); left.SetProgramVariable("sampleTracked",true); left.SetProgramVariable("samplePressed",false); left.SendCustomEvent("Submit"); Step();
            left.SetProgramVariable("samplePosition",center+axis*6); left.SendCustomEvent("Submit"); Step(); Near(Factor(),2.25f,.00003f,"Compiled rigid/mirrored/nonuniform range ratio");
        }
        zoom.SendCustomEvent("Cancel"); frame.position=position; frame.rotation=rotation; frame.localScale=scale; Physics.SyncTransforms();
    }
    static Transform Target() { return (Transform)zoom.GetProgramVariable("scaleTarget"); }
    static Vector3 RangePoint(float range) { return Origin+(Center-Origin).normalized*range; }
    static void Sample(float range,float dt=1f/72) { Feed(RangePoint(range)); Step(dt); }
    static void Step(float dt=1f/72) { zoom.SetProgramVariable("stepDelta",dt); zoom.SendCustomEvent("Process"); }
    static void Feed(Vector3 p,bool pressed=false,UdonBehaviour pointer=null)
    { if(pointer==null) pointer=left; pointer.SetProgramVariable("sampleOrigin",Origin); pointer.SetProgramVariable("samplePosition",p); pointer.SetProgramVariable("sampleTracked",true); pointer.SetProgramVariable("samplePressed",pressed); pointer.SendCustomEvent("Submit"); }
    static void Aim(string name,bool pressed=false) { Vector3 p=FindScene(name).transform.position; Feed(Origin+(p-Origin)*1.04f,pressed); }
    static float Factor() { return (float)zoom.GetProgramVariable("factor"); }
    static bool Flag(UdonBehaviour vm,string key) { return (bool)vm.GetProgramVariable(key); }
    static int Number(UdonBehaviour vm,string key) { return (int)vm.GetProgramVariable(key); }
    static UdonBehaviour Ref(UdonBehaviour vm,string key) { return (UdonBehaviour)vm.GetProgramVariable(key); }
    static void Near(float a,float b,float tolerance,string message) { Assert(Mathf.Abs(a-b)<=tolerance,message+": "+a+" vs "+b); }
    static void Assert(bool value,string message) { checks++; if(!value) throw new Exception("Assertion "+checks+": "+message); }
    static void Finish(bool ok,string message) { SessionState.SetBool(Active,false); File.WriteAllText("udon-map-result.txt",(ok?"PASS: ":"FAIL: ")+message); EditorApplication.Exit(ok?0:1); }
    static UdonBehaviour Vm<T>(string name) where T:UdonSharpBehaviour { return UdonSharpEditorUtility.GetBackingUdonBehaviour(FindScene(name).GetComponent<T>()); }
    static GameObject FindScene(string name) { foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>()) if(go.scene.IsValid() && go.name==name) return go; throw new Exception("Missing "+name); }
    static void Capture(string name)
    {
        station.SendCustomEvent("Refresh"); router.SendCustomEvent("Process"); Canvas.ForceUpdateCanvases(); Directory.CreateDirectory("../Validation/UdonMap");
        var layers=new Dictionary<Transform,int>(); foreach(Transform item in FindScene("Hanoi Menu Offset").GetComponentsInChildren<Transform>(true)) { layers[item]=item.gameObject.layer; item.gameObject.layer=30; }
        var camera=new GameObject("Map evidence camera").AddComponent<Camera>(); camera.transform.position=new Vector3(-2.65f,1.65f,-.4f); camera.transform.LookAt(new Vector3(-2.65f,1.75f,4)); camera.fieldOfView=55; camera.cullingMask=1<<30;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.04f,.07f,.11f);
        var texture=new RenderTexture(1400,1100,24) { antiAliasing=4 }; var pixels=new Texture2D(1400,1100,TextureFormat.RGB24,false);
        camera.targetTexture=texture; camera.Render(); RenderTexture.active=texture; pixels.ReadPixels(new Rect(0,0,1400,1100),0,0); pixels.Apply(); File.WriteAllBytes("../Validation/UdonMap/"+name+".png",pixels.EncodeToPNG());
        RenderTexture.active=null; camera.targetTexture=null; texture.Release(); Destroy(camera.gameObject); Destroy(texture); Destroy(pixels); foreach(var pair in layers) pair.Key.gameObject.layer=pair.Value;
    }
    static void CaptureStates()
    {
        // Rendering/PNG encoding can block >250 ms. Keep it out of the normal-frame
        // sequence so the intentional pause cancellation is not a capture artifact.
        Capture("map-open"); station.SendCustomEvent("ZoomMap"); Sample(6); for(int i=0;i<100;i++) Sample(12); Capture("map-zoomed");
        station.SendCustomEvent("ResetMap"); scroll.SetProgramVariable("stepDelta",1f/72);
        for(int i=0;i<45;i++) { Feed(Origin+(Center+Vector3.right*(i*.013f)-Origin).normalized*7); scroll.SendCustomEvent("Process"); }
        Capture("map-rotated"); panel.SendCustomEvent("Close"); Capture("map-back");
    }
    public static void Generate()
    {
        if(File.Exists(ScenePath)) throw new Exception("Refusing to overwrite authored map scene."); Compile();
        var scene=EditorSceneManager.OpenScene("Assets/BirdWorld/Scenes/BirdHanoiDemo.unity");
        var parent=FindScene("Hanoi Menu Offset").transform; var rootPanel=FindScene("UI Root Panel").GetComponent<BirdUiPanel>();
        var mapPanel=new GameObject("Map Panel").AddUdonSharpComponent<BirdUiPanel>(); mapPanel.transform.SetParent(parent,true); mapPanel.parent=rootPanel;
        var content=new GameObject("Map content").transform; content.SetParent(parent,true); mapPanel.content=content.gameObject;
        var pointers=new[]{FindScene("UI Left").GetComponent<BirdUiPointer>(),FindScene("UI Right").GetComponent<BirdUiPointer>()};
        var sphereObject=new GameObject("Map Sphere"); sphereObject.transform.SetParent(content,true); sphereObject.transform.position=Center;
        var sphere=sphereObject.AddComponent<SphereCollider>(); sphere.radius=1.05f; sphere.isTrigger=true;
        var rotation=new GameObject("Map rotation pivot").transform; rotation.SetParent(content,true); rotation.position=Center; rotation.localRotation=Quaternion.Euler(-30,-20,0);
        var map=new GameObject("Map scale pivot").transform; map.SetParent(rotation,false); BuildMap(map);
        var spin=sphereObject.AddUdonSharpComponent<BirdUiSphericalScroll>(); spin.sphere=sphere; spin.rotationTarget=rotation; spin.pointers=pointers; spin.panel=mapPanel;
        var scale=sphereObject.AddUdonSharpComponent<BirdUiRangeScale>(); scale.engagementVolume=sphere; scale.scaleTarget=map; scale.pointers=pointers; scale.panel=mapPanel; scale.minimumFactor=.3f; scale.maximumFactor=2.3f;
        var controls=new GameObject("Map Station").AddUdonSharpComponent<BirdMapStation>(); controls.transform.SetParent(content,true); controls.zoom=scale; controls.rotation=spin; controls.rotationTarget=rotation;
        controls.label=Label("Map instructions",content,new Vector3(-2.3f,3.05f,4),"ROTATE / point through the BACK",1100,110,.0016f);
        FindScene("UI title").transform.position=new Vector3(-2.3f,3.45f,4); FindScene("UI title").GetComponentInChildren<Text>().text="BIRD / COLORS + MAP + HANOI";
        var route=FindScene("UI Router").GetComponent<BirdUiRouter>(); var elements=new List<BirdUiElement>(route.elements);
        elements.Add(Button("UI Map",rootPanel.content.transform,new Vector3(-2.3f,1.65f,2.8f),rootPanel,BirdUiActionKind.OpenPanel,mapPanel,"MAP"));
        string[] labels={"ROTATE","ZOOM","RESET","BACK"}, events={"RotateMap","ZoomMap","ResetMap",""};
        for(int i=0;i<4;i++)
        {
            var button=Button("Map "+(i==0?"Rotate":i==1?"Zoom":i==2?"Reset":"Back"),content,new Vector3(-3.5f+i*.8f,.55f,3.5f),mapPanel,i==3?BirdUiActionKind.ClosePanel:BirdUiActionKind.SendEvent,mapPanel,labels[i]);
            if(i!=3) { button.GetComponent<BirdUiAction>().callback=UdonSharpEditorUtility.GetBackingUdonBehaviour(controls); button.GetComponent<BirdUiAction>().callbackEvent=events[i]; }
            elements.Add(button);
        }
        route.elements=elements.ToArray();
        for(int plane=0;plane<3;plane++)
        {
            var line=new GameObject("Fixed map sphere guide").AddComponent<LineRenderer>(); line.transform.SetParent(sphereObject.transform,false); line.useWorldSpace=false; line.loop=true; line.positionCount=96;
            line.startWidth=line.endWidth=.004f; line.sharedMaterial=Material("Guide",new Color(.13f,.3f,.4f));
            for(int i=0;i<96;i++) { float a=i*Mathf.PI*2/96,x=Mathf.Cos(a)*sphere.radius,y=Mathf.Sin(a)*sphere.radius; line.SetPosition(i,plane==0?new Vector3(x,y,0):plane==1?new Vector3(x,0,y):new Vector3(0,x,y)); }
        }
        RefineVisuals();
        foreach(var proxy in FindObjectsOfType<UdonSharpBehaviour>(true)) UdonSharpEditorUtility.CopyProxyToUdon(proxy);
        content.gameObject.SetActive(false); Physics.SyncTransforms(); if(!EditorSceneManager.SaveScene(scene,ScenePath)) throw new Exception("Scene save failed"); AssetDatabase.SaveAssets();
        File.WriteAllText("udon-map-generate-result.txt","PASS: authored map branch alongside colors and paired Hanoi; fixed gesture volume, distinct rotation/scale pivots, serialized local actions."); EditorApplication.Exit(0);
    }
    static BirdUiElement Button(string name,Transform parent,Vector3 position,BirdUiPanel panel,BirdUiActionKind kind,BirdUiPanel destination,string label)
    {
        var go=new GameObject(name); go.transform.SetParent(parent,true); go.transform.position=position;
        var volume=go.AddComponent<BoxCollider>(); volume.size=new Vector3(.72f,.24f,.1f); volume.isTrigger=true;
        var visual=Shape("Button visual",go.transform,Vector3.zero,volume.size,Material("Button",new Color(.1f,.22f,.3f)));
        var element=go.AddUdonSharpComponent<BirdUiElement>(); element.target=volume; element.panel=panel; element.feedback=visual.GetComponent<Renderer>();
        var action=go.AddUdonSharpComponent<BirdUiAction>(); action.element=element; action.action=kind; action.panel=destination; element.actionTarget=UdonSharpEditorUtility.GetBackingUdonBehaviour(action);
        Label(name+" label",go.transform,position-Vector3.forward*.065f,label,400,95,.0015f); return element;
    }
    public static void RefineLayout()
    {
        var scene=EditorSceneManager.OpenScene(ScenePath); RefineVisuals(); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        File.WriteAllText("udon-map-layout-result.txt","PASS: parent controls moved beside map; child labels enlarged."); EditorApplication.Exit(0);
    }
    static void RefineVisuals()
    {
        string[] names={"UI Colors","UI Map","UI Close","Hanoi Reset"};
        for(int i=0;i<names.Length;i++) FindScene(names[i]).transform.position=new Vector3(-4.05f,2.3f-i*.5f,3.5f);
        FindScene("UI Result").transform.position=new Vector3(-4.05f,2.85f,3.5f);
        foreach(string name in new[]{"UI Map","Map Rotate","Map Zoom","Map Reset","Map Back"})
        {
            var label=FindScene(name+" label").GetComponent<RectTransform>(); label.localScale=Vector3.one*.0015f;
            label.sizeDelta=new Vector2(400,95); label.anchoredPosition3D=new Vector3(0,0,-.065f);
            var text=label.GetComponentInChildren<Text>(); text.rectTransform.sizeDelta=new Vector2(400,95); text.fontSize=36;
        }
        var resetLabel=FindScene("Reset puzzles label").GetComponent<RectTransform>(); resetLabel.anchoredPosition3D=resetLabel.parent.InverseTransformVector(Vector3.back*.05f);
    }
    static void BuildMap(Transform parent)
    {
        var water=Material("Water",new Color(.05f,.3f,.48f)); var land=Material("Land",new Color(.28f,.55f,.44f)); var roof=Material("Roof",new Color(.67f,.77f,.59f)); var street=Material("Street",new Color(.08f,.18f,.23f));
        Shape("Map water",parent,new Vector3(0,-.04f,0),new Vector3(.82f,.035f,.82f),water);
        for(int x=0;x<6;x++) for(int z=0;z<6;z++)
        {
            if(x+z<2) continue; float h=.035f+((x*3+z*7)%5)*.018f; Vector3 p=new Vector3((x-2.5f)*.12f,h*.5f,(z-2.5f)*.12f);
            Shape("Map block",parent,p,new Vector3(.096f,h,.096f),land); Shape("Map roof",parent,p+Vector3.up*(h*.5f+.006f),new Vector3(.101f,.01f,.101f),roof);
        }
        for(int i=0;i<5;i++)
        {
            float offset=(i-2)*.12f; Shape("Street",parent,new Vector3(offset,-.014f,0),new Vector3(.012f,.008f,.72f),street);
            for(int x=0;x<6;x++) Shape("Street",parent,new Vector3((x-2.5f)*.12f,-.014f,offset),new Vector3(.104f,.008f,.012f),street);
        }
        Shape("North marker",parent,new Vector3(0,.03f,.385f),new Vector3(.025f,.025f,.06f),Material("North",Color.white));
    }
    static GameObject Shape(string name,Transform parent,Vector3 position,Vector3 size,Material material)
    { var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(parent,false); go.transform.localPosition=position; go.transform.localScale=size; go.GetComponent<Renderer>().sharedMaterial=material; DestroyImmediate(go.GetComponent<Collider>()); return go; }
    static Material Material(string name,Color color)
    { string path="Assets/BirdWorld/Materials/Map"+name+".mat"; var material=AssetDatabase.LoadAssetAtPath<Material>(path); if(material==null) { material=new Material(Shader.Find("Unlit/Color")) { color=color }; AssetDatabase.CreateAsset(material,path); } return material; }
    static Text Label(string name,Transform parent,Vector3 position,string value,int width,int height,float scale)
    {
        var canvas=new GameObject(name,typeof(Canvas)); canvas.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace; canvas.transform.position=position; canvas.transform.localScale=Vector3.one*scale; canvas.transform.SetParent(parent,true);
        var rect=canvas.GetComponent<RectTransform>(); rect.sizeDelta=new Vector2(width,height); var text=new GameObject("Text",typeof(Text)).GetComponent<Text>(); text.transform.SetParent(canvas.transform,false);
        text.rectTransform.sizeDelta=rect.sizeDelta; text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize=30; text.alignment=TextAnchor.MiddleCenter; text.color=Color.white; text.text=value; return text;
    }
    public static void CompileOnly() { Compile(); File.WriteAllText("udon-map-compile-result.txt","PASS: compiled map scale and mode programs."); EditorApplication.Exit(0); }
    static void Compile()
    {
        foreach(string name in new[]{"BirdUiRangeScale","BirdMapStation"})
        {
            string path="Assets/BirdWorld/Programs/"+name+".asset"; var source=AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/BirdGenerated/Runtime/"+name+".cs"); if(source==null) throw new Exception("Missing "+name);
            var program=AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path); if(program==null) { program=ScriptableObject.CreateInstance<UdonSharpProgramAsset>(); program.sourceCsScript=source; AssetDatabase.CreateAsset(program,path); } else if(program.sourceCsScript!=source) throw new Exception("Program source mismatch");
        }
        AssetDatabase.SaveAssets(); UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync(); if(UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) throw new Exception("Udon compile failed");
    }
}
#endif
