using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace DiasGames.ThirdPersonSystem.AI
{
    public class Roar : AIBehaviour
    {
        [SerializeField] private AIBehaviour m_ChaseBehaviour;

        public override void Initialize(AIController controller, NavMeshAgent agent)
        {
            base.Initialize(controller, agent);
        }

        public override void UpdateBehavior()
        {
            base.UpdateBehavior();

            if (FinishedAnimation())
                m_Controller.StartAIBehaviour(m_ChaseBehaviour);
        }

        public override void Stop()
        {
            m_Controller.AnimatorController.ResetTrigger(m_TriggerParameter);
            base.Stop();
        }
    }
}