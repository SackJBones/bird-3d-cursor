using UnityEngine;
using Leap.Unity;
using Leap;
using LeapHand = Leap.Hand;
using LeapFinger = Leap.Finger;
using LeapChirality = Leap.Unity.Chirality;
using System.Collections.Generic;

namespace Bird3DCursor {
    /// <summary>
    ///  Represents a hand for use with the Bird 3D cursor using the Ultraleap API.
    /// </summary>
    public class UltraLeapHand : Hand
    {
        private LeapChirality leapChirality;

        /// <summary>
        /// Initializes a new instance of the Hand class with the specified chirality.
        /// </summary>
        /// <param name="chirality">The chirality of the hand (Chirality.Left or Chirality.Right).</param>
        public UltraLeapHand(Hand.Chirality1 chirality) : base(chirality) 
        {
            leapChirality = chirality == Hand.Chirality1.Left ? LeapChirality.Left : LeapChirality.Right;
        }

        /// <summary>
        /// Gets the Ultraleap finger object for the specified chirality.
        /// </summary>
        /// <param name="finger">The finger for which to retrieve the Leap Motion object.</param>
        /// <returns>The Leap Finger object.</returns>
        private LeapFinger GetLeapFinger(Finger finger)
        {
            LeapHand hand = Hands.Get(leapChirality);

            if (hand == null) return null;

            switch (finger)
            {
                case Finger.Thumb:
                    return hand.GetThumb();
                case Finger.Index:
                    return hand.GetIndex();
                case Finger.Middle:
                    return hand.GetMiddle();
                case Finger.Ring:
                    return hand.GetRing();
                case Finger.Pinky:
                    return hand.GetPinky();
                default:
                    return null;
            }
        }

        /// <summary>
        /// Determines whether the hand is currently being tracked by a Leap-compatible device and has data available. 
        /// </summary>
        /// <returns>True if the hand is being tracked and has data available; otherwise, false.</returns>
        public override bool IsTracking()
        {
            LeapHand hand = Hands.Get(leapChirality);
            return hand != null;
        }

        /// <summary>
        /// Gets the base position of the specified finger.
        /// </summary>
        /// <param name="finger"></param>
        /// <returns>Base position of the finger (Vector3)</returns>
        public override Vector3 GetBasePosition(Finger finger)
        {
            var leapFinger = GetLeapFinger(finger);
            return leapFinger?.Bone(Bone.BoneType.TYPE_PROXIMAL)?.PrevJoint ?? Vector3.zero;
        }

        /// <summary>
        /// Gets the position of the joint at the proximal end of the middle bone of the specified finger.
        /// </summary>
        /// <param name="finger"></param>
        /// <returns>Middle proximal position of the finger (Vector3)</returns>
        public override Vector3 GetMiddleProximal(Finger finger)
        {
            var leapFinger = GetLeapFinger(finger);
            return leapFinger?.Bone(Bone.BoneType.TYPE_INTERMEDIATE)?.PrevJoint ?? Vector3.zero;
        }

        /// <summary>
        /// Gets the position of the joint at the distal end of the middle bone of the specified finger.
        /// </summary>
        /// <param name="finger"></param>
        /// <returns>Middle distal position of the finger (Vector3)</returns>
        public override Vector3 GetMiddleDistal(Finger finger)
        {
            var leapFinger = GetLeapFinger(finger);
            return leapFinger?.Bone(Bone.BoneType.TYPE_DISTAL)?.PrevJoint ?? Vector3.zero;
        }

        /// <summary>
        /// Gets the position of the tip of the specified finger.
        /// </summary>
        /// <param name="finger"></param>
        /// <returns>Tip position of the finger (Vector3)</returns>
        public override Vector3 GetTipPosition(Finger finger)
        {
            var leapFinger = GetLeapFinger(finger);
            return leapFinger?.Bone(Bone.BoneType.TYPE_DISTAL).NextJoint ?? Vector3.zero;
        }
    }
}