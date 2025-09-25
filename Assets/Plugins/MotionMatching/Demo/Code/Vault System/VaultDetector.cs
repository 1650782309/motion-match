// Copyright © 2017-2024 Vault Break Studios Pty Ltd

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MxM;

namespace MxMGameplay
{
    public class VaultDetector : MonoBehaviour
    {
        [SerializeField]
        private VaultDefinition[] m_vaultDefinitions = null;

        [SerializeField]
        private VaultDetectionConfig[] m_vaultConfigurations = null;

        [SerializeField]
        private float m_minStepUpDepth = 1f;

        //[SerializeField]
        //private float m_minStepOffDepth = 1f;

        [SerializeField]
        private LayerMask m_layerMask = new LayerMask();

        [SerializeField]
        private float m_minAdvance = 0.1f; //The minimum advance required to trigger a vault.

        [SerializeField]
        private float m_advanceSmoothing = 10f;

        [SerializeField]
        public float m_maxApproachAngle = 60f;

        [Header("Debug可视化")]
        [SerializeField] private bool m_showDebugGizmos = true;
        [SerializeField] private Color m_forwardRayColor = Color.red;
        [SerializeField] private Color m_verticalRayColor = Color.blue;
        [SerializeField] private Color m_hitPointColor = Color.green;

        private MxMAnimator m_mxmAnimator;
        private MxMRootMotionApplicator m_rootMotionApplicator;
        private GenericControllerWrapper m_controllerWrapper;
        private MxMTrajectoryGenerator m_trajectoryGenerator;

        private int m_vaultAnalysisIterations;

        private VaultDetectionConfig m_curConfig;

        private float m_minVaultRise;
        private float m_maxVaultRise;
        private float m_minVaultDepth;
        private float m_maxVaultDepth;
        private float m_minVaultDrop;
        private float m_maxVaultDrop;

        private bool m_isVaulting;

        // Debug 可视化变量
        private Vector3 m_debugForwardRayStart;
        private Vector3 m_debugForwardRayEnd;
        private Vector3 m_debugVerticalRayStart;
        private Vector3 m_debugVerticalRayEnd;
        private Vector3 m_debugHitPoint;
        private bool m_debugHasHit = false;

        public float Advance { get; set; }
        public float DesiredAdvance { get; set; }

        public void Awake()
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
        }

        public void OnEnable()
        {
            m_isVaulting = false;
        }

        public void Update()
        {
            //Check if we are already vaulting
            if (m_isVaulting)
            {
                HandleCurrentVault();
                return;
            }

            if (!CanVault())
            {
                m_debugHasHit = false;
                return;
            }
            else
            {
                Debug.Log("VaultDetector: 检测条件满足");
            }


            //We are going to use the transform position and forward direction a lot. Caching it here for perf
            Vector3 charPos = transform.position;
            Vector3 charForward = transform.forward;
            float approachAngle = 0f;

            //First we must fire a ray forward from the character, slightly above the minimum vault rise (aka the character controller minimum step)
            Vector3 probeStart = new Vector3(charPos.x, charPos.y +
                m_curConfig.DetectProbeRadius + m_minVaultRise,
                charPos.z);

            // 保存 Debug 信息
            m_debugForwardRayStart = probeStart;
            m_debugForwardRayEnd = probeStart + charForward * Advance;

            Ray forwardRay = new Ray(probeStart, charForward);

            if (Physics.SphereCast(forwardRay, m_curConfig.DetectProbeRadius, out RaycastHit forwardRayHit,
                   Advance, m_layerMask, QueryTriggerInteraction.Ignore))
            {
                if (m_showDebugGizmos)
                {
                    Debug.Log($"前方射线检测成功，距离: {forwardRayHit.distance:F2}");
                    Debug.Log($"前方命中点: {forwardRayHit.point}");
                }

                //If we hit something closer than the current advance, then we shorten the advance since we want the
                //first downward sphere case to hit the edge.
                if (forwardRayHit.distance < Advance)
                {
                    Advance = forwardRayHit.distance;
                    if (m_showDebugGizmos) Debug.Log($"调整前进距离为: {Advance:F2}");
                }

                Vector3 obstacleOrient = Vector3.ProjectOnPlane(forwardRayHit.normal, Vector3.up) * -1f;

                approachAngle = Vector3.SignedAngle(transform.forward, obstacleOrient, Vector3.up);

                if (m_showDebugGizmos)
                {
                    Debug.Log($"接近角度: {approachAngle:F2}, 最大允许角度: {m_maxApproachAngle:F2}");
                }

                //If we encounter an obstacle but at an angle above our max, we don't want to vault so return here.
                //QUESTION: What if the actual vault point (i.e. at the top of the obstacle is within the correct angle?
                if (Mathf.Abs(approachAngle) > m_maxApproachAngle)
                {
                    if (m_showDebugGizmos) Debug.Log($"❌ 接近角度太大，取消攀爬: {Mathf.Abs(approachAngle):F2} > {m_maxApproachAngle:F2}");
                    return;
                }

                if (m_showDebugGizmos) Debug.Log("✅ 前方检测通过，开始垂直检测");
            }
            else
            {
                if (m_showDebugGizmos) Debug.Log("❌ 前方射线检测失败，没有检测到障碍物");
                return; // 前方检测失败，直接返回
            }

            //Next we fire a ray vertically downward from the maximum vault rise to the maximum vault drop
            //NOTE: This does not take into consideration a roof or an overhang
            probeStart = transform.TransformPoint(new Vector3(0f, m_maxVaultRise, Advance));
            // probeStart.y += m_maxVaultRise;

            if (m_showDebugGizmos)
            {
                Debug.Log($"准备垂直射线检测，起点: {probeStart}");
                Debug.Log($"检测距离: {m_maxVaultRise + m_maxVaultDrop:F2}");
                Debug.Log($"Layer Mask: {m_layerMask.value}");
            }

            // 保存垂直射线 Debug 信息
            m_debugVerticalRayStart = probeStart;
            m_debugVerticalRayEnd = probeStart + Vector3.down * (m_maxVaultRise + m_maxVaultDrop);

            Ray probeRay = new Ray(probeStart, Vector3.down);
            if (Physics.SphereCast(probeRay, m_curConfig.DetectProbeRadius, out RaycastHit probeHit, m_maxVaultRise + m_maxVaultDrop,
                    m_layerMask, QueryTriggerInteraction.Ignore))
            {
                // 保存命中点信息
                m_debugHitPoint = probeHit.point;
                m_debugHasHit = true;

                if (m_showDebugGizmos)
                {
                    Debug.Log($"垂直射线检测成功，距离: {probeHit.distance:F2}");
                    Debug.Log($"命中点: {probeHit.point}");
                    Debug.Log($"最大攀爬高度: {m_maxVaultRise:F2}, 最小攀爬高度: {m_minVaultRise:F2}");
                }

                //Too high -> cancel the vault
                if (probeHit.distance < Mathf.Epsilon)
                {
                    if (m_showDebugGizmos) Debug.Log("❌ 障碍物太高，取消攀爬");
                    return;
                }

                //A 'vault over' or 'vault up' may have been detected if the probe distance is between the minimum and maximum vault rise
                if (probeHit.distance < (m_maxVaultRise - m_minVaultRise))
                {
                    if (m_showDebugGizmos) Debug.Log($"✅ 检测到可攀爬障碍物，距离: {probeHit.distance:F2}");

                    //A vault may have been detected

                    //Check if there is enough height to fit the character
                    if (!CheckCharacterHeightFit(probeHit.point, charForward))
                    {
                        if (m_showDebugGizmos) Debug.Log("❌ 角色高度检查失败，空间不足");
                        return;
                    }

                    if (m_showDebugGizmos) Debug.Log("✅ 角色高度检查通过，开始形状分析");

                    //Calculate the hit offset. This is the offset on a horizontal 2D plane between the start of the ray and the hit point

                    //Here we conduct a shape analysis of the vaultable object and store that data in a Vaultable Profile
                    VaultableProfile vaultable;
                    VaultShapeAnalysis(in probeHit, out vaultable);

                    if (m_showDebugGizmos)
                    {
                        Debug.Log($"形状分析结果: {vaultable.VaultType}");
                        Debug.Log($"分析参数 - Rise: {vaultable.Rise:F2}, Depth: {vaultable.Depth:F2}, Drop: {vaultable.Drop:F2}");
                    }

                    if (vaultable.VaultType == EVaultType.Invalid)
                    {
                        if (m_showDebugGizmos) Debug.Log("❌ 形状分析失败，障碍物类型无效");
                        return;
                    }

                    //Check for enough space on top of the object (for a step up)
                    if (vaultable.VaultType == EVaultType.StepUp && vaultable.Depth < m_minStepUpDepth)
                        return;

                    //Check object surface gradient (Assume ok for now)

                    //Select appropriate vault defenition
                    VaultDefinition vaultDef = ComputeBestVault(ref vaultable);

                    if (vaultDef == null)
                        return;

                    float facingAngle = transform.rotation.eulerAngles.y;

                    if (vaultDef.LineUpWithObstacle)
                    {
                        facingAngle += approachAngle;
                    }

                    //Pick contacts
                    vaultDef.EventDefinition.ClearContacts();

                    switch (vaultDef.OffsetMethod_Contact1)
                    {
                        case EVaultContactOffsetMethod.Offset: { vaultable.Contact1 += transform.TransformVector(vaultDef.Offset_Contact1); } break;
                        case EVaultContactOffsetMethod.DepthProportion: { vaultable.Contact1 += transform.TransformVector(vaultable.Depth * vaultDef.Offset_Contact1); } break;
                    }

                    vaultDef.EventDefinition.AddEventContact(vaultable.Contact1, facingAngle);

                    if (vaultable.VaultType == EVaultType.StepOver)
                    {
                        switch (vaultDef.OffsetMethod_Contact2)
                        {
                            case EVaultContactOffsetMethod.Offset: { vaultable.Contact2 += transform.TransformVector(vaultDef.Offset_Contact2); } break;
                            case EVaultContactOffsetMethod.DepthProportion: { vaultable.Contact2 += transform.TransformVector(vaultable.Depth * vaultDef.Offset_Contact2); } break;
                        }

                        vaultDef.EventDefinition.AddEventContact(vaultable.Contact2, facingAngle);
                    }

                    //Trigger event
                    m_mxmAnimator.BeginEvent(vaultDef.EventDefinition);

                    m_mxmAnimator.PostEventTrajectoryMode = EPostEventTrajectoryMode.Pause;

                    if (m_rootMotionApplicator != null)
                        m_rootMotionApplicator.EnableGravity = false;

                    if (vaultDef.DisableCollision && m_controllerWrapper != null)
                        m_controllerWrapper.CollisionEnabled = false;

                    m_isVaulting = true;
                }
                else //Detect a step off or vault over gap
                {
                    Vector3 flatHitPoint = new Vector3(probeHit.point.x, 0f, probeHit.point.z);
                    Vector3 flatProbePoint = new Vector3(probeStart.x, 0f, probeStart.z);

                    Vector3 dir = flatProbePoint - flatHitPoint; // The direction of the ledge

                    if (dir.sqrMagnitude > (m_curConfig.DetectProbeRadius * m_curConfig.DetectProbeRadius) / 4f)
                    {
                        //A step off may have occured

                        //Shape analysis 
                        Vector2 start2D = new Vector2(probeStart.x, probeStart.z);
                        Vector2 hit2D = new Vector2(probeHit.point.x, probeHit.point.z);

                        float hitOffset = Vector2.Distance(start2D, hit2D);

                        VaultableProfile vaultable;
                        VaultOffShapeAnalysis(in probeHit, out vaultable, hitOffset);

                        if (vaultable.VaultType == EVaultType.Invalid)
                            return;

                        VaultDefinition vaultDef = ComputeBestVault(ref vaultable);

                        if (vaultDef == null)
                            return;

                        float facingAngle = transform.rotation.eulerAngles.y;

                        //Pick contacts
                        vaultDef.EventDefinition.ClearContacts();

                        switch (vaultDef.OffsetMethod_Contact1)
                        {
                            case EVaultContactOffsetMethod.Offset: { vaultable.Contact1 += transform.TransformVector(vaultDef.Offset_Contact1); } break;
                            case EVaultContactOffsetMethod.DepthProportion: { vaultable.Contact1 += transform.TransformVector(vaultable.Depth * vaultDef.Offset_Contact1); } break;
                        }

                        vaultDef.EventDefinition.AddEventContact(vaultable.Contact1, facingAngle);

                        m_mxmAnimator.BeginEvent(vaultDef.EventDefinition);
                        m_mxmAnimator.PostEventTrajectoryMode = EPostEventTrajectoryMode.Pause;

                        //if (m_rootMotionApplicator != null)
                        //    m_rootMotionApplicator.EnableGravity = false;

                        //if (m_controllerWrapper != null)
                        //    m_controllerWrapper.CollisionEnabled = false;

                        m_isVaulting = true;
                    }
                }
            }
        }

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

        private bool CanVault()
        {
            //Do not trigger a vault if the character is not grounded
            if (!m_controllerWrapper.IsGrounded)
            {
                return false;
            }

            //Check that there is user movement input
            if (!m_trajectoryGenerator.HasMovementInput())
            {
                return false;
            }

            //Check that the angle beteen input and the character facing direction is within an acceptable range to vault
            float inputAngleDelta = Vector3.Angle(transform.forward, m_trajectoryGenerator.LinearInputVector);
            if (inputAngleDelta > 45f)
            {
                return false;
            }

            //Calculate Advance and determine if it higher than the minimum required advance to perform a vault
            DesiredAdvance = (m_mxmAnimator.BodyVelocity * m_curConfig.DetectProbeAdvanceTime).magnitude;
            Advance = Mathf.Lerp(Advance, DesiredAdvance, 1f - Mathf.Exp(-m_advanceSmoothing));
            if (Advance < m_minAdvance)
            {
                return false;
            }

            return true;
        }

        private bool CheckCharacterHeightFit(Vector3 a_fromPoint, Vector3 a_forward)
        {
            float radius = m_controllerWrapper.Radius;
            Vector3 fromPosition = a_fromPoint + (a_forward * radius * 2f) + (Vector3.up * radius * 1.1f);

            Ray upRay = new Ray(fromPosition, Vector3.up);
            RaycastHit rayHit;
            if (Physics.Raycast(upRay, out rayHit, m_controllerWrapper.Height, m_layerMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            return true;
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
        *  @brief This function is called to analyse the shape of a potentialy vaultable object. By 
        *  analysing the shape with raycasts, it's easy to then match the metrics of that shape to a 
        *  number of vaulable definitions with accompanying animations
        *  
        *  @param [in RaycastHit] a_rayHit - The raycast restuls for the ray that hit the vaultable edge
        *  @param [out VaultableProfile] a_vaultProfile - the container for all metrics of the shape analysis
        *  
        *  @Todo: Implement with Jobs and Burst
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

        private VaultDefinition ComputeBestVault(ref VaultableProfile a_vaultable)
        {
            if (m_showDebugGizmos)
            {
                Debug.Log($"=== 开始匹配攀爬定义 ===");
                Debug.Log($"障碍物类型: {a_vaultable.VaultType}");
                Debug.Log($"障碍物参数 - Rise: {a_vaultable.Rise:F2}, Depth: {a_vaultable.Depth:F2}, Drop: {a_vaultable.Drop:F2}");
                Debug.Log($"可用定义数量: {m_vaultDefinitions.Length}");
            }

            foreach (VaultDefinition vaultDef in m_vaultDefinitions)
            {
                if (m_showDebugGizmos)
                {
                    Debug.Log($"检查定义: {vaultDef.VaultType}");
                    Debug.Log($"  参数范围 - MinRise: {vaultDef.MinRise:F2}, MaxRise: {vaultDef.MaxRise:F2}");
                    Debug.Log($"  深度范围 - MinDepth: {vaultDef.MinDepth:F2}, MaxDepth: {vaultDef.MaxDepth:F2}");
                    Debug.Log($"  掉落范围 - MinDrop: {vaultDef.MinDrop:F2}, MaxDrop: {vaultDef.MaxDrop:F2}");
                }

                if (vaultDef.VaultType == a_vaultable.VaultType)
                {
                    if (m_showDebugGizmos)
                        Debug.Log($"  类型匹配: {vaultDef.VaultType}");

                    switch (vaultDef.VaultType)
                    {
                        case EVaultType.StepUp:
                            {
                                if (a_vaultable.Depth < vaultDef.MinDepth)
                                {
                                    if (m_showDebugGizmos) Debug.Log($"  深度不足: {a_vaultable.Depth:F2} < {vaultDef.MinDepth:F2}");
                                    continue;
                                }

                                if (a_vaultable.Rise < vaultDef.MinRise || a_vaultable.Rise > vaultDef.MaxRise)
                                {
                                    if (m_showDebugGizmos) Debug.Log($"  高度不匹配: {a_vaultable.Rise:F2} 不在 [{vaultDef.MinRise:F2}, {vaultDef.MaxRise:F2}] 范围内");
                                    continue;
                                }

                                if (m_showDebugGizmos) Debug.Log($"  ✅ StepUp 定义匹配成功!");
                            }
                            break;
                        case EVaultType.StepOver:
                            {
                                if (a_vaultable.Depth < vaultDef.MinDepth || a_vaultable.Depth > vaultDef.MaxDepth)
                                {
                                    if (m_showDebugGizmos) Debug.Log($"  深度不匹配: {a_vaultable.Depth:F2} 不在 [{vaultDef.MinDepth:F2}, {vaultDef.MaxDepth:F2}] 范围内");
                                    continue;
                                }

                                if (a_vaultable.Rise < vaultDef.MinRise || a_vaultable.Rise > vaultDef.MaxRise)
                                {
                                    if (m_showDebugGizmos) Debug.Log($"  高度不匹配: {a_vaultable.Rise:F2} 不在 [{vaultDef.MinRise:F2}, {vaultDef.MaxRise:F2}] 范围内");
                                    continue;
                                }

                                if (a_vaultable.Drop < vaultDef.MinDrop || a_vaultable.Drop > vaultDef.MaxDrop)
                                {
                                    if (m_showDebugGizmos) Debug.Log($"  掉落高度不匹配: {a_vaultable.Drop:F2} 不在 [{vaultDef.MinDrop:F2}, {vaultDef.MaxDrop:F2}] 范围内");
                                    continue;
                                }

                                if (m_showDebugGizmos) Debug.Log($"  ✅ StepOver 定义匹配成功!");
                            }
                            break;
                        case EVaultType.StepOff:
                            {
                                if (a_vaultable.Depth < vaultDef.MinDepth)
                                {
                                    if (m_showDebugGizmos) Debug.Log($"  深度不足: {a_vaultable.Depth:F2} < {vaultDef.MinDepth:F2}");
                                    continue;
                                }

                                if (a_vaultable.Drop < vaultDef.MinDrop || a_vaultable.Drop > vaultDef.MaxDrop)
                                {
                                    if (m_showDebugGizmos) Debug.Log($"  掉落高度不匹配: {a_vaultable.Drop:F2} 不在 [{vaultDef.MinDrop:F2}, {vaultDef.MaxDrop:F2}] 范围内");
                                    continue;
                                }

                                if (m_showDebugGizmos) Debug.Log($"  ✅ StepOff 定义匹配成功!");
                            }
                            break;
                    }

                    if (m_showDebugGizmos)
                        Debug.Log($"🎯 找到匹配的攀爬定义: {vaultDef.VaultType}");

                    return vaultDef;
                }
                else
                {
                    if (m_showDebugGizmos)
                        Debug.Log($"  类型不匹配: 需要 {a_vaultable.VaultType}, 当前是 {vaultDef.VaultType}");
                }
            }

            if (m_showDebugGizmos)
                Debug.Log("❌ 没有找到匹配的攀爬定义");

            return null;
        }

        //============================================================================================
        /**
        *  @brief 绘制可视化调试信息
        *         
        *********************************************************************************************/
        private void OnDrawGizmos()
        {
            if (!m_showDebugGizmos) return;

            // 绘制前方射线
            Gizmos.color = m_forwardRayColor;
            Gizmos.DrawLine(m_debugForwardRayStart, m_debugForwardRayEnd);
            Gizmos.DrawWireSphere(m_debugForwardRayStart, m_curConfig?.DetectProbeRadius ?? 0.1f);

            // 绘制垂直射线
            Gizmos.color = m_verticalRayColor;
            Gizmos.DrawLine(m_debugVerticalRayStart, m_debugVerticalRayEnd);
            Gizmos.DrawWireSphere(m_debugVerticalRayStart, m_curConfig?.DetectProbeRadius ?? 0.1f);

            // 绘制命中点
            if (m_debugHasHit)
            {
                Gizmos.color = m_hitPointColor;
                Gizmos.DrawWireSphere(m_debugHitPoint, 0.2f);
                Gizmos.DrawWireCube(m_debugHitPoint, Vector3.one * 0.1f);
            }

            // 绘制角色位置和朝向
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.DrawRay(transform.position, transform.forward * 2f);

            // 绘制前进距离
            Gizmos.color = Color.cyan;
            Vector3 advanceEnd = transform.position + transform.forward * Advance;
            Gizmos.DrawLine(transform.position, advanceEnd);
            Gizmos.DrawWireSphere(advanceEnd, 0.1f);

            // 绘制最小前进距离
            Gizmos.color = Color.magenta;
            Vector3 minAdvanceEnd = transform.position + transform.forward * m_minAdvance;
            Gizmos.DrawLine(transform.position, minAdvanceEnd);
            Gizmos.DrawWireSphere(minAdvanceEnd, 0.05f);
        }
    }
}