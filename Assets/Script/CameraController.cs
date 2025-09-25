using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class CameraController : MonoBehaviour
{
    [Header("相机控制")]
    [SerializeField] private CinemachineVirtualCamera virtualCamera;
    [SerializeField] private PlayerInputSystem playerInputSystem;
    [SerializeField] private Transform playerTransform; // 角色Transform
    [SerializeField] private Transform cameraTarget; // 相机跟随的目标（可以是空物体）

    // 鼠标移动速度控制变量
    [Header("鼠标灵敏度")]
    [SerializeField] private float mouseXSensitivity = 0.5f;
    [SerializeField] private float mouseYSensitivity = 0.5f;
    
    [Header("相机设置")]
    [SerializeField] private float cameraDistance = 5f; // 相机距离
    [SerializeField] private float cameraHeight = 2f; // 相机高度
    [SerializeField] private float rotationSpeed = 2f; // 旋转速度
    
    [Header("Strafe模式设置")]
    [SerializeField] private bool isStrafeMode = false;
    [SerializeField] private float strafeCameraDistance = 3f;
    [SerializeField] private float strafeCameraHeight = 1.5f;
    [SerializeField] private float strafeMouseXSensitivity = 0.3f;
    [SerializeField] private float strafeMouseYSensitivity = 0.3f;
    
    // 内部变量
    private float currentX = 0f;
    private float currentY = 0f;
    private float currentDistance;
    private float currentHeight;

    // Start is called before the first frame update
    void Start()
    {
        // 如果没有手动指定，尝试自动获取组件
        if (virtualCamera == null)
        {
            virtualCamera = GetComponent<CinemachineVirtualCamera>();
        }

        if (playerInputSystem == null)
        {
            playerInputSystem = FindObjectOfType<PlayerInputSystem>();
        }
        
        // 自动获取角色Transform
        if (playerTransform == null)
        {
            PlayerMotionController playerController = FindObjectOfType<PlayerMotionController>();
            if (playerController != null)
            {
                playerTransform = playerController.transform;
            }
        }
        
        // 初始化相机设置
        InitializeCamera();
    }
    
    private void InitializeCamera()
    {
        if (virtualCamera == null || playerTransform == null) return;
        
        // 创建相机跟随目标（如果不存在）
        if (cameraTarget == null)
        {
            GameObject targetObj = new GameObject("CameraTarget");
            cameraTarget = targetObj.transform;
            cameraTarget.SetParent(playerTransform);
            cameraTarget.localPosition = Vector3.zero;
        }
        
        // 设置VirtualCamera的Follow和LookAt
        virtualCamera.Follow = cameraTarget;
        virtualCamera.LookAt = playerTransform;
        
        // 初始化相机参数
        currentDistance = cameraDistance;
        currentHeight = cameraHeight;
        
        // 设置相机位置
        UpdateCameraPosition();
    }

    // Update is called once per frame
    void Update()
    {
        if (virtualCamera == null || playerInputSystem == null || cameraTarget == null) return;

        // 检查Strafe模式
        CheckStrafeMode();
        
        // 处理鼠标输入
        HandleMouseInput();
        
        // 更新相机设置
        UpdateCameraSettings();
        
        // 更新相机位置
        UpdateCameraPosition();
    }
    
    private void CheckStrafeMode()
    {
        if (playerInputSystem == null) return;
        
        bool shouldBeStrafeMode = playerInputSystem.isAimPressed;
        
        if (shouldBeStrafeMode != isStrafeMode)
        {
            isStrafeMode = shouldBeStrafeMode;
            OnStrafeModeChanged();
        }
    }
    
    private void OnStrafeModeChanged()
    {
        if (isStrafeMode)
        {
            Debug.Log("相机切换到Strafe模式");
        }
        else
        {
            Debug.Log("相机切换回普通模式");
        }
    }
    
    private void UpdateCameraSettings()
    {
        // 平滑过渡相机参数
        float targetDistance = isStrafeMode ? strafeCameraDistance : cameraDistance;
        float targetHeight = isStrafeMode ? strafeCameraHeight : cameraHeight;
        
        currentDistance = Mathf.Lerp(currentDistance, targetDistance, 5f * Time.deltaTime);
        currentHeight = Mathf.Lerp(currentHeight, targetHeight, 5f * Time.deltaTime);
    }
    
    private void HandleMouseInput()
    {
        var mouseInput = playerInputSystem.cameraInput;
        
        // 根据模式选择不同的灵敏度
        float currentXSensitivity = isStrafeMode ? strafeMouseXSensitivity : mouseXSensitivity;
        float currentYSensitivity = isStrafeMode ? strafeMouseYSensitivity : mouseYSensitivity;
        
        // 更新旋转角度
        currentX += mouseInput.x * currentXSensitivity;
        currentY -= mouseInput.y * currentYSensitivity; // 反转Y轴
        
        // 限制Y轴角度
        currentY = Mathf.Clamp(currentY, -80f, 80f);
    }
    
    private void UpdateCameraPosition()
    {
        if (cameraTarget == null || playerTransform == null) return;
        
        // 计算相机目标位置
        Vector3 direction = Quaternion.Euler(currentY, currentX, 0) * Vector3.back;
        Vector3 targetPosition = playerTransform.position + direction * currentDistance + Vector3.up * currentHeight;
        
        // 更新相机跟随目标的位置
        cameraTarget.position = targetPosition;
        
        // 让相机朝向角色
        cameraTarget.LookAt(playerTransform.position + Vector3.up * 1.5f); // 稍微向上看
    }
    
    // 调试方法：在Scene视图中显示相机信息
    private void OnDrawGizmos()
    {
        if (cameraTarget == null || playerTransform == null) return;
        
        // 绘制相机到角色的连线
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(cameraTarget.position, playerTransform.position);
        
        // 绘制相机目标位置
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(cameraTarget.position, 0.5f);
        
        // 显示当前模式
        Gizmos.color = isStrafeMode ? Color.green : Color.blue;
        Gizmos.DrawWireCube(playerTransform.position + Vector3.up * 3f, Vector3.one * 0.3f);
    }
    
    // 公共方法：手动设置相机模式（用于调试）
    public void SetStrafeMode(bool strafeMode)
    {
        isStrafeMode = strafeMode;
        OnStrafeModeChanged();
    }
    
    // 公共方法：获取当前相机信息
    public string GetCameraInfo()
    {
        if (virtualCamera == null) return "Camera not found";
        
        return $"Mode: {(isStrafeMode ? "Strafe" : "Normal")}\n" +
               $"Distance: {currentDistance:F2}\n" +
               $"Height: {currentHeight:F2}\n" +
               $"Rotation: X={currentX:F1}, Y={currentY:F1}\n" +
               $"Follow Target: {(virtualCamera.Follow != null ? virtualCamera.Follow.name : "None")}\n" +
               $"LookAt Target: {(virtualCamera.LookAt != null ? virtualCamera.LookAt.name : "None")}";
    }
}
