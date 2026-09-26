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

// Drives backing Udon heaps/events. Never executes a runtime C# proxy method.
public class UnityUdonUiChecks : MonoBehaviour
{
    const string ScenePath="Assets/BirdWorld/Scenes/BirdUiDemo.unity";
    const string Active="Bird.Udon.Ui.Checks";
    static readonly string[] Names={"BirdUiPointer","BirdUiPanel","BirdUiElement","BirdUiRouter","BirdUiAction","BirdUiSphericalScroll","BirdUiDesktopInput"};
    static int checks;
    static double deadline;
    bool ran;
    int frameStage;
    float frameStart;
    UdonBehaviour frameLeft, frameRouter, frameScroll;
    Quaternion releasedRotation;
    public static void Generate()
    {
        if(File.Exists(ScenePath)) throw new Exception("Refusing to overwrite UI scene.");
        Compile();
        var scene=EditorSceneManager.OpenScene("Assets/BirdWorld/Scenes/BirdFeasibility.unity");
        var landmark=GameObject.Find("Orientation landmark - Bird hand probe pending");
        if(landmark!=null) DestroyImmediate(landmark);
        var station=new GameObject("Bird UI station");
        var pointers=new BirdUiPointer[2];
        for(int i=0;i<2;i++) pointers[i]=new GameObject(i==0 ? "UI Left" : "UI Right").AddUdonSharpComponent<BirdUiPointer>();
        var root=new GameObject("UI Root Panel").AddUdonSharpComponent<BirdUiPanel>();
        root.content=new GameObject("Main menu content"); root.content.transform.SetParent(station.transform);
        var colors=new GameObject("UI Color Panel").AddUdonSharpComponent<BirdUiPanel>();
        colors.parent=root; colors.content=new GameObject("Color menu content"); colors.content.transform.SetParent(station.transform);
        var controls=new List<BirdUiElement>();
        controls.Add(Button("UI Open",new Vector3(0,.8f,2.2f),null,null,0,root,"REACH THROUGH TO OPEN",2));
        controls.Add(Button("UI Colors",new Vector3(-.8f,1.1f,2.8f),root.content.transform,root,0,colors,"COLORS"));
        controls.Add(Button("UI Close",new Vector3(.8f,1.1f,2.8f),root.content.transform,root,2,root,"CLOSE"));
        controls[2].activeInBackground=true;
        controls.Add(Button("UI Back",new Vector3(1.25f,.65f,3.5f),colors.content.transform,colors,1,colors,"BACK"));
        var content=new GameObject("UI Rotating Colors").transform; content.SetParent(colors.content.transform); content.position=new Vector3(0,1.75f,4);
        var volume=new GameObject("UI Sphere"); volume.transform.SetParent(colors.content.transform); volume.transform.position=content.position;
        var sphere=volume.AddComponent<SphereCollider>(); sphere.radius=1.05f; sphere.isTrigger=true;
        var scroll=volume.AddUdonSharpComponent<BirdUiSphericalScroll>(); scroll.sphere=sphere; scroll.rotationTarget=content; scroll.pointers=pointers; scroll.panel=colors;
        var result=Shape("UI Result",new Vector3(1.4f,1.85f,3.8f),Vector3.one*.22f,new Color(.6f,.7f,.8f),PrimitiveType.Sphere);
        result.transform.SetParent(station.transform); DestroyImmediate(result.GetComponent<Collider>());
        for(int i=0;i<12;i++)
        {
            Color color=Color.HSVToRGB(i/12f,.82f,1);
            var orb=Shape("UI Color "+i,content.position+Direction(i)*.53f,Vector3.one*.16f,color,PrimitiveType.Sphere);
            orb.transform.SetParent(content);
            var element=orb.AddUdonSharpComponent<BirdUiElement>(); element.target=orb.GetComponent<Collider>(); element.target.isTrigger=true;
            element.panel=colors; element.feedback=orb.GetComponent<Renderer>(); element.normalColor=color; element.highlightColor=Color.Lerp(color,Color.white,.4f);
            var action=orb.AddUdonSharpComponent<BirdUiAction>(); action.element=element; action.action=BirdUiActionKind.SetColor; action.color=color; action.colorTarget=result.GetComponent<Renderer>();
            element.actionTarget=UdonSharpEditorUtility.GetBackingUdonBehaviour(action); controls.Add(element);
        }
        Material wire=Material("UiWire",new Color(.18f,.45f,.6f));
        BuildEdges(content,.53f,wire);
        for(int plane=0;plane<3;plane++)
        {
            var line=new GameObject("Outer interaction sphere guide").AddComponent<LineRenderer>(); line.transform.SetParent(volume.transform,false);
            line.useWorldSpace=false; line.loop=true; line.positionCount=96; line.startWidth=line.endWidth=.005f; line.sharedMaterial=wire;
            for(int i=0;i<96;i++) { float a=i*Mathf.PI*2/96,x=Mathf.Cos(a)*sphere.radius,y=Mathf.Sin(a)*sphere.radius; line.SetPosition(i,plane==0 ? new Vector3(x,y,0) : plane==1 ? new Vector3(x,0,y) : new Vector3(0,x,y)); }
        }
        var reset=Button("UI Reset",new Vector3(-1.25f,.65f,3.5f),colors.content.transform,colors,4,null,"RESET ROTATION");
        var resetAction=reset.GetComponent<BirdUiAction>(); resetAction.resetTarget=content; resetAction.callback=UdonSharpEditorUtility.GetBackingUdonBehaviour(scroll); resetAction.callbackEvent="Cancel"; controls.Add(reset);
        var router=new GameObject("UI Router").AddUdonSharpComponent<BirdUiRouter>(); router.pointers=pointers; router.elements=controls.ToArray();
        var desktop=new GameObject("UI Desktop Input").AddUdonSharpComponent<BirdUiDesktopInput>(); desktop.pointer=pointers[0];
        var marker=Shape("UI Desktop Marker",Vector3.zero,Vector3.one*.032f,Color.white,PrimitiveType.Sphere); DestroyImmediate(marker.GetComponent<Collider>()); desktop.marker=marker.transform;
        desktop.label=Label("Desktop instructions",new Vector3(0,3.35f,4),1400,120,"DESKTOP INPUT / Look to aim, wheel to reach, click to choose");
        desktop.label.transform.parent.localScale=Vector3.one*.003f;
        var title=Label("UI title",new Vector3(0,3.75f,4),1100,110,"BIRD / LOCAL INTERACTION STATION"); title.fontSize=48; title.transform.parent.localScale=Vector3.one*.003f;
        var help=Label("Sphere instructions",new Vector3(0,2.98f,4),1150,90,"Free inside. Pierce the BACK to scroll. Withdraw to coast."); help.transform.parent.localScale=Vector3.one*.003f; help.transform.parent.SetParent(colors.content.transform,true);
        foreach(var proxy in FindObjectsOfType<UdonSharpBehaviour>(true)) UdonSharpEditorUtility.CopyProxyToUdon(proxy);
        root.content.SetActive(false); colors.content.SetActive(false);
        if(!EditorSceneManager.SaveScene(scene,ScenePath)) throw new Exception("Scene save failed");
        AssetDatabase.SaveAssets();
        File.WriteAllText("udon-ui-generate-result.txt","PASS: saved local UI station, seven compiled programs, two logical inputs, nested color branch and back-surface sphere. Runtime checks separate.");
        EditorApplication.Exit(0);
    }
    public static void Run()
    {
        File.WriteAllText("udon-ui-result.txt","PENDING"); Compile();
        EditorSceneManager.OpenScene(ScenePath);
        var accepted=new GameObject("UI accepted cursor fixture").AddUdonSharpComponent<BirdCursorState>();
        UdonSharpEditorUtility.CopyProxyToUdon(accepted);
        SessionState.SetBool(Active,true); EditorApplication.isPlaying=true;
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
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Begin() { if(SessionState.GetBool(Active,false)) new GameObject("UI VM checks").AddComponent<UnityUdonUiChecks>(); }
    void Update()
    {
        if(ran || !SessionState.GetBool(Active,false)) return;
        if(deadline==0) deadline=EditorApplication.timeSinceStartup+60;
        if(EditorApplication.timeSinceStartup>deadline) { Finish(false,"ClientSim startup timeout"); return; }
        if(!Utilities.IsValid(Networking.LocalPlayer) || Time.timeSinceLevelLoad<2) return;
        try
        {
            if(!FrameSequence()) return;
            ran=true; Check();
            Finish(true,checks+" actual compiled-Udon UI assertions; saved-scene normal LateUpdate open/select/drive/coast/loss plus explicit contact/routing tests and rendering. Synthetic logical input, not physical hand or multiplayer validation.");
        }
        catch(Exception e) { Finish(false,e.ToString()); }
    }
    bool FrameSequence()
    {
        Vector3 origin=new Vector3(0,1.65f,0), center=new Vector3(0,1.75f,4);
        if(frameStage==0)
        {
            frameLeft=Vm<BirdUiPointer>("UI Left"); frameRouter=Vm<BirdUiRouter>("UI Router"); frameScroll=Vm<BirdUiSphericalScroll>("UI Sphere");
            Vm<BirdUiDesktopInput>("UI Desktop Input").enabled=false;
            frameLeft.SendCustomEvent("Cancel"); Feed(frameLeft,origin,origin+Vector3.forward,false); frameStage++; return false;
        }
        if(frameStage==1) { Aim(frameLeft,"UI Open",false); frameStage++; return false; }
        if(frameStage==2)
        {
            Assert((int)Vm<BirdUiPanel>("UI Root Panel").GetProgramVariable("state")==1,"Normal LateUpdate opens root");
            Aim(frameLeft,"UI Colors",false); frameStage++; return false;
        }
        if(frameStage==3) { Aim(frameLeft,"UI Colors",true); frameStage++; return false; }
        if(frameStage==4)
        {
            Assert((int)Vm<BirdUiPanel>("UI Color Panel").GetProgramVariable("state")==1,"Normal LateUpdate opens child");
            Feed(frameLeft,origin,GameObject.Find("UI Color 4").transform.position,false); frameStage++; return false;
        }
        if(frameStage==5) { Feed(frameLeft,origin,GameObject.Find("UI Color 4").transform.position,true); frameStage++; return false; }
        if(frameStage==6)
        {
            Assert((int)Vm<BirdUiAction>("UI Color 4").GetProgramVariable("invocationCount")==1,"Normal LateUpdate selects interior color");
            Assert(frameScroll.GetProgramVariable("activePointer")==null,"Normal LateUpdate preserves free interior");
            frameStart=Time.unscaledTime; frameStage++;
        }
        if(frameStage==7)
        {
            float t=Time.unscaledTime-frameStart;
            Vector3 normal=Quaternion.AngleAxis(Mathf.Min(t,.65f)*60,Vector3.up)*Vector3.forward;
            Feed(frameLeft,origin,origin+(center+normal*1.05f-origin)*2,false);
            if(t>=.65f) frameStage++;
            return false;
        }
        if(frameStage==8)
        {
            Assert(frameScroll.GetProgramVariable("activePointer")!=null && ((Vector3)frameScroll.GetProgramVariable("angularVelocity")).magnitude>.1f,"Normal LateUpdate drives scroll");
            releasedRotation=GameObject.Find("UI Rotating Colors").transform.rotation;
            Feed(frameLeft,origin,center,false); frameStart=Time.unscaledTime; frameStage++; return false;
        }
        if(frameStage==9)
        {
            Feed(frameLeft,origin,center,false);
            if(Time.unscaledTime-frameStart<.4f) return false;
            Assert(frameScroll.GetProgramVariable("activePointer")==null && Quaternion.Angle(releasedRotation,GameObject.Find("UI Rotating Colors").transform.rotation)>2,"Normal LateUpdate withdrawal coasts");
            Capture("frame-driven-coast"); frameLeft.SendCustomEvent("Cancel"); frameStage++; return false;
        }
        if(frameStage==10)
        {
            Assert((Vector3)frameScroll.GetProgramVariable("angularVelocity")==Vector3.zero,"Normal LateUpdate loss clears inertia");
            Assert((int)Vm<BirdUiPanel>("UI Root Panel").GetProgramVariable("state")==0,"Normal LateUpdate loss closes root");
            Vm<BirdUiPanel>("UI Root Panel").SendCustomEvent("CloseAll");
            var content=(Transform)frameScroll.GetProgramVariable("rotationTarget"); content.rotation=Quaternion.identity; Physics.SyncTransforms();
            frameStage++; return true;
        }
        return true;
    }
    static void Check()
    {
        var left=Vm<BirdUiPointer>("UI Left"); var right=Vm<BirdUiPointer>("UI Right");
        var router=Vm<BirdUiRouter>("UI Router"); var scroll=Vm<BirdUiSphericalScroll>("UI Sphere");
        Vm<BirdUiDesktopInput>("UI Desktop Input").enabled=false;
        router.SetProgramVariable("automatic",false); scroll.SetProgramVariable("automatic",false);
        left.SendCustomEvent("Cancel"); right.SendCustomEvent("Cancel"); router.SendCustomEvent("Initialize");
        var root=Vm<BirdUiPanel>("UI Root Panel"); var colors=Vm<BirdUiPanel>("UI Color Panel");
        int colorBefore=(int)Vm<BirdUiAction>("UI Color 4").GetProgramVariable("invocationCount");
        Vector3 origin=new Vector3(0,1.65f,0);
        Feed(left,origin,new Vector3(0,.8f,1),false); router.SendCustomEvent("Process");
        Aim(left,"UI Open",false); router.SendCustomEvent("Process");
        Assert((int)root.GetProgramVariable("state")==1,"Reach-through opens root");
        Aim(left,"UI Colors",false); router.SendCustomEvent("Process");
        Assert((int)Vm<BirdUiElement>("UI Colors").GetProgramVariable("state")==2,"Point-through highlight");
        Aim(left,"UI Colors",true); router.SendCustomEvent("Process");
        Assert((int)root.GetProgramVariable("state")==2 && (int)colors.GetProgramVariable("state")==1,"Nested color focus");
        int opened=(int)Vm<BirdUiAction>("UI Colors").GetProgramVariable("invocationCount"); router.SendCustomEvent("Process");
        Assert((int)Vm<BirdUiAction>("UI Colors").GetProgramVariable("invocationCount")==opened,"No repeated revision action");
        Vector3 center=GameObject.Find("UI Sphere").transform.position;
        Vector3 choice=GameObject.Find("UI Color 4").transform.position;
        Feed(left,origin,choice,false); router.SendCustomEvent("Process"); scroll.SendCustomEvent("Process");
        Assert(scroll.GetProgramVariable("activePointer")==null,"Inside remains free");
        Feed(left,origin,choice,true); router.SendCustomEvent("Process");
        Assert((int)Vm<BirdUiAction>("UI Color 4").GetProgramVariable("invocationCount")==colorBefore+1,"Separate color action");
        var block=new MaterialPropertyBlock(); GameObject.Find("UI Result").GetComponent<Renderer>().GetPropertyBlock(block);
        Assert(Vector4.Distance(block.GetColor("_Color"),Color.HSVToRGB(4/12f,.82f,1))<.001f,"Action changes real renderer");
        Capture("inside-selected");
        Feed(left,origin,center-Vector3.forward*1.06f,false); scroll.SendCustomEvent("Process");
        Assert(scroll.GetProgramVariable("activePointer")==null,"Front entry insufficient");
        Feed(left,origin,center+Vector3.forward*1.04f,false); scroll.SendCustomEvent("Process");
        Assert(scroll.GetProgramVariable("activePointer")==null,"Back interior insufficient");
        Feed(left,origin,center+Vector3.forward*1.07f,false); scroll.SendCustomEvent("Process");
        Assert(scroll.GetProgramVariable("activePointer")!=null,"Beyond back acquires");
        var rateRows=new List<string>{"frame,hz,end_speed_rad_s,coast_degrees,analytic_degrees"};
        var frames=new[]{Quaternion.identity,Quaternion.Euler(0,0,180),Quaternion.Euler(40,-65,37)};
        for(int frameIndex=0;frameIndex<frames.Length;frameIndex++) foreach(int hz in new[]{30,72,120})
        {
            scroll.SendCustomEvent("Cancel"); GameObject.Find("UI Rotating Colors").transform.rotation=Quaternion.identity;
            scroll.SetProgramVariable("stepDelta",1f/hz);
            Vector3 frameOrigin=center-frames[frameIndex]*Vector3.forward*4;
            for(int i=0;i<=hz;i++)
            {
                Vector3 normal=frames[frameIndex]*Quaternion.AngleAxis(i*60f/hz,Vector3.up)*Vector3.forward;
                Feed(left,frameOrigin,frameOrigin+(center+normal*1.05f-frameOrigin)*2,false); scroll.SendCustomEvent("Process");
            }
            float speed=((Vector3)scroll.GetProgramVariable("angularVelocity")).magnitude;
            Assert(Mathf.Abs(speed-.95284766f)<.00005f,"Angular velocity at "+hz+" frame "+frameIndex);
            for(int i=0;i<hz;i++) { Feed(left,frameOrigin,center,false); scroll.SendCustomEvent("Process"); }
            float angle=Quaternion.Angle(Quaternion.identity,GameObject.Find("UI Rotating Colors").transform.rotation);
            Assert(scroll.GetProgramVariable("activePointer")==null,"Withdrawal releases at "+hz+" frame "+frameIndex);
            Assert(Mathf.Abs(angle-84.28226f)<.001f,"Integrated coast angle at "+hz+" frame "+frameIndex);
            rateRows.Add(frameIndex+","+hz+","+speed.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+angle.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+",84.28226");
        }
        Directory.CreateDirectory("../Validation/UdonUi"); File.WriteAllLines("../Validation/UdonUi/spherical-rates.csv",rateRows);
        Capture("coasting");
        left.SendCustomEvent("Cancel"); scroll.SendCustomEvent("Process");
        Assert((Vector3)scroll.GetProgramVariable("angularVelocity")==Vector3.zero,"Loss clears inertia");
        Feed(right,origin,center,false); router.SendCustomEvent("Process");
        Assert((int)colors.GetProgramVariable("state")==1,"Other hand retains shared focus");
        Feed(left,origin,center,true); router.SendCustomEvent("Process");
        Assert(!(bool)left.GetProgramVariable("pressedThisSample"),"Recovery held press suppressed");
        right.SetProgramVariable("userId","OtherUser"); Feed(right,origin,center,false);
        left.SendCustomEvent("Cancel"); router.SendCustomEvent("Process");
        Assert((int)root.GetProgramVariable("state")==0 && (int)colors.GetProgramVariable("state")==0,"Owner loss closes entire branch");
        Feed(left,origin,new Vector3(float.NaN,0,0),true);
        Assert(!(bool)left.GetProgramVariable("tracked"),"Nonfinite sample cancels");
        Capture("closed");
        RoutingContracts(left,right,router,root,colors,origin);
        // Test the compiled contact math against Unity physics, not another copy of the formula.
        var sphere=(SphereCollider)scroll.GetProgramVariable("sphere");
        colors.gameObject.SetActive(true);
        ((GameObject)colors.GetProgramVariable("content")).SetActive(true);
        scroll.SetProgramVariable("panel",null);
        right.SendCustomEvent("Cancel");
        foreach(Vector3 scale in new[]{Vector3.one,new Vector3(2,.6f,1.3f),new Vector3(-1.4f,2.1f,.8f)})
        {
            sphere.transform.localScale=scale; sphere.transform.rotation=Quaternion.Euler(25,38,-14); sphere.center=new Vector3(.2f,-.1f,.15f);
            Physics.SyncTransforms();
            Vector3 c=sphere.transform.TransformPoint(sphere.center);
            for(int i=0;i<40;i++)
            {
                Vector3 o=c+Vector3.back*6;
                Vector3 p=c+new Vector3(Mathf.Sin(i*.31f)*.4f,Mathf.Cos(i*.23f)*.4f,5);
                RaycastHit hit;
                Assert(sphere.Raycast(new Ray(p,(o-p).normalized),out hit,20),"Physics reverse ray control");
                scroll.SendCustomEvent("Cancel"); Feed(left,o,p,false); scroll.SendCustomEvent("Process");
                Assert(scroll.GetProgramVariable("activePointer")!=null,"Compiled contact acquires scaled sphere");
                Assert(Vector3.Distance((Vector3)scroll.GetProgramVariable("contactPoint"),hit.point)<.00002f,"Compiled/physics far hit agreement");
            }
        }
        sphere.transform.localScale=Vector3.one; sphere.transform.rotation=Quaternion.identity; sphere.center=Vector3.zero;
        scroll.SendCustomEvent("Cancel"); Feed(left,center-Vector3.forward*4,center+Vector3.forward*1e12f,false); scroll.SendCustomEvent("Process");
        Assert(scroll.GetProgramVariable("activePointer")!=null,"No short cast limit on far logical reach");
        Feed(left,origin,origin,false); scroll.SendCustomEvent("Process");
        Assert(scroll.GetProgramVariable("activePointer")==null,"Zero-length point cannot scroll");
        Feed(left,origin,center+Vector3.forward*3,false); scroll.SendCustomEvent("Process");
        left.SendCustomEvent("Cancel"); Feed(left,origin,center+new Vector3(.8f,0,3),true); scroll.SendCustomEvent("Process");
        Assert((Vector3)scroll.GetProgramVariable("angularVelocity")==Vector3.zero,"Loss/recovery between scroll frames rebases");
        Feed(right,origin,center+new Vector3(-.8f,0,3),false);
        left.SendCustomEvent("Cancel"); scroll.SendCustomEvent("Process");
        Assert((UdonBehaviour)scroll.GetProgramVariable("activePointer")==right && (Vector3)scroll.GetProgramVariable("angularVelocity")==Vector3.zero,"Other hand acquires without inherited angular impulse");
        scroll.SetProgramVariable("stepDelta",.3f); scroll.SendCustomEvent("Process");
        Assert(scroll.GetProgramVariable("activePointer")==null && (Vector3)scroll.GetProgramVariable("angularVelocity")==Vector3.zero,"Long pause cancels");
        scroll.SetProgramVariable("stepDelta",1f/72);
        scroll.SendCustomEvent("Process");
        right.gameObject.SetActive(false); scroll.SendCustomEvent("Process");
        Assert(scroll.GetProgramVariable("activePointer")==null,"Disabled input cannot scroll");
        right.gameObject.SetActive(true);
        LifecycleContracts(left,right,router,root,colors,scroll,origin,center);
    }
    static void Feed(UdonBehaviour pointer,Vector3 origin,Vector3 point,bool pressed)
    {
        pointer.SetProgramVariable("sampleOrigin",origin); pointer.SetProgramVariable("samplePosition",point);
        pointer.SetProgramVariable("sampleTracked",true); pointer.SetProgramVariable("samplePressed",pressed); pointer.SendCustomEvent("Submit");
    }
    static void Aim(UdonBehaviour pointer,string target,bool pressed)
    {
        Vector3 origin=new Vector3(0,1.65f,0), delta=GameObject.Find(target).transform.position-origin;
        Feed(pointer,origin,origin+delta.normalized*(delta.magnitude+.2f),pressed);
    }
    static UdonBehaviour Vm<T>(string name) where T:UdonSharpBehaviour
    {
        foreach(var proxy in Resources.FindObjectsOfTypeAll<T>())
            if(proxy.name==name && proxy.gameObject.scene.IsValid()) return UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy);
        throw new Exception("Missing scene behaviour: "+name+" / "+typeof(T));
    }
    static void Assert(bool value,string message) { checks++; if(!value) throw new Exception(message); }
    static Vector3 Direction(int i)
    {
        float phi=(1+Mathf.Sqrt(5))*.5f, a=i%4<2 ? -1:1,b=i%2==0 ? -phi:phi;
        return (i<4 ? new Vector3(0,b,a):i<8 ? new Vector3(a,0,b):new Vector3(b,a,0)).normalized;
    }
    static BirdUiElement Button(string name,Vector3 position,Transform parent,BirdUiPanel owner,int operation,BirdUiPanel destination,string text,int mode=1)
    {
        var go=Shape(name,position,new Vector3(.65f,.25f,.08f),new Color(.08f,.22f,.32f),PrimitiveType.Cube);
        if(parent!=null) go.transform.SetParent(parent,true);
        var element=go.AddUdonSharpComponent<BirdUiElement>(); element.target=go.GetComponent<Collider>(); element.target.isTrigger=true; element.panel=owner; element.activation=(BirdUiActivation)mode; element.feedback=go.GetComponent<Renderer>();
        var action=go.AddUdonSharpComponent<BirdUiAction>(); action.element=element; action.action=(BirdUiActionKind)operation; action.panel=destination;
        element.actionTarget=UdonSharpEditorUtility.GetBackingUdonBehaviour(action);
        var label=Label(name+" label",position-Vector3.forward*.05f,400,100,text); label.transform.parent.SetParent(go.transform,true);
        return element;
    }
    static GameObject Shape(string name,Vector3 position,Vector3 scale,Color color,PrimitiveType type)
    {
        var go=GameObject.CreatePrimitive(type); go.name=name; go.transform.position=position; go.transform.localScale=scale; go.GetComponent<Renderer>().sharedMaterial=Material(name.Replace(" ",""),color); return go;
    }
    static Material Material(string name,Color color)
    {
        string path="Assets/BirdWorld/Materials/"+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null) { material=new Material(Shader.Find("Unlit/Color")); material.color=color; AssetDatabase.CreateAsset(material,path); }
        return material;
    }
    static Text Label(string name,Vector3 position,int width,int height,string value)
    {
        var canvas=new GameObject(name,typeof(Canvas)); canvas.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
        canvas.transform.position=position; canvas.transform.localScale=Vector3.one*.0015f;
        var rect=canvas.GetComponent<RectTransform>(); rect.sizeDelta=new Vector2(width,height);
        var text=new GameObject("Text",typeof(Text)).GetComponent<Text>(); text.transform.SetParent(canvas.transform,false);
        text.rectTransform.sizeDelta=rect.sizeDelta; text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize=28; text.alignment=TextAnchor.MiddleCenter; text.color=Color.white; text.text=value; return text;
    }
    static void BuildEdges(Transform parent,float radius,Material material)
    {
        var vertices=new List<Vector3>();
        for(int i=0;i<12;i++) for(int j=i+1;j<12;j++) for(int k=j+1;k<12;k++)
        {
            Vector3 a=Direction(i),b=Direction(j),c=Direction(k);
            float determinant=Vector3.Dot(a,Vector3.Cross(b,c)); if(Mathf.Abs(determinant)<.0001f) continue;
            Vector3 v=(Vector3.Cross(b,c)+Vector3.Cross(c,a)+Vector3.Cross(a,b))*(radius/determinant);
            bool inside=true;
            for(int n=0;n<12;n++) if(Vector3.Dot(Direction(n),v)>radius+.0001f) inside=false;
            foreach(var existing in vertices) if((v-existing).sqrMagnitude<.000001f) inside=false;
            if(inside) vertices.Add(v);
        }
        float edge=float.PositiveInfinity;
        for(int i=0;i<vertices.Count;i++) for(int j=i+1;j<vertices.Count;j++) edge=Mathf.Min(edge,Vector3.Distance(vertices[i],vertices[j]));
        for(int i=0;i<vertices.Count;i++) for(int j=i+1;j<vertices.Count;j++)
            if(Vector3.Distance(vertices[i],vertices[j])<edge*1.01f)
            {
                var line=new GameObject("Dodecahedral guide edge").AddComponent<LineRenderer>(); line.transform.SetParent(parent,false);
                line.useWorldSpace=false; line.positionCount=2; line.SetPositions(new[]{vertices[i],vertices[j]}); line.startWidth=line.endWidth=.004f; line.sharedMaterial=material;
            }
    }
    static void RoutingContracts(UdonBehaviour left,UdonBehaviour right,UdonBehaviour router,UdonBehaviour root,UdonBehaviour colors,Vector3 origin)
    {
        right.SendCustomEvent("Cancel"); right.SetProgramVariable("userId","LocalUser");
        var open=Vm<BirdUiElement>("UI Open"); open.SetProgramVariable("activation",(int)BirdUiActivation.DirectionalPass);
        left.SendCustomEvent("Cancel"); Feed(left,origin,new Vector3(0,1.3f,2.2f),false); router.SendCustomEvent("Process");
        Feed(left,origin,new Vector3(0,.3f,2.2f),false); router.SendCustomEvent("Process");
        Assert((int)root.GetProgramVariable("state")==0,"Wrong-direction passage does not open");
        Feed(left,origin,new Vector3(0,1.3f,2.2f),false); router.SendCustomEvent("Process");
        Assert((int)root.GetProgramVariable("state")==1,"Upward swept passage opens");
        var action=Vm<BirdUiAction>("UI Colors"); action.SetProgramVariable("callback",router); action.SetProgramVariable("callbackEvent","Process");
        Aim(left,"UI Colors",false); router.SendCustomEvent("Process"); Aim(left,"UI Colors",true); router.SendCustomEvent("Process");
        Assert((int)colors.GetProgramVariable("state")==1,"Reentrant router callback does not corrupt branch");
        action.SetProgramVariable("callback",null);
        right.SetProgramVariable("userId","OtherUser"); Aim(right,"UI Back",false); router.SendCustomEvent("Process"); Aim(right,"UI Back",true); router.SendCustomEvent("Process");
        Assert((int)colors.GetProgramVariable("state")==1,"Different user cannot activate owned branch");
        right.SendCustomEvent("Cancel"); right.SetProgramVariable("userId","LocalUser");
        Aim(left,"UI Back",false); router.SendCustomEvent("Process"); Aim(left,"UI Back",true); router.SendCustomEvent("Process");
        Assert((int)colors.GetProgramVariable("state")==0 && (int)root.GetProgramVariable("state")==1,"Back restores parent focus");
        Aim(left,"UI Colors",false); router.SendCustomEvent("Process"); Aim(left,"UI Colors",true); router.SendCustomEvent("Process");
        var reset=Vm<BirdUiAction>("UI Reset");
        GameObject.Find("UI Rotating Colors").transform.rotation=Quaternion.Euler(20,30,40);
        Aim(left,"UI Reset",false); router.SendCustomEvent("Process"); Aim(left,"UI Reset",true); router.SendCustomEvent("Process");
        Assert(Quaternion.Angle(GameObject.Find("UI Rotating Colors").transform.rotation,Quaternion.identity)<.001f,"Supported world reset action");
        Vector3 a=GameObject.Find("UI Color 4").transform.position,b=GameObject.Find("UI Color 6").transform.position;
        var first=Vm<BirdUiAction>("UI Color 4"); var second=Vm<BirdUiAction>("UI Color 6");
        first.SetProgramVariable("callback",root); first.SetProgramVariable("callbackEvent","CloseAll");
        int before=(int)second.GetProgramVariable("invocationCount"), firstBefore=(int)first.GetProgramVariable("invocationCount");
        Feed(left,origin,a,false); Feed(right,origin,b,false); router.SendCustomEvent("Process");
        Feed(left,origin,a,true); Feed(right,origin,b,true); router.SendCustomEvent("Process");
        Assert((int)first.GetProgramVariable("invocationCount")==firstBefore+1,"First hand callback invoked");
        Assert((int)root.GetProgramVariable("state")==0 && (int)second.GetProgramVariable("invocationCount")==before,"Callbacks invalidate later hand candidate safely");
        first.SetProgramVariable("callback",null);
        root.SetProgramVariable("parent",colors); colors.SetProgramVariable("parent",root);
        Feed(left,origin,new Vector3(0,.3f,2.2f),false); router.SendCustomEvent("Process"); Feed(left,origin,new Vector3(0,1.3f,2.2f),false); router.SendCustomEvent("Process");
        Assert((int)root.GetProgramVariable("state")==0,"Inspector hierarchy cycle rejected");
        root.SetProgramVariable("parent",null); open.SetProgramVariable("activation",(int)BirdUiActivation.EnterThrough);
    }
    static void LifecycleContracts(UdonBehaviour left,UdonBehaviour right,UdonBehaviour router,UdonBehaviour root,UdonBehaviour colors,UdonBehaviour scroll,Vector3 origin,Vector3 center)
    {
        var accepted=Vm<BirdCursorState>("UI accepted cursor fixture");
        left.SetProgramVariable("cursor",accepted); left.SendCustomEvent("Cancel");
        accepted.SetProgramVariable("handRoot",origin); accepted.SetProgramVariable("position",new Vector3(1234567,4,5));
        accepted.SetProgramVariable("tracking",true); accepted.SetProgramVariable("poseValid",true); accepted.SetProgramVariable("selected",true);
        left.SendCustomEvent("SampleCursor");
        Assert((Vector3)left.GetProgramVariable("position")==new Vector3(1234567,4,5) && (Vector3)left.GetProgramVariable("origin")==origin,"Cursor adapter copies logical point/origin exactly");
        Assert(!(bool)left.GetProgramVariable("pressedThisSample"),"Cursor adapter suppresses first held press");
        accepted.SetProgramVariable("selected",false); left.SendCustomEvent("SampleCursor");
        accepted.SetProgramVariable("selected",true); left.SendCustomEvent("SampleCursor");
        Assert((bool)left.GetProgramVariable("pressedThisSample"),"Cursor adapter publishes next press");
        accepted.SetProgramVariable("poseValid",false); left.SendCustomEvent("SampleCursor");
        Assert(!(bool)left.GetProgramVariable("tracked") && !(bool)left.GetProgramVariable("pressed"),"Invalid accepted pose cancels");
        accepted.SetProgramVariable("poseValid",true); accepted.enabled=false; left.SendCustomEvent("SampleCursor");
        Assert(!(bool)left.GetProgramVariable("tracked"),"Disabled cursor source rejected");
        accepted.enabled=true; left.SetProgramVariable("cursor",null);
        left.SetProgramVariable("revision",int.MaxValue); Feed(left,origin,center,false);
        Assert((int)left.GetProgramVariable("revision")==0,"Revision wrap stays within Udon numeric bounds");
        left.enabled=false; Feed(left,origin,center,true);
        Assert(!(bool)left.GetProgramVariable("tracked") && !(bool)left.GetProgramVariable("pressed"),"Disabled pointer cannot queue input");
        left.enabled=true; Feed(left,origin,center,true);
        Assert(!(bool)left.GetProgramVariable("pressedThisSample"),"Reenabled held pointer reseeds");
        var open=Vm<BirdUiElement>("UI Open");
        open.SetProgramVariable("stateTarget",left); open.SetProgramVariable("stateEvent","Cancel"); open.SetProgramVariable("state",3);
        open.SendCustomEvent("BeginFrame"); open.SendCustomEvent("FinishFrame");
        Assert(!(bool)left.GetProgramVariable("tracked"),"State transition sends local callback");
        int revision=(int)left.GetProgramVariable("revision"); open.SendCustomEvent("FinishFrame");
        Assert((int)left.GetProgramVariable("revision")==revision,"Unchanged state does not repeat event");
        open.SetProgramVariable("stateTarget",null); open.enabled=false;
        Assert((int)open.GetProgramVariable("state")==0,"Component disable immediately clears visual state");
        var block=new MaterialPropertyBlock(); open.gameObject.GetComponent<Renderer>().GetPropertyBlock(block);
        Assert(Vector4.Distance(block.GetColor("_Color"),(Color)open.GetProgramVariable("normalColor")*.4f)<.001f,"Disabled visible control loses bright feedback");
        open.enabled=true; right.SendCustomEvent("Cancel"); root.SendCustomEvent("CloseAll"); router.SendCustomEvent("Initialize");
        Feed(left,origin,origin,false); router.SendCustomEvent("Process"); Aim(left,"UI Open",false); router.SendCustomEvent("Process");
        Aim(left,"UI Colors",false); router.SendCustomEvent("Process"); Aim(left,"UI Colors",true); router.SendCustomEvent("Process");
        Assert((int)colors.GetProgramVariable("state")==1,"Branch reopens after lifecycle controls");
        scroll.SetProgramVariable("panel",colors); Feed(left,origin,center+Vector3.forward*3,false); scroll.SendCustomEvent("Process");
        Assert(scroll.GetProgramVariable("activePointer")!=null,"Owned open panel permits scroll");
        colors.SetProgramVariable("state",2); scroll.SendCustomEvent("Process");
        Assert(scroll.GetProgramVariable("activePointer")==null && (Vector3)scroll.GetProgramVariable("angularVelocity")==Vector3.zero,"Background panel cancels scroll");
        colors.SetProgramVariable("state",1); router.enabled=false;
        Assert((int)root.GetProgramVariable("state")==0 && (int)colors.GetProgramVariable("state")==0,"Router disable closes branch immediately");
        router.enabled=true;
    }
    static void Capture(string name)
    {
        Directory.CreateDirectory("../Validation/UdonUi");
        var layers=new Dictionary<Transform,int>();
        foreach(string rootName in new[]{"Bird UI station","UI Open","UI title","Desktop instructions","Feasibility floor"})
        {
            var root=GameObject.Find(rootName);
            if(root==null) continue;
            foreach(Transform item in root.GetComponentsInChildren<Transform>(true)) { layers[item]=item.gameObject.layer; item.gameObject.layer=30; }
        }
        var camera=new GameObject("UI evidence camera").AddComponent<Camera>(); camera.transform.position=new Vector3(0,1.8f,-1.6f); camera.transform.LookAt(new Vector3(0,1.8f,3.7f)); camera.fieldOfView=52;
        camera.cullingMask=1<<30;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.035f,.065f,.1f);
        var texture=new RenderTexture(1400,1000,24) { antiAliasing=4 }; var pixels=new Texture2D(1400,1000,TextureFormat.RGB24,false);
        camera.targetTexture=texture; camera.Render(); RenderTexture.active=texture; pixels.ReadPixels(new Rect(0,0,1400,1000),0,0); pixels.Apply(); File.WriteAllBytes("../Validation/UdonUi/"+name+".png",pixels.EncodeToPNG());
        RenderTexture.active=null; camera.targetTexture=null; texture.Release(); Destroy(camera.gameObject); Destroy(texture); Destroy(pixels);
        foreach(var pair in layers) pair.Key.gameObject.layer=pair.Value;
    }
    static void Finish(bool ok,string result)
    {
        SessionState.SetBool(Active,false); File.WriteAllText("udon-ui-result.txt",(ok ? "PASS: ":"FAIL: ")+result); EditorApplication.Exit(ok ? 0:1);
    }
}
#endif
