using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BirdHanoiBoard : UdonSharpBehaviour
{
    public BirdObjectPolicy policy;
    public BirdObjectTarget[] pieces=new BirdObjectTarget[0]; // smallest first
    public float[] heights=new float[0];
    public Transform[] pegs=new Transform[0];
    public BirdObjectSnapTarget[] slots=new BirdObjectSnapTarget[0];
    [HideInInspector] public int[] locations=new int[0];
    [HideInInspector] public int moves;
    [HideInInspector] public bool solved;
    private BirdObjectTarget reserved;
    private bool ready;
    private void Start() { Initialize(); }
    public void Initialize()
    {
        if(ready || Busy()) return;
        if(pieces.Length==0 || pieces.Length!=heights.Length || pegs.Length!=3 || slots.Length!=3 || policy==null) return;
        for(int i=0;i<pieces.Length;i++)
        {
            if(pieces[i]==null || float.IsNaN(heights[i]) || float.IsInfinity(heights[i]) || heights[i]<=0) return;
            for(int j=0;j<i;j++) if(pieces[i]==pieces[j]) return;
        }
        for(int i=0;i<3;i++)
        {
            if(pegs[i]==null || slots[i]==null) return;
            for(int j=0;j<i;j++) if(pegs[i]==pegs[j] || slots[i]==slots[j]) return;
        }
        locations=new int[pieces.Length]; ready=true; ResetPuzzle();
    }
    public void ResetPuzzle()
    {
        if(!ready || Busy()) return;
        moves=0; solved=false; float y=0;
        for(int i=pieces.Length-1;i>=0;i--)
        {
            locations[i]=0;
            pieces[i].transform.position=transform.TransformPoint(transform.InverseTransformPoint(pegs[0].position)+Vector3.up*(y+heights[i]*.5f));
            y+=heights[i];
        }
        Physics.SyncTransforms();
    }
    private bool Busy()
    {
        if(reserved!=null) return true;
        foreach(BirdObjectTarget piece in pieces) if(piece!=null && piece.owner!=null) return true;
        return false;
    }
    public void EvaluatePlacement()
    {
        if(policy==null) return;
        policy.allowed=false;
        if(policy.operation==BirdObjectOperation.Cancel)
        { if(reserved==policy.item) reserved=null; policy.allowed=true; return; }
        if(!ready) Initialize();
        if(!ready || !enabled || !gameObject.activeInHierarchy) return;
        BirdObjectTarget item=policy.item; int rank=-1,peg=-1;
        for(int i=0;i<pieces.Length;i++) if(pieces[i]==item) rank=i;
        for(int i=0;i<slots.Length;i++) if(slots[i]==policy.destination) peg=i;
        if(rank<0) return;
        if(policy.operation==BirdObjectOperation.CanGrab || policy.operation==BirdObjectOperation.Begin)
        {
            if(reserved!=null) return;
            for(int i=0;i<rank;i++) if(locations[i]==locations[rank]) return;
            policy.allowed=true;
            if(policy.operation==BirdObjectOperation.CanGrab) return;
            reserved=item;
            for(int p=0;p<3;p++)
            {
                float y=0; for(int i=0;i<pieces.Length;i++) if(i!=rank && locations[i]==p) y+=heights[i];
                slots[p].transform.position=transform.TransformPoint(transform.InverseTransformPoint(pegs[p].position)+Vector3.up*(y+heights[rank]*.5f));
            }
            return;
        }
        if(reserved!=item || peg<0) return;
        for(int i=0;i<rank;i++) if(locations[i]==peg) return;
        policy.allowed=true;
        if(policy.operation!=BirdObjectOperation.Commit) return;
        if(locations[rank]!=peg) { locations[rank]=peg; moves++; }
        reserved=null; solved=true;
        foreach(int location in locations) if(location!=2) solved=false;
    }
    private void OnDisable() { if(reserved!=null && reserved.owner!=null) reserved.owner.CancelImmediately(); reserved=null; }
}
