// Hand.cs
using System.Collections.Generic;
using UnityEngine;

namespace Bird3DCursor {

    public enum Finger
    {
        Thumb,
        Index,
        Middle,
        Ring,
        Pinky
    }

    public abstract class Hand
    {
        public enum Chirality
        {
            Left,
            Right
        }

        protected Chirality chirality;

        public Hand(Chirality chirality)
        {
            this.chirality = chirality;
        }

        // Abstract method for whether the hand is active and has data available
        public abstract bool IsTracking();

        // Abstract methods for getting joint positions
        public abstract Vector3 GetBasePosition(Finger finger);
        public abstract Vector3 GetIntermediatePosition(Finger finger);
        public abstract Vector3 GetDistalPosition(Finger finger);
        public abstract Vector3 GetTipPosition(Finger finger);

        // Default implementation using the above specific methods
        public virtual List<Vector3> GetJointPositions(Finger finger)
        {
            return new List<Vector3>
            {
                GetBasePosition(finger),
                GetIntermediatePosition(finger),
                GetDistalPosition(finger),
                GetTipPosition(finger)
            };
        }
    }
}