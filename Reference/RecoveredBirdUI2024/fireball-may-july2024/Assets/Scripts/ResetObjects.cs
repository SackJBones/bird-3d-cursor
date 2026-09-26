using UnityEngine;

public class ResetObjects : MonoBehaviour
{
    private struct InitialPositionData
    {
        public Transform transform;
        public Vector3 initialPosition;
        public Quaternion initialRotation;
        public Rigidbody rigidbody;
    }

    private InitialPositionData[] initialPositions;
    private bool isResetting = false;
    private float resetStrength = 0f;
    private float resetDuration = 2f;
    private float resetTimer = 0f;
    private float swirlForce = 0.5f;
    private Vector3 baseCenter;

    private void Start()
    {
        // Record the initial positions and rotations of child objects
        RecordInitialPositions();
    }

    private void FixedUpdate()
    {
        if (isResetting)
        {
            // Increment the reset timer
            resetTimer += Time.fixedDeltaTime;

            // Calculate the current strength based on the reset timer and duration
            float currentStrength = Mathf.Clamp01(resetTimer / resetDuration);

            // Reset each child object to its initial position and rotation
            for (int i = 0; i < initialPositions.Length; i++)
            {
                InitialPositionData data = initialPositions[i];
                Transform childTransform = data.transform;
                Rigidbody childRigidbody = data.rigidbody;

                // Calculate the target position based on the current strength
                float rampdown = (1f - currentStrength);
                Vector3 targetPosition = data.initialPosition + rampdown * rampdown * (data.initialPosition - baseCenter) * 3f;
                Vector3 currentPosition = childTransform.position;
                Vector3 newPosition = Vector3.Lerp(currentPosition, targetPosition, currentStrength);

                // Apply physics-based movement to the rigidbody
                Vector3 velocity = (newPosition - currentPosition) / Time.fixedDeltaTime;
                float velocityBlend = currentStrength * currentStrength;
                childRigidbody.velocity = childRigidbody.velocity * (1f - velocityBlend) + velocity * velocityBlend;

                
                // Calculate the target rotation based on the current strength
                Quaternion targetRotation = data.initialRotation;
                Quaternion newRotation = Quaternion.Slerp(childTransform.rotation, targetRotation, currentStrength);

                // Apply physics-based rotation to the rigidbody
                Quaternion rotationDelta = newRotation * Quaternion.Inverse(childTransform.rotation);
                rotationDelta.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f)
                {
                    angle -= 360f;
                }
                Vector3 angularVelocity = (Mathf.Deg2Rad * angle / Time.fixedDeltaTime) * axis;
                float angularVelocityBlend = currentStrength;
                childRigidbody.angularVelocity = childRigidbody.angularVelocity * (1f - angularVelocityBlend) + angularVelocity * angularVelocityBlend;
            

                // Apply upward force at beginning
                childRigidbody.AddForce(Vector3.up * 11 * Mathf.Sqrt(Mathf.Sqrt(rampdown)));

                // Apply outward force at beginning
                childRigidbody.AddForce((currentPosition - baseCenter) * 5 * rampdown);

                // Apply gentle swirling force
                Vector3 swirlForceVector = Quaternion.Euler(0f, 90f, 0f) * (currentPosition - targetPosition).normalized * swirlForce * 40 * rampdown;
                childRigidbody.AddForce(swirlForceVector);
            }

            // Check if the reset is complete
            if (resetTimer >= resetDuration)
            {
                isResetting = false;
            }
        }
    }

    public void ResetToInitialPositions()
    {
        ResetToInitialPositions(.5f, 12f); // Set default reset strength and duration
    }

    public void ResetToInitialPositions(float strength, float duration)
    {
        // Store the reset strength and duration
        resetStrength = Mathf.Clamp01(strength);
        resetDuration = Mathf.Max(0f, duration);

        // Start the reset process
        isResetting = true;
        resetTimer = 0f;
    }

    private void RecordInitialPositions()
    {
        int childCount = transform.childCount;
        initialPositions = new InitialPositionData[childCount];
        baseCenter = Vector3.up*1000;

        for (int i = 0; i < childCount; i++)
        {
            Transform childTransform = transform.GetChild(i);
            Rigidbody childRigidbody = childTransform.GetComponent<Rigidbody>();

            initialPositions[i].transform = childTransform;
            initialPositions[i].initialPosition = childTransform.position;
            initialPositions[i].initialRotation = childTransform.rotation;
            initialPositions[i].rigidbody = childRigidbody;

            baseCenter.x += childTransform.position.x/childCount;
            baseCenter.y = Mathf.Min(childTransform.position.y, baseCenter.y);
            baseCenter.z += childTransform.position.z/childCount;
        }
    }
}