using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MxM;
using MxMGameplay;
public class VaultDectector_User : MonoBehaviour
{


    [SerializeField]
    private VaultDefinition[] m_vaultDefinitions = null;

    [SerializeField]
    private VaultDetectionConfig[] m_vaultConfigurations = null;

    [SerializeField]
    private float m_minStepUpDepth = 1f; //控制上台阶所需的最小平台深度


    [SerializeField]
    private float directDetectHightOffset = 0.1f; //直接检测高度偏移
    // [SerializeField]
    // private float m_defaultDetectDistance = 2f; //默认检测距离

    [SerializeField]
    private LayerMask m_layerMask = new LayerMask();


    [SerializeField]
    private float m_advanceSmoothing = 10f; //前进距离的平滑系数

    [SerializeField]
    public float m_maxApproachAngle = 60f; //角色面对障碍物的最大允许角度

    [Header("射线检测可视化")]
    [SerializeField]
    private bool m_showRaycastVisualization = true; //是否显示射线检测可视化
    [SerializeField]
    private Color m_raycastHitColor = Color.red; //射线检测命中颜色
    [SerializeField]
    private Color m_raycastMissColor = Color.gray; //射线检测未命中颜色
    [SerializeField]
    private Color m_sphereCastColor = Color.cyan; //球形射线颜色
    [SerializeField]
    private Color m_verticalRaycastHitColor = Color.green; //垂直射线检测命中颜色
    [SerializeField]
    private Color m_verticalRaycastMissColor = new Color(1f, 0.5f, 0f); //垂直射线检测未命中颜色（橙色）

    [Header("攀爬接触点可视化")]
    [SerializeField]
    private bool m_showContactVisualization = true; //是否显示攀爬接触点可视化
    [SerializeField]
    private Color m_contact1Color = Color.yellow; //第一个接触点颜色
    [SerializeField]
    private Color m_contact2Color = Color.magenta; //第二个接触点颜色
    [SerializeField]
    private float m_contactPointSize = 0.2f; //接触点球体大小
    [SerializeField]
    private bool m_showContactLabels = true; //是否显示接触点标签


    /// <summary>
    /// 组件引用
    /// </summary>
    private MxMAnimator m_mxmAnimator;
    private MxMRootMotionApplicator m_rootMotionApplicator;
    private GenericControllerWrapper m_controllerWrapper;
    private MxMTrajectoryGenerator m_trajectoryGenerator;

    private int m_vaultAnalysisIterations; //形状分析的迭代次数

    private VaultDetectionConfig m_curConfig; //当前配置

    private float m_minVaultRise; //最小攀爬高度
    private float m_maxVaultRise; //最大攀爬高度
    private float m_minVaultDepth; //最小平台深度
    private float m_maxVaultDepth; //最大平台深度
    private float m_minVaultDrop; //最小掉落高度
    private float m_maxVaultDrop; //最大掉落高度

    private bool m_isVaulting; //是否正在攀爬

    // 调试相关变量
    private bool m_lastRaycastHit = false; //上次射线检测是否命中
    private Vector3 m_lastRaycastHitPoint = Vector3.zero; //上次射线检测命中点
    private Vector3 m_lastRaycastStart = Vector3.zero; //上次射线检测起点
    private float m_lastRaycastDistance = 0f; //上次射线检测距离

    // 垂直射线检测调试变量
    private bool m_lastVerticalRaycastHit = false; //上次垂直射线检测是否命中
    private Vector3 m_lastVerticalRaycastHitPoint = Vector3.zero; //上次垂直射线检测命中点
    private Vector3 m_lastVerticalRaycastStart = Vector3.zero; //上次垂直射线检测起点
    private float m_lastVerticalRaycastDistance = 0f; //上次垂直射线检测距离

    // 攀爬接触点调试变量
    private bool m_hasContact1 = false; //是否有第一个接触点
    private Vector3 m_debugContact1 = Vector3.zero; //调试用第一个接触点
    private bool m_hasContact2 = false; //是否有第二个接触点
    private Vector3 m_debugContact2 = Vector3.zero; //调试用第二个接触点
    private EVaultType m_debugVaultType = EVaultType.Invalid; //调试用攀爬类型

    public float Advance { get; set; } //当前前进距离 射线检测距离、形状分析起点计算
    public float DesiredAdvance { get; set; } //目标前进距离 


    void Awake()
    {
        if (m_vaultConfigurations == null || m_vaultConfigurations.Length == 0)
        {
            Debug.LogError("VaultDetector: Trying to Awake Vault Detector with null or empty vault configurations (m_vaultConfiguration)");
            Destroy(this);
            return;
        }

        if (m_vaultDefinitions == null || m_vaultDefinitions.Length == 0)
        {
            Debug.LogError("VaultDetector: Trying to Awake Vault Detector with null or empty vault definitions (m_vaultDefinitions)");
            Destroy(this);
            return;
        }

        m_mxmAnimator = GetComponentInChildren<MxMAnimator>();

        if (m_mxmAnimator == null)
        {
            Debug.LogError("VaultDetector: Trying to Awake Vault Detector but the MxMAnimator component cannot be found");
            Destroy(this);
            return;
        }

        m_trajectoryGenerator = GetComponentInChildren<MxMTrajectoryGenerator>();

        if (m_trajectoryGenerator == null)
        {
            Debug.LogError("VaultDetector: Trying to Awake Vault Detector but there is no Trajectory component found that implements IMxMTrajectory.");
            Destroy(this);
            return;
        }

        m_rootMotionApplicator = GetComponentInChildren<MxMRootMotionApplicator>();
        m_controllerWrapper = GetComponentInChildren<GenericControllerWrapper>();

        m_minVaultRise = float.MaxValue;
        m_maxVaultRise = float.MinValue;
        m_minVaultDepth = float.MaxValue;
        m_maxVaultDepth = float.MinValue;
        m_minVaultDrop = float.MaxValue;
        m_maxVaultDrop = float.MinValue;

        foreach (VaultDefinition vd in m_vaultDefinitions)
        {
            switch (vd.VaultType)
            {
                case EVaultType.StepUp:
                    {
                        if (vd.MinRise < m_minVaultRise) { m_minVaultRise = vd.MinRise; }
                        if (vd.MaxRise > m_maxVaultRise) { m_maxVaultRise = vd.MaxRise; }
                        if (vd.MinDepth < m_minVaultDepth) { m_minVaultDepth = vd.MinDepth; }
                    }
                    break;
                case EVaultType.StepOver:
                    {
                        if (vd.MinRise < m_minVaultRise) { m_minVaultRise = vd.MinRise; }
                        if (vd.MaxRise > m_maxVaultRise) { m_maxVaultRise = vd.MaxRise; }
                        if (vd.MinDepth < m_minVaultDepth) { m_minVaultDepth = vd.MinDepth; }
                        if (vd.MaxDepth > m_maxVaultDepth) { m_maxVaultDepth = vd.MaxDepth; }
                    }
                    break;
                case EVaultType.StepOff:
                    {
                        if (vd.MinDepth < m_minVaultDepth) { m_minVaultDepth = vd.MinDepth; }
                        if (vd.MinDrop < m_minVaultDrop) { m_minVaultDrop = vd.MinDrop; }
                        if (vd.MaxDrop > m_maxVaultDrop) { m_maxVaultDrop = vd.MaxDrop; }
                    }
                    break;
            }
        }

        m_curConfig = m_vaultConfigurations[0];
        DesiredAdvance = Advance = 0f;

        m_vaultAnalysisIterations = (int)(m_maxVaultDepth / m_curConfig.ShapeAnalysisSpacing) + 1;

        // 输出最终计算的处理范围
        Debug.Log("=== VaultDetector 处理范围计算完成 ===");
        Debug.Log($"高度范围: {m_minVaultRise:F2}m - {m_maxVaultRise:F2}m");
        Debug.Log($"深度范围: {m_minVaultDepth:F2}m - {m_maxVaultDepth:F2}m");
        Debug.Log($"掉落范围: {m_minVaultDrop:F2}m - {m_maxVaultDrop:F2}m");
        Debug.Log($"形状分析迭代次数: {m_vaultAnalysisIterations}");
        Debug.Log($"当前配置: {m_curConfig.ConfigName}");
        Debug.Log($"检测探针半径: {m_curConfig.DetectProbeRadius:F2}m");
        Debug.Log($"检测探针前进时间: {m_curConfig.DetectProbeAdvanceTime:F2}s");
        Debug.Log($"形状分析间距: {m_curConfig.ShapeAnalysisSpacing:F2}m");
        Debug.Log("=====================================");
    }

    public void OnEnable()
    {
        m_isVaulting = false;
    }


    void Update()
    {
        //检查是否已经在攀爬
        if (m_isVaulting)
        {
            HandleCurrentVault();
            return;
        }

        // 清除之前的接触点调试信息
        m_hasContact1 = false;
        m_hasContact2 = false;
        m_debugVaultType = EVaultType.Invalid;

        if (!CanVault())
        {
            return;
        }
        else
        {
            Debug.Log("CanVault");
        }

        //我们将频繁使用变换位置和前方向量，在这里缓存以提高性能
        Vector3 charPos = transform.position;
        Vector3 charForward = transform.forward;
        float approachAngle = 0f;

        //首先必须从角色向前发射射线，稍微高于最小攀爬高度（即角色控制器最小步高）
        // Vector3 probeStart = new Vector3(charPos.x, charPos.y +
        //     m_curConfig.DetectProbeRadius + directDetectHightOffset,
        //     charPos.z);

        Vector3 probeStart = new Vector3(charPos.x, charPos.y +
                m_curConfig.DetectProbeRadius + m_minVaultRise,
                charPos.z);

        Ray forwardRay = new Ray(probeStart, charForward);

        // 计算混合检测距离：确保即使Advance为0也会向前检测
        // Advance = Mathf.Max(Advance, m_defaultDetectDistance);

        // 存储射线检测调试信息
        m_lastRaycastStart = probeStart;
        m_lastRaycastDistance = Advance;

        if (Physics.SphereCast(forwardRay, m_curConfig.DetectProbeRadius, out RaycastHit forwardRayHit,
               Advance, m_layerMask, QueryTriggerInteraction.Ignore))
        {

            //逻辑部分
            //如果我们检测到的障碍物比当前前进距离更近，则缩短前进距离，因为我们希望
            //第一个向下的球形检测能命中边缘
            if (forwardRayHit.distance < Advance)
            {
                Advance = forwardRayHit.distance;
            }
            Vector3 obstacleOrient = Vector3.ProjectOnPlane(forwardRayHit.normal, Vector3.up) * -1f;

            approachAngle = Vector3.SignedAngle(transform.forward, obstacleOrient, Vector3.up);

            //如果我们遇到障碍物但角度超过最大值，我们不想攀爬，所以在这里返回
            //问题：如果实际的攀爬点（即在障碍物顶部）在正确角度范围内会怎样？
            if (Mathf.Abs(approachAngle) > m_maxApproachAngle)
            {
                Debug.LogWarning("障碍物角度超过最大值，不进行攀爬");
                return;
            }
            //Debug 射线检测命中
            m_lastRaycastHit = true;
            m_lastRaycastHitPoint = forwardRayHit.point;
        }
        else
        {


            //Debug 射线检测未命中
            m_lastRaycastHit = false;
            m_lastRaycastHitPoint = probeStart + charForward * Advance;
        }


        //接下来我们从最大攀爬高度向最大掉落高度发射垂直向下的射线
        //注意：这没有考虑屋顶或悬垂物
        probeStart = transform.TransformPoint(new Vector3(0f, m_maxVaultRise, Advance));

        Ray probeRay = new Ray(probeStart, Vector3.down);

        // 存储垂直射线检测调试信息
        m_lastVerticalRaycastStart = probeStart;
        m_lastVerticalRaycastDistance = m_maxVaultRise + m_maxVaultDrop;

        if (Physics.SphereCast(probeRay, m_curConfig.DetectProbeRadius, out RaycastHit probeHit, m_maxVaultRise + m_maxVaultDrop,
                m_layerMask, QueryTriggerInteraction.Ignore))
        {

            //逻辑部分
            //Too high -> cancel the vault
            if (probeHit.distance < Mathf.Epsilon)
            {
                return;
            }

            //如果探针距离在最小和最大攀爬高度之间，可能检测到"vault over"或"vault up"
            if (probeHit.distance < (m_maxVaultRise - m_minVaultRise))
            {
                //可能检测到攀爬

                //检查是否有足够的高度容纳角色
                if (!CheckCharacterHeightFit(probeHit.point, charForward))
                {
                    return;
                }

                //计算命中偏移量。这是射线起点和命中点之间在水平2D平面上的偏移量

                //这里我们对可攀爬物体进行形状分析，并将数据存储在可攀爬配置文件中
                VaultableProfile vaultable;
                VaultShapeAnalysis(in probeHit, out vaultable);

                if (vaultable.VaultType == EVaultType.Invalid)
                {
                    return;
                }

                //检查物体顶部是否有足够的空间（用于上台阶）
                if (vaultable.VaultType == EVaultType.StepUp && vaultable.Depth < m_minStepUpDepth)
                    return;


                //Todo检查物体表面坡度（目前假设为正常）

                //选择设置适当的攀爬事件
                VaultDefinition vaultDef = ComputeBestVault(ref vaultable);

                if (vaultDef == null)
                    return;

                float facingAngle = transform.rotation.eulerAngles.y; //获取角色当前朝向

                if (vaultDef.LineUpWithObstacle) //如果攀爬定义需要与障碍物对齐，则调整朝向
                {
                    facingAngle += approachAngle;
                }

                //设置攀爬接触点
                vaultDef.EventDefinition.ClearContacts();

                switch (vaultDef.OffsetMethod_Contact1)
                {
                    case EVaultContactOffsetMethod.Offset: { vaultable.Contact1 += transform.TransformVector(vaultDef.Offset_Contact1); } break;
                    case EVaultContactOffsetMethod.DepthProportion: { vaultable.Contact1 += transform.TransformVector(vaultable.Depth * vaultDef.Offset_Contact1); } break;
                }

                vaultDef.EventDefinition.AddEventContact(vaultable.Contact1, facingAngle);

                // 保存第一个接触点的调试信息
                m_hasContact1 = true;
                m_debugContact1 = vaultable.Contact1;
                m_debugVaultType = vaultable.VaultType;

                if (vaultable.VaultType == EVaultType.StepOver)
                {
                    switch (vaultDef.OffsetMethod_Contact2)
                    {
                        case EVaultContactOffsetMethod.Offset: { vaultable.Contact2 += transform.TransformVector(vaultDef.Offset_Contact2); } break;
                        case EVaultContactOffsetMethod.DepthProportion: { vaultable.Contact2 += transform.TransformVector(vaultable.Depth * vaultDef.Offset_Contact2); } break;
                    }

                    vaultDef.EventDefinition.AddEventContact(vaultable.Contact2, facingAngle);

                    // 保存第二个接触点的调试信息
                    m_hasContact2 = true;
                    m_debugContact2 = vaultable.Contact2;
                }
                else
                {
                    // 如果不是StepOver类型，清除第二个接触点
                    m_hasContact2 = false;
                }

                // 触发攀爬事件
                m_mxmAnimator.BeginEvent(vaultDef.EventDefinition);

                m_mxmAnimator.PostEventTrajectoryMode = EPostEventTrajectoryMode.Pause;

                if (m_rootMotionApplicator != null)
                    m_rootMotionApplicator.EnableGravity = false;

                if (vaultDef.DisableCollision && m_controllerWrapper != null)
                    m_controllerWrapper.CollisionEnabled = false;

                m_isVaulting = true;
            }
            else //检测为step off 或者 vault over gap  （落下）
            {
                Vector3 flatHitPoint = new Vector3(probeHit.point.x, 0f, probeHit.point.z); //射线命中点水平投影
                Vector3 flatProbePoint = new Vector3(probeStart.x, 0f, probeStart.z);   //射线起点水平投影

                Vector3 dir = flatProbePoint - flatHitPoint; // 边缘方向向量

                // 如果射线起点和命中点在水平面上的距离大于检测探针半径的一半，就认为遇到了边缘。
                if (dir.sqrMagnitude > (m_curConfig.DetectProbeRadius * m_curConfig.DetectProbeRadius) / 4f)
                {
                    //A step off may have occured

                    //Shape analysis 
                    Vector2 start2D = new Vector2(probeStart.x, probeStart.z);
                    Vector2 hit2D = new Vector2(probeHit.point.x, probeHit.point.z);

                    float hitOffset = Vector2.Distance(start2D, hit2D);

                    //选取合适事件
                    VaultableProfile vaultable;
                    VaultOffShapeAnalysis(in probeHit, out vaultable, hitOffset);

                    if (vaultable.VaultType == EVaultType.Invalid)
                        return;

                    VaultDefinition vaultDef = ComputeBestVault(ref vaultable);

                    if (vaultDef == null)
                        return;

                    float facingAngle = transform.rotation.eulerAngles.y;

                    //设置攀爬接触点
                    vaultDef.EventDefinition.ClearContacts();

                    switch (vaultDef.OffsetMethod_Contact1)
                    {
                        case EVaultContactOffsetMethod.Offset: { vaultable.Contact1 += transform.TransformVector(vaultDef.Offset_Contact1); } break;
                        case EVaultContactOffsetMethod.DepthProportion: { vaultable.Contact1 += transform.TransformVector(vaultable.Depth * vaultDef.Offset_Contact1); } break;
                    }

                    vaultDef.EventDefinition.AddEventContact(vaultable.Contact1, facingAngle);

                    // 保存StepOff接触点的调试信息
                    m_hasContact1 = true;
                    m_debugContact1 = vaultable.Contact1;
                    m_debugVaultType = vaultable.VaultType;
                    m_hasContact2 = false; // StepOff只有一个接触点

                    //事件执行
                    m_mxmAnimator.BeginEvent(vaultDef.EventDefinition);
                    m_mxmAnimator.PostEventTrajectoryMode = EPostEventTrajectoryMode.Pause;

                    //if (m_rootMotionApplicator != null)
                    //    m_rootMotionApplicator.EnableGravity = false;

                    //if (m_controllerWrapper != null)
                    //    m_controllerWrapper.CollisionEnabled = false;

                    m_isVaulting = true;
                }
            }

            //Debug 垂直射线检测命中
            m_lastVerticalRaycastHit = true;
            m_lastVerticalRaycastHitPoint = probeHit.point;
        }
        else
        {
            //Debug 垂直射线检测未命中
            m_lastVerticalRaycastHit = false;
            m_lastVerticalRaycastHitPoint = probeStart + Vector3.down * m_lastVerticalRaycastDistance;
        }



    }

    private VaultDefinition ComputeBestVault(ref VaultableProfile a_vaultable)
    {

        foreach (VaultDefinition vaultDef in m_vaultDefinitions)
        {
            if (vaultDef.VaultType == a_vaultable.VaultType)
            {

                switch (vaultDef.VaultType)
                {
                    case EVaultType.StepUp:
                        {
                            if (a_vaultable.Depth < vaultDef.MinDepth)
                            {
                                continue;
                            }

                            if (a_vaultable.Rise < vaultDef.MinRise || a_vaultable.Rise > vaultDef.MaxRise)
                            {
                                continue;
                            }
                        }
                        break;
                    case EVaultType.StepOver:
                        {
                            if (a_vaultable.Depth < vaultDef.MinDepth || a_vaultable.Depth > vaultDef.MaxDepth)
                            {
                                continue;
                            }

                            if (a_vaultable.Rise < vaultDef.MinRise || a_vaultable.Rise > vaultDef.MaxRise)
                            {
                                continue;
                            }

                            if (a_vaultable.Drop < vaultDef.MinDrop || a_vaultable.Drop > vaultDef.MaxDrop)
                            {
                                continue;
                            }
                        }
                        break;
                    case EVaultType.StepOff:
                        {
                            if (a_vaultable.Depth < vaultDef.MinDepth)
                            {
                                continue;
                            }

                            if (a_vaultable.Drop < vaultDef.MinDrop || a_vaultable.Drop > vaultDef.MaxDrop)
                            {
                                continue;
                            }
                        }
                        break;
                }

                return vaultDef;
            }
        }

        return null;
    }

    private void VaultOffShapeAnalysis(in RaycastHit a_rayHit, out VaultableProfile a_vaultProfile, float hitOffset)
    {
        a_vaultProfile = new VaultableProfile();

        Vector3 lastPoint = a_rayHit.point;
        bool stepOffStart = false;
        for (int i = 1; i < m_vaultAnalysisIterations; ++i)
        {
            Vector3 start = transform.TransformPoint(Vector3.forward *
                (Advance + hitOffset + (float)i * m_curConfig.ShapeAnalysisSpacing));
            start.y += m_maxVaultRise;

            Ray ray = new Ray(start, Vector3.down);
            RaycastHit rayHit;

            if (Physics.Raycast(ray, out rayHit, m_maxVaultRise + m_maxVaultDrop, m_layerMask, QueryTriggerInteraction.Ignore))
            {
                float deltaHeight = rayHit.point.y - lastPoint.y;

                if (!stepOffStart)
                {
                    if (deltaHeight < -m_minVaultDrop)
                    {
                        a_vaultProfile.Drop = Mathf.Abs(deltaHeight);
                        a_vaultProfile.Contact1 = rayHit.point;
                        stepOffStart = true;
                    }
                }
                else
                {
                    if (deltaHeight > m_minVaultRise)
                    {
                        a_vaultProfile.Depth = a_vaultProfile.Depth = (i - 1) * m_curConfig.ShapeAnalysisSpacing;

                        if (a_vaultProfile.Depth > 1f)
                        {
                            a_vaultProfile.Rise = deltaHeight;
                            a_vaultProfile.VaultType = EVaultType.StepOff;
                        }
                        else
                        {
                            a_vaultProfile.VaultType = EVaultType.Invalid;
                        }

                        return;
                    }
                    else if (i == m_vaultAnalysisIterations - 1)
                    {
                        a_vaultProfile.Rise = 0f;
                        a_vaultProfile.Depth = a_vaultProfile.Depth = (i - 1) * m_curConfig.ShapeAnalysisSpacing;
                        a_vaultProfile.VaultType = EVaultType.StepOff;
                        return;
                    }
                }
            }
            else
            {
                a_vaultProfile.Depth = a_vaultProfile.Depth = (i - 1) * m_curConfig.ShapeAnalysisSpacing;

                if (a_vaultProfile.Depth > 1f)
                {
                    a_vaultProfile.Rise = 0f;
                    a_vaultProfile.VaultType = EVaultType.StepOff;
                }
                else
                {
                    a_vaultProfile.VaultType = EVaultType.Invalid;
                }

                return;
            }

            lastPoint = rayHit.point;
        }
    }

    //============================================================================================
    /**
    *  @brief 此函数用于分析潜在可翻越物体的形状。通过使用射线检测分析形状，
    *  可以轻松地将该形状的指标与多个带有配套动画的可翻越定义进行匹配
    *  
    *  @param [in RaycastHit] a_rayHit - 击中可翻越边缘的射线的射线检测结果
    *  @param [out VaultableProfile] a_vaultProfile - 包含形状分析所有指标的容器（检测结果）
    *  
    *  @Todo: 使用Jobs和Burst实现
    *         
    *********************************************************************************************/
    private void VaultShapeAnalysis(in RaycastHit a_rayHit, out VaultableProfile a_vaultProfile)
    {
        a_vaultProfile = new VaultableProfile();

        a_vaultProfile.Contact1 = a_rayHit.point;
        //  a_vaultProfile.Rise = m_maxVaultRise - a_rayHit.distance;

        Vector3 charPos = transform.position;
        Vector3 lastPoint = a_rayHit.point;
        Vector3 highestPoint = lastPoint;
        Vector3 lowestPoint = charPos;

        a_vaultProfile.Rise = a_rayHit.point.y - charPos.y;

        //We need to iterate several times, casting rays downwards to determine the shape of the object in
        //a straight line from the character
        for (int i = 1; i < m_vaultAnalysisIterations; ++i)
        {
            //Each iteration we move the starting point one spacing further
            Vector3 start = a_rayHit.point + transform.TransformVector(Vector3.forward
                * (float)i * m_curConfig.ShapeAnalysisSpacing);

            start.y = charPos.y + m_maxVaultRise;
            Ray ray = new Ray(start, Vector3.down);

            if (Physics.Raycast(ray, out RaycastHit rayHit, m_maxVaultRise + m_maxVaultDrop, m_layerMask, QueryTriggerInteraction.Ignore))
            {
                if (rayHit.point.y > highestPoint.y)
                {
                    highestPoint = rayHit.point;
                }
                else if (rayHit.point.y < lowestPoint.y)
                {
                    lowestPoint = rayHit.point;
                }

                float deltaHeight = rayHit.point.y - lastPoint.y;

                //If the change in height from one ray to another is greater than the minimum vault drop, then
                //we may have detected a step over. However! We can only declare a vault over if there is enough 
                //space for the character on the other side. This is determined by controller width and current velocity
                if (deltaHeight < -m_minVaultDrop)
                {
                    //Step Over
                    a_vaultProfile.Depth = (i - 1) * m_curConfig.ShapeAnalysisSpacing;

                    a_vaultProfile.Drop = a_rayHit.point.y - rayHit.point.y;
                    a_vaultProfile.VaultType = EVaultType.StepOver;
                    a_vaultProfile.Contact2 = rayHit.point;

                    return; //TODO: Remove this return point. The entire vault needs to be analysed before a decision is made in case the character doesn't fit
                }
                else if (i == m_vaultAnalysisIterations - 1)
                {
                    a_vaultProfile.Depth = (i - 1) * m_curConfig.ShapeAnalysisSpacing;
                    a_vaultProfile.Drop = 0f;
                    a_vaultProfile.VaultType = EVaultType.StepUp;
                }
            }
            else
            {
                //Step Over Fall
                a_vaultProfile.Drop = m_maxVaultDrop;
                a_vaultProfile.Depth = (i - 1) * m_curConfig.ShapeAnalysisSpacing;
                a_vaultProfile.VaultType = EVaultType.StepOverFall;
                return;
            }

            lastPoint = rayHit.point;
        }
    }

    /// <summary>
    /// 检测攀爬点上方是否可容纳角色
    /// </summary>
    /// <param name="a_fromPoint"></param>
    /// <param name="a_forward"></param>
    /// <returns></returns>
    private bool CheckCharacterHeightFit(Vector3 a_fromPoint, Vector3 a_forward)
    {
        float radius = m_controllerWrapper.Radius;
        Vector3 fromPosition = a_fromPoint + (a_forward * radius * 2f) + (Vector3.up * radius * 1.1f);

        Ray upRay = new Ray(fromPosition, Vector3.up);
        RaycastHit rayHit;
        if (Physics.Raycast(upRay, out rayHit, m_controllerWrapper.Height, m_layerMask, QueryTriggerInteraction.Ignore))
        {
            Debug.LogWarning("攀爬方向有障碍物");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 判断是否可攀爬
    /// </summary>
    /// <returns></returns>
    private bool CanVault()
    {
        // 如果角色没有着地，则不触发攀爬
        if (!m_controllerWrapper.IsGrounded)
        {
            return false;
        }

        // 检查是否有用户移动输入
        // if (!m_trajectoryGenerator.HasMovementInput())
        // {
        //     return false;
        // }

        // // 检查输入方向和角色朝向之间的角度是否在可接受的攀爬范围内
        // float inputAngleDelta = Vector3.Angle(transform.forward, m_trajectoryGenerator.LinearInputVector);
        // if (inputAngleDelta > 45f)
        // {
        //     return false;
        // }

        // 计算前进距离并确定是否高于执行攀爬所需的最小前进距离
        DesiredAdvance = (m_mxmAnimator.BodyVelocity * m_curConfig.DetectProbeAdvanceTime).magnitude;
        Advance = Mathf.Lerp(Advance, DesiredAdvance, 1f - Mathf.Exp(-m_advanceSmoothing));

        // if (Advance < m_minAdvance)
        // {
        //     return false;
        // }

        return true;
    }

    /// <summary>
    /// 碰撞/重力处理
    /// </summary>
    private void HandleCurrentVault()
    {
        if (m_rootMotionApplicator.EnableGravity == m_mxmAnimator.QueryUserTags(EUserTags.UserTag1))
        {
            m_rootMotionApplicator.EnableGravity = !m_rootMotionApplicator.EnableGravity;
        }

        if (m_controllerWrapper.CollisionEnabled == m_mxmAnimator.QueryUserTags(EUserTags.UserTag2))
        {
            m_controllerWrapper.CollisionEnabled = !m_controllerWrapper.CollisionEnabled;
        }

        if (m_mxmAnimator.IsEventComplete)
        {
            m_isVaulting = false;

            if (m_rootMotionApplicator != null)
                m_rootMotionApplicator.EnableGravity = true;

            if (m_controllerWrapper != null)
                m_controllerWrapper.CollisionEnabled = true;

            Advance = 0f;
        }

        return;
    }

    /// <summary>
    /// 在Scene视图中绘制射线检测可视化信息
    /// </summary>
    private void OnDrawGizmos()
    {
        if (m_controllerWrapper == null)
            return;

        Vector3 charForward = transform.forward;

        // 绘制射线检测可视化
        if (m_showRaycastVisualization)
        {
            DrawRaycastDebugVisualization(charForward);
            DrawVerticalRaycastDebugVisualization();
        }

        // 绘制攀爬接触点可视化
        if (m_showContactVisualization)
        {
            DrawContactPointsVisualization();
        }
    }

    /// <summary>
    /// 绘制射线检测的调试可视化信息
    /// </summary>
    /// <param name="charForward">角色前方向量</param>
    private void DrawRaycastDebugVisualization(Vector3 charForward)
    {
        if (m_lastRaycastDistance <= 0f)
            return;

        // 绘制射线起点
        Gizmos.color = m_sphereCastColor;
        Gizmos.DrawWireSphere(m_lastRaycastStart, m_curConfig.DetectProbeRadius);

        // 绘制射线路径
        if (m_lastRaycastHit)
        {
            DrawRaycastHitVisualization(charForward);
        }
        else
        {
            DrawRaycastMissVisualization(charForward);
        }
    }

    /// <summary>
    /// 绘制射线命中的可视化信息
    /// </summary>
    /// <param name="charForward">角色前方向量</param>
    private void DrawRaycastHitVisualization(Vector3 charForward)
    {
        // 射线命中 - 绘制到命中点的射线
        Gizmos.color = m_raycastHitColor;
        Gizmos.DrawLine(m_lastRaycastStart, m_lastRaycastHitPoint);

        // 绘制命中点
        Gizmos.DrawWireSphere(m_lastRaycastHitPoint, 0.15f);

        // 绘制命中点法线
        Gizmos.color = Color.white;
        Gizmos.DrawRay(m_lastRaycastHitPoint, Vector3.Cross(charForward, Vector3.up).normalized * 0.3f);
    }

    /// <summary>
    /// 绘制射线未命中的可视化信息
    /// </summary>
    /// <param name="charForward">角色前方向量</param>
    private void DrawRaycastMissVisualization(Vector3 charForward)
    {
        // 射线未命中 - 绘制完整射线路径
        Gizmos.color = m_raycastMissColor;
        Vector3 rayEnd = m_lastRaycastStart + charForward * m_lastRaycastDistance;
        Gizmos.DrawLine(m_lastRaycastStart, rayEnd);

        // 绘制射线终点
        Gizmos.DrawWireSphere(rayEnd, 0.1f);
    }

    /// <summary>
    /// 绘制垂直射线检测的调试可视化信息
    /// </summary>
    private void DrawVerticalRaycastDebugVisualization()
    {
        if (m_lastVerticalRaycastDistance <= 0f)
            return;

        // 绘制垂直射线起点
        Gizmos.color = m_sphereCastColor;
        Gizmos.DrawWireSphere(m_lastVerticalRaycastStart, m_curConfig.DetectProbeRadius);

        // 绘制垂直射线路径
        if (m_lastVerticalRaycastHit)
        {
            // 垂直射线命中 - 绘制到命中点的射线
            Gizmos.color = m_verticalRaycastHitColor;
            Gizmos.DrawLine(m_lastVerticalRaycastStart, m_lastVerticalRaycastHitPoint);

            // 绘制命中点
            Gizmos.DrawWireSphere(m_lastVerticalRaycastHitPoint, 0.15f);
        }
        else
        {
            // 垂直射线未命中 - 绘制完整射线路径
            Gizmos.color = m_verticalRaycastMissColor;
            Vector3 rayEnd = m_lastVerticalRaycastStart + Vector3.down * m_lastVerticalRaycastDistance;
            Gizmos.DrawLine(m_lastVerticalRaycastStart, rayEnd);

            // 绘制射线终点
            Gizmos.DrawWireSphere(rayEnd, 0.1f);
        }
    }

    /// <summary>
    /// 绘制攀爬接触点的可视化信息
    /// </summary>
    private void DrawContactPointsVisualization()
    {
        // 绘制第一个接触点
        if (m_hasContact1)
        {
            Gizmos.color = m_contact1Color;
            Gizmos.DrawWireSphere(m_debugContact1, m_contactPointSize);

            // 绘制接触点到角色的连线
            Gizmos.color = m_contact1Color * 0.5f;
            Gizmos.DrawLine(transform.position, m_debugContact1);
        }

        // 绘制第二个接触点（仅StepOver类型）
        if (m_hasContact2)
        {
            Gizmos.color = m_contact2Color;
            Gizmos.DrawWireSphere(m_debugContact2, m_contactPointSize);

            // 绘制接触点到角色的连线
            Gizmos.color = m_contact2Color * 0.5f;
            Gizmos.DrawLine(transform.position, m_debugContact2);

            // 绘制两个接触点之间的连线
            Gizmos.color = Color.white;
            Gizmos.DrawLine(m_debugContact1, m_debugContact2);
        }
    }

    /// <summary>
    /// 在Scene视图中绘制选中的射线检测可视化信息（更详细）
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (m_controllerWrapper == null)
            return;

        Vector3 charPos = transform.position;
        Vector3 charForward = transform.forward;

        // 绘制射线检测可视化
        if (m_showRaycastVisualization)
        {
            DrawRaycastDebugVisualization(charForward);
        }

        // 绘制攀爬接触点可视化
        if (m_showContactVisualization)
        {
            DrawContactPointsVisualization();
        }

        // 绘制射线检测文本标签
#if UNITY_EDITOR
        string debugText = "";
        if (m_lastRaycastDistance > 0f)
        {
            debugText += $"水平射线: {(m_lastRaycastHit ? "命中" : "未命中")}\n检测距离: {m_lastRaycastDistance:F2}m";
        }
        if (m_lastVerticalRaycastDistance > 0f)
        {
            if (debugText != "") debugText += "\n";
            debugText += $"垂直射线: {(m_lastVerticalRaycastHit ? "命中" : "未命中")}\n检测距离: {m_lastVerticalRaycastDistance:F2}m";
        }
        
        // 添加接触点信息
        if (m_hasContact1)
        {
            if (debugText != "") debugText += "\n";
            debugText += $"攀爬类型: {m_debugVaultType}\n接触点1: {m_debugContact1:F2}";
            if (m_hasContact2)
            {
                debugText += $"\n接触点2: {m_debugContact2:F2}";
            }
        }
        
        if (debugText != "")
        {
            UnityEditor.Handles.Label(charPos + Vector3.up * 2f, debugText);
        }
#endif
    }
}
