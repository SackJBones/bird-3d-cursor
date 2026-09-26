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

public class UnityUdonVisualChecks : MonoBehaviour
{
    const string ScenePath="Assets/BirdWorld/Scenes/BirdMapDemo.unity",Active="Bird.Udon.Visual.Checks";
    static int checks;
    static double deadline;
    static UdonBehaviour left,router,visual,element,root;
    static Transform art;
    static Renderer tintTarget;
    static Vector3 restPosition,restScale;
    static Quaternion restRotation;
    static Bounds hitBounds;
    static readonly Vector3 Origin=new Vector3(0,1.65f,0);
    float started;
    int stage;
    public static void Run()
    { File.WriteAllText("udon-visual-result.txt","PENDING"); Compile(); VerifyMigration(); EditorSceneManager.OpenScene(ScenePath); SessionState.SetBool(Active,true); EditorApplication.isPlaying=true; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Begin() { if(SessionState.GetBool(Active,false)) new GameObject("Visual VM checks").AddComponent<UnityUdonVisualChecks>(); }
    void Update()
    {
        if(!SessionState.GetBool(Active,false)) return;
        if(deadline==0) deadline=EditorApplication.timeSinceStartup+90;
        if(EditorApplication.timeSinceStartup>deadline) { Finish(false,"ClientSim/frame-sequence timeout"); return; }
        if(!Utilities.IsValid(Networking.LocalPlayer) || Time.timeSinceLevelLoad<2) return;
        try { if(!Frames()) return; Contracts(); Finish(true,checks+" compiled-Udon visual assertions, normal-frame hover/background/press/loss, fixed hit geometry and four captures. Synthetic input; no hardware/client claim."); }
        catch(Exception e) { Finish(false,e.ToString()); }
    }
    bool Frames()
    {
        if(stage==0)
        {
            left=Vm<BirdUiPointer>("UI Left"); router=Vm<BirdUiRouter>("UI Router"); root=Vm<BirdUiPanel>("UI Root Panel"); visual=Vm<BirdUiVisual>("UI Map"); element=Vm<BirdUiElement>("UI Map");
            Vm<BirdUiDesktopInput>("UI Desktop Input").enabled=false; art=(Transform)visual.GetProgramVariable("visualRoot"); tintTarget=(Renderer)visual.GetProgramVariable("tintRenderer");
            visual.SendCustomEvent("Restore"); restPosition=art.localPosition; restRotation=art.localRotation; restScale=art.localScale;
            Feed(Origin); stage++; return false;
        }
        if(stage==1) { Aim("UI Open"); stage++; return false; }
        if(stage==2) { Assert(Number(root,"state")==1,"Normal gate opens menu"); Physics.SyncTransforms(); hitBounds=((Collider)element.GetProgramVariable("target")).bounds; Aim("UI Map"); started=Time.unscaledTime; stage++; return false; }
        if(stage==3)
        {
            Aim("UI Map"); if(Time.unscaledTime-started<.25f) return false;
            Assert(Number(visual,"displayedState")==2 && art.localPosition.z<-.04f,"Normal hover moves visual toward user");
            Assert(((Collider)element.GetProgramVariable("target")).bounds==hitBounds,"Normal visual motion leaves hit volume fixed");
            Aim("UI Map",true); started=Time.unscaledTime; stage++; return false;
        }
        if(stage==4)
        {
            if(Time.unscaledTime-started<.25f) return false;
            Assert(Number(visual,"displayedState")==4 && art.localPosition.z>.07f,"Background artwork recedes after map action");
            Assert(Number(Vm<BirdUiPanel>("Map Panel"),"state")==1,"Existing map branch opens normally"); Aim("Map Zoom"); stage++; return false;
        }
        if(stage==5) { Aim("Map Zoom",true); started=Time.unscaledTime; stage++; return false; }
        if(stage==6)
        {
            Aim("Map Zoom",true); if(Time.unscaledTime-started<.25f) return false;
            var zoomVisual=Vm<BirdUiVisual>("Map Zoom"); Assert(Number(zoomVisual,"displayedState")==3,"Held state animates selected action");
            Assert(Number(Vm<BirdUiAction>("Map Zoom"),"invocationCount")==1,"Visual transition does not repeat action"); left.SendCustomEvent("Cancel"); stage++; return false;
        }
        if(stage==7) { Assert(Number(root,"state")==0 && !art.gameObject.activeInHierarchy,"Normal tracking loss closes artwork"); return true; }
        return false;
    }
    static void Contracts()
    {
        router.SetProgramVariable("automatic",false);
        foreach(var proxy in FindObjectsOfType<BirdUiVisual>(true)) UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy).SetProgramVariable("automatic",false);
        root.SetProgramVariable("state",1); root.SetProgramVariable("owner","LocalUser"); ((GameObject)root.GetProgramVariable("content")).SetActive(true);
        var block=new MaterialPropertyBlock(); block.SetColor("_Color",Color.magenta); block.SetFloat("_Sentinel",.42f);
        visual.SendCustomEvent("Restore"); tintTarget.SetPropertyBlock(block); visual.SetProgramVariable("transitionSeconds",.2f);
        State(1,0); Near(art.localPosition,restPosition,1e-7f,"Bind snaps first state without stale animation"); Capture("enabled");
        State(2,.1f); Near(art.localPosition,restPosition+Vector3.back*.0225f,1e-6f,"Half-time pose uses smoothstep");
        Vector3 interrupted=art.localPosition; Quaternion interruptedRotation=art.localRotation; State(3,0);
        Near(art.localPosition,interrupted,1e-7f,"Interrupted position continuous"); Assert(Quaternion.Angle(art.localRotation,interruptedRotation)<.001f,"Interrupted rotation continuous");
        State(3,.2f); Near(art.localScale,restScale*.95f,1e-6f,"Pressed scale multiplier"); Capture("activated");
        Physics.SyncTransforms(); Assert(((Collider)element.GetProgramVariable("target")).bounds==hitBounds,"Exact hit bounds independent of every visual state");
        State(4,.2f); Assert(art.localPosition.z>.079f,"Background pose"); Capture("background");
        State(0,.1f); Assert(art.gameObject.activeSelf,"Outgoing hidden state animates"); State(0,.1f); Assert(!art.gameObject.activeSelf && element.gameObject.activeSelf,"Visibility cannot disable control");
        State(1,0); Assert(art.gameObject.activeSelf,"Reappearance reenables artwork");
        visual.enabled=false; Near(art.localPosition,restPosition,1e-7f,"Disable restores position"); Near(art.localScale,restScale,1e-7f,"Disable restores scale");
        tintTarget.GetPropertyBlock(block); Assert(block.GetColor("_Color")==Color.magenta && Mathf.Abs(block.GetFloat("_Sentinel")-.42f)<1e-7f,"Complete property block restored"); visual.enabled=true;
        State(2,.2f); Vector3 saved=art.localPosition; State(3,float.NaN); Near(art.localPosition,saved,0,"Invalid dt does not poison visual");
        visual.SendCustomEvent("Restore"); visual.SetProgramVariable("highlightedPosition",new Vector3(float.NaN,0,0)); State(2,.2f); Near(art.localPosition,restPosition,0,"Nonfinite pose rejected before Transform write"); visual.SetProgramVariable("highlightedPosition",Vector3.back*.045f);
        visual.SendCustomEvent("Restore"); visual.SetProgramVariable("activatedScale",new Vector3(-1,1,1)); State(3,.2f); Near(art.localScale,restScale,0,"Negative multiplier rejected"); visual.SetProgramVariable("activatedScale",Vector3.one*.95f);
        visual.SendCustomEvent("Restore"); var extra=art.gameObject.AddComponent<BoxCollider>(); State(2,.2f); Assert(!string.IsNullOrEmpty((string)visual.GetProgramVariable("configurationError")),"Collider in visual branch rejected"); DestroyImmediate(extra);
        visual.SetProgramVariable("visualRoot",element.transform); State(2,.2f); Assert(!string.IsNullOrEmpty((string)visual.GetProgramVariable("configurationError")),"Source/controller ancestor rejected"); visual.SetProgramVariable("visualRoot",art);
        visual.SendCustomEvent("Restore"); visual.SetProgramVariable("idleColor",new Color(-float.MaxValue,0,0,1)); State(1,0);
        visual.SetProgramVariable("highlightedColor",new Color(float.MaxValue,0,0,1)); State(2,.1f); tintTarget.GetPropertyBlock(block);
        Color tint=block.GetColor("_Color"); Assert(tint==Color.magenta || tint==new Color(0,0,0,1),"Extreme tint transition either interpolates safely or restores its original block");
        visual.SetProgramVariable("idleColor",new Color(.1f,.35f,.5f)); visual.SetProgramVariable("highlightedColor",new Color(.4f,.8f,1));
        Rates();
        visual.SetProgramVariable("transitionSeconds",.14f); State(1,1); Capture("verified");
    }
    static void Rates()
    {
        var csv=new List<string>{"hz,x,y,z"}; var values=new List<Vector3>();
        foreach(int hz in new[]{30,72,120})
        {
            visual.SendCustomEvent("Restore"); visual.SetProgramVariable("transitionSeconds",.4f); State(1,0); State(2,0);
            for(int i=0;i<hz/10;i++) State(2,1f/hz); State(2,Mathf.Max(0,.1f-(hz/10)/(float)hz));
            float t=.25f; t=t*t*(3-2*t); Near(art.localPosition,restPosition+Vector3.back*.045f*t,1e-6f,"Compiled analytic visual curve at "+hz);
            values.Add(art.localPosition); csv.Add(hz+","+art.localPosition.x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+art.localPosition.y.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+art.localPosition.z.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
            State(2,2); Assert(!(bool)visual.GetProgramVariable("isTransitioning"),"Long pause reaches visual target");
        }
        Near(values[0],values[2],1e-7f,"Compiled 30/120 Hz agreement"); Near(values[1],values[2],1e-7f,"Compiled 72/120 Hz agreement");
        Directory.CreateDirectory("../Validation/UdonVisual"); File.WriteAllLines("../Validation/UdonVisual/visual-rates.csv",csv);
    }
    static void State(int state,float dt) { element.SetProgramVariable("state",state); visual.SetProgramVariable("stepDelta",dt); visual.SendCustomEvent("Process"); }
    static void Feed(Vector3 point,bool pressed=false) { left.SetProgramVariable("sampleOrigin",Origin); left.SetProgramVariable("samplePosition",point); left.SetProgramVariable("sampleTracked",true); left.SetProgramVariable("samplePressed",pressed); left.SendCustomEvent("Submit"); }
    static void Aim(string name,bool pressed=false) { Feed(Origin+(FindScene(name).transform.position-Origin)*1.04f,pressed); }
    static GameObject FindScene(string name) { foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>()) if(go.scene.IsValid() && go.name==name) return go; throw new Exception("Missing "+name); }
    static UdonBehaviour Vm<T>(string name) where T:UdonSharpBehaviour { return UdonSharpEditorUtility.GetBackingUdonBehaviour(FindScene(name).GetComponent<T>()); }
    static int Number(UdonBehaviour vm,string key) { return (int)vm.GetProgramVariable(key); }
    static void Assert(bool ok,string message) { checks++; if(!ok) throw new Exception("Assertion "+checks+": "+message); }
    static void Near(Vector3 a,Vector3 b,float tolerance,string message) { Assert(Vector3.Distance(a,b)<=tolerance,message+" "+a+" vs "+b); }
    static void Finish(bool ok,string message) { SessionState.SetBool(Active,false); File.WriteAllText("udon-visual-result.txt",(ok?"PASS: ":"FAIL: ")+message); EditorApplication.Exit(ok?0:1); }
    static void Capture(string name)
    {
        Directory.CreateDirectory("../Validation/UdonVisual"); Canvas.ForceUpdateCanvases();
        var layers=new Dictionary<Transform,int>(); foreach(Transform item in FindScene("Hanoi Menu Offset").GetComponentsInChildren<Transform>(true)) { layers[item]=item.gameObject.layer; item.gameObject.layer=30; }
        var camera=new GameObject("Visual evidence camera").AddComponent<Camera>(); camera.transform.position=new Vector3(-4.05f,1.85f,.8f); camera.transform.LookAt(new Vector3(-4.05f,1.85f,3.5f)); camera.fieldOfView=58; camera.cullingMask=1<<30;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.04f,.07f,.11f); var rt=new RenderTexture(1000,1000,24){antiAliasing=4}; var pixels=new Texture2D(1000,1000,TextureFormat.RGB24,false);
        camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt; pixels.ReadPixels(new Rect(0,0,1000,1000),0,0); pixels.Apply(); File.WriteAllBytes("../Validation/UdonVisual/"+name+".png",pixels.EncodeToPNG());
        RenderTexture.active=null; camera.targetTexture=null; rt.Release(); Destroy(camera.gameObject); Destroy(rt); Destroy(pixels); foreach(var pair in layers) pair.Key.gameObject.layer=pair.Value;
    }
    public static void Author()
    {
        Compile(); var scene=EditorSceneManager.OpenScene(ScenePath); int count=0;
        foreach(var element in FindObjectsOfType<BirdUiElement>(true))
        {
            // Color orbs keep their own art and scrolling semantics. Upgrade boxed controls only.
            var box=element.target as BoxCollider; if(box==null || element.GetComponent<BirdUiVisual>()!=null) continue;
            Vector3[] old=Corners(box); var controller=element.transform; Vector3 oldScale=controller.localScale;
            var mesh=controller.GetComponent<MeshFilter>(); var oldRenderer=controller.GetComponent<MeshRenderer>();
            if(mesh!=null && oldRenderer!=null)
            {
                foreach(Transform child in controller) { child.localPosition=Vector3.Scale(child.localPosition,oldScale); child.localScale=Vector3.Scale(child.localScale,oldScale); }
                box.center=Vector3.Scale(box.center,oldScale); box.size=Vector3.Scale(box.size,oldScale); controller.localScale=Vector3.one;
            }
            var branch=new GameObject("Visual state artwork").transform; branch.SetParent(controller,false); Renderer tint=null;
            if(mesh!=null && oldRenderer!=null)
            {
                var body=new GameObject("Button body",typeof(MeshFilter),typeof(MeshRenderer)); body.transform.SetParent(branch,false); body.transform.localScale=oldScale;
                body.GetComponent<MeshFilter>().sharedMesh=mesh.sharedMesh; tint=body.GetComponent<MeshRenderer>(); tint.sharedMaterials=oldRenderer.sharedMaterials;
                DestroyImmediate(oldRenderer); DestroyImmediate(mesh);
            }
            else
            {
                tint=element.feedback; if(tint==null) throw new Exception("Missing button artwork: "+element.name); tint.transform.SetParent(branch,true);
            }
            foreach(var canvas in controller.GetComponentsInChildren<Canvas>(true))
            {
                var label=canvas.GetComponent<RectTransform>(); label.SetParent(branch,true); label.anchoredPosition3D=label.localPosition;
            }
            element.feedback=null;
            var visual=element.gameObject.AddUdonSharpComponent<BirdUiVisual>(); visual.source=element; visual.visualRoot=branch; visual.tintRenderer=tint;
            visual.idleColor=element.normalColor; visual.highlightedColor=element.highlightColor; visual.activatedColor=new Color(1,.65f,.2f); visual.backgroundColor=element.normalColor*.4f;
            visual.inactiveScale=Vector3.one*.85f; visual.highlightedPosition=Vector3.back*.045f; visual.highlightedRotation=new Vector3(0,4,0); visual.highlightedScale=Vector3.one*1.06f;
            visual.activatedPosition=Vector3.forward*.025f; visual.activatedScale=Vector3.one*.95f; visual.backgroundPosition=Vector3.forward*.08f; visual.backgroundScale=Vector3.one*.93f;
            var current=Corners(box); for(int i=0;i<8;i++) if(Vector3.Distance(old[i],current[i])>.00001f) throw new Exception("Authoring changed hit corner: "+element.name+" / "+i);
            count++;
        }
        foreach(var proxy in FindObjectsOfType<UdonSharpBehaviour>(true)) UdonSharpEditorUtility.CopyProxyToUdon(proxy);
        if(!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed"); AssetDatabase.SaveAssets();
        File.WriteAllText("udon-visual-author-result.txt","PASS: upgraded "+count+" boxed controls with separate state artwork; existing hit bounds preserved. Rerunning skips authored controls."); EditorApplication.Exit(0);
    }
    static void VerifyMigration()
    {
        const string baseline="Assets/BirdGenerated/BirdMapBeforeVisual.unity";
        if(!File.Exists(baseline)) return; // Optional authoring evidence, not required for a fresh consumer checkout.
        var expected=new Dictionary<string,Vector3[]>(); EditorSceneManager.OpenScene(baseline);
        foreach(var source in FindObjectsOfType<BirdUiElement>(true)) if(source.target is BoxCollider) expected[source.name]=Corners((BoxCollider)source.target);
        EditorSceneManager.OpenScene(ScenePath); var rows=new List<string>{"control,corner,x,y,z"}; int count=0;
        foreach(var source in FindObjectsOfType<BirdUiElement>(true))
        {
            if(!(source.target is BoxCollider)) continue; Vector3[] prior; if(!expected.TryGetValue(source.name,out prior)) throw new Exception("Missing original hit shape: "+source.name);
            var current=Corners((BoxCollider)source.target);
            for(int i=0;i<8;i++)
            {
                if(Vector3.Distance(prior[i],current[i])>.00001f) throw new Exception("Authoring moved hit corner: "+source.name+" / "+i);
                rows.Add(source.name+","+i+","+prior[i].x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+prior[i].y.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+prior[i].z.ToString("R",System.Globalization.CultureInfo.InvariantCulture)); count++;
            }
        }
        if(count!=expected.Count*8) throw new Exception("Boxed control set changed");
        Directory.CreateDirectory("../Validation/UdonVisual"); File.WriteAllLines("../Validation/UdonVisual/original-hit-corners.csv",rows);
        File.WriteAllText("udon-visual-migration-result.txt","PASS: all "+count+" world-space corners of "+expected.Count+" boxed controls match the prior scene, including inactive content.");
    }
    static Vector3[] Corners(BoxCollider box)
    {
        var points=new Vector3[8]; for(int i=0;i<8;i++) points[i]=box.transform.TransformPoint(box.center+Vector3.Scale(box.size*.5f,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))); return points;
    }
    static void Compile()
    {
        const string path="Assets/BirdWorld/Programs/BirdUiVisual.asset"; var source=AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/BirdGenerated/Runtime/BirdUiVisual.cs");
        var program=AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path); if(program==null) { program=ScriptableObject.CreateInstance<UdonSharpProgramAsset>(); program.sourceCsScript=source; AssetDatabase.CreateAsset(program,path); }
        else if(program.sourceCsScript!=source) throw new Exception("Program source mismatch"); AssetDatabase.SaveAssets(); UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
        if(UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) throw new Exception("Udon compile failed");
    }
}
#endif
