using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DiasGames.ThirdPersonSystem
{
    public class TracerController : MonoBehaviour
    {
        [SerializeField] private float lifeTime = 0.1f;
        [Tooltip("When this property is set to true, the shhot line will fade instead to move until hit point. Useful for arrow trajectory.")]
        [SerializeField] private bool m_FadeOutTracer = false;

        private LineRenderer line;
        private Vector3 hit;
        private GradientAlphaKey[] alphaKeys;
        public float LifeTime { get { return lifeTime; } }
        
        private void Awake()
        {
            line = GetComponent<LineRenderer>();
        }

        private void OnEnable()
        {
            StartCoroutine(WaitToDestroy());
            alphaKeys = line.colorGradient.alphaKeys;
        }

        private void FixedUpdate()
        {
            if (m_FadeOutTracer)
            {
                for (int i = 0; i < alphaKeys.Length; i++)
                    alphaKeys[i].alpha = Mathf.Lerp(alphaKeys[i].alpha, 0, 0.1f);

                Gradient gradient = new Gradient();
                gradient.SetKeys(line.colorGradient.colorKeys, alphaKeys);
                line.colorGradient = gradient;

                line.SetPosition(1, hit);
                return;
            }

            line.SetPosition(1, Vector3.Lerp(line.GetPosition(1), hit, 0.4f));
            line.SetPosition(0, Vector3.Lerp(line.GetPosition(0), line.GetPosition(1), (1 / lifeTime) * Time.deltaTime));
        }

        public void SetTracerPoints(Vector3 firePoint, Vector3 hitPoint)
        {
            hit = hitPoint;
            Vector3 direction = hitPoint - firePoint;
            line.SetPosition(0, firePoint);
            line.SetPosition(1, hitPoint - direction.normalized * direction.magnitude * 0.5f);
        }

        IEnumerator WaitToDestroy()
        {
            yield return new WaitForSeconds(lifeTime);
            //ObjectPooler.Instance.Remove(gameObject);
            Destroy(gameObject);
        }
    }
}