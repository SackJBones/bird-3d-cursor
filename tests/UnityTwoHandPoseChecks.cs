#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Bird3DCursor.UI;
using Bird3DCursor.Manipulation;
using UnityEngine;

// Called from the real Unity pose fixture. No hand SDK or API doubles.
public static class UnityTwoHandPoseChecks
{
    static int checks;
    sealed class Fixture:IDisposable
    {
        public GameObject root=new GameObject("Two hand fixture");
        public BirdPointerInput left,right;
        public BirdGrabInteractor grip;
        public BirdTwoHandPose gesture;
        public BirdGrabTarget item;
        public BirdPlacementRegion region;
        public Vector3 origin=new Vector3(-.2f,1.2f,-2),span=Vector3.right*.4f,point=new Vector3(0,1.3f,0);
        public bool secondaryPressed;
        public Fixture()
        {
            left=root.AddComponent<BirdPointerInput>(); right=new GameObject("Other hand").AddComponent<BirdPointerInput>(); right.transform.SetParent(root.transform);
            region=root.AddComponent<BirdPlacementRegion>(); region.LocalBounds=new Bounds(Vector3.up*2,new Vector3(8,4,6));
            var go=new GameObject("Pose item"); go.transform.SetParent(root.transform); go.transform.localPosition=Vector3.up;
            var box=go.AddComponent<BoxCollider>(); box.center=Vector3.up*.3f; box.size=new Vector3(.8f,.6f,.5f);
            item=go.AddComponent<BirdGrabTarget>(); item.Configure(box,region,new BirdSnapTarget[0]); item.ConfigurePose(true,true);
            grip=root.AddComponent<BirdGrabInteractor>(); grip.automatic=false; grip.Configure(new[]{left,right},new[]{item});
            gesture=root.AddComponent<BirdTwoHandPose>(); gesture.automatic=false; gesture.interactor=grip; gesture.first=left; gesture.second=right;
            Physics.SyncTransforms(); gesture.Process(.01f);
        }
        public void Sample(bool pressed=true,float dt=1f/72)
        {
            left.Submit(origin,point,true,pressed); right.Submit(origin+span,item.Volume.transform.TransformPoint(item.Volume.center),true,secondaryPressed); Step(dt);
        }
        public void Step(float dt=1f/72) { gesture.Process(dt); grip.Process(dt); Inside(); }
        public void Pick() { Sample(false); Sample(); Assert(grip.ActiveTarget==item,"Primary acquires"); }
        public void Engage() { secondaryPressed=false; Sample(); secondaryPressed=true; Sample(); Assert(gesture.ActiveSecondary==right,"Fresh secondary point-through click engages"); }
        public void Inside()
        {
            Bounds bounds=region.LocalBounds; bounds.Expand(.0002f);
            for(int i=0;i<8;i++)
            {
                Vector3 sign=new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1);
                Assert(bounds.Contains(region.transform.InverseTransformPoint(item.Volume.transform.TransformPoint(item.Volume.center+Vector3.Scale(item.Volume.size*.5f,sign)))),"Whole box remains in workspace");
            }
        }
        public void Dispose() { UnityEngine.Object.DestroyImmediate(root); }
    }
    public static string Run()
    {
        checks=0;
        using(var f=new Fixture())
        {
            f.Pick(); f.Engage(); Near(Quaternion.Angle(f.item.transform.localRotation,Quaternion.identity),0,.001f,"No jump at clutch");
            f.span=Quaternion.Euler(0,90,0)*Vector3.right*.5f;
            for(int i=0;i<70;i++) f.Sample();
            Near(Quaternion.Angle(f.item.transform.localRotation,Quaternion.Euler(0,90,0)),0,.03f,"Two hand quarter turn"); Near(f.grip.CurrentScaleFactor,1.25f,.00001f,"Separation controls relative size");
            f.secondaryPressed=false; f.Sample(); Quaternion turn=f.item.transform.localRotation; Vector3 size=f.item.transform.localScale;
            f.span=Vector3.up*2; for(int i=0;i<25;i++) f.Sample();
            Assert(!f.gesture.IsEngaged && f.grip.ActiveTarget==f.item && f.item.transform.localRotation==turn && f.item.transform.localScale==size,"Release freezes displayed pose while primary continues");
            f.Engage(); f.Sample(); Near(Quaternion.Angle(turn,f.item.transform.localRotation),0,.001f,"Re-clutch rebases without rotational jump"); Near(f.grip.CurrentScaleFactor,1.25f,.00001f,"Re-clutch uses displayed size");
            f.gesture.Cancel(); f.Sample(); Assert(!f.gesture.IsEngaged,"Cancel consumes held secondary edge");
            f.Engage(); f.grip.followRate=2; f.span=Vector3.right*1.5f; f.Sample(); f.secondaryPressed=false; f.Sample(); turn=f.item.transform.localRotation; size=f.item.transform.localScale;
            for(int i=0;i<30;i++) f.Sample(); Assert(turn==f.item.transform.localRotation && size==f.item.transform.localScale,"Release stops lag immediately");
        }
        using(var f=new Fixture())
        {
            f.secondaryPressed=true; f.Pick(); f.Sample(); Assert(!f.gesture.IsEngaged,"Already-held secondary does not engage on pickup");
            f.Engage(); f.span=Vector3.right*1.2f; f.Sample(); Assert(f.grip.PoseLimited && f.gesture.IsEngaged,"Out-of-range size blocks stale intent but permits adjustment");
            f.secondaryPressed=false; f.Sample(); Assert(f.grip.PoseLimited,"Release cannot launder rejected intent");
            f.span=Vector3.right*.4f; f.Engage(); Assert(!f.grip.PoseLimited,"Fresh valid clutch restores eligibility");
            f.span=Vector3.zero; f.Sample(); Assert(!f.gesture.IsEngaged && f.grip.PoseLimited,"Coincident hands stop clutch");
            f.span=Vector3.right*.4f; f.Engage(); f.span=Vector3.left*.4f; f.Sample(); Assert(!f.gesture.IsEngaged && f.grip.PoseLimited,"Ambiguous opposite direction cannot flip pose");
            f.span=Vector3.right*.4f; f.Sample(); Assert(!f.gesture.IsEngaged,"Recovering span while held cannot auto-reengage");
        }
        using(var f=new Fixture())
        {
            f.Pick(); f.right.SetUser("Other user"); f.secondaryPressed=true; f.Sample(); Assert(!f.gesture.IsEngaged,"Different user cannot join"); f.right.SetUser("LocalUser");
            f.Engage(); f.right.Cancel(); f.Step(); Assert(!f.gesture.IsEngaged && f.grip.ActiveTarget==f.item && f.grip.PoseLimited,"Secondary loss freezes pose and requires fresh intent before placement");
            f.Sample(); Assert(!f.gesture.IsEngaged,"Recovered held secondary needs fresh press");
            f.Engage(); f.left.Cancel(); f.Step(); Assert(f.grip.IsReturning,"Primary loss rolls back transaction");
            for(int i=0;i<30;i++) f.Step(); Assert(f.grip.ActiveTarget==null,"Primary loss completes return");
        }
        using(var f=new Fixture())
        {
            f.Pick(); f.right.Submit(f.origin+f.span,new Vector3(100,100,0),true,false); f.Step(); f.right.Submit(f.origin+f.span,new Vector3(100,100,0),true,true); f.Step(); Assert(!f.gesture.IsEngaged,"Secondary click must point through held volume");
            f.Engage(); f.right.Submit(f.origin+Vector3.forward*.5f,f.point,true,true); f.Step(); Near(f.grip.RequestedScaleFactor,1,.00001f,"Unpaired sample waits");
            f.left.Submit(f.origin,f.point,true,true); f.Step(); Near(f.grip.RequestedScaleFactor,1.25f,.00001f,"Next fresh primary pairs pending secondary");
            for(int i=0;i<25;i++) { f.left.Submit(f.origin,f.point,true,true); f.Step(); }
            Assert(!f.gesture.IsEngaged && f.grip.ActiveTarget==f.item,"Stale secondary releases clutch");
            f.Engage(); f.gesture.enabled=false; Quaternion rotation=f.item.transform.localRotation; f.Sample(); Assert(!f.gesture.IsEngaged && f.item.transform.localRotation==rotation,"Disable freezes and releases");
            f.gesture.enabled=true; f.Sample(); Assert(!f.gesture.IsEngaged,"Enable cannot replay held press");
            f.Engage(); f.grip.TrySetHeldPose(Quaternion.Euler(0,25,0),1.1f); f.Sample(); Assert(!f.gesture.IsEngaged && Mathf.Abs(f.grip.RequestedScaleFactor-1.1f)<1e-6f,"Explicit host command takes over");
            f.gesture.Engaged.AddListener(f.gesture.Cancel); f.secondaryPressed=false; f.Sample(); f.secondaryPressed=true; f.Sample(); Assert(!f.gesture.IsEngaged,"Engaged callback can cancel reentrantly");
        }
        using(var f=new Fixture())
        {
            f.Pick(); f.Engage(); f.span=Quaternion.Euler(0,60,0)*Vector3.right*.6f; f.Sample(false);
            Assert(!f.gesture.IsEngaged && f.grip.IsReturning,"Primary release retains normal no-destination cancellation");
        }
        using(var f=new Fixture())
        {
            var dock=new GameObject("Home pose dock").AddComponent<BirdSnapTarget>(); dock.transform.SetParent(f.root.transform); dock.transform.position=f.item.transform.position;
            dock.matchRotation=dock.matchScale=true; dock.scaleFactor=1;
            f.item.Configure(f.item.Volume,f.region,new[]{dock}); f.grip.followRate=1; f.Pick(); f.Engage();
            f.span=Quaternion.Euler(0,90,0)*Vector3.right*.4f; f.Sample();
            Assert(Quaternion.Angle(f.item.transform.localRotation,Quaternion.identity)<12 && !f.grip.ReadyToPlace,"Lagging display matches dock while gesture intent disagrees");
            f.left.Submit(f.origin,f.point,true,false); f.right.Cancel(); f.Step();
            Assert(f.grip.IsReturning,"Secondary loss on primary release cannot manufacture a matching drop");
        }
        using(var f=new Fixture())
        {
            f.Pick(); f.right.Submit(f.origin+f.span,f.point,true,false); f.Step();
            Vector3 far=f.origin+f.span+(f.point-f.origin-f.span).normalized*1e6f;
            f.right.Submit(f.origin+f.span,far,true,true); f.Step(); Assert(f.gesture.IsEngaged,"Million-metre logical ray engages with unchanged hand-span sensitivity"); Assert(f.right.Position==far,"Gesture never rewrites Bird point");
        }
        using(var f=new Fixture())
        {
            f.Sample(false); f.secondaryPressed=true; f.Sample(false);
            Assert(f.grip.ActivePointer==f.right,"Either hand can own primary grip");
            f.Sample(true); Assert(f.gesture.ActiveSecondary==f.left,"Opposite hand can join as secondary");
            f.span=Quaternion.Euler(0,90,0)*Vector3.right*.4f;
            for(int i=0;i<70;i++) f.Sample();
            Near(Quaternion.Angle(f.item.transform.localRotation,Quaternion.Euler(0,90,0)),0,.03f,"Reversed roles preserve physical turn direction");
        }
        using(var f=new Fixture())
        {
            f.Pick(); f.Engage(); UnityEngine.Object.DestroyImmediate(f.right); f.Step();
            Assert(!f.gesture.IsEngaged && f.grip.PoseLimited && f.grip.ActiveTarget==f.item,"Destroyed secondary clears clutch and blocks stale placement");
        }
        foreach(bool turnOnly in new[]{true,false}) using(var f=new Fixture())
        {
            f.item.ConfigurePose(turnOnly,!turnOnly); f.Pick(); f.Engage();
            f.span=Quaternion.Euler(0,90,0)*Vector3.right*.5f;
            for(int i=0;i<70;i++) f.Sample();
            Near(Quaternion.Angle(f.item.transform.localRotation,turnOnly?Quaternion.Euler(0,90,0):Quaternion.identity),0,.03f,"Gesture respects rotation permission");
            Near(f.grip.CurrentScaleFactor,turnOnly?1:1.25f,.00001f,"Gesture respects scaling permission");
            Assert(!f.grip.PoseLimited,"Disabled pose axis does not reject the permitted gesture axis");
        }
        Rates();
        string result="PASS: "+checks+" actual Unity two-hand assertions, including per-frame box corners; fresh point-through clutch, origin turn/size, reentry/freeze, intent limits, antipodal/degenerate guard, pairing/staleness, loss, lifecycle and callback. Synthetic input, not physical feel.";
        File.WriteAllText("two-hand-result.txt",result); return result;
    }
    static void Rates()
    {
        var rows=new List<string>{"hz,angle_degrees,reference_factor"}; float angle=0,factor=0;
        foreach(int hz in new[]{30,72,120}) using(var f=new Fixture())
        {
            f.grip.followRate=10; f.Pick(); f.Engage(); f.span=Quaternion.Euler(0,90,0)*Vector3.right*.6f;
            for(int i=0;i<hz/2;i++) f.Sample(true,1f/hz);
            float a=Quaternion.Angle(Quaternion.identity,f.item.transform.localRotation),b=f.grip.CurrentScaleFactor;
            if(hz!=30) { Near(a,angle,.002f,"Constant gesture rotation cadence"); Near(b,factor,.000002f,"Constant gesture size cadence"); }
            angle=a; factor=b; rows.Add(hz+","+a.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+b.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
        }
        Directory.CreateDirectory("PoseCaptures"); File.WriteAllLines("PoseCaptures/two-hand-rates.csv",rows);
    }
    static void Assert(bool ok,string message) { checks++; if(!ok) throw new Exception("Two-hand assertion "+checks+": "+message); }
    static void Near(float a,float b,float tolerance,string message) { Assert(Mathf.Abs(a-b)<=tolerance,message+": "+a+" / "+b); }
}
#endif
