// BirdCursor.cs
using System.Collections.Generic;
using UnityEngine;

namespace Bird3DCursor
{

    public class BirdCursor : MonoBehaviour
    {
        public BirdHandAPI handTrackingAPI;
        public Bird bird;
        public Hand.Chirality1 chirality;
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
        public string chiralityStr;

        void Start()
        {
            Hand ultraLeapHand = HandFactory.CreateHand(chirality, handTrackingAPI);
            bird = new Bird(ultraLeapHand);
            int numFitPoints = bird.GetNumFitPoints();
            // makes debug markers
            debugMarkers = new GameObject[numFitPoints];
            for (int i = 0; i < numFitPoints; i++)
            {
                debugMarkers[i] = Instantiate(debugMarker);
            }
            hitMarker = Instantiate(debugMarker);
            chiralityStr = chirality == Hand.Chirality1.Left ? "Left" : "Right";
            hitMarker.name = $"{chiralityStr}HitMarker";
            debugGeometryVisible(showDebug);
            BirdManager.RegisterBird(this);
            associatedUser = defaultUser;
        }

        void Update()
        {
            bird.Update();
            birdMarker.transform.position = bird.birdPosition;
            List<Vector3> sphereFitPoints = bird.GetSphereFitPoints();
            for (int i = 0; i < sphereFitPoints.Count; i++)
            {
                debugMarkers[i].transform.position = sphereFitPoints[i];
                debugMarkers[i].name = $"{chiralityStr}DebugMarker{i}";
            }
            if (bird.down)
            {
                Material mat = birdSelectedMaterial; // will break if this material is null
                birdMarker.GetComponent<Renderer>().material = mat;
            }
            if (bird.up)
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
            Ray birdRay = bird.ray;
            rayWasHit = Physics.Raycast(birdRay, out birdRayHit, bird.range);
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

        // Commonly accessed bird properties
        public Vector3 CursorPosition()
        {
            return bird.birdPosition;
        }

        public float Range()
        {
            return bird.range;
        }
        // end commonly accessed bird properties

        public bool BirdHit(Collider coll, out RaycastHit hit)
        {
            Vector3 vel = bird.Velocity();
            Ray ray = new Ray(bird.prevBirdPosition, vel.normalized);
            return coll.Raycast(ray, out hit, vel.magnitude);
        }

        public bool WentThrough(Collider coll, Vector3? direction = null)
        {
            float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
            if (direction != null && Dot(bird.Velocity().normalized, direction.Value.normalized) < .7071f)
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

        public void SetAssociatedUser(string user) {
            associatedUser = user;
        }

        void OnDestroy()
        {
            BirdManager.UnregisterBird(this);
        }
    }
}