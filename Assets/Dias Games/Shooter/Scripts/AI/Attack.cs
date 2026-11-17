using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace DiasGames.ThirdPersonSystem.AI
{
    public class Attack : AIBehaviour
    {
        [SerializeField] private AIBehaviour m_ChaseBehaviour;
        [SerializeField] private Collider damageToActivate;
        
        public override void Initialize(AIController controller, NavMeshAgent agent)
        {
            base.Initialize(controller, agent);
            m_Agent.velocity = Vector3.zero;

            damageToActivate.enabled = true;
        }

        public override void UpdateBehavior()
        {
            base.UpdateBehavior();

            m_Controller.SetDestination(m_Controller.Target.position);

            transform.LookAt(m_Controller.Target);
            transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);

            if (FinishedAnimation())
                m_Controller.StartAIBehaviour(m_ChaseBehaviour);
        }

        public override void Stop()
        {
            base.Stop();
            DisableDamage();
        }

        private void DisableDamage()
        {
            damageToActivate.enabled = false;
        }
    }
}