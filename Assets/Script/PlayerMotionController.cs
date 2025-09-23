using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MxM;
using System;
using System.ComponentModel;
using System.Reflection;

public static class CalibrationDataState
{
    public const string General = "General";
    public const string Strafe = "Strafe";
}

public static class RequiredTag
{
    public const string Strafe = "Strafe";
    public const string General = "General";
}

public class PlayerMotionController : MonoBehaviour
{
    [Header("组件引用")]
    [SerializeField] public PlayerInputSystem playerInputSystem;
    [SerializeField] public MxMTrajectoryGenerator trajectoryGenerator;
    [SerializeField] public MxMAnimator mxmAnimator;
    [SerializeField] public Transform cameraTransform;

    [Header("Walk设置")]
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float walkPositionBias = 10f;
    [SerializeField] private float walkDirectionBias = 10f;

    [Header("Run设置")]
    [SerializeField] private float runSpeed = 5f;
    [SerializeField] private float runPositionBias = 6f;
    [SerializeField] private float runDirectionBias = 6f;

    [Header("Strafe设置")]
    [SerializeField] private float strafeSpeed = 2f;
    [SerializeField] private float strafePositionBias = 10f;
    [SerializeField] private float strafeDirectionBias = 10f;


    [Header("输入配置文件")]
    [SerializeField] private MxMInputProfile generalInputProfile;
    [SerializeField] private MxMInputProfile strafeInputProfile;

    [Header("Warp模块")]
    [SerializeField] private WarpModule generalWarpModule;
    [SerializeField] private WarpModule strafeWarpModule;

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
        if (trajectoryGenerator != null && generalInputProfile != null)
        {
            trajectoryGenerator.InputProfile = generalInputProfile;
        }

        //初始化tag
        mxmAnimator.AddRequiredTag(RequiredTag.General);
    }

    private void Update()
    {
        if (playerInputSystem == null || trajectoryGenerator == null) return;

        // 处理奔跑/行走切换
        HandleRunToggle();

        //处理strafe切换
        HandleStrafeToggle();

        // 处理跳跃
        HandleJump();

        // 将输入传递给轨迹生成器
        UpdateTrajectoryInput();

        //UserTag检查
        CheckUserTag();
    }

    private void HandleStrafeToggle()
    {
        if (playerInputSystem.isAimPressed)
        {
            // 切换到Strafe模式
            trajectoryGenerator.TrajectoryMode = ETrajectoryMoveMode.Strafe;
            mxmAnimator.SetCalibrationData(CalibrationDataState.Strafe);
            mxmAnimator.RemoveRequiredTag(RequiredTag.General);
            mxmAnimator.AddRequiredTag(RequiredTag.Strafe);
            mxmAnimator.SetWarpOverride(strafeWarpModule);
            if (trajectoryGenerator != null && strafeInputProfile != null)
            {
                trajectoryGenerator.InputProfile = strafeInputProfile;
            }

            // 如果没有移动输入，降低匹配权重，让系统选择更合适的动画
            if (playerInputSystem.moveInput.magnitude < 0.1f)
            {
                mxmAnimator.SetFavourCurrentPose(true, 0.8f); // 降低权重
            }
            else
            {
                mxmAnimator.SetFavourCurrentPose(false, 1.0f); // 正常匹配
            }
        }
        else
        {
            // 切换回正常模式
            trajectoryGenerator.TrajectoryMode = ETrajectoryMoveMode.Normal;
            mxmAnimator.RemoveRequiredTag(RequiredTag.Strafe);
            mxmAnimator.AddRequiredTag(RequiredTag.General);
            mxmAnimator.SetWarpOverride(generalWarpModule);
            if (trajectoryGenerator != null && generalInputProfile != null)
            {
                trajectoryGenerator.InputProfile = generalInputProfile;
            }
            // mxmAnimator.AddRequiredTag(RequiredTag.General);
            // mxmAnimator.SetFavourCurrentPose(false, 1.0f);
        }
    }

    private void HandleJump()
    {
        if (playerInputSystem.isJumpPressed)
        {
            Debug.Log("Jump");
        }
    }

    private void CheckUserTag()
    {
        if (mxmAnimator.QueryUserTag("DisableGravity"))
        {
            Debug.Log("DisableGravity");
        }
        if (mxmAnimator.QueryUserTag("DisableCollision"))
        {
            Debug.Log("DisableCollision");
        }
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
        mxmAnimator.AngularErrorWarpMethod = EAngularErrorWarpMethod.CurrentHeading;

    }

    private void SetRunMode()
    {
        isRunning = true;
        trajectoryGenerator.MaxSpeed = runSpeed;
        trajectoryGenerator.PositionBias = runPositionBias;
        trajectoryGenerator.DirectionBias = runDirectionBias;
        mxmAnimator.AngularErrorWarpMethod = EAngularErrorWarpMethod.CurrentHeading;
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