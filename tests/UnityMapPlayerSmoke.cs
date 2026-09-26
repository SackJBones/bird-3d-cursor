using System;
using System.IO;
using Bird3DCursor.UI;
using Bird3DCursor.Samples;
using UnityEngine;

public sealed class UnityMapPlayerSmoke : MonoBehaviour
{
    BirdMapPreview demo;
    string result;
    int stage;
    float started,frozen;
    Vector3 origin,axis;
    Quaternion rotation;
    Camera owned;
    GameObject unrelated;
    void Start()
    {
        string[] args=Environment.GetCommandLineArgs(); int index=Array.IndexOf(args,"-birdMapResult");
        if(index<0 || index+1>=args.Length) { enabled=false; return; }
        result=args[index+1]; Application.targetFrameRate=72; QualitySettings.vSyncCount=0;
        demo=GetComponent<BirdMapPreview>(); demo.desktopInput=false; demo.Initialize(); origin=demo.View.transform.position; axis=(demo.Volume.transform.position-origin).normalized;
        unrelated=new GameObject("Unrelated host object"); unrelated.transform.SetParent(transform,false);
    }
    void Update()
    {
        if(result==null) return;
        try
        {
            if(stage==0) { Feed(origin+Vector3.forward*.5f); stage++; return; }
            if(stage==1) { Aim(demo.OpenButton,false); stage++; return; }
            if(stage==2) { Require(demo.Menu.State==BirdMenuPanel.PanelState.Open,"Normal-frame map opens"); Aim(demo.ZoomButton,false); stage++; return; }
            if(stage==3) { Aim(demo.ZoomButton,true); stage++; return; }
            if(stage==4) { Require(demo.Zoom.ScalingEnabled && !demo.Scroll.enabled,"Mode event selects zoom"); Feed(origin+axis*4); stage++; return; }
            if(stage==5) { Require(demo.Zoom.ActivePointer==demo.Pointer,"Normal-frame zoom acquires"); started=Time.unscaledTime; stage++; }
            if(stage==6)
            {
                float t=Time.unscaledTime-started; Feed(origin+axis*(4*Mathf.Exp(Mathf.Min(t,1)*.5f)));
                if(t<1.15f) return;
                Require(demo.Zoom.Factor>2.2f && demo.Zoom.Factor<=2.3001f,"Normal-frame bounded zoom"); Capture("player-zoom"); frozen=demo.Zoom.Factor; Feed(origin+axis*.5f); stage++; return;
            }
            if(stage==7) { Require(demo.Zoom.ActivePointer==null && Mathf.Abs(demo.Zoom.Factor-frozen)<.001f,"Withdrawal freezes"); Feed(origin+axis*7); stage++; return; }
            if(stage==8) { Require(Mathf.Abs(demo.Zoom.Factor-frozen)<.001f,"Reentry has no jump"); Aim(demo.ResetButton,false); stage++; return; }
            if(stage==9) { Aim(demo.ResetButton,true); stage++; return; }
            if(stage==10)
            {
                Require(demo.Zoom.Factor==1 && !demo.Zoom.ScalingEnabled && demo.Scroll.enabled,"Normal reset switches to rotation");
                rotation=demo.Rotation.rotation; started=Time.unscaledTime; stage++;
            }
            if(stage==11)
            {
                float t=Time.unscaledTime-started; Vector3 normal=Quaternion.AngleAxis(t*50,Vector3.up)*Vector3.forward;
                Feed(origin+(demo.Volume.transform.position+normal*.95f-origin)*2);
                if(t<.7f) return;
                Require(demo.Scroll.ActivePointer!=null && Quaternion.Angle(rotation,demo.Rotation.rotation)>10,"Normal back-surface rotation");
                rotation=demo.Rotation.rotation; Feed(demo.Volume.transform.position); started=Time.unscaledTime; stage++; return;
            }
            if(stage==12)
            {
                Feed(demo.Volume.transform.position); if(Time.unscaledTime-started<.3f) return;
                Require(demo.Scroll.ActivePointer==null && Quaternion.Angle(rotation,demo.Rotation.rotation)>1,"Map coasts after withdrawal"); Capture("player-rotated"); demo.Pointer.Cancel(); stage++; return;
            }
            if(stage==13) { Require(demo.Menu.State==BirdMenuPanel.PanelState.Closed && demo.Scroll.AngularVelocity==Vector3.zero,"Loss closes map and clears motion"); owned=demo.View; Destroy(demo); stage++; return; }
            if(stage==14) { stage++; return; }
            Require(owned==null && unrelated!=null,"Sample cleanup preserves unrelated host content");
            File.WriteAllText(result,"PASS: Windows player normal Update/LateUpdate map open, mode events, bounded zoom, withdraw/reentry, reset, back-surface rotation/coast, loss and cleanup; two captures. Synthetic logical samples, not mouse/headset use."); Application.Quit(0); enabled=false;
        }
        catch(Exception e) { File.WriteAllText(result,"FAIL: "+e); Debug.LogException(e); Application.Quit(1); enabled=false; }
    }
    void Feed(Vector3 point,bool pressed=false) { demo.Pointer.Submit(origin,point,true,pressed); }
    void Aim(BirdMenuElement element,bool pressed) { Feed(origin+(element.transform.position-origin)*1.1f,pressed); }
    void Capture(string name)
    {
        var rt=new RenderTexture(1400,1000,24) { antiAliasing=4 }; var pixels=new Texture2D(1400,1000,TextureFormat.RGB24,false);
        demo.View.targetTexture=rt; demo.View.Render(); RenderTexture.active=rt; pixels.ReadPixels(new Rect(0,0,1400,1000),0,0); pixels.Apply(); File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(result),name+".png"),pixels.EncodeToPNG());
        RenderTexture.active=null; demo.View.targetTexture=null; rt.Release(); Destroy(rt); Destroy(pixels);
    }
    static void Require(bool value,string message) { if(!value) throw new Exception(message); }
}
