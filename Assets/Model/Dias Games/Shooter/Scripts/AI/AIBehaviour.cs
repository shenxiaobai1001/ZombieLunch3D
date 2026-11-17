using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace DiasGames.ThirdPersonSystem.AI
{
    public abstract class AIBehaviour : MonoBehaviour
    {
        [SerializeField] protected string m_TriggerParameter = string.Empty;
        [SerializeField] protected float m_MoveSpeed = 1f;

        protected NavMeshAgent m_Agent;
        protected AIController m_Controller;

        public string MoveState { get { return m_TriggerParameter; } }

        protected bool IsActive = false;

        private float startedTime = 0;
        private Animator m_Animator;

        public virtual void Initialize(AIController controller, NavMeshAgent agent)
        {
            m_Animator = GetComponent<Animator>();

            IsActive = true;
            m_Agent = agent;
            m_Controller = controller;

            m_Controller.AnimatorController.SetTrigger(m_TriggerParameter);
            m_Agent.speed = m_MoveSpeed;

            startedTime = Time.fixedTime;
        }

        public virtual void UpdateBehavior()
        {
            if (m_Animator.applyRootMotion)
            {
                m_Agent.updatePosition = false;
                m_Agent.nextPosition = transform.position + m_Animator.deltaPosition;
            }
        }

        public virtual void Stop()
        {
            IsActive = false;
        }

        protected bool FinishedAnimation()
        {
            if (Time.fixedTime - startedTime < 1f)
                return false;

            return (m_Controller.AnimatorController.GetCurrentAnimatorStateInfo(0).normalizedTime % 1 >= 0.8f);
        }
    }
}