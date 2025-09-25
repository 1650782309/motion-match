using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using System.Diagnostics;
using System;

public class CameraController : MonoBehaviour
{
    [Header("相机控制")]
    [SerializeField] private CinemachineFreeLook freeLookCamera;
    [SerializeField] private CinemachineVirtualCamera virtualCamera;
    [SerializeField] private PlayerInputSystem playerInputSystem;


    private bool isStrafing = false;

    private void Start()
    {
        if (freeLookCamera == null)
        {
            freeLookCamera = GetComponentInChildren<CinemachineFreeLook>();
        }
        if (virtualCamera == null)
        {
            virtualCamera = GetComponentInChildren<CinemachineVirtualCamera>();
        }

        InitializeCameraPriorities();
    }

    private void Update()
    {
        if (playerInputSystem.isAimPressed)
        {
            SwitchStrafeMode();
        }
        else
        {
            SwitchDirectMode();
        }
    }

    private void InitializeCameraPriorities()
    {
        SwitchDirectMode();
    }


    private void SwitchDirectMode()
    {
        if (!isStrafing) return;

        isStrafing = false;
        freeLookCamera.Priority = 10;
        virtualCamera.Priority = 0;
    }

    private void SwitchStrafeMode()
    {
        if (isStrafing) return;

        isStrafing = true;
        virtualCamera.Priority = 10;
        freeLookCamera.Priority = 0;
    }

    // 在 CinemachineBrain 组件上设置混合时间
    // 这样 Cinemachine 会自动处理平滑过渡
}
