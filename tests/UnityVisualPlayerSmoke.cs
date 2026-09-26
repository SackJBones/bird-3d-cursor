using System;
using System.IO;
using Bird3DCursor.UI;
using Bird3DCursor.Samples;
using UnityEngine;

public class UnityVisualPlayerSmoke : MonoBehaviour
{
    string result;
    BirdMenuPreview demo;
    BirdMenuVisual visual;
    Bounds bounds;
    float started;
    int stage;
    void Start()
    {
        var args=Environment.GetCommandLineArgs(); int at=Array.IndexOf(args,"-birdVisualResult"); if(at<0 || at+1>=args.Length) { enabled=false; return; }
        result=args[at+1]; demo=GetComponent<BirdMenuPreview>(); demo.desktopInput=false; GetComponent<BirdVisualStatesPreview>().Initialize();
        Application.targetFrameRate=72; QualitySettings.vSyncCount=0;
    }
    void Update()
    {
        if(result==null) return;
        try
        {
            if(stage==0) { Feed(new Vector3(0,.35f,-.2f)); stage++; return; }
            if(stage==1) { Feed(new Vector3(0,1.05f,-.2f)); stage++; return; }
            if(stage==2)
            {
                Require(demo.Menu.State==BirdMenuPanel.PanelState.Open,"Normal-frame menu opens");
                visual=demo.ColorButton.GetComponent<BirdMenuVisual>(); bounds=demo.ColorButton.GetComponent<Collider>().bounds;
                Feed(demo.ColorButton.transform.position); started=Time.unscaledTime; stage++; return;
            }
            if(stage==3)
            {
                Feed(demo.ColorButton.transform.position); if(Time.unscaledTime-started<.25f) return;
                Require(visual.DisplayedState==BirdMenuVisualState.Highlighted && visual.VisualRoot.localPosition.z<-.04f,"Normal-frame artwork moves toward user");
                Require(demo.ColorButton.GetComponent<Collider>().bounds==bounds,"Normal-frame hit geometry remains fixed"); Capture("player-highlight");
                Feed(demo.ColorButton.transform.position,true); stage++; return;
            }
            if(stage==4) { Require(demo.Colors.State==BirdMenuPanel.PanelState.Open,"Menu action remains independent"); Feed(demo.CyanButton.transform.position); stage++; return; }
            if(stage==5) { Feed(demo.CyanButton.transform.position,true); started=Time.unscaledTime; stage++; return; }
            if(stage==6)
            {
                Feed(demo.CyanButton.transform.position,true); if(Time.unscaledTime-started<.25f) return;
                var pressed=demo.CyanButton.GetComponent<BirdMenuVisual>();
                Require(pressed.DisplayedState==BirdMenuVisualState.Activated && demo.SelectionCount==1,"Held cue and single activation");
                Require(visual.DisplayedState==BirdMenuVisualState.Background && visual.VisualRoot.localPosition.z>.07f,"Parent artwork recedes in background"); Capture("player-pressed");
                demo.Pointer.Cancel(); stage++; return;
            }
            Require(demo.Menu.State==BirdMenuPanel.PanelState.Closed && !visual.VisualRoot.gameObject.activeInHierarchy,"Loss closes content");
            File.WriteAllText(result,"PASS: actual Windows player normal-frame visual hover/press/background/loss, fixed collider bounds, independent single action and two captures."); Application.Quit(0); enabled=false;
        }
        catch(Exception e) { File.WriteAllText(result,"FAIL: "+e); Debug.LogException(e); Application.Quit(1); enabled=false; }
    }
    void Feed(Vector3 point,bool pressed=false) { demo.Pointer.Submit(demo.View.transform.position,point,true,pressed); }
    void Capture(string name)
    {
        var rt=new RenderTexture(1280,900,24){antiAliasing=4}; var pixels=new Texture2D(1280,900,TextureFormat.RGB24,false); demo.View.targetTexture=rt; demo.View.Render(); RenderTexture.active=rt;
        pixels.ReadPixels(new Rect(0,0,1280,900),0,0); pixels.Apply(); File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(result),name+".png"),pixels.EncodeToPNG()); RenderTexture.active=null; demo.View.targetTexture=null; rt.Release(); Destroy(rt); Destroy(pixels);
    }
    static void Require(bool ok,string message) { if(!ok) throw new Exception(message); }
}
