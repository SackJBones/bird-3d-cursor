using System;
using System.IO;
using UnityEngine;
using Bird3DCursor.Samples;
using Bird3DCursor.Manipulation;

public sealed class UnityHanoiPlayerSmoke : MonoBehaviour
{
    BirdHanoiPreview demo;
    BirdGrabTarget item;
    Vector3 home,end;
    Transform generated;
    string path;
    int phase;
    float elapsed;
    void Start()
    {
        string[] args=Environment.GetCommandLineArgs();
        for(int i=0;i<args.Length-1;i++) if(args[i]=="-birdHanoiResult") path=args[i+1];
        if(string.IsNullOrEmpty(path)) { enabled=false; return; }
        try
        {
            demo=GetComponent<BirdHanoiPreview>(); demo.desktopInput=false; demo.Initialize(); generated=demo.Generated;
            Application.targetFrameRate=72; QualitySettings.vSyncCount=0;
            item=demo.Tabletop.Pieces[0]; home=item.transform.position;
        }
        catch(Exception e) { Fail(e); }
    }
    void Next() { phase++; elapsed=0; }
    void Feed(Vector3 point,bool pressed=false)
    {
        Vector3 origin=item!=null?home-item.Region.transform.forward*2*Mathf.Abs(item.Region.transform.lossyScale.z):Vector3.zero;
        demo.Pointer.Submit(origin,point,true,pressed);
    }
    void Update()
    {
        if(string.IsNullOrEmpty(path)) return;
        try
        {
            elapsed+=Time.deltaTime;
            switch(phase)
            {
                case 0: Feed(home); if(elapsed>.1f) Next(); break;
                case 1: Feed(home,true); Next(); break;
                case 2:
                    Require(demo.Interactor.ActiveTarget==item,"Normal LateUpdate acquisition");
                    end=demo.Tabletop.Slots[2].transform.position; Feed(end+Vector3.up*.65f,true);
                    if(elapsed>.6f) Next(); break;
                case 3: Feed(end,true); if(elapsed>.6f) Next(); break;
                case 4:
                    Require(demo.Interactor.ReadyToPlace,"Frame-driven guided dock readiness");
                    Capture("hanoi-player-dock.png"); Feed(end,false); Next(); break;
                case 5:
                    Require(demo.Interactor.ActiveTarget==null && demo.Tabletop.Moves==1,"Frame-driven legal commit");
                    item=demo.Buildings.Pieces[0]; home=item.transform.position; Feed(home); Next(); break;
                case 6: Feed(home,true); Next(); break;
                case 7:
                    Require(demo.Interactor.ActiveTarget==item,"Frame-driven building acquisition");
                    Feed(demo.View.transform.position,true); if(elapsed>.7f) Next(); break;
                case 8:
                    Require(item.Volume.bounds.min.z>300,"Closed hand cannot bring building onto viewer");
                    Require(!demo.Interactor.ReadyToPlace,"Clamped building cannot silently snap");
                    Capture("hanoi-player-bounded.png"); demo.Pointer.Cancel(); Next(); break;
                case 9:
                    if(elapsed>.5f)
                    {
                        Require(demo.Interactor.ActiveTarget==null && demo.Buildings.Moves==0,"Tracking loss rolls back without puzzle mutation");
                        Require(Vector3.Distance(item.transform.position,home)<.001f,"Returning building reaches original placement");
                        new GameObject("Unrelated child").transform.SetParent(transform,false);
                        Destroy(demo); Next();
                    }
                    break;
                case 10:
                    if(elapsed>.1f)
                    {
                        Require(generated==null && transform.Find("Unrelated child")!=null,"Removing preview cleans owned objects only");
                        File.WriteAllText(path,"PASS: Windows player normal Update/LateUpdate pickup, approach, firm placement, distant safe return, tracking-loss rollback, rendering and owned-object cleanup. Synthetic logical samples; not physical or Udon validation.");
                        enabled=false; Application.Quit(0);
                    }
                    break;
            }
        }
        catch(Exception e) { Fail(e); }
    }
    static void Require(bool value,string message) { if(!value) throw new Exception(message); }
    void Fail(Exception e) { File.WriteAllText(path,"FAIL: "+e); Debug.LogException(e); enabled=false; Application.Quit(1); }
    void Capture(string filename)
    {
        demo.UpdateFeedback();
        var rt=new RenderTexture(1440,1000,24){antiAliasing=4}; var pixels=new Texture2D(1440,1000,TextureFormat.RGB24,false);
        var old=RenderTexture.active; var target=demo.View.targetTexture;
        try
        {
            demo.View.targetTexture=rt; demo.View.Render(); RenderTexture.active=rt;
            pixels.ReadPixels(new Rect(0,0,1440,1000),0,0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(path),filename),pixels.EncodeToPNG());
        }
        finally { demo.View.targetTexture=target; RenderTexture.active=old; rt.Release(); Destroy(rt); Destroy(pixels); }
    }
}
