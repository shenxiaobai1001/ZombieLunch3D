using UnityEngine;

namespace DiasGames.ThirdPersonSystem.Shooter
{
    public enum CoverDirection { Right, Left}

    public class Cover : ThirdPersonAbility
    {
        [Header("Cast Parameters")]
        [SerializeField] private LayerMask m_CoverMask = (1 << 22);
        [SerializeField] private float m_CastDistance = 2f;
        [SerializeField] private float m_CastRadius = 0.35f;

        [Header("Positioning Parameters")]
        [SerializeField] private float m_PositioningDuration = 0.25f;
        [SerializeField] private float m_OffsetFromWall = 0.7f;

        [Space]
        [SerializeField] private float m_CapsuleHeightOnCrouch = 1f;

        [SerializeField] private string m_TurnState = "Crouch Cover.Turn";

        // Internal variables
        private RaycastHit m_CoverHit;
        private CoverDirection m_CoverDirection = CoverDirection.Right;

        // Smoth positioning
        Vector3 m_Position = Vector3.zero;
        Quaternion m_Rotation = Quaternion.identity;
        float m_PositionStep = 0;
        float m_RotationStep = 0;
        float m_Time = 0;

        private bool m_AimingCovered = false;
        private float m_StopCheckSideTime = 0;

        // Components
        CharacterShooterController m_Shooter;
        Transform coverHelper = null;

        public override void Initialize(ThirdPersonSystem mainSystem, AnimatorManager animatorManager, UnityInputManager inputManager)
        {
            base.Initialize(mainSystem, animatorManager, inputManager);

            m_Shooter = GetComponent<CharacterShooterController>();
        }

        /// <summary>
        /// Cast a capsule in the world to find a place to cover
        /// </summary>
        /// <returns></returns>
        private bool FoundCover()
        {
            Vector3 direction = Vector3.zero;

            if (Active)
            {
                direction = m_CoverDirection == CoverDirection.Right ? -transform.right : transform.right;
            }
            else 
            { 
                direction = Mathf.Approximately(m_InputManager.RelativeInput.magnitude, 0) ?
                    transform.forward : m_InputManager.RelativeInput.normalized;
            }

            Vector3 Start = transform.position + Vector3.up * m_CastRadius - direction*m_CastRadius;
            Vector3 End = Start + Vector3.up * (m_System.CapsuleOriginalHeight - m_CastRadius * 3);

            // Cast capsule to find a cover object
            if (Physics.CapsuleCast(Start, End, m_CastRadius, direction, out m_CoverHit, m_CastDistance, m_CoverMask, QueryTriggerInteraction.Ignore))
            {
                if (Vector3.Dot(direction, m_CoverHit.normal) < -0.3f)
                {
                    if(Active)
                        return true;
                    else // In case of this ability isn't active yet, check if the point found has enough space to allow hide
                    {
                        if (coverHelper == null)
                            coverHelper = new GameObject("Cover Helper").transform;

                        coverHelper.forward = m_CoverHit.normal;

                        Vector3 right01 = m_CoverHit.point + coverHelper.right * 0.5f;
                        Vector3 right02 = m_CoverHit.point + coverHelper.right * 1f;
                        Vector3 left01 = m_CoverHit.point - coverHelper.right * 0.5f;
                        Vector3 left02 = m_CoverHit.point - coverHelper.right * 1f;

                        // All conditions to check if object can hide character on cover
                        if (Physics.OverlapSphere(right02, 0.15f, m_CoverMask, QueryTriggerInteraction.Ignore).Length > 0)
                            return true;
                        else if (Physics.OverlapSphere(left02, 0.15f, m_CoverMask, QueryTriggerInteraction.Ignore).Length > 0)
                            return true;
                        else if (Physics.OverlapSphere(right01, 0.15f, m_CoverMask, QueryTriggerInteraction.Ignore).Length > 0 &&
                                 Physics.OverlapSphere(left01, 0.15f, m_CoverMask, QueryTriggerInteraction.Ignore).Length > 0)
                            return true;
                    }
                }
            }

            return false;
        }

        public override bool TryEnterAbility()
        {
            return FoundCover();
        }

        public override bool ForceEnterAbility()
        {
            if (m_System.ActiveAbility is RollAbility)
                return TryEnterAbility() && m_AnimatorManager.GetNormalizedTime() > 0.75f;

            return base.ForceEnterAbility();
        }

        public override void OnEnterAbility()
        {
            base.OnEnterAbility();

            m_Position = GetCoverPosition();
            m_Rotation = GetCoverRotation();

            CalculatePositionStep(m_Position);
            CalculateRotationStep(m_Rotation.eulerAngles.y);

            m_Time = 0;

            m_System.ChangeCapsuleSize(m_CapsuleHeightOnCrouch);
        }

        /// <summary>
        /// Calculate position step to make a smoothness positioning
        /// </summary>
        /// <param name="target"></param>
        private void CalculatePositionStep(Vector3 target)
        {
            m_PositionStep = Vector3.Distance(transform.position, target) / m_PositioningDuration;
        }

        /// <summary>
        /// Calculate rotation step to make a smothness positioning
        /// </summary>
        /// <param name="targetYDegree"></param>
        private void CalculateRotationStep(float targetYDegree)
        {
            m_RotationStep = Mathf.Abs(transform.rotation.eulerAngles.y - targetYDegree) / m_PositioningDuration;
        }

        public override void FixedUpdateAbility()
        {
            base.FixedUpdateAbility();

            // Check if a weapon is being used, in case of true, don't update movement parameters
            if (m_Shooter.SelectedWeapon != null)
            {
                if (m_Shooter.SelectedWeapon.State != WeaponState.Holst && m_System.IsZooming)
                {
                    m_System.RotateByCamera();
                    m_AnimatorManager.SetVerticallParameter(0);
                    m_AnimatorManager.SetHorizontalParameter(0);
                    m_AimingCovered = true;
                    m_System.ChangeCapsuleSize(m_System.CapsuleOriginalHeight);
                    return;
                }
            }

            // In case of character was aiming in the last frame, re-positionate it for cover position and rotation
            if (m_AimingCovered)
            {
                m_Time = 0;

                m_Position = transform.position;
                m_Rotation = GetCoverRotation();
                CalculateRotationStep(m_Rotation.eulerAngles.y);

                m_AimingCovered = false; // Reset aiming condition. Now character stops aiming
                m_System.ChangeCapsuleSize(m_CapsuleHeightOnCrouch);
            }

            // Case system ask to positionate character using smoothness
            if (m_Time < m_PositioningDuration)
            {
                transform.position = Vector3.MoveTowards(transform.position, m_Position, m_PositionStep * Time.fixedDeltaTime);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, m_Rotation, m_RotationStep * Time.fixedDeltaTime);
                
                // If turning, wait it to finish positionating
                if(m_CurrentStatePlaying == m_TurnState)
                {
                    if (m_AnimatorManager.HasFinishedAnimation(m_TurnState))
                    {
                        m_Time = m_PositioningDuration;
                        m_Position = transform.position;
                        m_Rotation = transform.rotation;
                    }

                    return;
                }

                m_AnimatorManager.SetForwardParameter(0);
                m_Time += Time.fixedDeltaTime;
                return;
            }

            // Character is covering and need to constantly find cover object
            if (FoundCover())
            {
                // If character find the end of object, don't turn, stop movement!
                if (FoundEndCover())
                    return;
                
                // Check if character was turning in the last frame
                if (m_CurrentStatePlaying == m_TurnState)
                {
                    SetState(m_EnterState, m_TransitionDuration);
                    m_Time = 0;

                    m_Position = GetCoverPosition();
                    m_Rotation = GetCoverRotation();

                    CalculatePositionStep(m_Position);
                    CalculateRotationStep(m_Rotation.eulerAngles.y);

                    return;
                }

                // Set character position and rotation on cover object
                SetPositionOnCover();

                // Update movement parameters and animator
                m_System.CalculateMoveVars();
                m_System.UpdateMovementAnimator();

                // Check condition to turn
                if(Vector3.Dot(m_InputManager.RelativeInput.normalized, transform.forward) < -0.1f)
                {
                    // Turn character to oposite direction
                    m_Rotation = transform.rotation * Quaternion.Euler(0, 180f, 0);
                    m_Time = 0;
                    m_PositionStep = 0;
                    m_RotationStep = 180;

                    m_CoverDirection = m_CoverDirection == CoverDirection.Right ? CoverDirection.Left : CoverDirection.Right;
                    SetState(m_TurnState, m_TransitionDuration);
                }
            }
        }

        /// <summary>
        /// Cast capsules to check if character found and edge of object and cannot turn
        /// </summary>
        /// <returns></returns>
        private bool FoundEndCover()
        {
            // If player is not asking to move, do nothing
            if (Mathf.Approximately(m_InputManager.RelativeInput.magnitude, 0))
                return false;

            // To avoid glitches, this time stops checking for a short time to allow character turn objects
            if (m_StopCheckSideTime > 0)
            {
                m_StopCheckSideTime -= Time.fixedDeltaTime;
                return false;
            }

            Vector3 forward = Vector3.Dot(m_InputManager.RelativeInput.normalized, transform.forward) >= 0 ? transform.forward : -transform.forward;

            Vector3 cp1 = transform.position + forward * m_System.m_Capsule.radius * 4 + Vector3.up * m_System.m_Capsule.radius;
            Vector3 cp2 = cp1 + Vector3.up * (m_CapsuleHeightOnCrouch - m_System.m_Capsule.radius * 3);
            RaycastHit sideHit;

            if(!Physics.CapsuleCast(cp1,cp2, m_System.m_Capsule.radius, -m_CoverHit.normal, m_OffsetFromWall * 2, m_CoverMask, QueryTriggerInteraction.Ignore))
            {
                // Adjust capsule points
                cp1 -= m_CoverHit.normal * (m_OffsetFromWall + m_System.m_Capsule.radius * 4);
                cp2 -= m_CoverHit.normal * (m_OffsetFromWall + m_System.m_Capsule.radius * 4);

                if (Physics.CapsuleCast(cp1, cp2, m_System.m_Capsule.radius, -forward, 1.25f, m_CoverMask, QueryTriggerInteraction.Ignore))
                {
                    m_StopCheckSideTime = 0.25f;
                    return false;
                }

                Vector3 start = cp1 + m_CoverHit.normal * m_System.m_Capsule.radius * 4;
                for (int i = 0; i < 10; i++)
                {
                    Vector3 startIterate = start - m_CoverHit.normal * i * 0.025f;

                    Debug.DrawLine(startIterate, startIterate - forward * 1.25f, Color.blue);

                    if(Physics.Raycast(startIterate, -forward, out sideHit, 1.25f, m_CoverMask, QueryTriggerInteraction.Ignore))
                    {
                        Vector3 hit = transform.InverseTransformPoint(sideHit.point);
                        hit.x = 0;
                        hit = transform.TransformPoint(hit);

                        m_Position = hit - sideHit.normal * m_System.m_Capsule.radius * 2f;
                        m_Position.y = transform.position.y;

                        m_Time = 0;
                        CalculatePositionStep(m_Position);
                        m_RotationStep = 0;
                        
                        break;
                    }
                }

                return true;
            }

            return false;
        }

        public override bool TryExitAbility()
        {
            return m_InputToEnter.WasPressed && Time.fixedTime - AbilityEnterFixedTime > 0.1f;
        }

        private void SetPositionOnCover()
        {
            transform.position = GetCoverPosition();
            transform.rotation = GetCoverRotation();
        }

        private Vector3 GetCoverPosition()
        {
            Vector3 targetPos = m_CoverHit.point + m_CoverHit.normal * m_OffsetFromWall;
            targetPos.y = transform.position.y;

            return targetPos;
        }

        private Quaternion GetCoverRotation()
        {
            Quaternion targetRot = Quaternion.identity;
           
            Vector3 normal = m_CoverHit.normal;
            normal.y = 0;
            normal.Normalize();

            float dot = Vector3.Dot(transform.right, normal);
            if (dot > 0)
            {
                targetRot = GetRotationFromDirection(normal) * Quaternion.Euler(0, -90f, 0);
                m_CoverDirection = CoverDirection.Right;
            }
            else
            {
                targetRot = GetRotationFromDirection(-normal) * Quaternion.Euler(0, -90f, 0);
                m_CoverDirection = CoverDirection.Left;
            }

            return targetRot;
        }

        private void Reset()
        {
            m_EnterState = "Crouch Cover.Movement";
            m_FinishOnAnimationEnd = false;
            m_UseRootMotion = true;
            m_AllowCameraZoom = true;
            m_TransitionDuration = 0.25f;

            m_UseInputStateToEnter = InputEnterType.ButtonDown;
            InputButton = InputReference.Interact;
        }
    }
}