using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private PlayerInputActions inputMap;
    private bool isStretching = false;
    private Transform originTran;

    private void Awake()
    {
        inputMap = new PlayerInputActions();

        // Hide and lock cursor to center of screen
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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
        Vector2 movement = context.ReadValue<Vector2>();

        if (context.canceled)
            movement = Vector2.zero;

        // Handle movement logic here
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

}