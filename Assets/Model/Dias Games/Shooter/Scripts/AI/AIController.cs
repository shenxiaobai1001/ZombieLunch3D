using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace DiasGames.ThirdPersonSystem.AI
{
    public class AIController : MonoBehaviour
    {
        [Header("Sight Parameters")]
        [SerializeField] private LayerMask m_PlayerMask;
        [SerializeField] private LayerMask m_SightLayers;
        [SerializeField] private Transform m_LookOriginTransform;
        [SerializeField] private float m_SightFieldOfView = 90f;
        [SerializeField] private float m_SightRange = 30f;
        [SerializeField] private float m_DistanceToLosePlayer = 100f;

        [Header("Behaviour Parameters")]
        [SerializeField] private AIBehaviour m_InitialBehaviour;

        public NavMeshAgent Agent { get; private set; }
        public Animator AnimatorController { get; private set; }
        public AIBehaviour ActiveBehavior { get; private set; }

        public Transform Target { get; private set; }

        private float m_InitialStoppingDistance = 0.1f;

        private Health m_Health;
        private GameObject m_PlayerTarget;

        public Action OnFoundPlayer, OnLoosePlayer, OnDie;

        private void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            AnimatorController = GetComponent<Animator>();
            m_Health = GetComponent<Health>();
            m_PlayerTarget = GameObject.FindGameObjectWithTag("Player");

            m_InitialStoppingDistance = Agent.stoppingDistance;

            StartAIBehaviour(m_InitialBehaviour);
        }

        private void OnEnable()
        {
            if (m_Health != null)
            {
                m_Health.OnCharacterDie += Die;
                m_Health.OnCharacterDamage += SetTarget;
            }
        }
        private void OnDisable()
        {
            if (m_Health != null)
            {
                m_Health.OnCharacterDie -= Die;
                m_Health.OnCharacterDamage -= SetTarget;
            }
        }

        private void FixedUpdate()
        {
            if (!Agent.enabled)
                return;

            OnSight();

            if (ActiveBehavior != null)
                ActiveBehavior.UpdateBehavior();
        }

        private void Die()
        {
            //Agent.isStopped = true;
            Agent.enabled = false;

            OnDie?.Invoke();
        }

        /// <summary>
        /// Cast level to find player
        /// </summary>
        private void OnSight()
        {
            Collider[] colls = Physics.OverlapSphere(m_LookOriginTransform.position, m_SightRange, m_PlayerMask); // check player near to enemy

            if (colls.Length == 0) // Did not find player
                return;

            GameObject m_Player = null;
            for (int i = 0; i < colls.Length; i++)
            {
                if (colls[i].gameObject.tag == "Player")
                {
                    m_Player = colls[i].gameObject;
                    break;
                }
            }

            if (m_Player == null)
            {
                if(Target != null)
                {
                    if (Vector3.Distance(Target.position, transform.position) > m_DistanceToLosePlayer)
                    {
                        Target = null;
                        OnLoosePlayer?.Invoke();
                    }
                }

                return;
            }

            if (colls.Length > 0)
            {
                Vector3 lookTarget = m_Player.transform.position + Vector3.up; // Get player position
                Animator otherAnimator = m_Player.GetComponent<Animator>(); // Get player Animator

                // Check if player has an Aniamtor
                if (otherAnimator != null)
                    lookTarget = otherAnimator.GetBoneTransform(HumanBodyBones.Chest).position; // Set target to character chest


                Vector3 direction = (lookTarget - m_LookOriginTransform.position); // Shot a ray to character head
                                                                                   // Check if player is in the Field of View
                if (Vector3.Angle(direction, m_LookOriginTransform.forward) <= m_SightFieldOfView)
                {
                    RaycastHit hit; // hit infos
                    if (Physics.Raycast(m_LookOriginTransform.position, direction.normalized, out hit, m_SightRange, m_SightLayers, QueryTriggerInteraction.Collide))
                    {
                        if (hit.collider.tag == "Player") // Found player
                        {
                            Debug.DrawRay(m_LookOriginTransform.position, direction.normalized * m_SightRange);
                            SetSightParameters(hit);
                            return;
                        }
                    }

                    // sweep in case of single ray fail
                    for (int i = 0; i <= m_SightFieldOfView; i++)
                    {
                        float angle = (i * 2 + 90 - m_SightFieldOfView) * Mathf.Deg2Rad; // Change angle along sweep

                        // Local directions
                        direction.x = Mathf.Cos(angle);
                        direction.z = Mathf.Sin(angle);

                        direction = m_LookOriginTransform.TransformDirection(direction); // Transform direction to world space

                        direction.y = (lookTarget.y - m_LookOriginTransform.position.y) / Vector3.Distance(lookTarget, m_LookOriginTransform.position); // Get vertical direction

                        Debug.DrawRay(m_LookOriginTransform.position, direction * m_SightRange, Color.red); // Draw all sweep

                        RaycastHit hit2; // hit infos
                                         // Raycast to check player
                        if (Physics.Raycast(m_LookOriginTransform.position, direction, out hit2, m_SightRange, m_SightLayers, QueryTriggerInteraction.Collide))
                        {
                            if (hit2.collider.tag == "Player")
                            {
                                Debug.DrawRay(m_LookOriginTransform.position, direction.normalized * m_SightRange);
                                SetSightParameters(hit2);
                                return;
                            }
                        }
                    }
                }
            }

        }

        /// <summary>
        /// Set parameters after found player
        /// </summary>
        /// <param name="hit"></param>
        private void SetSightParameters(RaycastHit hit)
        {
            if (Target == null)
                OnFoundPlayer?.Invoke();

            Target = hit.transform;
        }

        private void SetTarget()
        {
            if (Target == null)
            {
                Target = m_PlayerTarget.transform;
                OnFoundPlayer?.Invoke();
            }
        }


        /// <summary>
        /// Starts a new behavior
        /// </summary>
        /// <param name="newBehavior"></param>
        public void StartAIBehaviour(AIBehaviour newBehavior)
        {
            if (ActiveBehavior != null)
                ActiveBehavior.Stop();

            ActiveBehavior = newBehavior;
            ResetStoppingDistance();
            ActiveBehavior.Initialize(this, Agent);
        }

        /// <summary>
        /// Set destination for AI
        /// </summary>
        /// <param name="target"></param>
        public void SetDestination(Vector3 target)
        {
            if (Agent.enabled)
                Agent.SetDestination(target);
        }


        public void SetStoppingDistance(float distance)
        {
            Agent.stoppingDistance = distance;
        }

        public void ResetStoppingDistance()
        {
            Agent.stoppingDistance = m_InitialStoppingDistance;
        }
    }
}