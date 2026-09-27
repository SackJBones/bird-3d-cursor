#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UdonSharpEditor;
using VRC.SDKBase;
using VRC.SDK3.Components;

[DefaultExecutionOrder(32000)] // Observe after the SDK PostLateUpdater (31000).
public class UnityTrackingLabChecks : MonoBehaviour
{
    const string Active="Bird.TrackingLab.Checks";
    float deadline,next;
    int stage;
    int cadenceFrames;
    public static void Run()
    {
        File.WriteAllText("lab-check-result.txt","PENDING");
        EditorSceneManager.OpenScene("Assets/BirdWorld/Scenes/BirdTrackingLab.unity");
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
        if(UdonSharp.UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) throw new Exception("Udon compile failure");
        SessionState.SetBool(Active,true); EditorApplication.isPlaying=true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Begin() { if(SessionState.GetBool(Active,false)) new GameObject("Tracking lab checks").AddComponent<UnityTrackingLabChecks>(); }
    void Start() { deadline=Time.unscaledTime+60; }
    void Update()
    {
        if(!SessionState.GetBool(Active,false)) return;
        try
        {
            if(Time.unscaledTime>deadline) throw new Exception("ClientSim lab timeout");
            if(stage==3) return;
            if(!Utilities.IsValid(Networking.LocalPlayer) || Time.timeSinceLevelLoad<3 || Time.unscaledTime<next) return;
            var probe=FindObjectOfType<BirdHandDataProbe>(true); var pv=UdonSharpEditorUtility.GetBackingUdonBehaviour(probe);
            var control=GameObject.Find("Hand markers control").GetComponent<BirdLabToggle>(); var cv=UdonSharpEditorUtility.GetBackingUdonBehaviour(control);
            var mirrorControl=GameObject.Find("Mirror control").GetComponent<BirdLabToggle>(); var mv=UdonSharpEditorUtility.GetBackingUdonBehaviour(mirrorControl);
            var mirror=(GameObject)mv.GetProgramVariable("target");
            if(stage==0 || stage==2)
            {
                Require((int)pv.GetProgramVariable("leftAvailable")==16 && (int)pv.GetProgramVariable("rightAvailable")==16,"Real SDK bone availability");
                var markers=(Transform[])pv.GetProgramVariable("markers"); Require(markers.Length==32,"32 authored markers");
                foreach(var marker in markers) Require(marker.gameObject.activeSelf && marker.GetComponent<Collider>()==null,"Live noncolliding marker");
                var status=FindObjectOfType<BirdLabStatus>(); var sv=UdonSharpEditorUtility.GetBackingUdonBehaviour(status);
                Require((bool)sv.GetProgramVariable("announced"),"Local player runtime heartbeat");
                Require(Vector3.Distance(((Transform)sv.GetProgramVariable("leftOrigin")).position,Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position)<.002f,"Tracking-origin binding");
                Require(!mirror.activeSelf,"Mirror defaults off / restores off");
                if(stage==2)
                {
                    var descriptor=FindObjectOfType<VRCSceneDescriptor>();
                    Require(Physics.Raycast(descriptor.spawns[0].position+Vector3.up,Vector3.down,out RaycastHit hit,2) && hit.collider.name=="Laboratory floor","Spawn has floor support");
                    stage=3; return;
                }
                Require(cv.RunEvent("_interact"),"Native hand toggle event"); Require(mv.RunEvent("_interact"),"Native mirror toggle event");
            }
            else
            {
                Require((int)pv.GetProgramVariable("leftAvailable")==0 && (int)pv.GetProgramVariable("rightAvailable")==0,"Disabled overlay clears counts");
                foreach(var marker in (Transform[])pv.GetProgramVariable("markers")) Require(!marker.gameObject.activeInHierarchy,"No disabled marker remains visible");
                Require(mirror.activeSelf,"Mirror toggles on");
                cv.RunEvent("_interact"); mv.RunEvent("_interact");
            }
            stage++; next=Time.unscaledTime+.5f;
        }
        catch(Exception e) { Finish(false,e.ToString()); }
    }
    void LateUpdate()
    {
        if(!SessionState.GetBool(Active,false) || stage!=3) return;
        try
        {
            var probe=FindObjectOfType<BirdHandDataProbe>(); var pv=UdonSharpEditorUtility.GetBackingUdonBehaviour(probe);
            var status=FindObjectOfType<BirdLabStatus>(); var sv=UdonSharpEditorUtility.GetBackingUdonBehaviour(status);
            var markers=(Transform[])pv.GetProgramVariable("markers"); var bones=(int[])pv.GetProgramVariable("bones");
            var left=(Transform)sv.GetProgramVariable("leftOrigin"); var right=(Transform)sv.GetProgramVariable("rightOrigin");
            for(int i=0;i<markers.Length;i++) Require(Vector3.Distance(markers[i].position,Networking.LocalPlayer.GetBonePosition((HumanBodyBones)bones[i]))<.0001f,"Same-frame avatar bone "+i);
            Require(Vector3.Distance(left.position,Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position)<.0001f,"Same-frame left origin");
            Require(Vector3.Distance(right.position,Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position)<.0001f,"Same-frame right origin");
            if(++cadenceFrames==30)
            {
                Capture("spawn",new Vector3(0,1.65f,-3),new Vector3(0,1.6f,2));
                Capture("overview",new Vector3(4.7f,2.6f,-4.8f),new Vector3(-.3f,1.5f,2));
                Finish(true,"Saved scene/compiled Udon: all 34 markers restored to SDK positions on 30 consecutive post-IK frames; both native Interact toggles, disable/recovery, spawn floor and two renders. Synthetic ClientSim avatar; device validation separate."); return;
            }
            // Perturb only the output after observing it. A throttled updater leaves
            // this stale position next frame even when the simulator hand is still.
            foreach(var marker in markers) marker.position+=Vector3.up;
            left.position+=Vector3.up; right.position+=Vector3.up;
        }
        catch(Exception e) { Finish(false,e.ToString()); }
    }
    static void Require(bool condition,string message) { if(!condition) throw new Exception(message); }
    static void Capture(string name,Vector3 position,Vector3 look)
    {
        Directory.CreateDirectory("../Validation/TrackingLab");
        var camera=new GameObject("Lab documentation capture").AddComponent<Camera>(); camera.transform.position=position; camera.transform.LookAt(look); camera.fieldOfView=75;
        // Exclude the simulator's overlay/player, not the authored world-space text (layer 0).
        camera.cullingMask=~((1<<5)|(1<<9)|(1<<10)|(1<<19));
        var target=new RenderTexture(1600,1000,24){antiAliasing=4}; camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
        var image=new Texture2D(1600,1000,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1600,1000),0,0); image.Apply();
        File.WriteAllBytes("../Validation/TrackingLab/"+name+".png",image.EncodeToPNG()); RenderTexture.active=null; camera.targetTexture=null; target.Release(); Destroy(target); Destroy(image); Destroy(camera.gameObject);
    }
    static void Finish(bool success,string message) { SessionState.SetBool(Active,false); File.WriteAllText("lab-check-result.txt",(success?"PASS: ":"FAIL: ")+message); EditorApplication.Exit(success?0:1); }
}
#endif
