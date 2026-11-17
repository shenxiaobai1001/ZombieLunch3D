using System;
using System.Collections.Generic;
using UnityEngine;
using DiasGames.ThirdPersonSystem;

namespace DiasGames.ThirdPersonSystem.AI
{
    public class EnemyHealthManager : MonoBehaviour
    {
        [SerializeField] private float m_MaxHealth = 100f;
        [SerializeField] private string m_DeathBool = "Die";

        private Animator m_Animator;
        
        public float HealthValue { get; private set; }
        public float MaximumHealth { get { return m_MaxHealth; } }

        public event Action OnDie, OnDamage;

        private List<GameObject> children = new List<GameObject>();

        private void Awake()
        {
            m_Animator = GetComponent<Animator>();

            HealthValue = MaximumHealth;
            GlobalEvents.AddEvent("Damage", Damage);

            Collider[] colls = GetComponentsInChildren<Collider>();
            foreach (Collider coll in colls)
                children.Add(coll.gameObject);
        }

        private void Damage(GameObject obj, object amount)
        {
            if (!children.Contains(obj))
                return;

            HealthValue -= (float)amount;
            OnDamage?.Invoke();

            if (HealthValue <= 0)
            {
                HealthValue = 0;
                Die();
            }

        }

        private void Die()
        {
            m_Animator.SetBool(m_DeathBool, true);
            OnDie?.Invoke();
        }

    }
}