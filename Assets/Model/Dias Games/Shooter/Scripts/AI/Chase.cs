using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace DiasGames.ThirdPersonSystem.AI
{
    public class Chase : AIBehaviour
    {
        [SerializeField] private AIBehaviour m_PatrolBehaviour;
        [SerializeField] private AIBehaviour m_AttackBehaviour;
        [SerializeField] private float m_MinDistanceToAttack = 0.5f;

        public override void Initialize(AIController controller, NavMeshAgent agent)
        {
            base.Initialize(controller, agent);

            m_Controller.SetStoppingDistance(m_MinDistanceToAttack);
            m_Controller.SetDestination(m_Controller.Target.position);
        }

        public override void UpdateBehavior()
        {
            base.UpdateBehavior();

            if (m_Controller.Target == null)
            {
                m_Controller.StartAIBehaviour(m_PatrolBehaviour);
                return;
            }

            m_Controller.SetDestination(m_Controller.Target.position);

            if (m_Agent.remainingDistance <= m_MinDistanceToAttack)
                m_Controller.StartAIBehaviour(m_AttackBehaviour);
        }

        public override void Stop()
        {
            base.Stop();
            m_Controller.AnimatorController.ResetTrigger(m_TriggerParameter);
        }
    }
}