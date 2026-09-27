#if UNITY_EDITOR && BIRD_OPENXR_ENABLED
using System;
using System.IO;
using Bird3DCursor.Samples;
using Bird3DCursor.UI;
using Bird3DCursor.Manipulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Real host factory, placement, accepted-input binding and normal frame ordering; synthetic tracked states.
public sealed class UnityQuestObjectChecks : MonoBehaviour
{
    const string Active="Bird.Quest.Objects.Checks";
    BirdSphericalSelectorPreview colors;
    BirdPosePreview poses;
    BirdPointerInput left,right;
    BirdCursorState leftState,rightState;
    BirdGrabTarget item;
    Camera view;
    Vector3 home,anchor,end,palm,span;
    int stage,checks;
    float started,deadline;
    public static void Run()
    {
        File.WriteAllText("objects-result.txt","PENDING"); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Active,true); EditorApplication.isPlaying=true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Begin() { if(SessionState.GetBool(Active,false)) new GameObject("Live objects host checks").AddComponent<UnityQuestObjectChecks>(); }
    void Start()
    {
        try
        {
            deadline=Time.unscaledTime+60;
            view=new GameObject("Synthetic tracked view").AddComponent<Camera>(); view.transform.position=new Vector3(3,1.85f,1); view.transform.rotation=Quaternion.Euler(0,35,0); view.fieldOfView=80;
            UnityQuestVista.Create(view);
            left=new GameObject("Left accepted input").AddComponent<BirdPointerInput>(); right=new GameObject("Right accepted input").AddComponent<BirdPointerInput>();
            leftState=left.gameObject.AddComponent<BirdCursorState>(); rightState=right.gameObject.AddComponent<BirdCursorState>();
            colors=new GameObject("Live colors").AddComponent<BirdSphericalSelectorPreview>(); colors.Initialize(new[]{left,right}); UnityQuestHands.PlaceSelector(colors,view);
            poses=UnityQuestHands.CreateObjects(left,right); UnityQuestHands.PlaceObjects(poses,view); UnityQuestHands.SetExperience(colors,poses,true);
            Require(poses.externalLeft==left && poses.externalRight==right && poses.TwoHandPose.first==left && poses.TwoHandPose.second==right && poses.View==null && !poses.desktopInput,"Host owns both accepted sources and camera");
            Require(Vector3.Distance(poses.transform.position,view.transform.position-Vector3.up*1.65f)<1e-6f && Quaternion.Angle(poses.transform.rotation,Quaternion.Euler(0,35,0))<.01f,"Head placement moves experience only");
            Require(!colors.gameObject.activeInHierarchy && poses.gameObject.activeInHierarchy,"Object mode excludes color input consumers");
            Bind(poses.Items[0]); Feed(anchor,false,false); Capture("objects-forward");
        }
        catch(Exception e) { Finish(false,e.ToString()); }
    }
    void Bind(BirdGrabTarget target)
    {
        item=target; home=item.transform.position; anchor=item.Volume.transform.TransformPoint(item.Volume.center); end=anchor+item.Destinations[1].transform.position-home;
        palm=poses.transform.TransformPoint(new Vector3(-.2f,1.2f,0)); span=poses.transform.TransformVector(Vector3.right*.4f);
    }
    void Feed(Vector3 point,bool primary,bool secondary)
    {
        leftState.handRoot=palm; leftState.position=point; leftState.tracking=leftState.poseValid=true; leftState.selected=primary;
        rightState.handRoot=palm+span; rightState.position=item.Volume.transform.TransformPoint(item.Volume.center); rightState.tracking=rightState.poseValid=true; rightState.selected=secondary;
        UnityQuestHands.PublishUiSample(left,leftState); UnityQuestHands.PublishUiSample(right,rightState);
        Require(left.Origin==palm && left.Position==point && right.Origin==palm+span,"Accepted binding preserves hand roots and logical point");
    }
    void Update()
    {
        if(!SessionState.GetBool(Active,false)) return;
        try
        {
            if(Time.unscaledTime>deadline) throw new Exception("Frame sequence timed out");
            switch(stage)
            {
                case 0: Feed(anchor,false,false); stage++; break;
                case 1: Feed(anchor,true,false); stage++; break;
                case 2: Require(poses.Interactor.ActiveTarget==item,"Live-bound normal-frame primary pickup"); Feed(anchor,true,true); stage++; break;
                case 3:
                    Require(poses.TwoHandPose.IsEngaged,"Live-bound normal-frame secondary clutch"); span=poses.transform.TransformVector(Quaternion.Euler(0,90,0)*Vector3.right*.5f); started=Time.unscaledTime; stage++; break;
                case 4:
                    Feed(end,true,true); if(Time.unscaledTime-started<.8f) break;
                    Require(poses.Interactor.ReadyToPlace,"Live-bound two-hand pose matches dock"); Capture("objects-table-ready"); Feed(end,false,false); stage++; break;
                case 5:
                    Require(poses.Interactor.ActiveTarget==null && Quaternion.Angle(item.transform.localRotation,Quaternion.Euler(0,90,0))<.01f && Vector3.Distance(item.transform.localScale,Vector3.one*1.25f)<.00001f,"Exact tabletop drop");
                    Bind(poses.Items[1]); Feed(anchor,false,false); stage++; break;
                case 6: Feed(anchor,true,false); stage++; break;
                case 7: Require(poses.Interactor.ActiveTarget==item,"Live-bound far building pickup"); Feed(anchor,true,true); stage++; break;
                case 8:
                    Require(poses.TwoHandPose.IsEngaged,"Far building has same second-hand clutch"); span=poses.transform.TransformVector(Quaternion.Euler(0,60,0)*Vector3.right*.5f); started=Time.unscaledTime; stage++; break;
                case 9:
                    Feed(palm,true,true); if(Time.unscaledTime-started<.5f) break;
                    Bounds box=item.Volume.bounds; Require(Vector3.Distance(box.ClosestPoint(view.transform.position),view.transform.position)>450,"Closing primary hand keeps full building far away");
                    Capture("objects-building-bounded"); UnityQuestHands.SetExperience(colors,poses,false); stage++; break;
                case 10:
                    Require(poses.Interactor.ActiveTarget==null && !poses.TwoHandPose.IsEngaged && Vector3.Distance(item.transform.position,home)<.002f && Vector3.Distance(item.transform.localScale,Vector3.one)<1e-6f,"Mode switch immediately rolls back complete pose");
                    Require(colors.gameObject.activeInHierarchy && !poses.gameObject.activeInHierarchy,"Colors mode excludes object consumers");
                    leftState.position=colors.Choices[4].transform.position; UnityQuestHands.PublishUiSample(left,leftState); stage++; break;
                case 11:
                    Require(colors.SelectionCount==0,"Held click cannot transfer into color action"); leftState.selected=false; UnityQuestHands.PublishUiSample(left,leftState); stage++; break;
                case 12: leftState.selected=true; UnityQuestHands.PublishUiSample(left,leftState); stage++; break;
                case 13:
                    Require(colors.SelectionCount==1 && colors.Scroll.ActivePointer==null,"Fresh color action still works inside larger sphere");
                    Capture("colors-restored"); UnityQuestHands.SetExperience(colors,poses,true); Feed(anchor,true,true); stage++; break;
                case 14:
                    Require(poses.Interactor.ActiveTarget==null && !poses.TwoHandPose.IsEngaged,"Held clicks cannot transfer back into object mode");
                    Finish(true,checks+" host-binding/frame assertions; two-hand tabletop dock, distant rotated/resized boundary, mode-switch full rollback, no held-edge transfer, restored color action and four real vista captures. Synthetic accepted states, not physical XR tracking/feel or VRChat validation."); break;
            }
        }
        catch(Exception e) { Finish(false,e.ToString()); }
    }
    void Require(bool value,string message) { checks++; if(!value) throw new Exception("Host assertion "+checks+": "+message); }
    void Capture(string name)
    {
        Directory.CreateDirectory("VistaCaptures"); poses.UpdateFeedback(); colors.RefreshVisuals();
        var rt=new RenderTexture(1600,1000,24){antiAliasing=4}; var pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false); view.targetTexture=rt; view.Render(); RenderTexture.active=rt;
        pixels.ReadPixels(new Rect(0,0,1600,1000),0,0); pixels.Apply(); File.WriteAllBytes("VistaCaptures/"+name+".png",pixels.EncodeToPNG());
        RenderTexture.active=null; view.targetTexture=null; rt.Release(); Destroy(rt); Destroy(pixels);
    }
    void Finish(bool ok,string message) { SessionState.SetBool(Active,false); File.WriteAllText("objects-result.txt",(ok?"PASS: ":"FAIL: ")+message); EditorApplication.Exit(ok?0:1); }
}
#endif
