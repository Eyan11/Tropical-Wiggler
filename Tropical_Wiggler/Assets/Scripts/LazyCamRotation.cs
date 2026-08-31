using UnityEngine;
using Unity.Cinemachine;

public class LazyCamRotation : MonoBehaviour
{
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private float maxRotationSpeed = 0.6f;
    [SerializeField] private float minRotationSpeed = 0.03f;
    [SerializeField] private float minAngleThreshold = 1f;
    [SerializeField] private float maxAngleThreshold = 177f;
    [Tooltip("Curve that maps minRotationSpeed to maxRotationSpeed based on the unsigned angle between camera forward and player movement direction.")]
    [SerializeField] private AnimationCurve rotationSpeedCurve;
    [Tooltip("Curve that applies a multiplier to final rotation speed based on vertical rotation of camera, where 0 is at the bottom ring and 1 is at the top ring in the orbital follow.")]
    [SerializeField] private AnimationCurve verticalRotationMultiplierCurve;
    [Tooltip("The minimum velocity the player must be moving to cause the camera to rotate.")]
    [SerializeField] private float minVelocityThreshold = 3.25f;
    private CinemachineCamera cinemachineCamera;
    private CinemachineOrbitalFollow orbitalFollow;

    private void Awake()
    {
        cinemachineCamera = GetComponent<CinemachineCamera>();
        orbitalFollow = cinemachineCamera.GetComponent<CinemachineOrbitalFollow>();
    }

    private void FixedUpdate() // Using fixed update since that is when movement is calculated
    {
        Vector3 velocity = playerRigidbody.linearVelocity;
        velocity.y = 0f;
        float speed = velocity.magnitude;

        if (speed < minVelocityThreshold) return;
        velocity /= speed; // Normalize velocity

        Vector3 cameraForward = cinemachineCamera.transform.forward;
        cameraForward.y = 0f;
        cameraForward.Normalize();

        // Get angle between camera forward and player movement direction
        float signedAngle = Vector3.SignedAngle(cameraForward, velocity, Vector3.up);
        float unsignedAngle = Mathf.Abs(signedAngle);

        if (unsignedAngle < minAngleThreshold) return;
        if (unsignedAngle > maxAngleThreshold) return;
        
        // Map angle to rotation speed using the rotationSpeedCurve
        float rotationSpeed = Mathf.Lerp(minRotationSpeed, maxRotationSpeed, rotationSpeedCurve.Evaluate(unsignedAngle / maxAngleThreshold));

        // Apply multiplier based on vertical rotation of camera (used to slowly stop camera panning as vertical rotation approaches a top down view)
        float verticalRotationStep = Mathf.InverseLerp(orbitalFollow.VerticalAxis.Range.x, orbitalFollow.VerticalAxis.Range.y, orbitalFollow.VerticalAxis.Value);
        rotationSpeed *= verticalRotationMultiplierCurve.Evaluate(verticalRotationStep);

        // Pan camera towards player movement direction
        orbitalFollow.HorizontalAxis.Value += signedAngle * rotationSpeed * Time.fixedDeltaTime;
    }
}
