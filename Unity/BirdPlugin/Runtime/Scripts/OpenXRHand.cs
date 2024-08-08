#if BIRD_OPENXR_ENABLED
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.XR.Hands;

namespace Bird3DCursor { 
    /// <summary>
    ///  Represents a hand for use with the Bird 3D cursor using the Ultraleap API.
    /// </summary>
    public class OpenXRHand : Hand
    {
        /// <summary>
        /// Initializes a new instance of the Hand class with the specified chirality.
        /// </summary>
        /// <param name="chirality">The chirality of the hand (Chirality.Left or Chirality.Right).</param>
        /// 
        private Handedness handedness;
        private XRHandSubsystem m_Subsystem;
        static readonly List<XRHandSubsystem> s_SubsystemsReuse = new List<XRHandSubsystem>();

        //this is an initializer
        public OpenXRHand(Hand.Chirality chirality) : base(chirality) 
        {
            //documentation of mysterious incantation save for posterity
            //handedness = chirality == Hand.Chirality.Left ? LeapChirality.Left : LeapChirality.Right;

            if (chirality == Hand.Chirality.Left)
            {
                handedness = Handedness.Left;
            }
            else if (chirality == Hand.Chirality.Right)
            {
                handedness = Handedness.Right;
            }
            else
            {
                Debug.LogError("Your hand is neither left nor right. Something is very wrong.");
            }

        }//end of initializer

        XRHand GetSubsystemHand()
        {
            if (handedness == Handedness.Left)
            {
                return m_Subsystem.leftHand;
            } 
            else{
                return m_Subsystem.rightHand;
            }
        }

        private void FindOpenXRSubsystem()
        {
            if (m_Subsystem != null && m_Subsystem.running)
                return;

            SubsystemManager.GetSubsystems(s_SubsystemsReuse);
            var foundRunningHandSubsystem = false;
            for (var i = 0; i < s_SubsystemsReuse.Count; ++i)
            {
                var handSubsystem = s_SubsystemsReuse[i];
                if (handSubsystem.running)
                {
                    m_Subsystem = handSubsystem;
                    foundRunningHandSubsystem = true;
                    break;
                }
            }

            if (!foundRunningHandSubsystem)
                return;
        }


        /// <summary>
        /// Determines whether the hand is currently being tracked by a Leap-compatible device and has data available. 
        /// </summary>
        /// <returns>True if the hand is being tracked and has data available; otherwise, false.</returns>
        public override bool IsTracking()
        {
            FindOpenXRSubsystem();
            if (m_Subsystem == null) return false;
            return GetSubsystemHand().isTracked;
        }

        /// <summary>
        /// Gets the base position of the specified finger.
        /// </summary>
        /// <param name="finger"></param>
        /// <returns>Base position of the finger (Vector3)</returns>
        public override Vector3 GetBasePosition(Finger finger)
        {
            XRHandJointID jointId;
            switch (finger)
            {
                case Finger.Thumb:
                    jointId = XRHandJointID.ThumbMetacarpal;
                    break;
                case Finger.Index:
                    jointId = XRHandJointID.IndexProximal;
                    break;
                case Finger.Middle:
                    jointId = XRHandJointID.MiddleProximal;
                    break;
                case Finger.Ring:
                    jointId = XRHandJointID.RingProximal;
                    break;
                case Finger.Pinky:
                    jointId = XRHandJointID.LittleProximal;
                    break;
                default:
                    return Vector3.zero;
            }
            UnityEngine.Pose pose;
            GetSubsystemHand().GetJoint(jointId).TryGetPose(out pose);
            if (pose == null) return Vector3.zero;
            return pose.position;
        }

        /// <summary>
        /// Gets the position of the joint at the proximal end of the middle bone of the specified finger.
        /// </summary>
        /// <param name="finger"></param>
        /// <returns>Middle proximal position of the finger (Vector3)</returns>
        public override Vector3 GetIntermediatePosition(Finger finger)
        {
            XRHandJointID jointId;
            switch (finger)
            {
                case Finger.Thumb:
                    jointId = XRHandJointID.ThumbProximal;
                    break;
                case Finger.Index:
                    jointId = XRHandJointID.IndexIntermediate;
                    break;
                case Finger.Middle:
                    jointId = XRHandJointID.MiddleIntermediate;
                    break;
                case Finger.Ring:
                    jointId = XRHandJointID.RingIntermediate;
                    break;
                case Finger.Pinky:
                    jointId = XRHandJointID.LittleIntermediate;
                    break;
                default:
                    return Vector3.zero;
            }
            UnityEngine.Pose pose;
            GetSubsystemHand().GetJoint(jointId).TryGetPose(out pose);
            if (pose == null) return Vector3.zero;
            return pose.position;
        }

        /// <summary>
        /// Gets the position of the joint at the distal end of the middle bone of the specified finger.
        /// </summary>
        /// <param name="finger"></param>
        /// <returns>Middle distal position of the finger (Vector3)</returns>
        public override Vector3 GetDistalPosition(Finger finger)
        {
            XRHandJointID jointId;
            switch (finger)
            {
                case Finger.Thumb:
                    jointId = XRHandJointID.ThumbDistal;
                    break;
                case Finger.Index:
                    jointId = XRHandJointID.IndexDistal;
                    break;
                case Finger.Middle:
                    jointId = XRHandJointID.MiddleDistal;
                    break;
                case Finger.Ring:
                    jointId = XRHandJointID.RingDistal;
                    break;
                case Finger.Pinky:
                    jointId = XRHandJointID.LittleDistal;
                    break;
                default:
                    return Vector3.zero;
            }
            UnityEngine.Pose pose;
            GetSubsystemHand().GetJoint(jointId).TryGetPose(out pose);
            if (pose == null) return Vector3.zero;
            return pose.position;
        }

        /// <summary>
        /// Gets the position of the tip of the specified finger.
        /// </summary>
        /// <param name="finger"></param>
        /// <returns>Tip position of the finger (Vector3)</returns>
        public override Vector3 GetTipPosition(Finger finger)
        {
            XRHandJointID jointId;
            switch (finger)
            {
                case Finger.Thumb:
                    jointId = XRHandJointID.ThumbTip;
                    break;
                case Finger.Index:
                    jointId = XRHandJointID.IndexTip;
                    break;
                case Finger.Middle:
                    jointId = XRHandJointID.MiddleTip;
                    break;
                case Finger.Ring:
                    jointId = XRHandJointID.RingTip;
                    break;
                case Finger.Pinky:
                    jointId = XRHandJointID.LittleTip;
                    break;
                default:
                    return Vector3.zero;
            }
            UnityEngine.Pose pose;
            GetSubsystemHand().GetJoint(jointId).TryGetPose(out pose);
            if (pose == null) return Vector3.zero;
            return pose.position;
        }
    }
}
#endif