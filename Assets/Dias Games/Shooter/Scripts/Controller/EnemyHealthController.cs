using UnityEngine;
using UnityEngine.UI;


namespace DiasGames.ThirdPersonSystem.Shooter
{
    public class EnemyHealthController : MonoBehaviour
    {
        public float Health = 50;
        public Image healthBar;

        private float initialHealth = 0;

        // Start is called before the first frame update
        void Awake()
        {
            initialHealth = Health;
            GlobalEvents.AddEvent("Damage", ReceiveDamage);
        }

        private void ReceiveDamage(GameObject obj, object amount)
        {
            if (obj != gameObject)
                return;

            Health -= (float)amount;

            healthBar.fillAmount = Health / initialHealth;

            if (Health <= 0)
                gameObject.SetActive(false);
        }
    }
}