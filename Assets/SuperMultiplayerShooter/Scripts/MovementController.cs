using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Visyde
{
    /// <summary>
    /// Movement Controller
    /// - A very simple 2d character controller that uses Rigidbody2D.
    /// - You can use your favorite character controller by replacing this component!
    ///   To make your custom controller work with this template, the controller must have a "ground" checker, a "jump" system, and 
    ///   an "isMine" boolean to check if this is ours or not.
    /// </summary>

    public class MovementController : MonoBehaviour
    {
        [Header("Settings:")]
        public float groundCheckerRadius;
        public Vector2 groundCheckerOffset;

        [Space]
        [Header("References:")]
        public Rigidbody2D rg;

        // The movement speed and jump force doesn't need to be set manually 
        // because they will be overriden by the character data anyway:
        [HideInInspector] public float moveSpeed;
        [HideInInspector] public float jumpForce;

        [HideInInspector] public bool isMine;
        [HideInInspector] public bool allowJump = true;
        [HideInInspector] public Vector2 movement;
        [HideInInspector] public bool isGrounded;

        // Internal:
        float inputX;

        void Update()
        {

            // Only enable movement controls if ours:
            if (isMine)
            {
                movement.x = isGrounded ? inputX : inputX != 0 ? inputX : movement.x;
                if (!isGrounded)
                {
                    movement.x = Mathf.MoveTowards(movement.x, 0, Time.deltaTime);
                }
            }

            // make this immovable if not ours:
            if (!isMine)
            {
                rg.mass = 1000;
                rg.gravityScale = 0;
            }

            // Check if grounded:
            allowJump = true;
            isGrounded = false;
            Collider2D[] cols = Physics2D.OverlapCircleAll(groundCheckerOffset + new Vector2(transform.position.x, transform.position.y), groundCheckerRadius);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i].CompareTag("JumpPad")){
                    allowJump = false;
                }
                if (cols[i].gameObject != gameObject)
                {
                    if (!isGrounded)
                    {
                        isGrounded = true;
                    }
                }
            }
        }

        void FixedUpdate()
        {
            if (isMine)
            {
                Vector2 veloc = rg.velocity;
                veloc.x = movement.x * (moveSpeed / 10);
                rg.velocity = veloc;
            }
        }

        // For local movement controlling: 
        public void InputMovement(float x)
        {
            // Movement input:
            inputX = x;
        }

        public void Jump()
        {
            Vector2 veloc = rg.velocity;
            veloc.y = jumpForce;
            rg.velocity = veloc;

            // Don't allow jumping after jump:
            allowJump = false;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + new Vector3(groundCheckerOffset.x, groundCheckerOffset.y), groundCheckerRadius);
        }
    }
}
