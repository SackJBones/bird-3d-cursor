#if UNITY_EDITOR
using System;
using UnityEngine;

// One behavioral contract exercised against ordinary Unity and compiled Udon.
// The adapter supplies a unit sphere at zero and two independent logical pointers.
public static class UnitySphereEntryContract
{
    public static int Run(Action<int,Vector3,Vector3> feed,Action<float> step,Func<int> active,
        Func<Vector3> velocity,Action reset,Action<int> lose,Action<int,string> user,Action<Vector3,float> volume)
    {
        int count=0;
        Action<bool,string> check=(ok,message)=>{count++;if(!ok)throw new Exception("Deliberate sphere contact: "+message);};
        Vector3 origin=Vector3.back*3;
        Action<int,Vector3> sample=(hand,point)=>{feed(hand,origin,point);step(.01f);};
        Action<int> enter=hand=>{sample(hand,Vector3.zero);sample(hand,Vector3.forward*1.01f);};

        reset();
        for(int i=0;i<25;i++)
        {
            sample(0,new Vector3(Mathf.Sin(i*.3f)*.5f,0,2));
            check(active()==-1,"startup/outside sweep must not acquire");
        }
        sample(0,Vector3.back*2); sample(0,Vector3.forward*2);
        check(active()==-1,"outside-to-outside jump through entire volume is not contact");
        reset();sample(0,Vector3.zero);sample(0,Vector3.forward);sample(0,Vector3.forward*1.01f);
        check(active()==0,"a sample exactly on the back surface preserves outward entry");

        reset(); sample(0,Vector3.zero); check(active()==-1,"inside is free");
        sample(0,Vector3.forward*1.002f); check(active()==-1,"back margin is not yet pierced");
        sample(0,Vector3.forward*1.01f); check(active()==0,"continued outward crossing acquires");
        sample(0,new Vector3(.5f,0,1.5f)); check(velocity().magnitude>0,"acquired contact drives motion");
        sample(0,Vector3.zero); check(active()==-1 && velocity().magnitude>0,"withdrawal releases into coast");
        sample(0,Vector3.forward*1.01f); check(active()==0,"observed withdrawal rearms");
        sample(0,new Vector3(3,0,2)); check(active()==-1,"ray missing sphere loses contact");
        sample(0,Vector3.forward*2); check(active()==-1,"returning aim outside does not reacquire");
        sample(0,Vector3.forward*.999f); sample(0,Vector3.forward*1.01f);
        check(active()==-1,"grazing inner boundary is not a full withdrawal");
        enter(0); check(active()==0,"fresh interior visit restores contact");

        reset(); sample(0,Vector3.zero); sample(0,Vector3.back*1.1f); sample(0,Vector3.forward*2);
        check(active()==-1,"front exit consumes arming");
        reset(); sample(0,Vector3.zero); sample(0,Vector3.right*1.1f); sample(0,Vector3.forward*2);
        check(active()==-1,"side exit cannot arm a later back contact");
        reset(); sample(0,Vector3.forward*.8f);
        sample(0,origin+Quaternion.AngleAxis(12,Vector3.up)*Vector3.forward*3.8f);
        check(active()==-1,"constant-range wrist sweep does not pierce deliberately");
        sample(0,Vector3.forward*2);check(active()==-1,"outward movement after a rejected sideways exit needs a new interior visit");
        reset(); sample(0,Vector3.zero);
        feed(0,origin+Vector3.forward*1.2f,Vector3.forward*1.2f);step(.01f);
        check(active()==-1,"rigid hand/point translation does not extend reach");
        sample(0,Vector3.forward*2);check(active()==-1,"translation exit cannot leave an armed outside pointer");

        reset(); sample(0,Vector3.zero); volume(Vector3.back*2,1); sample(0,Vector3.forward*.1f);
        check(active()==-1,"moving volume cannot manufacture a crossing");
        reset(); sample(0,Vector3.forward*.4f); volume(Vector3.zero,.5f); sample(0,Vector3.forward*.6f);
        check(active()==-1,"shrinking volume cannot manufacture a crossing");
        reset(); enter(0); sample(0,new Vector3(.5f,0,1.5f));volume(Vector3.right*.1f,1);step(.01f);
        check(active()==-1 && velocity()==Vector3.zero,"moving active volume cancels stale contact and inertia");

        reset(); sample(0,Vector3.zero);
        feed(0,origin,Vector3.forward*.5f);feed(0,origin,Vector3.forward*2);step(.01f);
        check(active()==-1,"skipped input revisions cannot bridge an unobserved crossing");
        enter(0); check(active()==0,"entry recovers after skipped samples");
        reset(); sample(0,Vector3.zero);lose(0);feed(0,origin,Vector3.zero);feed(0,origin,Vector3.forward*2);step(.01f);
        check(active()==-1,"loss and recovery between consumer steps needs rearming");
        reset(); sample(0,Vector3.zero);for(int i=0;i<30;i++)step(.01f);sample(0,Vector3.forward*2);
        check(active()==-1,"stale interior sample cannot arm later contact");
        reset();enter(0);sample(0,new Vector3(.5f,0,1.5f));for(int i=0;i<30;i++)step(.01f);
        check(active()==-1 && velocity()==Vector3.zero,"stale active sample clears drive and inertia");
        sample(0,Vector3.forward*2);check(active()==-1,"fresh outside sample after staleness is not contact");
        reset();sample(0,Vector3.zero);user(0,"Changed owner");sample(0,Vector3.forward*2);
        check(active()==-1,"ownership change invalidates arming");
        reset();sample(0,Vector3.zero);step(.3f);sample(0,Vector3.forward*2);
        check(active()==-1,"long consumer pause invalidates arming");

        reset();feed(0,origin,Vector3.zero);feed(1,origin,Vector3.zero);step(.01f);
        sample(0,Vector3.forward*2);check(active()==0,"first hand acquires");
        sample(1,Vector3.forward*2);check(active()==0,"second hand cannot steal active contact");
        sample(0,Vector3.zero);check(active()==-1,"outside second hand has no queued takeover");
        enter(1);check(active()==1,"second hand's new inside/out crossing acquires");
        reset();
        return count;
    }

    // Ten small foreground spheres at z=2,4,...20 (radius .3), one large
    // background sphere at z=100 (radius 20). All observe the SAME pointer.
    public static int Layered(Action<Vector3> feed,Action<float> step,Func<int,bool> active,Func<int,Vector3> velocity)
    {
        int count=0;
        Action<bool,string> check=(ok,message)=>{count++;if(!ok)throw new Exception("Layered sphere contact: "+message);};
        Action<Vector3> sample=p=>{feed(p);step(.01f);};
        sample(Vector3.back);sample(Vector3.forward*100);sample(Vector3.forward*125);
        check(active(10),"deliberately reached background sphere acquires");
        for(int i=0;i<25;i++)
        {
            sample(new Vector3(Mathf.Sin(i*.07f)*8,Mathf.Cos(i*.11f)*3,125));
            for(int near=0;near<10;near++) check(!active(near) && velocity(near)==Vector3.zero,"untouched foreground selector "+near+" stays idle");
        }
        check(active(10) && velocity(10).magnitude>0,"large background selector responds through foreground ones");
        sample(Vector3.forward*100);sample(Vector3.forward*10);sample(Vector3.forward*10.4f);
        check(active(4),"can subsequently address one foreground selector deliberately");
        for(int i=0;i<11;i++) if(i!=4) check(!active(i),"other selector remains uncaptured "+i);
        return count;
    }
}
#endif
