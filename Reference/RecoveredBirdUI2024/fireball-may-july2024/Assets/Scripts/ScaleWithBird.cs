using UnityEngine;
using Bird3DCursor;
using DG.Tweening;

public class ScaleWithBird : MonoBehaviour
{
    public Collider coll;
    public GameObject hitMarker;
    public bool activelyScaling;
    public float maxScaleFactor = 425f; // suitable for demo scene for which this script was designed; settable to anything
    private Vector3 startingScale = Vector3.one;
    private float startingRange;
    // public GameObject pointer;
    private BirdProvider activeBird;

    void Start()
    {
        startingScale = transform.localScale;
        startingRange = 0f;
    }

    void Update()
    {
        if (!activelyScaling) return;
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
                float rangeRatio = activeBird.GetRange() / startingRange;
                // calculate a multiplier rage ratio squared, clamped no larger than maxScaleFactor
                float multiplier = Mathf.Clamp(rangeRatio * rangeRatio, 0f, maxScaleFactor);
                Vector3 newScale = multiplier * startingScale;
                transform.DOScale(newScale, 0.3f);
            }
            else
            {
                activeBird = null;
            }
        }
    }

    public void StartScaling()
    {
        activelyScaling = true;
        //startingScale = transform.localScale;
        //startingRange = activeBird.range;
    }

    public void StopScaling()
    {
        activelyScaling = false;
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
                startingRange = activeBird.GetRange();
                return;
            }
        }
    }

    // method that takes a bird and returns a vector3 if the raycast hits the collider AND the bird range is larger than the distance to the hit; otherwise, vector3.zero.
    Vector3 GetBirdRayHit(BirdProvider birdCursor)
    {
        RaycastHit hit;
        Ray ray = birdCursor.GetRay();

        // collider raycast
        if (coll.Raycast(ray, out hit, 2000f))
        {
            if (hitMarker != null)
                hitMarker.transform.position = hit.point;
            if (birdCursor.GetRange() > (hit.point - ray.origin).magnitude)
            {
                return hit.point;
            }
        }
        return Vector3.zero;
    }
}
