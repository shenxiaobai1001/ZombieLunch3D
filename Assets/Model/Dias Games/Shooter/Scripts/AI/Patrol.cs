using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace DiasGames.ThirdPersonSystem.AI
{
    public class Patrol : AIBehaviour
    {
        [SerializeField] private AIBehaviour m_PlayerDetectedBehaviour;
        [Tooltip("Bool Parameter")][SerializeField] private string m_IdleParameter = "Idle";
        [SerializeField] private Transform[] m_PatrolPoints;
        [SerializeField] private float m_StandTime = 3f;

        private int currentPoint = 0;
        private bool changingPatrol = false;
        
        public override void Initialize(AIController controller, NavMeshAgent agent)
        {
            base.Initialize(controller, agent);

            m_Controller.AnimatorController.SetBool(m_IdleParameter, false);

            if (m_PatrolPoints.Length > 0)
                m_Controller.SetDestination(m_PatrolPoints[0].position);
        }

        public override void UpdateBehavior()
        {
            base.UpdateBehavior();

            if (m_Controller.Target != null && m_PlayerDetectedBehaviour != null)
            {
                m_Controller.StartAIBehaviour(m_PlayerDetectedBehaviour);
                return;
            }

            if (m_PatrolPoints.Length == 0)
                return;

            if (m_Agent.remainingDistance <= m_Agent.stoppingDistance && !changingPatrol)
                StartCoroutine(ChangePatrolPoint());
        }


        public override void Stop()
        {
            base.Stop();

            changingPatrol = false;
            StopAllCoroutines();
        }


        IEnumerator ChangePatrolPoint()
        {
            if (!changingPatrol)
            {
                changingPatrol = true;
                m_Controller.AnimatorController.SetBool(m_IdleParameter, true);

                yield return new WaitForSeconds(m_StandTime);

                currentPoint++;
                if (currentPoint >= m_PatrolPoints.Length)
                    currentPoint = 0;

                m_Controller.SetDestination(m_PatrolPoints[currentPoint].position);

                m_Controller.AnimatorController.SetBool(m_IdleParameter, false);

                yield return new WaitForSeconds(0.5f);

                changingPatrol = false;
            }
        }
    }
}