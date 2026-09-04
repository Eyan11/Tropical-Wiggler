using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(InputManager))]
[RequireComponent(typeof(StretchBody))]
public class StretchController : MonoBehaviour
{
    [Header("Stretch Movement Settings")]
    [SerializeField] private float maxSpeed = 9f;
    [Tooltip("Defines the multiplier to max speed when at max stretch distance. Y = 1 means speed is maxSpeed, Y = 0 means speed is 0. X = 0 means move direction is perpendicular to stretch direction, X = 1 means move direction is in the same direction as stretch direction.")]
    [SerializeField] private AnimationCurve maxStretchSpeedReductionCurve;
    [SerializeField] private float accelerationForce = 25f;
    [SerializeField] private float deaccelerationForce = -15f;
    [SerializeField] private float maxStretchOpposingForce = 15f;
    private Vector3 moveDirection = Vector3.zero;
    private StretchState currentStretchState = StretchState.Disabled;

    [Header("Stretch Orientation Settings")]
    [SerializeField] private Transform orientationTran;
    [SerializeField] private float maxRotationSpeed = 200f;
    [SerializeField] private float rotationAcceleration = 30f;
    [SerializeField] private float maxRotation = 90f;
    private float rotationSpeed;
    private Quaternion targetRotation = Quaternion.identity;
    private Transform camTran;
    private Rigidbody body;
    private InputManager input;
    private StretchBody stretchBody;

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
        stretchBody = GetComponent<StretchBody>();
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
        rotationSpeed += rotationAcceleration * Time.deltaTime;
        rotationSpeed = Mathf.Min(rotationSpeed, maxRotationSpeed);

        if (input.GetCurMoveInput() != Vector2.zero)
        {
            Vector3 normalizedCamForward = camTran.forward;
            normalizedCamForward.y = 0f;
            normalizedCamForward.Normalize();
            // Cam.right y value always equals 0 and is already normalized

            Vector2 lastMoveInput = input.GetLastNonZeroMoveInput();
            moveDirection = lastMoveInput.x * camTran.right + lastMoveInput.y * normalizedCamForward;

            targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
        }
        
        
        // Stop rotating when very close to target rotation
        float rotationDifference = Quaternion.Angle(orientationTran.rotation, targetRotation);
        if (rotationDifference < 0.1f)
        {
            orientationTran.rotation = targetRotation;
            return;
        }

        int dir = 1;
        float headToFrontSignedAngle = Vector3.SignedAngle(orientationTran.forward, stretchBody.GetFrontBodyForward(), Vector3.up);
        float frontToTargetAngle = Quaternion.Angle(stretchBody.GetFrontBodyRotation(), targetRotation);
        float frontRightAndTargetDot = Vector3.Dot(stretchBody.GetFrontBodyRight(), moveDirection);
        float frontRightAndHeadDot = Vector3.Dot(stretchBody.GetFrontBodyRight(), orientationTran.forward);

        // Stop rotating if both head and target rotation are past max rotation AND head is on the same side as target rotation with respect to front body
        if (Mathf.Abs(headToFrontSignedAngle) > maxRotation && frontToTargetAngle > maxRotation && Mathf.Sign(frontRightAndTargetDot) == Mathf.Sign(frontRightAndHeadDot)) return;

        float headToTargetSignedAngle = Vector3.SignedAngle(orientationTran.forward, moveDirection, Vector3.up);

        // If head is on opposite side as target rotation with respect to front body AND default rotation direction is towards front body, then rotate in opposite direction
        if (Mathf.Sign(frontRightAndTargetDot) != Mathf.Sign(frontRightAndHeadDot) && Mathf.Sign(-headToFrontSignedAngle) == Mathf.Sign(headToTargetSignedAngle)) dir = -1;

        orientationTran.rotation = Quaternion.RotateTowards(orientationTran.rotation, targetRotation, dir * rotationSpeed * Time.deltaTime);
    }

    private void MovePlayer()
    {
        // Deaaccelerate when no input until reaching speed of 0
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
            // Add force towards front body sphere to prevent bug where player pushes against environment to get further away
            acceleration += maxStretchOpposingForce * Time.fixedDeltaTime * -front;
            body.linearVelocity += acceleration;

            // Lerp speed from 0 to max speed based on closeness of move direction to stretch direction
            float dot = Vector3.Dot(moveDirection.normalized, front);
            float t = maxStretchSpeedReductionCurve.Evaluate(dot);
            float lerpedSpeed = Mathf.Lerp(0f, maxSpeed, t);
            body.linearVelocity = Vector3.ClampMagnitude(body.linearVelocity, lerpedSpeed);
        }
        // Accelerate towards move direction which is camera-relative input
        else
        {
            Vector3 acceleration = accelerationForce * Time.fixedDeltaTime * moveDirection;
            body.linearVelocity += acceleration;
            body.linearVelocity = Vector3.ClampMagnitude(body.linearVelocity, maxSpeed);
        }
    }
}
