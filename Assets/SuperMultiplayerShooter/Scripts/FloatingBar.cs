using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Visyde
{
    /// <summary>
    /// Floating Bar
    /// - component for floating health bar which handles the health bar and player name display as well as the fire rate indicator
    /// </summary>

    public class FloatingBar : MonoBehaviour
    {
        public PlayerController owner;

        [Space]
        [Header("Settings:")]
        public float yOffset;
        public Color nameTextColorOwner = Color.white;
        public float colorFadeSpeed;

        [Header("References:")]
        public Text playerNameText;
        public Image fill;
        public Slider rateOfFireIndicator;
        public GameObject hpBarObj;
        public CanvasGroup cg;

        [HideInInspector] public GameManager gm;
        int lastHealth;

        void Start()
        {
            if (owner)
            {

                // Set text of name text to owner's name:
                playerNameText.text = owner.GetOwner().NickName;

                // Set name text color:
                playerNameText.color = owner.IsPlayerOurs() ? nameTextColorOwner : Color.white;

                // Show/Hide health bar:
                if (!owner.IsPlayerOurs() && !gm.showEnemyHealth)
                {
                    Destroy(hpBarObj);
                }
            }
        }

        void Update()
        {
            if (owner)
            {

                if (owner.isDead)
                {
                    Destroy(gameObject); // Destroy this when the owner dies.
                    return;
                }

                // Fill amount:
                if (fill)
                {
                    fill.fillAmount = (float)owner.health / (float)owner.characters[owner.curCharacter].data.maxHealth;
                }

                // Fire rate indicator:
                if (owner.curWeapon)
                {
                    rateOfFireIndicator.gameObject.SetActive(owner.curWeapon.curFR < 1 && owner.curWeapon.curAmmo > 0 && owner.IsPlayerOurs());
                    rateOfFireIndicator.value = owner.curWeapon.curFR;
                }

                if (owner.isDead)
                {
                    Destroy(gameObject);
                }
            }
            else
            {
                Destroy(gameObject); // Destroy this if the owner doesn't exist anymore.
            }
        }

        void LateUpdate()
        {
            if (owner)
            {
                // Positioning:
                transform.position = Camera.main.WorldToScreenPoint(owner.transform.position + Vector3.up * yOffset);
            }
        }
    }
}