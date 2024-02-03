using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon;

namespace Visyde
{
    /// <summary>
    /// Grenade Controller
    /// - is the primary component of a grenade game object. Unlike the projectile, grenades are spawned
    /// across the network and not locally so that any movement can be synced between clients.
    /// </summary>

    public class GrenadeController : PunBehaviour
    {
        [Header("Settings:")]
        public int damage;
        public float radius;
        public float delay;
        public AudioClip impactSound;

        [Space]
        [Header("References:")]
        public AudioSource aus;
        public GameObject explosionEffect;
        public GameObject graphic;
        public Rigidbody2D rg;

        // Network:
        Vector2 moveTo;
        float rotTo;

        // Use this for initialization
        void Start()
        {
            // Only the owner explodes:
            if (photonView.isMine)
            {
                // Force:
                Vector2 throwDir = (Vector2)photonView.instantiationData[0];
                rg.AddForce(throwDir, ForceMode2D.Impulse);

                Invoke("ExplodeCallFromOwner", delay);
            }
        }

        // Update is called once per frame
        void Update()
        {
            // Positioning, rotation etc.:
            if (photonView.isMine)
            {
                //moveTo = rg.position;
                //rotTo = rg.rotation;
            }
            else
            {

                //transform.position = Vector3.MoveTowards (transform.position, moveTo, Time.deltaTime * 10);
                //rg.rotation = Mathf.MoveTowards (rg.rotation, rotTo, Time.deltaTime * 400);
                rg.gravityScale = 0;
            }
        }

        void OnCollisionEnter2D(Collision2D col)
        {
            if (photonView.isMine && ((GameManager.instance.ourPlayer && col.transform.root != GameManager.instance.ourPlayer.transform) || !GameManager.instance.ourPlayer))
            {
                photonView.RPC("CollisionSound", PhotonTargets.All);
            }
        }

        [PunRPC]
        public void CollisionSound()
        {
            aus.PlayOneShot(impactSound);
        }

        void ExplodeCallFromOwner()
        {
            photonView.RPC("Explode", PhotonTargets.All);
        }

        [PunRPC]
        public void Explode()
        {
            // VFX:
            Instantiate(explosionEffect, transform.position, Quaternion.identity);

            // Disable collider and graphic:
            GetComponent<Collider2D>().enabled = false;
            graphic.SetActive(false);

            // Damaging:
            if (photonView.isMine)
            {
                Collider2D[] cols = Physics2D.OverlapCircleAll(transform.position, radius);
                for (int i = 0; i < cols.Length; i++)
                {
                    if (cols[i].CompareTag("Player"))
                    {
                        PlayerController p = cols[i].GetComponent<PlayerController>();
                        if (((p.photonView.owner == photonView.owner && GameManager.instance.allowHurtingSelf) || p.photonView.owner != photonView.owner) && !p.invulnerable)
                        {
                            if (!p.isDead)
                            {
                                Vector2 grPos = new Vector2(transform.position.x, transform.position.y);
                                RaycastHit2D[] hits = Physics2D.RaycastAll(grPos, new Vector2(cols[i].transform.position.x, cols[i].transform.position.y) - grPos, radius);
                                RaycastHit2D hit = new RaycastHit2D();
                                for (int h = 0; h < hits.Length; h++)
                                {
                                    if (hits[h].collider.gameObject == cols[i].gameObject)
                                    {
                                        hit = hits[h];
                                        // Calculate the damage based on distance:
                                        int finalDamage = Mathf.RoundToInt(damage * (1 - ((transform.position - new Vector3(hit.point.x, hit.point.y)).magnitude / radius)));
                                        // Apply damage:
                                        p.photonView.RPC("Hurt", PhotonTargets.AllBuffered, (string)photonView.instantiationData[1], finalDamage, false);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }

                // Destroy:
                PhotonNetwork.Destroy(photonView);
            }
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.isWriting && photonView.isMine)
            {
                // Send position over network
                //stream.SendNext (moveTo);
                //stream.SendNext (rotTo);
            }
            else if (stream.isReading)
            {
                // Receive positions
                //moveTo = (Vector2)stream.ReceiveNext();
                //rotTo = (float)stream.ReceiveNext ();
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1, 0, 0, 0.3f);
            Gizmos.DrawSphere(transform.position, radius);
        }
    }
}
