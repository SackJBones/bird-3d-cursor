#if UNITY_EDITOR
using System;
using System.IO;
using Bird3DCursor.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class UnityDepthVisualChecks : MonoBehaviour
{
    public static string CheckMath()
    {
        int checks=0;
        foreach (BirdDepthVisual.SizeMode mode in Enum.GetValues(typeof(BirdDepthVisual.SizeMode)))
        {
            foreach (float distance in new[] { .02f,.2f,.8f,1f,2f,3f,4f })
            {
                Require(BirdDepthVisual.TargetDiameter(distance,mode) == .032f, "Working-volume size changed"); checks++;
                Require(BirdDepthVisual.AdvanceDiameter(50000,50000,distance,.001f,mode) == .032f, "Giant return into working volume"); checks++;
                Require(BirdDepthVisual.TrailWidth(distance) == .002f, "Working-volume trail widened"); checks++;
            }
        }
        float size=.032f;
        float farTarget=BirdDepthVisual.TargetDiameter(1000,BirdDepthVisual.SizeMode.InflationWithLag);
        size=BirdDepthVisual.AdvanceDiameter(size,farTarget,1000,1f/72,BirdDepthVisual.SizeMode.InflationWithLag);
        Require(size>.032f && size<farTarget*.1f,"Outward growth did not lag"); checks++;
        Require(BirdDepthVisual.AdvanceDiameter(size,.032f,.3f,1f/72,BirdDepthVisual.SizeMode.InflationWithLag)==.032f,"One-frame return oversize"); checks++;
        float last=0;
        foreach(float distance in new[]{.01f,1f,4f,20f,100f,100.001f,1000f,1e6f,1e9f,1e12f})
        {
            float r=BirdDepthVisual.RenderDistance(distance);
            Require(r>=last && r<=500 && !float.IsNaN(r),"Depth proxy invalid"); last=r; checks++;
            Vector3 p=new Vector3(.2f,0,1).normalized*distance;
            Vector3 projected=BirdDepthVisual.Project(p,Vector3.zero);
            Require(Vector3.Angle(projected,p)<.05f,"Proxy changed logical direction"); checks++;
        }
        Require(Mathf.Abs(BirdDepthVisual.TargetDiameter(4.001f,BirdDepthVisual.SizeMode.Inflation)-.032f)<.000001f,"Inflation onset jumps"); checks++;
        Require(Mathf.Abs(BirdDepthVisual.TrailWidth(4.001f)-.002f)<.000001f,"Trail onset jumps"); checks++;
        var alternate = new BirdDepthStyle { nearDiameter=.05f, workingDistance=6f, nearTrailWidth=.003f };
        Require(BirdDepthVisual.TargetDiameter(5,BirdDepthVisual.SizeMode.Inflation,alternate)==.05f,"Custom working volume ignored"); checks++;
        Require(BirdDepthVisual.AdvanceDiameter(100,100,5,.01f,BirdDepthVisual.SizeMode.InflationWithLag,alternate)==.05f,"Custom return unsafe"); checks++;
        Require(BirdDepthVisual.TrailWidth(5,alternate)==.003f,"Custom trail ignored"); checks++;
        // After the inflation band, the solid marker must shrink in visual angle.
        float angle20=BirdDepthVisual.TargetDiameter(20,BirdDepthVisual.SizeMode.Inflation)/20;
        float angle200=BirdDepthVisual.TargetDiameter(200,BirdDepthVisual.SizeMode.Inflation)/200;
        Require(angle200<angle20*.4f,"Far core became constant angular size"); checks++;
        return "PASS: "+checks+" depth visual checks; constant 32mm/2mm cursor/trail through 4m, immediate far-to-near shrink, outward growth lag, shrinking far core, finite monotonic direction-preserving render proxy to 1e12m";
    }

    public static void Render()
    {
        File.WriteAllText("depth-visual-result.txt","PENDING");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool("Bird.DepthVisual.Render",true);
        EditorApplication.isPlaying=true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Begin()
    {
        if(SessionState.GetBool("Bird.DepthVisual.Render",false)) new GameObject("Depth checks").AddComponent<UnityDepthVisualChecks>();
    }
    void Start()
    {
        if(!SessionState.GetBool("Bird.DepthVisual.Render",false))return;
        try
        {
            string result=CheckMath();
            foreach(var old in FindObjectsOfType<Camera>()) old.gameObject.SetActive(false);
            var camera=new GameObject("Depth capture camera").AddComponent<Camera>();
            camera.transform.position=Vector3.zero;
            camera.nearClipPlane=.005f; camera.farClipPlane=1000;
            camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.012f,.018f,.035f);
            camera.fieldOfView=45;
            var texture=new RenderTexture(1024,1024,24) { antiAliasing=4 };
            camera.targetTexture=texture;
            var visual=new GameObject("Captured cursor").AddComponent<BirdDepthVisual>();
            visual.tint=Color.cyan;
            Directory.CreateDirectory("DepthVisualCaptures");
            float clock=0;
            int captures=0;
            foreach (BirdDepthVisual.SizeMode mode in Enum.GetValues(typeof(BirdDepthVisual.SizeMode)))
            foreach (float distance in new[]{.4f,2f,4f,10f,40f,1000f,1e6f})
            {
                visual.Clear();
                visual.sizeMode=mode;
                for(int i=0;i<100;i++)
                {
                    clock+=1f/72;
                    float t=i/99f;
                    Vector3 p=new Vector3(.12f*Mathf.Sin(t*4),.07f*Mathf.Cos(t*4),1).normalized*distance;
                    visual.Draw(p,camera,false,clock,1f/72);
                }
                camera.Render();
                RenderTexture.active=texture;
                var png=new Texture2D(1024,1024,TextureFormat.RGB24,false);
                png.ReadPixels(new Rect(0,0,1024,1024),0,0); png.Apply();
                File.WriteAllBytes("DepthVisualCaptures/"+mode+"-distance-"+distance.ToString(System.Globalization.CultureInfo.InvariantCulture)+".png",png.EncodeToPNG());
                captures++;
                Destroy(png);
            }
            File.WriteAllText("depth-visual-result.txt",result+"; "+captures+" real Unity render captures from 0.4m to 1,000,000m across 3 modes");
            SessionState.SetBool("Bird.DepthVisual.Render",false);
            EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText("depth-visual-result.txt","FAIL: "+e);Debug.LogException(e);SessionState.SetBool("Bird.DepthVisual.Render",false);EditorApplication.Exit(1);}
    }
    static void Require(bool pass,string message){if(!pass)throw new Exception(message);}
}
#endif
