using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputSystem : MonoBehaviour, PlayerInputAction.IPlayerActions
{
    [Header("输入系统")]
    private PlayerInputAction inputActions;
    
    [Header("输入值")]
    public Vector2 moveInput;
    public Vector2 cameraInput;
    public bool isFirePressed;
    public bool isJumpPressed;
    public bool isAimPressed;
    public bool isRunPressed;
    public bool isCrouchPressed;


    // Start is called before the first frame update
    void Start()
    {
        // 初始化输入系统
        inputActions = new PlayerInputAction();
        
        // 启用输入系统
        inputActions.Enable();
        
        // 绑定回调
        inputActions.Player.SetCallbacks(this);
    }

    // Update is called once per frame
    void Update()
    {
        // 获取输入值
        moveInput = inputActions.Player.moveController.ReadValue<Vector2>();
        cameraInput = inputActions.Player.cameraController.ReadValue<Vector2>();
    }

    private void OnDestroy()
    {
        // 清理输入系统
        if (inputActions != null)
        {
            inputActions.Disable();
            inputActions.Dispose();
        }
    }

    // 输入回调方法
    public void OnMoveController(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnCameraController(InputAction.CallbackContext context)
    {
        cameraInput = context.ReadValue<Vector2>();
    }

    public void OnFire(InputAction.CallbackContext context)
    {
        isFirePressed = context.performed;
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        isJumpPressed = context.performed;
    }

    public void OnAim(InputAction.CallbackContext context)
    {
        isAimPressed = context.performed;
    }

    public void OnRun(InputAction.CallbackContext context)
    {
        isRunPressed = context.performed;
    }

    public void OnCrouch(InputAction.CallbackContext context)
    {
        isCrouchPressed = context.performed;
    }
}
