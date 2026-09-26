#if UNITY_EDITOR && BIRD_OPENXR_ENABLED
using System;
using System.IO;
using Bird3DCursor.Samples;
using Bird3DCursor.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Renders the world-space UI against the actual vista, without physical tracking.
public sealed class UnityQuestUiRenderChecks : MonoBehaviour
{
    const string Active="Bird.Quest.Ui.Render";
    public static void Run()
    {
        File.WriteAllText("ui-render-result.txt","PENDING");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Active,true); EditorApplication.isPlaying=true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Begin() { if(SessionState.GetBool(Active,false)) new GameObject("UI vista render").AddComponent<UnityQuestUiRenderChecks>(); }
    void Start()
    {
        try
        {
            Directory.CreateDirectory("VistaCaptures");
            var camera=new GameObject("View").AddComponent<Camera>();
            camera.transform.position=new Vector3(0,1.65f,0); camera.fieldOfView=80;
            camera.clearFlags=CameraClearFlags.SolidColor;
            UnityQuestVista.Create(camera);
            var left=new GameObject("Left logical point").AddComponent<BirdPointerInput>();
            var right=new GameObject("Right logical point").AddComponent<BirdPointerInput>();
            var ui=new GameObject("World-space selector").AddComponent<BirdSphericalSelectorPreview>();
            ui.Initialize(new[]{left,right});
            UnityQuestHands.PlaceSelector(ui,camera);
            var texture=new RenderTexture(1600,1000,24) { antiAliasing=4 };
            var pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false); camera.targetTexture=texture;
            Action<string> capture=name=>{
                ui.RefreshVisuals(); camera.Render(); RenderTexture.active=texture;
                pixels.ReadPixels(new Rect(0,0,1600,1000),0,0); pixels.Apply();
                File.WriteAllBytes("VistaCaptures/"+name+".png",pixels.EncodeToPNG());
            };
            capture("ui-forward");
            camera.transform.LookAt(ui.Sphere.transform.position);
            left.Submit(camera.transform.position,ui.Choices[4].transform.position,true,false); ui.Interactor.Process();
            left.Submit(camera.transform.position,ui.Choices[4].transform.position,true,true); ui.Interactor.Process(); ui.Scroll.Process(.01f);
            if(ui.SelectionCount!=1 || ui.Scroll.ActivePointer!=null) throw new Exception("Placed UI selection/scroll separation");
            capture("ui-facing");
            File.WriteAllText("ui-render-result.txt","PASS: two real Unity views of spherical UI against the actual bright vista; external-input color selection without scrolling; synthetic camera placement, not XR stereo/headset validation.");
            SessionState.SetBool(Active,false); EditorApplication.Exit(0);
        }
        catch(Exception e)
        {
            File.WriteAllText("ui-render-result.txt","FAIL: "+e); SessionState.SetBool(Active,false); EditorApplication.Exit(1);
        }
    }
}
#endif
