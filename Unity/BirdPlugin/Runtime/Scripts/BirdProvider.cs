// BirdCursor.cs
using System.Collections.Generic;
using UnityEngine;

namespace Bird3DCursor
{

    public class BirdProvider : MonoBehaviour
    {
        public BirdHandAPI handTrackingAPI;
        private Bird bird;
        public Hand.Chirality chirality;
        public GameObject targetUnitSphere; // visible sphere-- good for debugging
        public GameObject debugMarker;
        public GameObject birdMarker;
        public bool rayWasHit;
        public RaycastHit birdRayHit;
        public Material birdSelectedMaterial, birdMaterial;
        public bool showDebug; // turn this on to see hand points and transparent sphere that represents fit. Can be turned off.
        GameObject[] debugMarkers;
        GameObject hitMarker;
        bool showingDebug;
        public Quaternion rotation;
        private string associatedUser;
        public static string defaultUser = "DefaultUser";
        private string chiralityStr;

        void Start()
        {
            birdMarker = Instantiate(birdMarker);
            targetUnitSphere = Instantiate(targetUnitSphere);
            Hand apiSpecificHand = HandFactory.CreateHand(chirality, handTrackingAPI);
            bird = new Bird(apiSpecificHand);
            int numFitPoints = bird.GetNumFitPoints();
            // makes debug markers
            debugMarkers = new GameObject[numFitPoints];
            for (int i = 0; i < numFitPoints; i++)
            {
                debugMarkers[i] = Instantiate(debugMarker);
            }
            hitMarker = Instantiate(debugMarker);
            chiralityStr = chirality == Hand.Chirality.Left ? "Left" : "Right";
            hitMarker.name = $"{chiralityStr}HitMarker";
            debugGeometryVisible(showDebug);
            BirdManager.RegisterBird(this);
            associatedUser = defaultUser;
        }

        void Update()
        {
            bird.Update();
            birdMarker.transform.position = bird.GetPosition();
            List<Vector3> sphereFitPoints = bird.GetSphereFitPoints();
            for (int i = 0; i < sphereFitPoints.Count; i++)
            {
                debugMarkers[i].transform.position = sphereFitPoints[i];
                debugMarkers[i].name = $"{chiralityStr}DebugMarker{i}";
            }
            if (bird.GetClickDown())
            {
                Material mat = birdSelectedMaterial; // will break if this material is null
                birdMarker.GetComponent<Renderer>().material = mat;
            }
            if (bird.GetClickUp())
            {
                Material mat = birdMaterial; // will break if this material is null
                birdMarker.GetComponent<Renderer>().material = mat;
            }
            Vector3 sphereFitCenter = bird.GetSphereFitCenter();
            float sphereFitRadius = bird.GetSphereFitRadius();
            targetUnitSphere.transform.position = sphereFitCenter;
            float sphereDiam = sphereFitRadius * 2;
            targetUnitSphere.transform.localScale = new Vector3(sphereDiam, sphereDiam, sphereDiam);
            Vector3 handRoot = bird.GetHandRoot();
            Ray birdRay = bird.GetRay();
            rayWasHit = Physics.Raycast(birdRay, out birdRayHit, bird.GetRange());
            if (showingDebug && rayWasHit)
            {
                hitMarker.SetActive(true);
                hitMarker.transform.position = birdRayHit.point;
            }
            else
            {
                hitMarker.SetActive(false);
            }
            // TODO: this was empirical and it's terrible, but I don't know how to straighten it out yet
            // Note: did not do.
            rotation = transform.rotation * Quaternion.AngleAxis(90, Vector3.up) * Quaternion.AngleAxis(90, Vector3.forward) * Quaternion.AngleAxis(-90, Vector3.right) * Quaternion.AngleAxis(90, Vector3.up) * Quaternion.AngleAxis(30, Vector3.right);
        }

        public bool BirdHit(Collider coll, out RaycastHit hit)
        {
            Vector3 vel = bird.GetVelocity();
            Ray ray = new Ray(bird.GetPrevPosition(), vel.normalized);
            return coll.Raycast(ray, out hit, vel.magnitude);
        }

        public bool WentThrough(Collider coll, Vector3? direction = null)
        {
            float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
            if (direction != null && Dot(bird.GetVelocity().normalized, direction.Value.normalized) < .7071f)
            { // root 1/2, i.e. must be less than 45 degrees from direction
                return false;
            }
            RaycastHit hit;
            return BirdHit(coll, out hit);
        }

        // if geometry, geometry
        void debugGeometryVisible(bool show)
        {
            showingDebug = show; for (int i = 0; i < bird.GetNumFitPoints(); i++)
            {
                debugMarkers[i].SetActive(show);
            }
            targetUnitSphere.SetActive(show);
        }

        public string GetAssociatedUser() {
            return associatedUser;
        }

        public string GetChiralityStr() {
            return chiralityStr;
        }

        public void SetAssociatedUser(string user) {
            associatedUser = user;
        }

        public RaycastHit GetRayHit() {
            return birdRayHit;
        }

        void OnDestroy()
        {
            BirdManager.UnregisterBird(this);
        }

        public float GetRange()
        {
            return bird.GetRange();
        }

        public Vector3 GetPosition()
        {
            return bird.GetPosition();
        }

        public Vector3 GetVelocity()
        {
            return bird.GetVelocity();
        }

        public Vector3 GetPrevPosition()
        {
            return bird.GetPrevPosition();
        }

        public Ray GetRay()
        {
            return bird.GetRay();
        }

        public bool GetClick()
        {
            return bird.GetClick();
        }

        public bool GetClickDown()
        {
            return bird.GetClickDown();
        }

        public bool GetClickUp()
        {
            return bird.GetClickUp();
        }

        public float GetTwist()
        {
            return bird.GetTwist();
        }

        public Bird GetBird()
        {
            return bird; // may be needed for more detailed or uncommon access to bird properties like GetHandRoot(). generally avoid using this and prefer getters.
        }
    }
}