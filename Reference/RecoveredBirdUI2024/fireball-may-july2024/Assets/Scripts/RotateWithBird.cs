using UnityEngine;
using Bird3DCursor;

public class RotateWithBird : MonoBehaviour
{
    public Collider coll;
    public float momentum = 0.5f; // Degree of momentum after the ray is no longer intersecting the object. Set this between 0 and 1.
    public bool castFromBack = false; // True to cast the ray from the back of the object, false to cast from the front.
    public GameObject hitMarker;
    // public GameObject pointer;

    private Vector3 lastRayPoint;
    private Vector3 angularVelocity;
    private BirdProvider activeBird;

    void Update()
    {
        if (coll == null)
        {
            coll = GetComponent<Collider>();
        }
        if (coll == null)
        {
            return;
        }
        AcquireBird();
        if (activeBird != null)
        {

            Vector3 hitPoint = GetBirdRayHit(activeBird);
            if (hitPoint != Vector3.zero)
            {
                if (lastRayPoint == Vector3.zero)
                {
                    lastRayPoint = hitPoint;
                }

                Vector3 currentAngularVelocity = Vector3.Cross((lastRayPoint - transform.position).normalized, (hitPoint - transform.position).normalized)/Time.deltaTime;
                angularVelocity = Vector3.Lerp(angularVelocity, currentAngularVelocity, Time.deltaTime * 10f);
                // if (pointer != null)
                // {
                //     pointer.transform.rotation = Quaternion.LookRotation(angularVelocity.normalized, Vector3.up);
                //     pointer.transform.position = transform.position;
                // }

                lastRayPoint = hitPoint;

            }
            else
            {
                lastRayPoint = Vector3.zero;
                activeBird = null;
            }
        }

        // Apply rotation and gradual slowdown
        transform.Rotate(angularVelocity.normalized, Mathf.Asin(angularVelocity.magnitude * Time.deltaTime) * 180f / Mathf.PI, Space.World);
        angularVelocity = Vector3.Lerp(angularVelocity, Vector3.zero, Time.deltaTime * (1f - momentum));
    }

    void AcquireBird()
    {
        if (activeBird != null) return;

        // loop through BirdManager.GetAllBirds until we find one that has a bird ray hit, set it as activeBird, and return
        foreach (BirdProvider birdCursor in BirdManager.GetAllBirds())
        {
            Vector3 birdRayHit = GetBirdRayHit(birdCursor);
            if (birdRayHit != Vector3.zero)
            {
                activeBird = birdCursor;
                return;
            }
        }
    }

    // method that takes a bird and returns a vector3 if the raycast hits the collider AND the bird range is larger than the distance to the hit; otherwise, vector3.zero.
    Vector3 GetBirdRayHit(BirdProvider birdCursor)
    {
        RaycastHit hit;
        Ray ray = birdCursor.GetRay();
        Vector3 birdOrigin = ray.origin;
        if (castFromBack)
        {
            // reverse the ray direction and move its origin far away along the ray
            ray = new Ray(ray.origin + (ray.direction * 1000f), -ray.direction);
        }
        // collider raycast
        if (coll.Raycast(ray, out hit, 2000f))
        {
            if (hitMarker != null)
                hitMarker.transform.position = hit.point;
            if (birdCursor.GetRange() > (hit.point - birdOrigin).magnitude)
            {
                return hit.point;
            }
        }
        return Vector3.zero;
    }
}
