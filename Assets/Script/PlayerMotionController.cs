using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MxM;

public class PlayerMotionController : MonoBehaviour
{
    [Header("组件引用")]
    [SerializeField] public PlayerInputSystem playerInputSystem;
    [SerializeField] public MxMTrajectoryGenerator trajectoryGenerator;
    [SerializeField] public MxMAnimator mxmAnimator;
    [SerializeField] public Transform cameraTransform;

    [Header("移动设置")]
    [SerializeField] private float walkSpeed = 4.3f;
    [SerializeField] private float runSpeed = 6.7f;
    [SerializeField] private float walkPositionBias = 10f;
    [SerializeField] private float runPositionBias = 6f;
    [SerializeField] private float walkDirectionBias = 10f;
    [SerializeField] private float runDirectionBias = 6f;

    [Header("输入配置文件")]
    [SerializeField] private MxMInputProfile universalInputProfile; // 一个配置文件处理所有状态

    [Header("状态")]
    [SerializeField] private bool isRunning = false;

    private void Start()
    {
        // 自动获取组件引用
        if (playerInputSystem == null)
            playerInputSystem = GetComponent<PlayerInputSystem>();

        if (trajectoryGenerator == null)
            trajectoryGenerator = GetComponent<MxMTrajectoryGenerator>();

        if (mxmAnimator == null)
            mxmAnimator = GetComponent<MxMAnimator>();

        if (cameraTransform == null)
            cameraTransform = Camera.main?.transform;

        // 设置相机引用到轨迹生成器
        if (trajectoryGenerator != null && cameraTransform != null)
        {
            trajectoryGenerator.RelativeCameraTransform = cameraTransform;
        }
        // 设置通用输入配置文件
        if (trajectoryGenerator != null && universalInputProfile != null)
        {
            trajectoryGenerator.InputProfile = universalInputProfile;
        }

    }

    private void Update()
    {
        if (playerInputSystem == null || trajectoryGenerator == null) return;

        // 处理奔跑/行走切换
        HandleRunToggle();

        // 将输入传递给轨迹生成器
        UpdateTrajectoryInput();
    }

    private void HandleRunToggle()
    {
        bool shouldRun = playerInputSystem.isRunPressed && playerInputSystem.moveInput.magnitude > 0.1f;

        if (shouldRun && !isRunning)
        {
            SetRunMode();
        }
        else if (!shouldRun && isRunning)
        {
            SetWalkMode();
        }
    }

    private void SetWalkMode()
    {
        isRunning = false;
        trajectoryGenerator.MaxSpeed = walkSpeed;
        trajectoryGenerator.PositionBias = walkPositionBias;
        trajectoryGenerator.DirectionBias = walkDirectionBias;
    }

    private void SetRunMode()
    {
        isRunning = true;
        trajectoryGenerator.MaxSpeed = runSpeed;
        trajectoryGenerator.PositionBias = runPositionBias;
        trajectoryGenerator.DirectionBias = runDirectionBias;
    }

    private void UpdateTrajectoryInput()
    {
        // 将输入系统的移动输入转换为轨迹生成器需要的格式
        Vector2 moveInput = playerInputSystem.moveInput;
        Vector3 inputVector = new Vector3(moveInput.x, 0f, moveInput.y);

        // 设置到轨迹生成器
        trajectoryGenerator.InputVector = inputVector;
    }

    // 公共方法供外部调用
    public bool IsRunning => isRunning;
    public Vector2 GetMoveInput() => playerInputSystem.moveInput;
    public Vector2 GetCameraInput() => playerInputSystem.cameraInput;

    // 手动设置模式（供外部调用）
    public void ForceWalkMode()
    {
        SetWalkMode();
    }

    public void ForceRunMode()
    {
        SetRunMode();
    }
}