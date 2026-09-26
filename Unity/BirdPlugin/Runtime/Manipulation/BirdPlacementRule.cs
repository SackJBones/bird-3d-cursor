using UnityEngine;

namespace Bird3DCursor.Manipulation
{
    /// <summary>Optional experience rule. Reserve without changing committed placement; commit atomically.</summary>
    public abstract class BirdPlacementRule : MonoBehaviour
    {
        public abstract bool CanGrab(BirdGrabTarget item);
        public abstract bool TryBegin(BirdGrabTarget item);
        public abstract bool CanPlace(BirdGrabTarget item,BirdSnapTarget destination);
        public abstract bool TryCommit(BirdGrabTarget item,BirdSnapTarget destination);
        public abstract void Cancel(BirdGrabTarget item);
    }
}
