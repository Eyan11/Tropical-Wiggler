using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(InputManager))]
[RequireComponent(typeof(StretchBody))]
public class StretchController : MonoBehaviour
{
    [Header("Stretch Movement Settings")]
    [SerializeField] private float maxSpeed = 7f;
    [SerializeField] private float accelerationForce = 20f;
    [SerializeField] private float deaccelerationForce = -15f;
    [Header("Stretch Orientation Settings")]
    [SerializeField] private Transform orientationTran;
    [SerializeField] private float rotationSpeed = 100f;
    [Header("Visuals")]
    [SerializeField] private Transform backBodyTran;
    private StretchState currentStretchState = StretchState.Disabled;
    private Transform camTran;
    private Rigidbody body;
    private InputManager input;
    private StretchBody stretchBody;
    private Vector3 moveDirection = Vector3.zero;
    private Transform backBodyParentTran;

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
        backBodyParentTran = backBodyTran.parent;
        body = GetComponent<Rigidbody>();
        stretchBody = GetComponent<StretchBody>();
        input = GetComponent<InputManager>();
        input.OnStretchInputChanged += OnStretchInputChanged;
    }

    private void OnStretchInputChanged(bool isStretching)
    {
        if (isStretching)
        {
            backBodyTran.SetParent(null);
            currentStretchState = StretchState.Stretching;
        }
        else
        {
            backBodyTran.SetParent(backBodyParentTran);
            backBodyTran.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
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
        // Prevent player from going past max stretch distance
        else if (stretchBody.IsMaxStretchReached())
        {
            Vector3 direction = moveDirection;
            Vector3 front = stretchBody.GetFrontBodyForward();
            Vector3 right = stretchBody.GetFrontBodyRight();

            // Remove move input in stretch direction
            if (Vector3.Dot(moveDirection, front) > 0f)
            {
                float amount = Vector3.Dot(moveDirection, right);
                direction = amount * right;
            }

            // Remove velocity in stretch direction
            if (Vector3.Dot(body.linearVelocity, front) > 0f)
            {
                float rightAmount = Vector3.Dot(body.linearVelocity, right);
                Vector3 newVel = rightAmount * right;
                body.linearVelocity = new Vector3(newVel.x, body.linearVelocity.y, newVel.z);
            }

            Vector3 acceleration = accelerationForce * Time.fixedDeltaTime * direction;
            body.linearVelocity += acceleration;
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
