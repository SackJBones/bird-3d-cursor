#if UNITY_EDITOR
using System;
using System.IO;
using Bird3DCursor.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class UnityQuestVistaChecks : MonoBehaviour
{
    const string Active="Bird.Vista.Checks";
    public static void Run()
    {
        File.WriteAllText("vista-result.txt","PENDING");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Active,true); EditorApplication.isPlaying=true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Begin()
    { if (SessionState.GetBool(Active,false)) new GameObject("Vista render checks").AddComponent<UnityQuestVistaChecks>(); }
    void Start()
    {
        try
        {
            Directory.CreateDirectory("VistaCaptures");
            var camera=new GameObject("View").AddComponent<Camera>();
            camera.transform.position=new Vector3(0,1.65f,0); camera.nearClipPlane=.005f; camera.farClipPlane=1000; camera.fieldOfView=80;
            camera.clearFlags=CameraClearFlags.SolidColor;
            var root=UnityQuestVista.Create(camera);
            var table=root.transform.Find("0.75m table");
            if (Mathf.Abs(table.position.y+table.localScale.y*.5f-.75f)>.001f) throw new Exception("Table scale reference");
            if (root.transform.Find("300 m - 65 m tall").localScale.y!=65) throw new Exception("Landmark scale reference");
            var texture=new RenderTexture(1600,1000,24) { antiAliasing=4 }; var pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            camera.targetTexture=texture;
            var cursor=new GameObject("Inflate reference cursor").AddComponent<BirdDepthVisual>();
            cursor.sizeMode=BirdDepthVisual.SizeMode.Inflation; cursor.showTrail=false;
            Capture(camera,texture,pixels,"house-vista");
            cursor.Draw(new Vector3(0,3,10),camera,false,0,1f/72);
            Capture(camera,texture,pixels,"cursor-10m");
            cursor.Draw(new Vector3(0,18,100),camera,false,1,1f/72);
            Capture(camera,texture,pixels,"cursor-100m");
            cursor.Draw(new Vector3(-30,65,300),camera,false,2,1f/72);
            Capture(camera,texture,pixels,"cursor-300m");
            cursor.gameObject.SetActive(false); camera.transform.rotation=Quaternion.Euler(12,-35,0);
            Capture(camera,texture,pixels,"furniture");
            camera.transform.position=new Vector3(10,5,16); camera.transform.LookAt(new Vector3(0,1.5f,0));
            Capture(camera,texture,pixels,"house-exterior");
            camera.targetTexture=null; RenderTexture.active=null; texture.Release(); Destroy(texture); Destroy(pixels);
            File.WriteAllText("vista-result.txt","PASS: actual Unity house/vista rendering; six captures; 0.75m table and 65m distant building checked. Not headset comfort/performance validation.");
            SessionState.SetBool(Active,false); EditorApplication.Exit(0);
        }
        catch(Exception e) { File.WriteAllText("vista-result.txt","FAIL: "+e); SessionState.SetBool(Active,false); EditorApplication.Exit(1); }
    }
    static void Capture(Camera camera,RenderTexture target,Texture2D readback,string name)
    {
        camera.Render(); RenderTexture.active=target; readback.ReadPixels(new Rect(0,0,1600,1000),0,0); readback.Apply();
        File.WriteAllBytes("VistaCaptures/"+name+".png",readback.EncodeToPNG());
    }
}
#endif
