using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DiasGames.ThirdPersonSystem {

    public class ObjectDestroyer : MonoBehaviour
    {
        [SerializeField] private float lifeTime = 0.1f;
        [SerializeField] private bool m_OnlyDeactivate = true;

        private void OnEnable()
        {
            StartCoroutine(WaitToDestroy());
        }

        IEnumerator WaitToDestroy()
        {
            yield return new WaitForSeconds(lifeTime);

            if (m_OnlyDeactivate)
                gameObject.SetActive(false);
            else
                Destroy(gameObject);
        }
    }
}