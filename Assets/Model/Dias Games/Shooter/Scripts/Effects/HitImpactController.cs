using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DiasGames.ThirdPersonSystem.Shooter
{
    public class HitImpactController : MonoBehaviour
    {
        [SerializeField] private List<Impact> m_ImpactEffects = new List<Impact>();
        [SerializeField] private int m_MaxObjects = 10;

        // Start is called before the first frame update
        private void Awake()
        {
            PoolDictionary = new Dictionary<string, Queue<GameObject>>();   // Starts a new pool

            foreach(Impact impact in m_ImpactEffects)
                CreatePool(impact, m_MaxObjects);
            
        }

        public void SpawnImpact(RaycastHit hit)
        {
            if (hit.collider == null)
                return;

            GameObject decal = Spawn(hit.collider.tag, hit.point + hit.normal * 0.01f, Quaternion.FromToRotation(Vector3.forward, hit.normal), hit.collider.transform);
            //decal.transform.forward = hit.normal;
        }

        #region ObjectPooler

        private Dictionary<string, Queue<GameObject>> PoolDictionary;       // Main pool controller
        
        /// <summary>
        /// Create pool for a specific type of object
        /// </summary>
        /// <param name="objectToPool"></param>
        /// <param name="amount"></param>
        public void CreatePool(Impact impact,int amount)
        {
            if (!PoolDictionary.ContainsKey(impact.tag))     // check if object already exists in the pool
            {
                // Add this type of object to the list
                Queue<GameObject> objectQueue = new Queue<GameObject>();

                for (int i = 0; i < amount; i++)
                {
                    GameObject obj = Instantiate(impact.m_Effect, transform);

                    obj.SetActive(false);
                    objectQueue.Enqueue(obj);
                }

                PoolDictionary.Add(impact.tag, objectQueue);         // Add this type of Object to the pool
            }
        }


        public GameObject Spawn(string key, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            //CreatePool(key, m_MaxObjects);     // Create pool for this gameobject, if it already exists, nothing will happen

            if (PoolDictionary.ContainsKey(key))
            {
                GameObject spawnedObject = PoolDictionary[key].Dequeue();

                spawnedObject.SetActive(false);
                spawnedObject.transform.position = position;
                spawnedObject.transform.rotation = rotation;
                spawnedObject.transform.localScale = Vector3.one;

                if (parent != null)
                {
                    spawnedObject.transform.parent = parent;

                    Vector3 lossy = spawnedObject.transform.lossyScale;
                    Vector3 local = spawnedObject.transform.localScale;

                    spawnedObject.transform.localScale = new Vector3(local.x / lossy.x, local.y / lossy.y, local.z / lossy.z);
                }

                spawnedObject.SetActive(true);
                PoolDictionary[key].Enqueue(spawnedObject);
                return spawnedObject;
            }

            return null;
        }



        public GameObject Spawn(string key)
        {
            return Spawn(key, Vector3.zero, Quaternion.identity);
        }


        public void Remove(GameObject target)
        {
            target.SetActive(false);
        }

        #endregion
    }

    [System.Serializable]
    public class Impact
    {
        public string tag = string.Empty;
        public GameObject m_Effect = null;
    }
}