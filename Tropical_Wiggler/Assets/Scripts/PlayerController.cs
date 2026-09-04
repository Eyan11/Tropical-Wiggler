using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(InputManager))]
public class PlayerController : MonoBehaviour
{
    [Header("Player Movement Settings")]
    [SerializeField] private float maxSpeed = 4f;
    [SerializeField] private float accelerationForce = 15f;
    [SerializeField] private float deaccelerationForce = -10f;
    [Header("Orientation Settings")]
    [SerializeField] private Transform orientationTran;
    [SerializeField] private float rotationSpeed = 900;
    [Tooltip("The angle threshold (in degrees) within which the player must be rotated towards target direction before they can move when they are not already moving.")]
    [SerializeField] private float canMoveAngleThreshold = 5f;
    private Transform camTran;
    private Rigidbody body;
    private InputManager input;
    private bool isMoving = false; // True if facing forward and able to move (lerp towards target rotation is basically done)
    private Vector3 moveDirection = Vector3.zero;
    private bool isEnabled = true; // False if in stretch mode

    private void Awake()
    {
        camTran = Camera.main.transform;
        body = GetComponent<Rigidbody>();

        input = GetComponent<InputManager>();
        input.OnMoveInputCanceled += OnMoveInputCanceled;
        input.OnStretchInputChanged += OnStretchInputChanged;
    }

    private void OnMoveInputCanceled()
    {
        isMoving = false; // Make player rotate towards input direction before allowing movement
    }

    private void OnStretchInputChanged(bool isStretching)
    {
        isEnabled = !isStretching;
        // Reset move direction to current orientation
        if (isEnabled) moveDirection = orientationTran.forward;
    }


    // *** Movement and Rotation ******************************************************************
    
    private void Update()
    {
        if (!isEnabled) return; // Let StretchController handle stretch rotation
        RotateTowardsInputDirection();
    }

    private void FixedUpdate()
    {
        if (!isEnabled) return; // Let StretchController handle stretch movement
        MovePlayer();
    }

    private void RotateTowardsInputDirection()
    {
        Vector2 lastNonZeroMoveInput = input.GetLastNonZeroMoveInput();
        if (lastNonZeroMoveInput == Vector2.zero) return;

        // If player is not moving, use last camera position for move direction
        if (input.GetCurMoveInput() != Vector2.zero)
        {
            Vector3 normalizedCamForward = camTran.forward;
            normalizedCamForward.y = 0f;
            normalizedCamForward.Normalize();
            // Cam.right y value always equals 0 and is already normalized

            moveDirection = lastNonZeroMoveInput.x * camTran.right + lastNonZeroMoveInput.y * normalizedCamForward;
        }

        Quaternion targetRotation = Quaternion.LookRotation(moveDirection);

        // Rotate at constant speed towards last non-zero movement direction
        orientationTran.rotation = Quaternion.RotateTowards(orientationTran.rotation, targetRotation, rotationSpeed * Time.deltaTime);

        float rotationDifference = Quaternion.Angle(orientationTran.rotation, targetRotation);

        if (rotationDifference < 0.1f) // Stop rotating when very close to target rotation
        {
            orientationTran.rotation = targetRotation;
        }

        // Allow movement again when movement input and player orientation are aligned
        if (!isMoving && input.GetCurMoveInput() != Vector2.zero && rotationDifference < canMoveAngleThreshold)
        {
            isMoving = true;
        }
    }

    private void MovePlayer()
    {
        // Deaccelerate when no input until reaching speed of 0
        if (input.GetCurMoveInput() == Vector2.zero)
        {
            Vector2 deacceleration = deaccelerationForce * Time.fixedDeltaTime * new Vector2(body.linearVelocity.x, body.linearVelocity.z);
            body.linearVelocity += new Vector3(deacceleration.x, 0f, deacceleration.y);

            if (body.linearVelocity.magnitude < 0.1f)
                body.linearVelocity = new Vector3(0f, body.linearVelocity.y, 0f);
        }
        // Accelerate towards move direction which is camera-relative input
        else if (isMoving)
        {
            Vector3 acceleration = accelerationForce * Time.fixedDeltaTime * moveDirection;
            body.linearVelocity += acceleration;
        }

        body.linearVelocity = Vector3.ClampMagnitude(body.linearVelocity, maxSpeed);
    }

}