using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Visyde
{
    /// <summary>
    /// Melee Weapon
    /// - A fixed melee weapon controller (doesn't require a weapon type)
    /// </summary>

    public class MeleeWeaponController : MonoBehaviour
    {
        [Header("Settings:")]
        public int damage = 50;
        public float attackSpeed = 4;
        public Vector2 attackRange;
        public float attackRangeYOffset;
        [Header("References:")]
        public Animator animator;
        public GameObject weaponGraphic;
        public AudioClip[] attackSFX;
        public AudioSource aus;

        [HideInInspector] public bool isAttacking;
        PlayerController ourPlayer;

        // Update is called once per frame
        void Update()
        {
            isAttacking = weaponGraphic.activeSelf;
        }

        /// <summary>
        /// Melee attack!!!
        /// </summary>
        public void Attack(bool mine, PlayerController player)
        {
            ourPlayer = player;
            if (mine)
            {
                Collider2D[] cols = Physics2D.OverlapBoxAll(transform.position + new Vector3((attackRange.x / 2) * (ourPlayer ? ourPlayer.transform.localScale.x : 1), attackRangeYOffset, 0), attackRange, 0);
                if (cols.Length > 0)
                {
                    for (int i = 0; i < cols.Length; i++)
                    {
                        if (cols[i].CompareTag("Player"))
                        {

                            // Get the "PlayerController" component of the affected gameObject:
                            PlayerController p = cols[i].GetComponent<PlayerController>();

                            // Don't hurt self and the invulnerable:
                            if (p.GetOwner() != ourPlayer.GetOwner() && !p.invulnerable)
                            {
                                p.photonView.RPC("Hurt", PhotonTargets.All, ourPlayer.GetOwner().NickName, damage, false);

                                // VFX
                                ourPlayer.pooler.Spawn("BodyHit", p.transform.position, Quaternion.identity);
                            }
                        }
                    }
                }
            }

            // Animation:
            animator.Play("MeleeAttack");

            // SFX:
            if (attackSFX.Length > 0)
            {
                aus.PlayOneShot(attackSFX[Random.Range(0, attackSFX.Length)]);
            }
        }

        void OnDrawGizmos()
        {
            // Melee range:
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(transform.position + new Vector3((attackRange.x / 2) * (ourPlayer ? ourPlayer.transform.localScale.x : 1), attackRangeYOffset, 0), attackRange);
        }
    }
}