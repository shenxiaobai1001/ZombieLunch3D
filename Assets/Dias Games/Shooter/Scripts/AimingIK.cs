using UnityEngine;

namespace DiasGames.ThirdPersonSystem
{
    public class AimingIK : Modifier
    {
        private Transform m_SpineAligner, m_ExtraAligner;                   // Transform that helps system find forward direction
        private Vector3 m_LookTarget;                       // Point found by Raycast that character should look
        
        // Components
        private Camera m_Camera;                            // Main camera reference
        private Animator m_Animator;                        // Animator reference

        // Body look parameters
        private Vector3 m_LookTargetVelocity;                   // Velocity used on SmoothDamp method
        private Quaternion m_LocalTargetRotation;               // Target spine local rotation to aim to correct direction
        private float YawOffset, PitchOffset, AzimuteOffset;    // Yaw and Pitch offset sent by weapon
        private float bodyWeight = 0;                           // IK weight. 1 means to use IK, 0 to not use.
        private float weightVelocity = 0f;                      // Velocity to be used with SmoothDamp
        private float handWeightVelocity = 0f;                  // Velocity to be used with SmoothDamp

        // Hand IK parameters to put second hand in the gun
        private Transform handTransformTarget;              // Transform reference for non dominant hand
        private float handWeight = 0;                       // Weight of Hand IK
        private float lastHandTime = 0;                     // Helps system turn on or turn off hand IK

        private bool m_ApplyBodyIK = false;
        private bool m_ApplyHandIK = false;
        
        private void Awake()
        {
            m_Camera = Camera.main;
            m_Animator = GetComponent<Animator>();

            // Create new game object to be spine helper
            GameObject helper = new GameObject("Aiming Spine Helper");
            m_SpineAligner = helper.transform;
            m_SpineAligner.parent = m_Animator.GetBoneTransform(HumanBodyBones.Hips).transform;
            m_SpineAligner.localPosition = Vector3.zero;

        }


        /// <summary>
        /// Called by weapon to tell system to use body IK
        /// </summary>
        public void ApplyBodyIK(Vector3 rotOffset)
        {
            if(m_Animator.GetLayerWeight(AnimatorManager.UpperLayerIndex) < 0.5f)
            {
                StopBodyIK();
                return;
            }

            PitchOffset = rotOffset.x;
            YawOffset = rotOffset.y;
            AzimuteOffset = rotOffset.z;

            weightVelocity = (1 - bodyWeight) / 0.1f;

            m_ApplyBodyIK = true;
        }
        

        public void StopBodyIK()
        {
            m_ApplyBodyIK = false;
            weightVelocity = (0 - bodyWeight) / 0.1f;
        }


        /// <summary>
        /// Called by weapon to tell system to put non dominant hand on the gun
        /// </summary>
        public void ApplyHandIK(Transform target)
        {
            m_ApplyHandIK = true;
            handWeightVelocity = (1 - handWeight) / 0.35f;

            handTransformTarget = target;
        }

        public void StopHandIK()
        {
            m_ApplyHandIK = false;
            handWeightVelocity = (0 - handWeight) / 0.1f;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            HandIK(layerIndex);
            if(layerIndex == 0)
                BodyIK();
        }

        /// <summary>
        /// Calculate and set body rotation to make weapon aim forward to the aim point
        /// </summary>
        private void BodyIK()
        {
            if (Mathf.Approximately(bodyWeight, 0))
                return;

            RaycastHit hit = m_System.GetScreenHitPoint(m_Camera.transform, 150f, LayerMaskManager.IgnoreOnlyPlayer);             // Get aim point from crosshair
            Vector3 hitPoint = m_Camera.transform.position + m_Camera.transform.forward * 50f;                                     // Aim to a point 50 meters from camera forward
            if(hit.collider != null)
            {
                if (hit.distance > 50f)
                    hitPoint = hit.point;           // Get hit point only if it's far enough
            }

            m_LookTarget = Vector3.SmoothDamp(m_LookTarget, hitPoint, ref m_LookTargetVelocity, 0.05f);     // Set look target
            
            Vector3 direction = (m_LookTarget - m_SpineAligner.position).normalized;                        // Get direction to look
            m_SpineAligner.forward = direction;                                                             // Set spine direction

            // Rotates yaw and pitch offsets
            m_SpineAligner.rotation *= Quaternion.Euler(0, YawOffset, 0);                                   // Add yaw offset from weapon
            m_SpineAligner.localRotation *= Quaternion.Euler(PitchOffset, 0, AzimuteOffset);                // Add pitch & azimute ofsset from weapon

            Quaternion spineCurrentRotation = m_Animator.GetBoneTransform(HumanBodyBones.Spine).localRotation;      // Get current spine local rotation
            m_LocalTargetRotation = m_SpineAligner.localRotation;                                                   // Set Desired local target rotation

            m_LocalTargetRotation.y = Mathf.Clamp(m_LocalTargetRotation.y, 
                -60 * Mathf.Deg2Rad, 60 * Mathf.Deg2Rad);                   // Lock y rotation to avoid weird IK

            // Set bone rotation with pitch offset
            m_Animator.SetBoneLocalRotation(HumanBodyBones.Spine, Quaternion.Lerp(spineCurrentRotation, m_LocalTargetRotation, bodyWeight));
        }

        /// <summary>
        /// Handles hand Ik position and rotation according hand transform target
        /// </summary>
        private void HandIK(int layer)
        {
            if (Mathf.Approximately(handWeight, 0) || handTransformTarget == null || layer != AnimatorManager.LeftArm)
                return;

            m_Animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, handWeight);
            m_Animator.SetIKPosition(AvatarIKGoal.LeftHand, handTransformTarget.position);

            m_Animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, handWeight);
            m_Animator.SetIKRotation(AvatarIKGoal.LeftHand, handTransformTarget.rotation);
        }

        public override void FixedUpdateModifier()
        {
            base.FixedUpdateModifier();
            bodyWeight += weightVelocity * Time.fixedDeltaTime;
            bodyWeight = Mathf.Clamp01(bodyWeight);

            handWeight += handWeightVelocity * Time.fixedDeltaTime;
            handWeight = Mathf.Clamp(handWeight,0,1);
        }
    }
}
