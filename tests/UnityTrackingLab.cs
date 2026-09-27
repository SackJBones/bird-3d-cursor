#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UdonSharp;
using UdonSharpEditor;
using VRC;
using VRC.Core;
using VRC.Editor;
using VRC.SDK3.Components;
using VRC.SDK3.Editor;
using VRC.SDKBase;
using VRC.SDKBase.Editor;
using VRC.SDKBase.Editor.Api;
using VRC.SDKBase.Editor.Validation;

// Uses the normal SDK world builder. No validation overrides, SDK patches or upload calls.
public static class UnityTrackingLab
{
    public const string ScenePath = "Assets/BirdWorld/Scenes/BirdTrackingLab.unity";
    const string Folder = "Assets/BirdWorld/TrackingLab";
    static Transform architecture;
    static Material floor, wall, dark, accent, cyan, pink;
    static VRCSdkControlPanel panel;
    static VRCSceneDescriptor descriptor;

    public static void Generate()
    {
        try
        {
            if (File.Exists(ScenePath)) throw new Exception("Refusing to overwrite an authored tracking lab.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/BirdWorld", "TrackingLab");
            foreach (string name in new[] { "BirdHandDataProbe", "BirdLabToggle", "BirdLabStatus" })
            {
                string path = "Assets/BirdWorld/Programs/" + name + ".asset";
                var source = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/BirdGenerated/Runtime/" + name + ".cs");
                if (source == null) throw new Exception("Missing restored source " + name);
                var program = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path);
                if (program == null)
                {
                    program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                    program.sourceCsScript = source; AssetDatabase.CreateAsset(program, path);
                }
                else if (program.sourceCsScript != source) throw new Exception("Existing program has a different source: " + name);
            }
            Compile();
            var scene = EditorSceneManager.OpenScene("Assets/BirdWorld/Scenes/BirdFeasibility.unity");
            foreach (string name in new[] { "Feasibility floor", "Orientation landmark - Bird hand probe pending" })
                if (GameObject.Find(name) != null) UnityEngine.Object.DestroyImmediate(GameObject.Find(name));
            descriptor = UnityEngine.Object.FindObjectOfType<VRCSceneDescriptor>();
            if (descriptor.GetComponent<PipelineManager>() == null) descriptor.gameObject.AddComponent<PipelineManager>();
            descriptor.spawns[0].position = new Vector3(0, 0.05f, -3);
            descriptor.spawns[0].rotation = Quaternion.identity;
            descriptor.RespawnHeightY = -8;
            architecture = new GameObject("Lab architecture / meters").transform;
            floor = Material("Floor", new Color(.36f,.42f,.43f));
            wall = Material("Stone", new Color(.63f,.66f,.6f));
            dark = Material("Ink", new Color(.055f,.1f,.13f), true);
            accent = Material("Amber", new Color(1,.6f,.2f), true);
            cyan = Material("Left", Color.cyan, true); pink = Material("Right", new Color(1,.25f,.6f), true);
            Box("Laboratory floor", new Vector3(0,-.15f,1), new Vector3(12,.3f,14), floor);
            Box("Left wall", new Vector3(-6,1.6f,0), new Vector3(.2f,3.2f,12), wall);
            Box("Right wall", new Vector3(6,1.6f,0), new Vector3(.2f,3.2f,12), wall);
            Box("Back wall", new Vector3(0,1.6f,-6), new Vector3(12,3.2f,.2f), wall);
            Box("Roof", new Vector3(0,3.3f,-1), new Vector3(12,.2f,10), wall);
            Box("Front rail", new Vector3(0,.9f,7.8f), new Vector3(12,.12f,.12f), dark);
            Box("Front boundary", new Vector3(0,.7f,7.95f), new Vector3(12,1.4f,.15f), wall).GetComponent<Renderer>().enabled = false;
            for (int i=-5;i<=5;i++) Box("One meter floor grid",new Vector3(i,.006f,1),new Vector3(.012f,.006f,13),dark,false);
            for (int i=-5;i<=7;i++) Box("One meter floor grid",new Vector3(0,.006f,i),new Vector3(11,.006f,.012f),dark,false);
            Box("Work bench",new Vector3(0,.72f,1.8f),new Vector3(3.2f,.12f,1),wall);
            Box("Bench support L",new Vector3(-1.35f,.35f,1.8f),new Vector3(.12f,.7f,.75f),dark);
            Box("Bench support R",new Vector3(1.35f,.35f,1.8f),new Vector3(.12f,.7f,.75f),dark);
            Box("Reference cube 10cm",new Vector3(-.5f,.83f,1.7f),Vector3.one*.1f,accent,false);
            Box("Reference cube 25cm",new Vector3(.05f,.905f,1.7f),Vector3.one*.25f,cyan,false);
            Box("Reference cube 50cm",new Vector3(.8f,1.03f,1.8f),Vector3.one*.5f,pink,false);
            Label("Reference sizes",new Vector3(0,.55f,1.28f),3,.22f,30).text="10 cm                 25 cm                 50 cm";
            Board("Welcome",new Vector3(0,2.65f,3.3f),new Vector2(4.8f,.65f),
                "BIRD / TRACKING LAB 01\nAvatar hands, real scale, room to experiment",42);
            Board("Directions",new Vector3(0,1.8f,3.3f),new Vector2(4.8f,.85f),
                "Use VRChat's normal interaction on the two controls.\nCompare your avatar hands with the colored bone markers.\nBird will be added here after the input is understood.",30);
            var overlay = new GameObject("Local hand diagnostics");
            var probe = overlay.AddUdonSharpComponent<BirdHandDataProbe>();
            probe.markers = new Transform[32];
            for(int i=0;i<32;i++)
            {
                var marker=GameObject.CreatePrimitive(PrimitiveType.Sphere); marker.name="Avatar bone "+i;
                marker.transform.SetParent(overlay.transform); marker.transform.localScale=Vector3.one*.009f;
                UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
                marker.GetComponent<Renderer>().sharedMaterial=i<16?cyan:pink;
                marker.SetActive(false); probe.markers[i]=marker.transform;
            }
            probe.status=Board("Bone availability",new Vector3(3.5f,1.95f,.8f),new Vector2(2.7f,1.05f),"Waiting for avatar bones",27);
            var status=overlay.AddUdonSharpComponent<BirdLabStatus>();
            status.status=Board("Tracking origins",new Vector3(3.5f,.92f,.8f),new Vector2(2.7f,.9f),"Waiting for local player",25);
            status.leftOrigin=Box("Left tracking origin",Vector3.zero,Vector3.one*.018f,accent,false).transform;
            status.rightOrigin=Box("Right tracking origin",Vector3.zero,Vector3.one*.018f,accent,false).transform;
            status.leftOrigin.SetParent(overlay.transform); status.rightOrigin.SetParent(overlay.transform);
            UdonSharpEditorUtility.CopyProxyToUdon(probe); UdonSharpEditorUtility.CopyProxyToUdon(status);
            Toggle("Hand markers",new Vector3(.65f,1.05f,.85f),overlay,true);
            var mirror=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.vrchat.worlds/Samples/UdonExampleScene/Prefabs/VRCMirror.prefab"));
            mirror.name="Inspection mirror"; mirror.transform.position=new Vector3(-3.7f,1.5f,2.8f); mirror.transform.localScale=new Vector3(2.8f,2.6f,1);
            var reflection=mirror.GetComponent<VRC_MirrorReflection>();
            var mirrorSettings=new SerializedObject(reflection); mirrorSettings.FindProperty("mirrorResolution").intValue=512; mirrorSettings.ApplyModifiedPropertiesWithoutUndo();
            mirror.SetActive(false);
            Box("Mirror backing",new Vector3(-3.7f,1.5f,2.84f),new Vector3(3,2.8f,.06f),dark);
            Toggle("Mirror",new Vector3(-.65f,1.05f,.85f),mirror,false);
            Board("Mirror instructions",new Vector3(-3.7f,3.04f,2.75f),new Vector2(3,.35f),"INSPECTION MIRROR / SWITCH AT BENCH",25);
            Box("Outside terrain",new Vector3(0,-4,150),new Vector3(800,4,700),floor);
            foreach(int distance in new[]{10,30,100})
            {
                Box(distance+" meter range reference",new Vector3(0,1.5f,distance),new Vector3(.35f,3,.35f),accent,false);
                Board(distance+" meter sign",new Vector3(0,3.4f,distance-.2f),new Vector2(2,.5f),distance+" m",42);
            }
            for(int i=0;i<7;i++) Box("Distant scale reference",new Vector3((i-3)*35,15,140+(i%2)*60),new Vector3(12,30+(i%3)*15,14),wall,false);
            new GameObject("Bird integration / future local input and presentation");
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight=new Color(.62f,.68f,.75f);
            var light=UnityEngine.Object.FindObjectOfType<Light>(); if(light!=null) { light.intensity=.8f; light.shadows=LightShadows.None; }
            if(!EditorSceneManager.SaveScene(scene,ScenePath)) throw new Exception("Could not save authored lab");
            AssetDatabase.SaveAssets();
            Finish("lab-generate",true,"Authored normal SDK world with local diagnostics, standard mirror and scale references.");
        }
        catch(Exception e) { Finish("lab-generate",false,e.ToString()); }
    }

    static Material Material(string name,Color color,bool unlit=false)
    {
        string path=Folder+"/"+name+".mat"; var value=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(value!=null) return value;
        var shader=Shader.Find(unlit?"Unlit/Color":"Standard"); if(shader==null) throw new Exception("Missing built-in shader");
        value=new Material(shader){color=color}; if(!unlit) { value.SetFloat("_Glossiness",0); value.SetFloat("_Metallic",0); }
        AssetDatabase.CreateAsset(value,path); return value;
    }
    static GameObject Box(string name,Vector3 position,Vector3 size,Material material,bool collision=true)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(architecture); go.transform.position=position; go.transform.localScale=size;
        go.GetComponent<Renderer>().sharedMaterial=material; if(!collision) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
    }
    static Text Label(string name,Vector3 position,float width,float height,int fontSize)
    {
        var canvas=new GameObject(name,typeof(RectTransform)).AddComponent<Canvas>(); canvas.renderMode=RenderMode.WorldSpace;
        canvas.transform.position=position; canvas.transform.localScale=Vector3.one*.002f;
        var label=new GameObject("Text",typeof(RectTransform)).AddComponent<Text>(); label.transform.SetParent(canvas.transform,false);
        label.rectTransform.sizeDelta=new Vector2(width/.002f,height/.002f); label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize=fontSize*2; label.alignment=TextAnchor.MiddleCenter; label.raycastTarget=false; return label;
    }
    static Text Board(string name,Vector3 position,Vector2 size,string text,int fontSize)
    {
        Box(name+" backing",position+Vector3.forward*.025f,new Vector3(size.x+.08f,size.y+.08f,.02f),dark,false);
        var label=Label(name,position,size.x,size.y,fontSize); label.text=text; return label;
    }
    static void Toggle(string name,Vector3 position,GameObject target,bool initiallyEnabled)
    {
        var go=Box(name+" control",position,new Vector3(1.05f,.23f,.13f),dark);
        var value=go.AddUdonSharpComponent<BirdLabToggle>(); value.target=target; value.title=name; value.initiallyEnabled=initiallyEnabled;
        value.label=Label(name+" label",position-Vector3.forward*.08f,1,.2f,32); value.label.text=name+(initiallyEnabled?" / ON":" / OFF");
        UdonSharpEditorUtility.CopyProxyToUdon(value);
        var vm=UdonSharpEditorUtility.GetBackingUdonBehaviour(value); vm.proximity=3; vm.interactText="Toggle "+name;
    }
    public static void Compile()
    {
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
        if(UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) throw new Exception("Udon compile failure");
    }
    public static void RefineLayout()
    {
        try
        {
            var scene=EditorSceneManager.OpenScene(ScenePath);
            foreach(var text in UnityEngine.Object.FindObjectsOfType<Text>(true))
            {
                string name=text.transform.parent.name;
                int size=name=="Welcome"?84:name=="Directions"?60:name=="Bone availability"?54:name=="Tracking origins"?50:name=="Mirror instructions"?50:name=="Reference sizes"?60:name.EndsWith("label")?64:84;
                text.fontSize=size;
            }
            foreach(string name in new[]{"Floor","Stone"})
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+name+".mat");
                material.shader=Shader.Find("Standard"); material.color=name=="Floor"?new Color(.36f,.42f,.43f):new Color(.63f,.66f,.6f);
                material.SetFloat("_Glossiness",0); material.SetFloat("_Metallic",0); EditorUtility.SetDirty(material);
            }
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets(); Finish("lab-layout",true,"Larger readable labels and tinted standard materials saved.");
        }
        catch(Exception e) { Finish("lab-layout",false,e.ToString()); }
    }
    public static void BuildAndroid() { Build(false); }
    public static void BuildAndTestAndroid() { Build(true); }
    static async void Build(bool launch)
    {
        File.WriteAllText("lab-build-result.txt","PENDING");
        bool hadPref=EditorPrefs.HasKey("VRC.SDKBase_StripAllShaders"), oldPref=EditorPrefs.GetBool("VRC.SDKBase_StripAllShaders");
        try
        {
            if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.Android) throw new Exception("Android target required");
            EditorSceneManager.OpenScene(ScenePath); Compile(); descriptor=UnityEngine.Object.FindObjectOfType<VRCSceneDescriptor>();
            panel=ScriptableObject.CreateInstance<VRCSdkControlPanel>(); var builder=new VRCSdkControlPanelWorldBuilder(); builder.RegisterBuilder(panel);
            if(!builder.IsValidBuilder(out string reason)) throw new Exception(reason);
            string sdkErrors="",artifact=null;
            builder.OnSdkBuildError+=(_,e)=>sdkErrors+=e+"\n";
            builder.OnSdkBuildSuccess+=(_,path)=>artifact=path;
            if(launch) await builder.BuildAndTest(); else artifact=await builder.Build();
            if(!string.IsNullOrEmpty(sdkErrors) || string.IsNullOrEmpty(artifact) || !File.Exists(artifact)) throw new Exception("SDK build did not produce a successful artifact: "+sdkErrors);
            Directory.CreateDirectory("../Validation/TrackingLab"); string output="../Validation/TrackingLab/BirdTrackingLab_Android.vrcw"; File.Copy(artifact,output,true);
            string hash; using(var stream=File.OpenRead(output)) using(var sha=SHA256.Create()) hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
            // The upload path performs these same platform size checks after BuildWithSignature.
            if(ValidationEditorHelpers.CheckIfAssetBundleFileTooLarge(ContentType.World,output,out int size,true)) throw new Exception("Upload compressed size check failed: "+size);
            if(ValidationEditorHelpers.CheckIfUncompressedAssetBundleFileTooLarge(ContentType.World,out int unpacked,true)) throw new Exception("Upload uncompressed size check failed: "+unpacked);
            Finish("lab-build",true,"SDK Android world build; bytes="+new FileInfo(output).Length+" SHA256="+hash+"; normal SDK validation passed; "+(launch?"BuildAndTest transfer/launch requested; inspect device for actual load.":"not launched.") ,false);
        }
        catch(Exception e)
        {
            string detail=e.ToString();
            if(panel!=null && descriptor!=null) detail+="\nSDK issues: "+string.Join("\n",panel.GetGuiErrorsOrIssuesForItem(descriptor).Select(i=>i.issueText));
            Finish("lab-build",false,detail,false);
        }
        finally
        {
            if(hadPref) EditorPrefs.SetBool("VRC.SDKBase_StripAllShaders",oldPref); else EditorPrefs.DeleteKey("VRC.SDKBase_StripAllShaders");
            if(panel!=null) UnityEngine.Object.DestroyImmediate(panel);
            EditorApplication.Exit(File.ReadAllText("lab-build-result.txt").StartsWith("PASS:")?0:1);
        }
    }
    public static void Finish(string stem,bool success,string message,bool exit=true)
    {
        File.WriteAllText(stem+"-result.txt",(success?"PASS: ":"FAIL: ")+message); if(exit) EditorApplication.Exit(success?0:1);
    }
}
#endif
