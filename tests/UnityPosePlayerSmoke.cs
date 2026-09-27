using System;
using System.IO;
using Bird3DCursor.Samples;
using Bird3DCursor.Manipulation;
using UnityEngine;

public sealed class UnityPosePlayerSmoke : MonoBehaviour
{
    BirdPosePreview demo;
    BirdGrabTarget item;
    Vector3 home,anchor,origin,end;
    string path;
    int phase;
    bool secondaryPrepared;
    float elapsed;
    Transform generated;
    void Start()
    {
        var args=Environment.GetCommandLineArgs(); int at=Array.IndexOf(args,"-birdPoseResult");
        if(at<0 || at+1>=args.Length) { enabled=false; return; }
        path=args[at+1]; demo=GetComponent<BirdPosePreview>(); demo.desktopInput=false; demo.Initialize(); generated=demo.Generated;
        Application.targetFrameRate=72; QualitySettings.vSyncCount=0; Bind(demo.Items[0]);
    }
    void Bind(BirdGrabTarget target)
    {
        item=target; home=item.transform.position; anchor=item.Volume.transform.TransformPoint(item.Volume.center);
        origin=anchor-item.Region.transform.forward*5*Mathf.Abs(item.Region.transform.lossyScale.z);
        end=anchor+(item.Destinations[1].transform.position-home);
    }
    void Feed(Vector3 point,bool pressed=false) { demo.Pointer.Submit(origin,point,true,pressed); }
    void Next() { phase++; elapsed=0; }
    void Update()
    {
        if(path==null) return;
        try
        {
            elapsed+=Time.deltaTime;
            switch(phase)
            {
                case 0: Feed(anchor); if(elapsed>.1f) Next(); break;
                case 1: Feed(anchor,true); Next(); break;
                case 2:
                    Require(demo.Interactor.ActiveTarget==item,"Normal-frame pickup"); Feed(end,true);
                    if(elapsed>.6f)
                    {
                        Require(!demo.Interactor.ReadyToPlace,"Position alone cannot commit wrong pose");
                        demo.OtherPointer.Submit(origin+Vector3.right*.4f,item.Volume.transform.TransformPoint(item.Volume.center),true,secondaryPrepared);
                        if(secondaryPrepared) Next(); else secondaryPrepared=true;
                    } break;
                case 3:
                    Require(demo.TwoHandPose.IsEngaged,"Normal frame second-hand clutch"); Feed(end,true);
                    demo.OtherPointer.Submit(origin+Quaternion.Euler(0,90,0)*Vector3.right*.5f,item.Volume.transform.TransformPoint(item.Volume.center),true,true);
                    if(elapsed>.7f) Next(); break;
                case 4:
                    Require(demo.Interactor.ReadyToPlace,"Normal-frame two-hand pose readiness"); Capture("pose-player-ready.png");
                    demo.OtherPointer.Submit(origin+Quaternion.Euler(0,90,0)*Vector3.right*.5f,item.Volume.transform.TransformPoint(item.Volume.center),true,false); Feed(end,false); Next(); break;
                case 5:
                    Require(demo.Interactor.ActiveTarget==null,"Full pose committed");
                    Require(Quaternion.Angle(item.transform.localRotation,item.Destinations[1].transform.localRotation)<.001f && Vector3.Distance(item.transform.localScale,Vector3.one*1.25f)<1e-6f,"Exact pose after release");
                    Bind(demo.Items[1]); Feed(anchor); Next(); break;
                case 6: Feed(anchor,true); Next(); break;
                case 7:
                    Require(demo.Interactor.ActiveTarget==item,"Building picked up"); demo.Controls.RotateAroundWorkspaceUp(90); demo.Grow(); Feed(demo.View.transform.position,true); Next(); break;
                case 8:
                    Feed(demo.View.transform.position,true); if(elapsed>.7f) { Require(item.Volume.bounds.min.z>300,"Rotated/resized building remains far from viewer"); Capture("pose-player-building.png"); demo.Pointer.Cancel(); Next(); } break;
                case 9:
                    if(elapsed>.5f) { Require(demo.Interactor.ActiveTarget==null,"Loss completes rollback"); Require(Vector3.Distance(item.transform.position,home)<.001f && Vector3.Distance(item.transform.localScale,Vector3.one)<1e-6f && Quaternion.Angle(item.transform.localRotation,Quaternion.identity)<.001f,"Loss restores entire building pose"); Destroy(demo); Next(); } break;
                case 10:
                    Require(generated==null,"Sample cleans up owned hierarchy"); File.WriteAllText(path,"PASS: Windows player normal-frame second-hand clutch/rotation/resize, gated exact placement, full-volume distant boundary, loss rollback, two renders and owned-object cleanup. Synthetic input; no hardware claim."); enabled=false; Application.Quit(0); break;
            }
        }
        catch(Exception e) { File.WriteAllText(path,"FAIL: "+e); Debug.LogException(e); enabled=false; Application.Quit(1); }
    }
    void Capture(string name)
    {
        var rt=new RenderTexture(1440,1000,24){antiAliasing=4}; var pixels=new Texture2D(1440,1000,TextureFormat.RGB24,false); demo.View.targetTexture=rt; demo.View.Render(); RenderTexture.active=rt;
        pixels.ReadPixels(new Rect(0,0,1440,1000),0,0); pixels.Apply(); File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(path),name),pixels.EncodeToPNG()); demo.View.targetTexture=null; RenderTexture.active=null; rt.Release(); Destroy(rt); Destroy(pixels);
    }
    static void Require(bool ok,string message) { if(!ok) throw new Exception(message); }
}
