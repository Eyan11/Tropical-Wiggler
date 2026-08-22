using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(InputManager))]
public class StretchController : MonoBehaviour
{
    [Header("Player Movement Settings")]
    [SerializeField] private float maxSpeed = 7f;
    [SerializeField] private float accelerationForce = 20f;
    [SerializeField] private float deaccelerationForce = -15f;
    [Header("Orientation Settings")]
    [SerializeField] private Transform orientationTran;
    [SerializeField] private float rotationSpeed = 100f;
    private StretchState currentStretchState = StretchState.Disabled;
    private Transform camTran;
    private Rigidbody body;
    private InputManager input;
    private Vector3 moveDirection = Vector3.zero;

    private enum StretchState
    {
        Disabled,
        Stretching,
        ContractingForward,
        ContractingBackward,
    }

    private void Awake()
    {
        camTran = Camera.main.transform;
        body = GetComponent<Rigidbody>();
        input = GetComponent<InputManager>();
        input.OnStretchInputChanged += OnStretchInputChanged;
    }

    private void OnStretchInputChanged(bool isStretching)
    {
        if (isStretching)
        {
            currentStretchState = StretchState.Stretching;
        }
        else
        {
            currentStretchState = StretchState.Disabled; // Temporary
            // TODO: Set to contracting forward/backward
        }
    }



    // *** Movement and Rotation ******************************************************************
    
    private void Update()
    {
        if (currentStretchState == StretchState.Disabled) return;
        RotateTowardsInputDirection();
    }

    private void FixedUpdate()
    {
        if (currentStretchState == StretchState.Disabled) return;
        MovePlayer();
    }

    private void RotateTowardsInputDirection()
    {
        if (input.GetCurMoveInput() == Vector2.zero) return;

        Vector3 normalizedCamForward = camTran.forward;
        normalizedCamForward.y = 0f;
        normalizedCamForward.Normalize();
        // Cam.right y value always equals 0 and is already normalized

        Vector2 lastMoveInput = input.GetLastNonZeroMoveInput();
        moveDirection = lastMoveInput.x * camTran.right + lastMoveInput.y * normalizedCamForward;

        Quaternion targetRotation = Quaternion.LookRotation(moveDirection);

        // Rotate at constant speed towards last non-zero movement direction
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

        float rotationDifference = Quaternion.Angle(transform.rotation, targetRotation);

        if (rotationDifference < 0.1f) // Stop rotating when very close to target rotation
        {
            orientationTran.rotation = targetRotation;
        }
    }

    private void MovePlayer()
    {
        // Deaaccelerate when no input unbtil reaching speed of 0
        if (input.GetCurMoveInput() == Vector2.zero)
        {
            Vector2 deacceleration = deaccelerationForce * Time.fixedDeltaTime * new Vector2(body.linearVelocity.x, body.linearVelocity.z);
            body.linearVelocity += new Vector3(deacceleration.x, 0f, deacceleration.y);

            if (body.linearVelocity.magnitude < 0.1f)
                body.linearVelocity = new Vector3(0f, body.linearVelocity.y, 0f);
        }
        // Accelerate towards move direction which is camera-relative input
        else
        {
            Vector3 acceleration = accelerationForce * Time.fixedDeltaTime * moveDirection;
            body.linearVelocity += acceleration;
        }

        body.linearVelocity = Vector3.ClampMagnitude(body.linearVelocity, maxSpeed);
    }
}
