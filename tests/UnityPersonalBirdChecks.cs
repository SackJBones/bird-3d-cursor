#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UdonSharpEditor;
using VRC.SDKBase;
using VRC.Udon;

// Exercises the saved prefab through compiled Udon in normal ClientSim frames.
// Synthetic bone returns isolate acquisition/lifecycle from physical hand quality.
[DefaultExecutionOrder(32000)]
public class UnityPersonalBirdChecks : MonoBehaviour
{
    const string Active = "Bird.Personal.Checks";
    const string Folder = "../Validation/CoastalWorld/PersonalBird";
    static Func<VRCPlayerApi,HumanBodyBones,Vector3> originalPosition;
    static Func<VRCPlayerApi,HumanBodyBones,Quaternion> originalRotation;
    static readonly Dictionary<HumanBodyBones,Vector3> positions = new Dictionary<HumanBodyBones,Vector3>();
    static readonly Dictionary<HumanBodyBones,Quaternion> rotations = new Dictionary<HumanBodyBones,Quaternion>();
    static readonly string[] suffix = { "Hand","ThumbProximal","ThumbIntermediate","ThumbDistal","IndexProximal","IndexIntermediate","IndexDistal","MiddleProximal","MiddleIntermediate","MiddleDistal","RingProximal","RingIntermediate","RingDistal","LittleProximal","LittleIntermediate","LittleDistal" };
    BirdPersonalStation station, other;
    BirdPointPresentation[] views;
    IEnumerator sequence;
    float deadline;
    int assertions, frames;
    public static void Run()
    {
        File.WriteAllText("coastal-bird-check-result.txt", "PENDING");
        try
        {
            // Compare authored value settings with the actual latest lab, not a second list of defaults.
            EditorSceneManager.OpenScene("Assets/BirdWorld/Scenes/BirdTrackingLab.unity");
            var lab = FindObjectsOfType<BirdAvatarHandInput>().OrderBy(i=>i.rightHand).ToArray();
            string[] expected = lab.Select(Configuration).ToArray();
            EditorSceneManager.OpenScene("Assets/BirdWorld/Scenes/BirdCoastalWorld.unity");
            var stations = FindObjectsOfType<BirdPersonalStation>(true);
            if (stations.Length != 1) throw new Exception("Exactly one saved station required");
            var actual = stations[0].personalRig.GetComponentsInChildren<BirdAvatarHandInput>(true).OrderBy(i=>i.rightHand).ToArray();
            if (actual.Length != 2 || !actual.Select(Configuration).SequenceEqual(expected)) throw new Exception("World defaults differ from Lab 14");
            // Independent prefab instance stands in for independent client-local state.
            // This is not a multi-client network test.
            var clone = Instantiate(stations[0].gameObject); clone.name = "Isolation fixture"; clone.transform.position += Vector3.right*1000;
            UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
            if (UdonSharp.UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) throw new Exception("Udon compile failure");
            SessionState.SetBool(Active,true); EditorApplication.isPlaying = true;
        }
        catch(Exception e) { File.WriteAllText("coastal-bird-check-result.txt","FAIL: "+e); EditorApplication.Exit(1); }
    }
    static string Configuration(BirdAvatarHandInput input)
    {
        var result = new List<string>();
        foreach(var source in new UdonSharp.UdonSharpBehaviour[]{input,input.cursor,input.cursor.fitter,input.cursor.adaptiveFilter,input.cursor.centerFilter})
        {
            if (source == null) return "Missing policy";
            foreach(var field in source.GetType().GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance))
            {
                if (field.GetCustomAttributes(typeof(HideInInspector),true).Length>0) continue;
                if (field.FieldType==typeof(float)||field.FieldType==typeof(bool)||field.FieldType==typeof(int))
                    result.Add(source.GetType().Name+"."+field.Name+"="+Convert.ToString(field.GetValue(source),System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        return string.Join("\n",result);
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Begin() { if (SessionState.GetBool(Active,false)) new GameObject("Personal Bird checks").AddComponent<UnityPersonalBirdChecks>(); }
    void Start() { deadline=Time.unscaledTime+100; }
    void LateUpdate()
    {
        if (!SessionState.GetBool(Active,false)) return;
        try
        {
            if (Time.unscaledTime>deadline) throw new Exception("Personal Bird test timed out");
            if (!Utilities.IsValid(Networking.LocalPlayer) || Time.timeSinceLevelLoad<3) return;
            if (sequence==null)
            {
                var stations=FindObjectsOfType<BirdPersonalStation>();
                station=stations.Single(s=>s.name!="Isolation fixture"); other=stations.Single(s=>s.name=="Isolation fixture");
                views=station.personalRig.GetComponentsInChildren<BirdPointPresentation>(true);
                originalPosition=VRCPlayerApi._GetBonePosition; originalRotation=VRCPlayerApi._GetBoneRotation;
                VRCPlayerApi._GetBonePosition=Position; VRCPlayerApi._GetBoneRotation=Rotation;
                sequence=Scenarios();
            }
            frames++;
            if (!sequence.MoveNext()) Finish(true,"Saved Lab 14 settings match; compiled Udon "+assertions+" assertions / "+frames+" frames. Touch and native acquisition, independent local instances, both live cursors, trails, loss/recovery and logical-depth rendering pass. Synthetic SDK bones; not physical feel or multiplayer acceptance.");
        }
        catch(Exception e) { Finish(false,e.ToString()); }
    }
    IEnumerator Scenarios()
    {
        Require(!station.personalRig.activeSelf&&!Get<bool>(VM(station),"acquired"),"Off on arrival");
        Require(views.Length==2,"Two point embodiments");
        SetHands(100,Quaternion.identity,Vector3.zero); yield return null;
        Require(!station.personalRig.activeSelf,"Distant hands do not collect Bird");
        Vector3 palm=positions[Bone(0,4)]*.3f+positions[Bone(0,13)]*.3f+positions[Bone(0,1)]*.4f;
        Vector3 shift=station.touchPoint.position-palm;
        SetHands(100,Quaternion.identity,shift);
        float until=Time.time+.2f; while(Time.time<until) yield return null;
        Require(Get<bool>(VM(station),"acquired")&&station.personalRig.activeSelf,"Touch acquires Bird without a controller");
        Require(!other.personalRig.activeSelf&&!Get<bool>(VM(other),"acquired"),"Other local instance unaffected");
        Require(VM(station).RunEvent("_interact"),"Normal VRChat Use event"); yield return null;
        Require(!station.personalRig.activeSelf,"Use puts Bird away");
        until=Time.time+.2f; while(Time.time<until) yield return null;
        Require(!station.personalRig.activeSelf,"Hand must withdraw before touch reacquires");
        SetHands(100,Quaternion.identity,Vector3.zero); yield return null;
        SetHands(100,Quaternion.identity,shift);
        until=Time.time+.2f; while(Time.time<until) yield return null;
        Require(station.personalRig.activeSelf,"Reentry reacquires unlimited Bird");
        for(int i=0;i<36;i++)
        {
            SetHands(110-i*2,Quaternion.AngleAxis(i*2,Vector3.up),Vector3.zero); yield return null;
            foreach(var view in views)
            {
                var cursor=VM(view.cursor); var input=station.personalRig.GetComponentsInChildren<BirdAvatarHandInput>().Single(x=>x.cursor==view.cursor);
                Require(Get<bool>(cursor,"poseValid")&&view.core.enabled,"Live point after automatic input");
                Require(Get<int>(VM(input),"sampledFrame")==Time.frameCount,"Post-IK same-frame input");
                Require(!Get<bool>(cursor,"clicksAllowed"),"Unvalidated avatar clicks remain disabled");
                Require(Get<int>(VM(view),"trailCount")<=32,"Trail budget bounded");
            }
        }
        Require(views.All(v=>v.trailRenderer.enabled&&v.trailMesh.sharedMesh!=null),"Both live trails built by Udon");
        positions.Keys.ToList().ForEach(k=>positions[k]=Vector3.zero); yield return null;
        Require(views.All(v=>!v.core.enabled&&!v.trailRenderer.enabled&&Get<int>(VM(v),"trailCount")==0),"Loss clears points and trail immediately");
        SetHands(100,Quaternion.identity,Vector3.zero); yield return null;
        Require(views.All(v=>v.core.enabled&&Get<int>(VM(v),"trailCount")==1),"Recovery starts a fresh trail");
        // Isolate presentation after proving the real adapter drives it.
        foreach(var input in station.personalRig.GetComponentsInChildren<BirdAvatarHandInput>()) VM(input).enabled=false;
        Vector3 eye=Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
        foreach(float range in new[]{1f,4f,30f,10000f})
        {
            foreach(var view in views) VM(view).SendCustomEvent("Clear");
            float start=Time.realtimeSinceStartup;
            while(Time.realtimeSinceStartup-start<.2f)
            {
                foreach(var view in views)
                {
                    float sweep=(Time.realtimeSinceStartup-start)/.2f;
                    VM(view.cursor).SetProgramVariable("position",eye+new Vector3((sweep-.5f)*.12f*range,0,range));
                    VM(view.cursor).SetProgramVariable("poseValid",true);
                }
                yield return null;
            }
            foreach(var view in views)
            {
                Require(Vector3.Distance(eye,view.core.transform.position)<501,"Astronomical point stays in render shell");
                if(range<=1) Require(Mathf.Abs(view.core.transform.lossyScale.x-.032f)<.00001f,"Near point stays marble-sized");
                Require(view.trailMesh.sharedMesh.vertices.All(v=>!float.IsNaN(v.x)&&!float.IsInfinity(v.x)),"Finite ribbon geometry");
            }
            Render(views[0],eye,"range-"+range.ToString("0"));
        }
        VM(station).RunEvent("_interact"); yield return null;
        Require(views.All(v=>!v.core.enabled&&!v.trailRenderer.enabled),"Put-away clears presentation");
        VM(other).RunEvent("_interact"); yield return null;
        Require(other.personalRig.activeSelf&&!station.personalRig.activeSelf,"Either instance can acquire independently");
    }
    void Render(BirdPointPresentation view,Vector3 eye,string name)
    {
        var camera=new GameObject("Point render test").AddComponent<Camera>();camera.transform.position=eye;camera.transform.LookAt(view.core.transform.position);
        camera.fieldOfView=60;camera.nearClipPlane=.03f;camera.farClipPlane=10000;camera.cullingMask=1<<31;camera.backgroundColor=Color.black;camera.clearFlags=CameraClearFlags.SolidColor;camera.enabled=false;
        var render=new RenderTexture(1024,1024,24){antiAliasing=4};camera.targetTexture=render;var image=new Texture2D(1024,1024,TextureFormat.RGB24,false);
        var objects=new[]{view.core.gameObject,view.halo.gameObject,view.trailMesh.gameObject};var layers=objects.Select(o=>o.layer).ToArray();foreach(var o in objects)o.layer=31;
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=31;wall.transform.position=eye+camera.transform.forward*.25f;wall.transform.rotation=camera.transform.rotation;wall.transform.localScale=new Vector3(4,4,.01f);
        var mat=new Material(Shader.Find("Unlit/Color")){color=Color.black};wall.GetComponent<Renderer>().sharedMaterial=mat;
        try
        {
            for(int blocked=0;blocked<2;blocked++)
            {
                wall.SetActive(blocked==1);camera.Render();RenderTexture.active=render;image.ReadPixels(new Rect(0,0,1024,1024),0,0);image.Apply();RenderTexture.active=null;
                int colored=image.GetPixels32().Count(c=>c.g>40&&c.b>40&&c.r<30);
                if(blocked==0){Directory.CreateDirectory(Folder);File.WriteAllBytes(Folder+"/"+name+".png",image.EncodeToPNG());}
                Require(blocked==0?colored>=3:colored==0,"Actual cursor/trail visibility and logical occlusion "+name+" blocked="+blocked+" pixels="+colored+
                    " core="+view.core.enabled+" position="+view.core.transform.position+" scale="+view.core.transform.lossyScale+" halo="+view.halo.enabled+" width="+view.halo.startWidth+" tint="+view.halo.startColor+" knots="+Get<int>(VM(view),"trailCount")+" trail="+view.trailRenderer.enabled+" headRotation="+Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation);
            }
        }
        finally
        {
            for(int i=0;i<objects.Length;i++)objects[i].layer=layers[i];camera.targetTexture=null;render.Release();DestroyImmediate(camera.gameObject);DestroyImmediate(render);DestroyImmediate(image);DestroyImmediate(wall);DestroyImmediate(mat);
        }
    }
    static void SetHands(float bend,Quaternion rotation,Vector3 shift)
    {
        positions.Clear();rotations.Clear();
        for(int side=0;side<2;side++)
        {
            Vector3 offset=new Vector3(side==0?-.22f:.22f,1.4f,-3)+shift;
            Func<Vector3,Vector3> mirror=p=>new Vector3(side==0?p.x:-p.x,p.y,p.z);
            Func<Vector3,Vector3> point=p=>offset+rotation*mirror(p);
            positions[Bone(side,0)]=point(new Vector3(-.02f,-.07f,0));rotations[Bone(side,0)]=rotation;
            for(int f=0;f<5;f++)
            {
                Vector3 start=f==0?new Vector3(-.045f,-.035f,0):new Vector3(-.025f+(f-1)*.018f,f==2?.005f:f==4?-.01f:0,0);
                float a=f==0?.023f:f==1?.032f:f==2?.038f:f==3?.034f:.027f,b=f==0?.023f:f==1?.02f:f==2?.025f:f==3?.023f:.018f;
                Vector3 da=f==0?new Vector3(-.65f,.76f,0).normalized:Direction(bend*.32f),db=f==0?da:Direction(bend*.72f),dc=f==0?da:Direction(bend);
                int first=1+f*3;Vector3 middle=start+da*a,distal=middle+db*b;
                positions[Bone(side,first)]=point(start);positions[Bone(side,first+1)]=point(middle);positions[Bone(side,first+2)]=point(distal);
                for(int j=0;j<3;j++)rotations[Bone(side,first+j)]=rotation*Quaternion.FromToRotation(Vector3.up,mirror(j==0?da:j==1?db:dc));
            }
        }
    }
    static Vector3 Direction(float degrees){float a=degrees*Mathf.Deg2Rad;return new Vector3(0,Mathf.Cos(a),-Mathf.Sin(a));}
    static HumanBodyBones Bone(int side,int i){return (HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),(side==0?"Left":"Right")+suffix[i]);}
    static Vector3 Position(VRCPlayerApi p,HumanBodyBones b){return p.isLocal&&positions.ContainsKey(b)?positions[b]:originalPosition(p,b);}
    static Quaternion Rotation(VRCPlayerApi p,HumanBodyBones b){return p.isLocal&&rotations.ContainsKey(b)?rotations[b]:originalRotation(p,b);}
    static UdonBehaviour VM(UdonSharp.UdonSharpBehaviour p){return UdonSharpEditorUtility.GetBackingUdonBehaviour(p);}
    static T Get<T>(UdonBehaviour vm,string field){return (T)vm.GetProgramVariable(field);}
    void Require(bool condition,string message){assertions++;if(!condition)throw new Exception(message);}
    void Finish(bool success,string message)
    {
        if(originalPosition!=null)VRCPlayerApi._GetBonePosition=originalPosition;if(originalRotation!=null)VRCPlayerApi._GetBoneRotation=originalRotation;
        SessionState.SetBool(Active,false);Directory.CreateDirectory(Folder);
        string result=(success?"PASS: ":"FAIL: ")+message;
        File.WriteAllText("coastal-bird-check-result.txt",result);File.WriteAllText(Folder+"/check-"+EditorUserBuildSettings.activeBuildTarget+".txt",result);EditorApplication.Exit(success?0:1);
    }
}
#endif
