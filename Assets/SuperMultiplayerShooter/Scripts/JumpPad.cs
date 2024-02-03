using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon;

namespace Visyde
{
    /// <summary>
    /// Jump Pad
    /// - The jump pad component. This requires a trigger collider to work.
    /// </summary>

    public class JumpPad : PunBehaviour
    {
        public float force;                 // force amount
        public AudioClip launch;
        public AudioSource aus;

        void OnTriggerStay2D(Collider2D col)
        {
            if (col.CompareTag("Player"))
            {
                PlayerController p = col.GetComponent<PlayerController>();
                if (p)
                {
                    if (p.photonView.isMine && !p.isOnJumpPad)
                    {
                        // Let the player know that they're on a jump pad:
                        p.isOnJumpPad = true;

                        // Apply the force:
                        Vector2 veloc = p.movementController.rg.velocity;
                        veloc.y = force;
                        p.movementController.rg.velocity = veloc;
                        photonView.RPC("Jumped", PhotonTargets.All);
                    }
                }
            }
        }
        void OnTriggerExit2D(Collider2D col)
        {
            if (col.CompareTag("Player"))
            {
                PlayerController p = col.GetComponent<PlayerController>();
                if (p)
                {
                    if (p.photonView.isMine) p.isOnJumpPad = false;
                }
            }
        }

        [PunRPC]
        public void Jumped()
        {
            // Sound:
            aus.PlayOneShot(launch);
        }
    }
}
