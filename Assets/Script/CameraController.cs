using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class CameraController : MonoBehaviour
{
    [Header("相机控制")]
    [SerializeField] private CinemachineFreeLook freeLookCamera;
    [SerializeField] private PlayerInputSystem playerInputSystem;

    // 鼠标移动速度控制变量
    [Header("鼠标灵敏度")]
    [SerializeField] private float mouseXSensitivity =0.5f;
    [SerializeField] private float mouseYSensitivity = 0.5f;

    // Start is called before the first frame update
    void Start()
    {
        // 如果没有手动指定，尝试自动获取组件
        if (freeLookCamera == null)
        {
            freeLookCamera = GetComponent<CinemachineFreeLook>();
        }

        if (playerInputSystem == null)
        {
            playerInputSystem = FindObjectOfType<PlayerInputSystem>();
        }


    }

    // Update is called once per frame
    void Update()
    {
        if (freeLookCamera == null || playerInputSystem == null) return;

        //输入更新
        var mouseInput = playerInputSystem.cameraInput;
        freeLookCamera.m_XAxis.m_InputAxisValue = mouseInput.x * mouseXSensitivity;
        freeLookCamera.m_YAxis.m_InputAxisValue = mouseInput.y * mouseYSensitivity;
    }
}
