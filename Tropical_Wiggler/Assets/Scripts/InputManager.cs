using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class InputManager : MonoBehaviour
{
    private PlayerInputActions inputMap;
    private bool isStretching = false;
    private Vector2 curMoveInput = Vector2.zero;
    private Vector2 lastNonZeroMoveInput = Vector2.zero; // Last non-zero move input for orientation when input is zero

    public event Action<bool> OnStretchInputChanged;
    public event Action OnMoveInputCanceled;


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
        curMoveInput = context.ReadValue<Vector2>(); // Already normalized for KBM

        if (context.canceled) 
        {
            curMoveInput = Vector2.zero;
            OnMoveInputCanceled?.Invoke();
        }
        // Controller input can be (0,0) before being "canceled"
        else if (curMoveInput != Vector2.zero) lastNonZeroMoveInput = curMoveInput;
    }


    private void OnStretch(InputAction.CallbackContext context)
    {
        if (context.performed) isStretching = true;
        else if (context.canceled) isStretching = false;

        OnStretchInputChanged?.Invoke(isStretching);
    }


    // *** Public Getters *************************************************************************
    public Vector2 GetCurMoveInput() => curMoveInput;
    public Vector2 GetLastNonZeroMoveInput() => lastNonZeroMoveInput;
    public bool GetIsStretching() => isStretching;
}
