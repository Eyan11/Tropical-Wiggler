using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Player Movement Settings")]
    [SerializeField] private float maxSpeed = 5f;
    [SerializeField] private float accelerationForce = 10f;
    [SerializeField] private float deaccelerationForce = -50f;
    [Header("Orientation Settings")]
    [SerializeField] private Transform orientationTran;
    [SerializeField] private float rotationSpeed = 1000f;
    [Tooltip("The angle threshold (in degrees) within which the player must be rotated towards target direction before they can move when they are not already moving.")]
    [SerializeField] private float canMoveAngleThreshold = 5f;
    private bool isStretching = false;
    private PlayerInputActions inputMap;
    private Transform camTran;
    private Rigidbody body;
    private Vector2 curMoveInput = Vector2.zero;
    private Vector2 lastMoveInput = Vector2.zero; // Last non-zero move input for orientation when input is zero
    private bool canMove = false; // True if facing forward and able to move (lerp towards target rotation is basically done)

    private void Awake()
    {
        inputMap = new PlayerInputActions();

        // Hide and lock cursor to center of screen
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        camTran = Camera.main.transform;
        body = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        inputMap.Player.Enable();
        inputMap.Player.Move.performed += OnMove;
        inputMap.Player.Move.canceled += OnMove;
        
        inputMap.Player.Stretch.performed += OnStretch;
        inputMap.Player.Stretch.canceled += OnStretch;

        // Look input is hooked up to cinemachine directly
    }

    private void OnDisable()
    {
        inputMap.Player.Disable();
        inputMap.Player.Move.performed -= OnMove;
        inputMap.Player.Move.canceled -= OnMove;
        
        inputMap.Player.Stretch.performed -= OnStretch;
        inputMap.Player.Stretch.canceled -= OnStretch;

        // Look input is hooked up to cinemachine directly
    }

    void OnDestroy()
    {
        inputMap.Dispose(); // Destroy asset
    }



    // *** Input Event Handlers *******************************************************************

    private void OnMove(InputAction.CallbackContext context)
    {
        curMoveInput = context.ReadValue<Vector2>();

        if (context.canceled)
        {
            canMove = false; // Make player rotate towards input direction before allowing movement
            curMoveInput = Vector2.zero;
        }
        else {
            lastMoveInput = curMoveInput;
        }

        curMoveInput.Normalize();
    }


    private void OnStretch(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            isStretching = true;
        }
        else if (context.canceled)
        {
            isStretching = false;
        }

        // Handle stretching logic here
    }

    // *** Movement *******************************************************************************
    
    private void Update()
    {
        RotateTowardsInputDirection();
    }

    private void FixedUpdate()
    {
        MovePlayer();
    }

    private void RotateTowardsInputDirection()
    {
        if (lastMoveInput == Vector2.zero) return;
        
        Vector3 targetDirection = lastMoveInput.x * camTran.right + lastMoveInput.y * camTran.forward;
        targetDirection.y = 0f;
        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);

        // Rotate at constant speed towards last non-zero movement direction
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

        float rotationDifference = Quaternion.Angle(transform.rotation, targetRotation);

        if (rotationDifference < 0.1f) // Stop rotating when very close to target rotation
        {
            orientationTran.rotation = targetRotation;
        }

        if (!canMove && curMoveInput != Vector2.zero && rotationDifference < canMoveAngleThreshold) // Allow movement again when facing forward
        {
            canMove = true;
        }
    }

    private void MovePlayer()
    {
        if (curMoveInput == Vector2.zero)
        {
            Vector2 deacceleration = deaccelerationForce * Time.fixedDeltaTime * new Vector2(body.linearVelocity.x, body.linearVelocity.z);
            body.linearVelocity += new Vector3(deacceleration.x, 0f, deacceleration.y);

            if (body.linearVelocity.magnitude < 0.1f)
                body.linearVelocity = new Vector3(0f, body.linearVelocity.y, 0f);
        }
        else if (canMove && curMoveInput != Vector2.zero)
        {
            Vector3 acceleration = curMoveInput.x * camTran.right + curMoveInput.y * camTran.forward;
            acceleration.y = 0f;
            acceleration.Normalize();

            acceleration *= accelerationForce * Time.fixedDeltaTime;
            body.linearVelocity += acceleration;
        }

        body.linearVelocity = Vector3.ClampMagnitude(body.linearVelocity, maxSpeed);
    }

}