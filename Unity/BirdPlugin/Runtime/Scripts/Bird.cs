///PART OF SPHERE FIT BIRD
///Sphere Fit Bird is a collaboration between Dana Gretton (good at math) and
///Aubrey Simonson (his boyfriend, inventor) based on Aubrey's 2021 MIT Media Lab Thesis project, Bird.
///For more information on Bird, see: https://drive.google.com/file/d/1p6IUu9QIzWNBERz3IW_yVcojQjVz06rl/view?usp=sharing
///This project works with the OVR Toolkit, and once OpenXR is more stable, someone should probably make an OpenXR version.
/// 
/// Bird.cs handles all of the crazy linear algebra for converting the hand skeleton points to 
/// an individual point in space which moves smoothly in an easy to control way. 
/// 
/// It does so using sphere fit and Kalman filtering.
/// For how on Earth one does sphere fit code, we are grateful for this useful article:
/// https://jekel.me/2015/Least-Squares-Sphere-Fit/
/// 
/// Doesn't strictly require other scripts (that's on purpose!)
/// Remember to give it materials for birdSelectedMaterial and birdMaterial in the inspector
/// Put this script on the hand! Specifically, it should go an OVRHandPrefab with an OVRSkeleton component
/// 
///???---> asimonso@mit.edu/followspotfour@gmail.com // dgretton@mit.edu/dana.gretton@gmail.com
///Last edited February 2022

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bird3DCursor {
    public class Bird {
        public Vector3 birdPosition;
        private float range;
        private bool selected;
        private bool down;
        private bool up;
        private Ray ray;
        private float twist;
        private bool twistReverse;
        private Vector3 prevBirdPosition;
        private Hand hand;
        private int numFitPoints;
        private List<Vector3> fitPoints;
        private Vector3 sphereFitCenter;
        private Vector3 handRoot;
        private float sphereFitRadius;
        private Vector3[] pointPoss;
        private float[] pointDists;
        private float[] a2Xs;
        private float[] a2Ys;
        private float[] a2Zs;
        private float[] aOnes;
        private float[] aIntermediates;
        private KalmanFilterVector3 filter;
        private Hand.Chirality chirality;

        public Bird(Hand hand) {
            this.hand = hand;
            numFitPoints = 16; // number of joint positions in the hand skeleton that will be used to fit the sphere
            pointPoss = new Vector3[numFitPoints]; // this is the set of points we fit a sphere to
            fitPoints = new List<Vector3>(); // user-friendlier list of the same

            // arrays that get filled with intermediate results every frame
            pointDists = new float[numFitPoints]; // the distance, for each point, from the average position of all points in pointPoss
            a2Xs = new float[numFitPoints];
            a2Ys = new float[numFitPoints];
            a2Zs = new float[numFitPoints];
            aOnes = new float[numFitPoints];
            for (int i = 0; i < numFitPoints; i++) aOnes[i] = 1.0f;
            aIntermediates = new float[4];

            // it's important that we change R based on bird distance from hand (in the update function) because jitter at the end of a 
            // raycast which is far away is more than the scale of intentional movements nearby
            filter = new KalmanFilterVector3(0.001f, .06f); // Q is process variance, R is measurement variance

            selected = false; // we are not selecting on the first frame
        } // end constructor

        public void Update() {

            if (!hand.IsTracking()) {
                return;
            }

            Vector3 indexRoot = hand.GetBasePosition(Finger.Index);
            handRoot = 0.6f * indexRoot + 0.4f * hand.GetBasePosition(Finger.Thumb);
            // Vector3 handRoot = 
            // (0.3f * hand.GetIndex().Bone(Bone.BoneType.TYPE_PROXIMAL).PrevJoint) + 
            // (0.3f * hand.GetIndex().Bone(Bone.BoneType.TYPE_PROXIMAL).PrevJoint) + (0.4f * hand.GetThumb().Bone(Bone.BoneType.TYPE_PROXIMAL).PrevJoint);
            Vector3 indexTip = hand.GetTipPosition(Finger.Index);

            fitPoints = new List<Vector3>()
            {
                // leave out thumb base; including it in the sphere fit tends to pin it to the wrist,
                // making it difficult to expand beyond a certain size
                hand.GetMiddleProximal(Finger.Thumb),
                hand.GetMiddleDistal(Finger.Thumb),
                hand.GetTipPosition(Finger.Thumb),

                // leave out index finger joints, but include knuckle
                indexRoot,

                hand.GetBasePosition(Finger.Middle),
                hand.GetMiddleProximal(Finger.Middle),
                hand.GetMiddleDistal(Finger.Middle),
                hand.GetTipPosition(Finger.Middle),

                hand.GetBasePosition(Finger.Ring),
                hand.GetMiddleProximal(Finger.Ring),
                hand.GetMiddleDistal(Finger.Ring),
                hand.GetTipPosition(Finger.Ring),

                hand.GetBasePosition(Finger.Pinky),
                hand.GetMiddleProximal(Finger.Pinky),
                hand.GetMiddleDistal(Finger.Pinky),
                hand.GetTipPosition(Finger.Pinky),
            };

            // List<Vector3> points = new List<Vector3>()
            // {
            //     hand.GetThumb().Bone(Bone.BoneType.TYPE_INTERMEDIATE).PrevJoint,
            //     hand.GetThumb().Bone(Bone.BoneType.TYPE_DISTAL).PrevJoint,
            //     hand.GetIndex().Bone(Bone.BoneType.TYPE_PROXIMAL).PrevJoint,
            //     hand.GetMiddle().Bone(Bone.BoneType.TYPE_PROXIMAL).PrevJoint,
            //     hand.GetMiddle().Bone(Bone.BoneType.TYPE_INTERMEDIATE).PrevJoint,
            //     hand.GetMiddle().Bone(Bone.BoneType.TYPE_DISTAL).PrevJoint,
            //     hand.GetRing().Bone(Bone.BoneType.TYPE_PROXIMAL).PrevJoint,
            //     hand.GetRing().Bone(Bone.BoneType.TYPE_INTERMEDIATE).PrevJoint,
            //     hand.GetRing().Bone(Bone.BoneType.TYPE_DISTAL).PrevJoint,
            //     hand.GetPinky().Bone(Bone.BoneType.TYPE_PROXIMAL).PrevJoint,
            //     hand.GetPinky().Bone(Bone.BoneType.TYPE_INTERMEDIATE).PrevJoint,
            //     hand.GetPinky().Bone(Bone.BoneType.TYPE_DISTAL).PrevJoint,
            //     hand.GetThumb().TipPosition,
            //     hand.GetMiddle().TipPosition,
            //     hand.GetRing().TipPosition,
            //     hand.GetPinky().TipPosition
            // };
            // end getting position of bones

            string chiralityStr = chirality == Hand.Chirality.Left ? "Left" : "Right";
            // string chiralityStr = chirality == Chirality.Left ? "Left" : "Right";

            for (int i = 0; i < fitPoints.Count; i++)
            {
                pointPoss[i] = fitPoints[i];
            }

            Vector4 fitVector = LeastSqSphere(pointPoss); // calls linear algebra from later in this script
            float sphereFitCenterX = fitVector[0];
            float sphereFitCenterY = fitVector[1];
            float sphereFitCenterZ = fitVector[2];
            sphereFitRadius = fitVector[3];

            sphereFitCenter = new Vector3(sphereFitCenterX, sphereFitCenterY, sphereFitCenterZ);
            Vector3 pointing = sphereFitCenter - handRoot; // direction that the bird goes out from the hand-- the pointing vector
            float m = pointing.magnitude; // distance from hand root to center of sphere

            prevBirdPosition = birdPosition;

            // this line determines the position of the bird based on pointing and bird range function--
            // bird range function can be whatever you want. We define it later in this script.
            // this smooths using a Kalman filter.
            birdPosition = filter.Update(pointing/m*birdRangeFunc(m) + handRoot, null, m*m*m*270f); // m^3 * 270 is R
            range = (birdPosition - handRoot).magnitude; // figure out far away bird is

            ray = new Ray(handRoot, (birdPosition - handRoot).normalized);

            Vector3 handUpVector = indexRoot - handRoot; // we will compare this "hand up" to "world up" to calculate a twist angle
            handUpVector = handUpVector - Vector3.Project(handUpVector, pointing); // a vector in the plane of {pointing, origin, index knuckle}, perpendicular to pointing
            Vector3 noTwistVector = Vector3.up - Vector3.Project(Vector3.up, pointing); // a vector in the plane of {pointing, origin, up}, perpendicular to pointing
            twist = Vector3.SignedAngle(handUpVector, noTwistVector, pointing);
            if (twistReverse)
            {
                twist *= -1;
            }

            // start clicking-related things

            // the following two lines prevent rapid clicking/unclicking
            float selectDepth = .007f; // distance in meters that the tip of the pointer finger must penetrate the sphere to enter a selected state
            float releaseDepth = .005f; // distance into the sphere that tip of pointer finger needs to be retracted to to exit selected state

            Vector3 selectCenter;

            // if the tip of the finger is closer to the center of the sphere than to the bird
            if ((indexTip - sphereFitCenter).magnitude < (indexTip - birdPosition).magnitude)
            {
                selectCenter = sphereFitCenter;
            }
            else
            {
                selectCenter = birdPosition;
            }

            float indexDepth = sphereFitRadius - (indexTip - selectCenter).magnitude; // depth into sphere centered at selectCenter that index tip is penetrating
            down = false;
            up = false;
            if(!selected && indexDepth > selectDepth)
            {
                selected = true;
                down = true;
            }
            if (selected && indexDepth < releaseDepth)
            {
                selected = false;
                up = true;
            }
        } // end update

        public Vector3 GetPosition() {
            return birdPosition;
        }

        public Vector3 GetPrevPosition() {
            return prevBirdPosition;
        }

        public float GetRange() {
            return range;
        }

        public bool GetClickDown() {
            return down;
        }

        public bool GetClickUp() {
            return up;
        }

        public bool GetClick() {
            return selected;
        }

        public Ray GetRay() {
            return ray;
        }

        public float GetTwist() {
            return twist;
        }

        public List<Vector3> GetSphereFitPoints() {
            return fitPoints;
        }

        public Vector3 GetSphereFitCenter() {
            return sphereFitCenter;
        }

        public float GetSphereFitRadius() {
            return sphereFitRadius;
        }

        public int GetNumFitPoints() {
            return numFitPoints;
        }

        public Vector3 GetHandRoot() {
            return handRoot;
        }

        public Vector3 GetVelocity() {
            return (birdPosition - prevBirdPosition)/Time.deltaTime;
        }

        // THIS IS THE BIRD RANGE FUNCTION!
        // This is the function called above which determines how far along the ray the bird should go--
        // You can replace it with your own if it seems like this should be different
        // movement is scaled up farther away
        // in the current implementation, movement scales up slowly first, then fast
        private float birdRangeFunc(float x) // should be equal to x near 0, where "near" is relative to characteristic distance
        {
            float characteristic1 = .02f; // more of this close
            float characteristic2 = .03f; // more of this far away
            float x_norm1 = x / characteristic1;
            float x_norm2 = x / characteristic2;
            return (x_norm1 + (x_norm1 * x_norm1) + (x_norm2 * x_norm2 * x_norm2 * x_norm2 * x_norm2 * x_norm2)) * characteristic1; // x+x^2+y^6
        }

        // Here we define some linear algebra things which... aren't? in Unity by default.
        // So we wrote them.
        private float Dot(float[] v1, float[] v2)
        {
            int len = v1.Length;
            if (v2.Length != len)
            {
                throw new System.ArgumentException("Cannot take dot product of vectors of unequal length");
            }
            float dot_product = 0.0f;
            for (int i = 0; i < len; i++)
            {
                dot_product += v1[i] * v2[i];
            }
            return dot_product;
        }

        private float Dot(Vector3 v1, float[] v2)
        {
            return Dot(new float[] { v1[0], v1[1], v1[2] }, v2);
        }

        private float Dot(float[] v1, Vector3 v2)
        {
            return Dot(v2, v1);
        }

        private float Dot(Vector3 v1, Vector3 v2)
        {
            return Dot(new float[] { v1[0], v1[1], v1[2] }, new float[] { v2[0], v2[1], v2[2] });
        }

        private float Dot(Vector4 v1, float[] v2)
        {
            return Dot(new float[] { v1[0], v1[1], v1[2], v1[3] }, v2);
        }

        private float Dot(float[] v1, Vector4 v2)
        {
            return Dot(v2, v1);
        }

        // more linear algebra-- returns a vector 4 where the first 3 components are the center of the sphere and the last component is the radius
        private Vector4 LeastSqSphere(Vector3[] pointVectors) {
            // Return a Vector4([center x, center y, center z, radius])
            Vector3 meanPoint = Vector3.zero;
            for (int i = 0; i < pointVectors.Length; i++)
            {
                meanPoint += pointVectors[i];
            }
            meanPoint /= pointVectors.Length;
            for (int i = 0; i < pointVectors.Length; i++)
            {
                Vector3 pos = pointVectors[i] - meanPoint;
                pointDists[i] = pos.sqrMagnitude;
                a2Xs[i] = pos.x * 2.0f;
                a2Ys[i] = pos.y * 2.0f;
                a2Zs[i] = pos.z * 2.0f;
            }
            aIntermediates[0] = Dot(a2Xs, pointDists);
            aIntermediates[1] = Dot(a2Ys, pointDists);
            aIntermediates[2] = Dot(a2Zs, pointDists);
            aIntermediates[3] = Dot(aOnes, pointDists);
            float a4XYs = Dot(a2Xs, a2Ys);
            float a4XZs = Dot(a2Xs, a2Zs);
            float a4YZs = Dot(a2Ys, a2Zs);
            float aSum2Xs = Dot(a2Xs, aOnes);
            float aSum2Ys = Dot(a2Ys, aOnes);
            float aSum2Zs = Dot(a2Zs, aOnes);
            Matrix4x4 aTa = new Matrix4x4();
            aTa.SetRow(0, new Vector4(Dot(a2Xs, a2Xs), a4XYs, a4XZs, aSum2Xs));
            aTa.SetRow(1, new Vector4(a4XYs, Dot(a2Ys, a2Ys), a4YZs, aSum2Ys));
            aTa.SetRow(2, new Vector4(a4XZs, a4YZs, Dot(a2Zs, a2Zs), aSum2Zs));
            aTa.SetRow(3, new Vector4(aSum2Xs, aSum2Ys, aSum2Zs, pointDists.Length));
            Matrix4x4 aTaInv = aTa.inverse;
            float sphereFitCenterX = Dot(aTaInv.GetRow(0), aIntermediates);
            float sphereFitCenterY = Dot(aTaInv.GetRow(1), aIntermediates);
            float sphereFitCenterZ = Dot(aTaInv.GetRow(2), aIntermediates);
            float sphereSqRadDifference = Dot(aTaInv.GetRow(3), aIntermediates);
            Vector3 sphereFitCenter = new Vector3(sphereFitCenterX, sphereFitCenterY, sphereFitCenterZ);
            Vector3 sphereCorrectedCenter = sphereFitCenter + meanPoint;
            float sphereFitRadius = Mathf.Sqrt(sphereSqRadDifference + sphereFitCenter.sqrMagnitude);
            return new Vector4(sphereCorrectedCenter.x, sphereCorrectedCenter.y, sphereCorrectedCenter.z, sphereFitRadius);
        }
    } // end of Bird class
}
