#if BIRD_OCULUS_OVR_ENABLED
using UnityEngine;
using OVRSpecificHand = OVRPlugin.Hand.OVRHand;

namespace Bird3DCursor
{

    public class OVRHand : Hand
    {
        private OVRBone index1;
        private OVRBone index2;
        private OVRBone index3;
        private OVRBone indexTip;
        private OVRBone middle1;
        private OVRBone middle2;
        private OVRBone middle3;
        private OVRBone middleTip;
        private OVRBone ring1;
        private OVRBone ring2;
        private OVRBone ring3;
        private OVRBone ringTip;
        private OVRBone pinky1;
        private OVRBone pinky2;
        private OVRBone pinky3;
        private OVRBone pinkyTip;
        private OVRBone thumb1;
        private OVRBone thumb2;
        private OVRBone thumb3;
        private OVRBone thumbTip;

        public OVRHand(Hand.Chirality chirality) : base(chirality)
        {
            OVRSpecificHand ovrHand;
            if (chirality == Hand.Chirality.Left)
            {
                ovrHand = GameObject.FindObjectOfType<OVRHand>(h => h.HandType == OVRHand.Hand.HandLeft);
            }
            else
            {
                ovrHand = GameObject.FindObjectOfType<OVRHand>(h => h.HandType == OVRHand.Hand.HandRight);
            }
            OVRSkeleton skeleton = ovrHand.GetComponent<OVRSkeleton>();
            foreach (OVRBone bone in skeleton.Bones)
            {
                if (bone.Id == OVRSkeleton.BoneId.Hand_Index1)
                {
                    index1 = bone;
                }
                if (bone.Id == OVRSkeleton.BoneId.Hand_Index2)
                {
                    index2 = bone;
                }
                if (bone.Id == OVRSkeleton.BoneId.Hand_Index3)
                {
                    index3 = bone;
                }
                if (bone.Id == OVRSkeleton.BoneId.Hand_IndexTip)
                {
                    indexTip = bone;
                }
                if (bone.Id == OVRSkeleton.BoneId.Hand_Middle1)
                {
                    middle1 = bone;
                }
                if (bone.Id == OVRSkeleton.BoneId.Hand_Middle2)
                {
                    middle2 = bone;
                }
                if (bone.Id == OVRSkeleton.BoneId.Hand_Middle3)
                {
                    middle3 = bone;
                }
                if (bone.Id == OVRSkeleton.BoneId.Hand_MiddleTip)
                {
                    middleTip = bone;
                }
                if (bone.Id == OVRSkeleton.BoneId.Hand_Ring1)
                {
                    ring1 = bone;
                }
                if (bone.Id == OVRSkeleton.BoneId.Hand_Ring2)
                {
                    ring2 = bone;
                }
                if (bone.Id == OVRSkeleton.BoneId.Hand_Ring3)
                {
                    ring3 = bone;
                }
                if (bone.Id == OVRSkeleton.BoneId.Hand_RingTip)
                {
                    ringTip = bone;
                }
                if (bone.Id == OVRSkeleton.BoneId.Hand_Pinky1)
                {
                    pinky1 = bone;
                }
                if (bone.Id == OVRSkeleton.BoneId.Hand_Pinky2)
                {
                    pinky2 = bone;
                }
                if (bone.Id == OVRSkeleton.BoneId.Hand_Pinky3)
                {
                    pinky3 = bone;
                }
                if (bone.Id == OVRSkeleton.BoneId.Hand_PinkyTip)
                {
                    pinkyTip = bone;
                }
                if (bone.Id == OVRSkeleton.BoneId.Hand_Thumb1)
                {
                    thumb1 = bone;
                }
                if (bone.Id == OVRSkeleton.BoneId.Hand_Thumb2)
                {
                    thumb2 = bone;
                }
                if (bone.Id == OVRSkeleton.BoneId.Hand_Thumb3)
                {
                    thumb3 = bone;
                }
                if (bone.Id == OVRSkeleton.BoneId.Hand_ThumbTip)
                {
                    thumbTip = bone;
                }
            }
            //if any are null throw error
            if (index1 == null || index2 == null || index3 == null || indexTip == null
                || middle1 == null || middle2 == null || middle3 == null || middleTip == null
                || ring1 == null || ring2 == null || ring3 == null || ringTip == null
                || pinky1 == null || pinky2 == null || pinky3 == null || pinkyTip == null
                || thumb1 == null || thumb2 == null || thumb3 == null || thumbTip == null)
            {
                throw new System.Exception("Failed to find all bones in OVR hand");
            }
        } // end constructor

        public override bool IsTracking()
        {
            return true; // we don't expect OVR hands to not be tracking, they're always somewhere (even if they randomly teleport to the origin)
        }

        public override Vector3 GetBasePosition(Finger finger)
        {
            switch (finger)
            {
                case Finger.Index:
                    return index1.Transform.position;
                case Finger.Middle:
                    return middle1.Transform.position;
                case Finger.Ring:
                    return ring1.Transform.position;
                case Finger.Pinky:
                    return pinky1.Transform.position;
                case Finger.Thumb:
                    return thumb1.Transform.position;
                default:
                    throw new System.Exception("Unknown finger " + finger);
            }
        }//end get base position

        public override Vector3 GetIntermediatePosition(Finger finger)
        {
            switch (finger)
            {
                case Finger.Index:
                    return index2.Transform.position;
                case Finger.Middle:
                    return middle2.Transform.position;
                case Finger.Ring:
                    return ring2.Transform.position;
                case Finger.Pinky:
                    return pinky2.Transform.position;
                case Finger.Thumb:
                    return thumb2.Transform.position;
                default:
                    throw new System.Exception("Unknown finger " + finger);
            }
        }//end get middle proximal

        public override Vector3 GetDistalPosition(Finger finger)
        {
            switch (finger)
            {
                case Finger.Index:
                    return index3.Transform.position;
                case Finger.Middle:
                    return middle3.Transform.position;
                case Finger.Ring:
                    return ring3.Transform.position;
                case Finger.Pinky:
                    return pinky3.Transform.position;
                case Finger.Thumb:
                    return thumb3.Transform.position;
                default:
                    throw new System.Exception("Unknown finger " + finger);
            }
        }//end get middle distal

        public override GetTipPosition(Finger finger)
        {
            switch (finger)
            {
                case Finger.Index:
                    return indexTip.Transform.position;
                case Finger.Middle:
                    return middleTip.Transform.position;
                case Finger.Ring:
                    return ringTip.Transform.position;
                case Finger.Pinky:
                    return pinkyTip.Transform.position;
                case Finger.Thumb:
                    return thumbTip.Transform.position;
                default:
                    throw new System.Exception("Unknown finger " + finger);
            }
        }//end get tip position
    }
}
#endif