using UnityEngine;

public class FloatingObject : MonoBehaviour
{
    public Transform target;
    public float distance = 2f;
    public float floatSpeed = 2f;
    public float rotationSpeed = 5f;

    private Vector3 targetPosition;

    private void Start()
    {
        // Set the initial target position
        UpdateTargetPosition();
    }

    private void Update()
    {
        // Update the target position if it has moved
        UpdateTargetPosition();

        // Move the floating object towards the target position
        float step = floatSpeed * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, step);

        // Rotate the floating object to align its front direction with the target's front direction
        Vector3 directionToTarget = (transform.position - target.position).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void UpdateTargetPosition()
    {
        // Calculate the target position based on the target object and set distance
        // Prevent the object from floating too high or going through the floor by using a forward direction not too far above or below horizontal
        Vector3 forwardClamped = target.forward;
        forwardClamped.y = Mathf.Clamp(forwardClamped.y, .02f, .15f);
        targetPosition = target.position + (forwardClamped.normalized * distance);

    } 
}
