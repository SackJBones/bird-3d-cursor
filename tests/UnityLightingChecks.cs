#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(32000)]
public class UnityLightingChecks:MonoBehaviour
{
    const string Active="Bird.Lighting.Checks",Folder="../Validation/CoastalWorld/Lighting01";
    public static void Run()
    {
        try{EditorSceneManager.OpenScene("Assets/BirdWorld/Scenes/BirdCoastalWorld.unity");SessionState.SetBool(Active,true);EditorApplication.isPlaying=true;}
        catch(Exception e){Finish("coastal-lighting-check",false,e.ToString());}
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void StartChecks(){if(SessionState.GetBool(Active,false))new GameObject("Lighting checks").AddComponent<UnityLightingChecks>();}
    void LateUpdate()
    {
        if(!SessionState.GetBool(Active,false)||Time.timeSinceLevelLoad<4)return;
        SessionState.SetBool(Active,false);
        try
        {
            string folder=Folder+"/"+EditorUserBuildSettings.activeBuildTarget;Directory.CreateDirectory(folder);
            var maps=LightmapSettings.lightmaps;
            Require(maps.Length>0&&maps.Length<=4,"One to four lightmaps");
            Require(maps.All(m=>m.lightmapColor!=null&&m.lightmapDir==null&&m.lightmapColor.width<=1024&&m.lightmapColor.height<=1024),"Bounded non-directional textures");
            Require(LightmapSettings.lightProbes!=null&&LightmapSettings.lightProbes.count>50&&LightmapSettings.lightProbes.count<=512,"Bounded visitor light probes");
            Require(FindObjectsOfType<Light>().All(l=>l.lightmapBakeType==LightmapBakeType.Baked),"No new runtime lights");
            var receivers=FindObjectsOfType<MeshRenderer>().Where(r=>r.enabled&&r.lightmapIndex>=0&&r.lightmapIndex<maps.Length).ToArray();
            Require(receivers.Length>150,"Saved static receivers retain baked assignments");
            var arrival=Capture(folder,"01-arrival",new Vector3(0,1.65f,-10),new Vector3(0,2,8),70);
            Capture(folder,"02-pedestal",new Vector3(0,1.6f,-3.7f),new Vector3(0,1.2f,-1.4f),65);
            Capture(folder,"03-stair-return",new Vector3(2,2.65f,7),new Vector3(0,1.2f,-3),75);
            Capture(folder,"04-left-passage",new Vector3(-17.8f,1.65f,2),new Vector3(-13,2,1),90);
            Capture(folder,"05-rear-wall",new Vector3(0,1.65f,-8),new Vector3(0,5,-18),90);
            Capture(folder,"06-lantern-room",new Vector3(-23,1.65f,1),new Vector3(-18,1.5f,0),85);
            Capture(folder,"07-support",new Vector3(12,2.65f,26),new Vector3(3,7,14),75);
            Capture(folder,"08-exterior",new Vector3(65,22,78),new Vector3(-3,10,15),70);
            File.WriteAllText(folder+"/inventory.txt","Lightmaps="+maps.Length+"; texels="+maps.Sum(m=>(long)m.lightmapColor.width*m.lightmapColor.height)+"; receivers="+receivers.Length+"; probes="+LightmapSettings.lightProbes.count+"; runtime lights=0; quality="+QualitySettings.names[QualitySettings.GetQualityLevel()]+". No physical/performance acceptance.");
            var flat=arrival.Select(p=>Mathf.Max(p.r,Mathf.Max(p.g,p.b))).ToArray();
            Require(flat.Count(v=>v>180)>10000,"Arrival has bright visible architecture");
            Finish("coastal-lighting-check",true,"Saved baked lighting executes under normal "+EditorUserBuildSettings.activeBuildTarget+" quality, bounded maps/probes, "+receivers.Length+" receivers, eight actual renders. Review and physical acceptance remain separate.");
        }catch(Exception e){Finish("coastal-lighting-check",false,e.ToString());}
    }
    static Color32[] Capture(string folder,string name,Vector3 eye,Vector3 target,float fov)
    {
        var camera=new GameObject("Lighting evidence camera").AddComponent<Camera>();camera.enabled=false;camera.transform.position=eye;camera.transform.LookAt(target);
        camera.fieldOfView=fov;camera.nearClipPlane=.03f;camera.farClipPlane=10000;camera.clearFlags=RenderSettings.skybox!=null?CameraClearFlags.Skybox:CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.48f,.68f,.78f);camera.cullingMask=~((1<<18)|(1<<19));
        var texture=new RenderTexture(1200,750,24){antiAliasing=4};camera.targetTexture=texture;var image=new Texture2D(1200,750,TextureFormat.RGB24,false);
        try{camera.Render();RenderTexture.active=texture;image.ReadPixels(new Rect(0,0,1200,750),0,0);image.Apply();File.WriteAllBytes(folder+"/"+name+".png",image.EncodeToPNG());return image.GetPixels32();}
        finally{RenderTexture.active=null;camera.targetTexture=null;texture.Release();DestroyImmediate(texture);DestroyImmediate(image);DestroyImmediate(camera.gameObject);}
    }
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    static void Finish(string stem,bool pass,string message){File.WriteAllText(stem+"-result.txt",(pass?"PASS: ":"FAIL: ")+message);EditorApplication.Exit(pass?0:1);}
}
#endif
