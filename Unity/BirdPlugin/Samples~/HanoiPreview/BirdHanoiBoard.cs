using UnityEngine;
using Bird3DCursor.Manipulation;

namespace Bird3DCursor.Samples
{
    /// <summary>Hanoi rules only. Geometry, dragging and snapping belong to the shared components.</summary>
    public sealed class BirdHanoiBoard : BirdPlacementRule
    {
        [SerializeField] BirdGrabTarget[] pieces=new BirdGrabTarget[0]; // smallest first
        [SerializeField] float[] heights=new float[0];
        [SerializeField] Transform[] pegs=new Transform[0];
        [SerializeField] BirdSnapTarget[] slots=new BirdSnapTarget[0];
        int[] locations;
        BirdGrabTarget reserved;
        public int Moves { get; private set; }
        public bool Solved { get { if(locations==null || locations.Length==0) return false; foreach(int peg in locations) if(peg!=2) return false; return true; } }
        public BirdGrabTarget[] Pieces { get { return pieces; } }
        public BirdSnapTarget[] Slots { get { return slots; } }
        public int Location(int rank) { return locations[rank]; }

        public void Configure(BirdGrabTarget[] orderedPieces,float[] pieceHeights,Transform[] supports,BirdSnapTarget[] destinations)
        {
            if(Busy()) throw new System.InvalidOperationException("Reset the active interaction before reconfiguring the puzzle.");
            if(orderedPieces==null || pieceHeights==null || orderedPieces.Length!=pieceHeights.Length || supports==null || supports.Length!=3 || destinations==null || destinations.Length!=3)
                throw new System.ArgumentException("Hanoi requires ordered pieces/heights, three supports and three slots.");
            for(int i=0;i<orderedPieces.Length;i++)
            {
                if(orderedPieces[i]==null || !BirdPlacementRegion.Finite(pieceHeights[i]) || pieceHeights[i]<=0) throw new System.ArgumentException("Each piece needs a finite positive height.");
                for(int j=0;j<i;j++) if(orderedPieces[i]==orderedPieces[j]) throw new System.ArgumentException("Pieces must be distinct.");
            }
            for(int i=0;i<3;i++)
            {
                if(supports[i]==null || destinations[i]==null) throw new System.ArgumentException("Missing support or placement slot.");
                for(int j=0;j<i;j++) if(supports[i]==supports[j] || destinations[i]==destinations[j]) throw new System.ArgumentException("Supports and slots must be distinct.");
            }
            pieces=(BirdGrabTarget[])orderedPieces.Clone(); heights=(float[])pieceHeights.Clone(); pegs=(Transform[])supports.Clone(); slots=(BirdSnapTarget[])destinations.Clone();
            locations=new int[pieces.Length]; ResetPuzzle();
        }
        void Start() { if(locations==null && pieces.Length>0) Configure(pieces,heights,pegs,slots); }
        public void ResetPuzzle()
        {
            if(Busy()) return;
            if(locations==null) locations=new int[pieces.Length];
            Moves=0;
            float y=0;
            for(int i=pieces.Length-1;i>=0;i--)
            {
                locations[i]=0;
                pieces[i].transform.position=transform.TransformPoint(transform.InverseTransformPoint(pegs[0].position)+Vector3.up*(y+heights[i]*.5f));
                y+=heights[i];
            }
            Physics.SyncTransforms();
        }
        bool Busy() { if(reserved!=null) return true; foreach(var item in pieces) if(item!=null && item.Owner!=null) return true; return false; }
        int Rank(BirdGrabTarget item) { return System.Array.IndexOf(pieces,item); }
        int Peg(BirdSnapTarget slot) { return System.Array.IndexOf(slots,slot); }
        public override bool CanGrab(BirdGrabTarget item)
        {
            int rank=Rank(item);
            if(!isActiveAndEnabled || locations==null || rank<0 || reserved!=null) return false;
            for(int i=0;i<rank;i++) if(locations[i]==locations[rank]) return false;
            return true;
        }
        public override bool TryBegin(BirdGrabTarget item)
        {
            if(!CanGrab(item)) return false;
            reserved=item; int rank=Rank(item);
            for(int peg=0;peg<3;peg++)
            {
                float y=0; for(int i=0;i<pieces.Length;i++) if(i!=rank && locations[i]==peg) y+=heights[i];
                slots[peg].transform.position=transform.TransformPoint(transform.InverseTransformPoint(pegs[peg].position)+Vector3.up*(y+heights[rank]*.5f));
            }
            return true;
        }
        public override bool CanPlace(BirdGrabTarget item,BirdSnapTarget destination)
        {
            int rank=Rank(item),peg=Peg(destination);
            if(!isActiveAndEnabled || reserved!=item || rank<0 || peg<0) return false;
            for(int i=0;i<rank;i++) if(locations[i]==peg) return false;
            return true;
        }
        public override bool TryCommit(BirdGrabTarget item,BirdSnapTarget destination)
        {
            if(!CanPlace(item,destination)) return false;
            int rank=Rank(item),peg=Peg(destination);
            if(locations[rank]!=peg) { locations[rank]=peg; Moves++; }
            reserved=null; return true;
        }
        public override void Cancel(BirdGrabTarget item) { if(reserved==item) reserved=null; }
    }
}
