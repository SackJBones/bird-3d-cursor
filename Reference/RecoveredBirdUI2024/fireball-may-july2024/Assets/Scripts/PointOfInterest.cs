using UnityEngine;
using DG.Tweening;


public class PointOfInterest : MonoBehaviour
{
    [SerializeField]
    private GameObject markerPrefab; // Assign in Inspector

    [SerializeField]
    private Transform mapSphere; // Assign in Inspector

    [SerializeField]
    private float sphereRadius; // Assign in Inspector

    private GameObject markerInstance;
    private bool wasEnabled = false;

    void Start()
    {
        // Instantiate the marker and initially hide it
        markerInstance = Instantiate(markerPrefab);
        markerInstance.SetActive(false);
        // Use the parent object as the sphere origin if mapSphere is not assigned
        if (mapSphere == null)
        {
            mapSphere = transform.parent;
        }
    }

    void Update()
    {
        // Move the marker to this POI's position in world space
        markerInstance.transform.position = transform.position;

        // Enable or disable the marker based on whether it's inside the sphere
        float distanceToSphereCenter = Vector3.Distance(transform.position, mapSphere.position);

        // hide them if they're outside the sphere, or if they're so close to the center the map must be at zero scale
        if (distanceToSphereCenter <= sphereRadius && distanceToSphereCenter > 0.001f)
        {
            EnableMarker();
        }
        else
        {
            DisableMarker();
        }
    }

    public void EnableMarker()
    {
        // TODO: Add tweening here
        // Just kidding, tweening can't keep up with the fast motion of the map zooming out
        // if (markerInstance.activeSelf)
        // {
        //     return; // nothing to do or previous tween has not finished yet
        // }
        markerInstance.SetActive(true);
        markerInstance.transform.localScale = Vector3.one;
        // if (!wasEnabled)
        // {
        //     // use DOTween to scale the marker to 100% while spinning sedately, coming to rest at the POI's position and stopping spinning after about half a second
        //     markerInstance.transform.DOScale(1f, 0.2f).SetEase(Ease.OutBack);
        //     markerInstance.transform.DORotate(new Vector3(0f, 360f, 0), .3f, RotateMode.FastBeyond360).SetEase(Ease.Linear).SetLoops(2, LoopType.Incremental);
        //     markerInstance.transform.DOMove(transform.position, 0.2f).SetEase(Ease.OutBack);
        // }
        wasEnabled = true;
    }

    public void DisableMarker()
    {
        // TODO: Add tweening here
        //markerInstance.SetActive(false);
        if (wasEnabled)
        {
            // use DOTween to scale the marker to 0% while executing a half turn
            markerInstance.transform.DOScale(0f, 0.15f).SetEase(Ease.InBack);
            markerInstance.transform.DORotate(new Vector3(0f, 180f, 0f), 0.15f).SetEase(Ease.Linear);
            markerInstance.transform.DOMove(transform.position + Vector3.up * .1f, 0.15f).SetEase(Ease.InBack).OnComplete(() => markerInstance.SetActive(false));
        }
        wasEnabled = false;
    }
}
