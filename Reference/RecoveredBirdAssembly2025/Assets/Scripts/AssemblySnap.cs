using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BirdInteractable))]
public class AssemblySnap : MonoBehaviour
{
    [Header("Assembly Settings")]
    public Vector3 assemblyPosition;
    public Quaternion assemblyRotation;
    public float snapDistance = 0.2f;
    public float snapRotationThreshold = 30f; // degrees
    
    [Header("Snapping Behavior")]
    public float snapSpeed = 10f;
    public bool maintainOffset = false;
    
    [Header("Events")]
    public UnityEvent OnAssemblySnap;
    public UnityEvent OnAssemblyUnsnap;
    
    private BirdInteractable birdInteractable;
    private bool isSnapped = false;
    private Vector3 offsetFromAssemblyPosition;
    
    void Start()
    {
        birdInteractable = GetComponent<BirdInteractable>();
        
        // Store initial position and rotation if not set
        if (assemblyPosition == Vector3.zero)
            assemblyPosition = transform.position;
        if (assemblyRotation.eulerAngles == Vector3.zero)
            assemblyRotation = transform.rotation;
            
        // Calculate initial offset if maintaining offset
        if (maintainOffset)
            offsetFromAssemblyPosition = transform.position - assemblyPosition;
    }
    
    void LateUpdate()
    {
        // Only check for snapping when the object is being manipulated by Bird
        if (!birdInteractable.enabled || !birdInteractable.IsFollowing())
            return;
            
        Vector3 targetPosition = maintainOffset ? assemblyPosition + offsetFromAssemblyPosition : assemblyPosition;
        float distanceToAssembly = Vector3.Distance(transform.position, targetPosition);
        
        if (!isSnapped && distanceToAssembly < snapDistance)
        {
            // Check rotation alignment
            float angleDifference = Quaternion.Angle(transform.rotation, assemblyRotation);
            
            if (angleDifference < snapRotationThreshold)
            {
                StartSnapping();
            }
        }
        else if (isSnapped && distanceToAssembly > snapDistance * 1.5f) // Add hysteresis to prevent rapid toggling
        {
            StopSnapping();
        }
        
        if (isSnapped)
        {
            // Smoothly move to assembly position
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * snapSpeed);
            transform.rotation = Quaternion.Lerp(transform.rotation, assemblyRotation, Time.deltaTime * snapSpeed);
            
            // Disable Bird control temporarily while snapping
            birdInteractable.enabled = false;
        }
    }
    
    private void StartSnapping()
    {
        isSnapped = true;
        OnAssemblySnap?.Invoke();
        
        // Optional: Add visual/audio feedback here
    }
    
    private void StopSnapping()
    {
        isSnapped = false;
        birdInteractable.enabled = true;
        OnAssemblyUnsnap?.Invoke();
        
        // Optional: Add visual/audio feedback here
    }
    
    // Public method to manually set assembly position/rotation
    public void SetAssemblyPoint(Vector3 position, Quaternion rotation)
    {
        assemblyPosition = position;
        assemblyRotation = rotation;
        if (maintainOffset)
            offsetFromAssemblyPosition = transform.position - position;
    }
    
    // Editor-only: Visualize snap zone
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green * 0.5f;
        Gizmos.DrawWireSphere(assemblyPosition, snapDistance);
    }
}