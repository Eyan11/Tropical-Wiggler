using UnityEngine;
using System;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(InputManager))]
[RequireComponent(typeof(StretchBody))]
public class StretchController : MonoBehaviour
{
    public static event Action OnStretchStartedEvent;
    public static event Action OnContractionFinishedEvent;

    [Header("Stretch Movement Settings")]
    [SerializeField] private float maxSpeed = 9f;
    [Tooltip("Defines the multiplier to max speed when at max stretch distance. Y = 1 means speed is maxSpeed, Y = 0 means speed is 0. X = 0 means move direction is perpendicular to stretch direction, X = 1 means move direction is in the same direction as stretch direction.")]
    [SerializeField] private AnimationCurve maxStretchSpeedReductionCurve;
    [SerializeField] private float accelerationForce = 50f;
    [SerializeField] private float deaccelerationForce = -15f;
    [SerializeField] private float maxStretchOpposingForce = 30f;
    private Vector3 moveDirection = Vector3.zero;
    private StretchState currentStretchState = StretchState.Disabled;
    private Rigidbody body;
    private Transform camTran;
    private InputManager input;
    private StretchBody stretchBody;

    [Header("Stretch Orientation Settings")]
    [SerializeField] private Transform orientationTran;
    [SerializeField] private float maxRotationSpeed = 200f;
    [SerializeField] private float rotationAcceleration = 200f;
    [SerializeField] private float maxRotation = 50f;
    private float rotationSpeed;
    private Quaternion targetRotation = Quaternion.identity;

    [Header("Contraction Settings")]
    [SerializeField] private Transform groundCheckTran;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private ParticleSystem splashParticles;
    [SerializeField] private float groundCheckFrequency = 0.1f;
    [SerializeField] private float groundCheckDistance = 0.15f;
    private float groundCheckTimer = 0f;
    private bool isGrounded = true;

    [Header ("Animation Settings")]
    [SerializeField] private Animator headAnim;
    private int speedHash = Animator.StringToHash("speed");
    private int bounceForwardHash = Animator.StringToHash("bounce_forward");
    private int bounceBackwardHash = Animator.StringToHash("bounce_backward");

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
    }

    private void OnEnable()
    {
        input.OnStretchInputChanged += OnStretchInputChanged;
        stretchBody.OnContractionFinishedEvent += OnContractionFinished;
    }

    private void OnDisable()
    {
        input.OnStretchInputChanged -= OnStretchInputChanged;
        stretchBody.OnContractionFinishedEvent -= OnContractionFinished;
    }

    private void OnStretchInputChanged(bool isStretchInputDown)
    {
        // Player is NOT stretching and presses stretch input
        if (isStretchInputDown && currentStretchState == StretchState.Disabled)
        {
            currentStretchState = StretchState.Stretching;
            OnStretchStartedEvent?.Invoke();
        }
        // Player is stretching and releases stretch input while grounded
        else if (!isStretchInputDown && isGrounded && currentStretchState == StretchState.Stretching)
        {
            body.linearVelocity = Vector3.zero;
            currentStretchState = StretchState.ContractingForward;
            headAnim.SetFloat(speedHash, 0f); // Head is not moving
            stretchBody.StartCoroutine(stretchBody.ContractBodyForward());
        }
        // Player is stretching and releases stretch input while NOT grounded
        else if (!isStretchInputDown && !isGrounded && currentStretchState == StretchState.Stretching)
        {
            body.linearVelocity = Vector3.zero;
            currentStretchState = StretchState.ContractingBackward;
            headAnim.SetFloat(speedHash, 1f); // Head is moving towards tail, so make head legs animate
            stretchBody.StartCoroutine(stretchBody.ContractBodyBackward());
        }
    }

    private void OnContractionFinished()
    {
        if (currentStretchState == StretchState.ContractingForward)
        {
            headAnim.SetTrigger(bounceForwardHash);
        }
        else if (currentStretchState == StretchState.ContractingBackward)
        {
            headAnim.SetTrigger(bounceBackwardHash);
        }

        currentStretchState = StretchState.Disabled;
        headAnim.SetFloat(speedHash, 0f);
        splashParticles.Play();
        OnContractionFinishedEvent?.Invoke();
    }


    // *** Movement and Rotation ******************************************************************
    
    private void Update()
    {
        if (currentStretchState != StretchState.Stretching) return;
        RotateTowardsInputDirection();

        // Handle ground check
        groundCheckTimer += Time.deltaTime;
        if (groundCheckTimer >= groundCheckFrequency)
        {
            isGrounded = IsGrounded();
            groundCheckTimer = 0f;
        }
    }

    private void FixedUpdate()
    {
        if (currentStretchState != StretchState.Stretching) return;
        MovePlayer();
    }

    // Rotates the player towards camera relative input direction.
    //  Rotation is limited to maxRotation degrees away from the front body's rotation
    private void RotateTowardsInputDirection()
    {
        rotationSpeed += rotationAcceleration * Time.deltaTime;
        rotationSpeed = Mathf.Min(rotationSpeed, maxRotationSpeed);

        if (input.GetCurMoveInput() != Vector2.zero)
        {
            Vector3 normalizedCamForward = camTran.forward;
            normalizedCamForward.y = 0f;
            normalizedCamForward.Normalize();

            // Camera y value can sometimes be a very small non-zero
            Vector3 normalizedCamRight = camTran.right;
            normalizedCamRight.y = 0f;
            normalizedCamRight.Normalize();

            Vector2 lastMoveInput = input.GetLastNonZeroMoveInput();
            moveDirection = lastMoveInput.x * normalizedCamRight + lastMoveInput.y * normalizedCamForward;

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

    // Moves the player towards camera relative input direction.
    //  Limits movement when at max stretch distance and deaccelerates when no input is given.
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
            front.y = 0f;
            front.Normalize();
            Vector3 right = stretchBody.GetFrontBodyRight();
            right.y = 0f;
            right.Normalize();

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

        // Update animation speed based on current velocity
        headAnim.SetFloat(speedHash, body.linearVelocity.magnitude / maxSpeed);
    }

    // Returns true if head is grounded
    private bool IsGrounded()
    {
        return Physics.Raycast(groundCheckTran.position, Vector3.down, groundCheckDistance, groundLayer);
    }
}
