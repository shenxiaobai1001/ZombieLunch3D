using UnityEngine;

namespace DiasGames.ThirdPersonSystem
{
    [CreateAssetMenu]
    public class Bullet : ScriptableObject
    {
        [SerializeField] private Sprite m_AmmoSprite;

        public Sprite AmmoSprite { get { return m_AmmoSprite; } }

        public int maximumCapacity = 10;
    }
}
