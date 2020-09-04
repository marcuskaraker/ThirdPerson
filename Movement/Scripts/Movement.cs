using System.Collections;
using UnityEngine;

namespace MK.ThirdPerson
{
    public enum GroundCheckResult { InAir, OnSteep, OnGround, InWater }

    public class Movement : MonoBehaviour
    {
        [Header("General Settings")]
        public float speed = 7f;
        public float rotationLerpSpeed = 20f;
        public float jumpForce = 16f;
        public float slopeSteepness = 0.75f;
        [Tooltip("The gravity that will be applied to this controller. " +
            "If not zero, useGravity on the Rigidbody will be turned off.")]
        public Vector3 artificialGravity = new Vector3(0f, -30f, 0f);
        
        [Space]
        public bool moveInAir;

        [Tooltip("Will apply movement if stuck in air (on edges etc).")]
        public bool stuckCorrection = true;

        [Tooltip("Will apply movement away from steep terrain.")]
        public bool steepnessCorrection;

        [Header("Ground Check")]
        public LayerMask groundMask;
        public float groundCheckDist = 2f;
        public bool isSwimming;

        private Rigidbody rb;
        private Animator animator;
        private Collider movementCollider;

        private float defaultStaticFriction;
        private float defaultDynamicFriction;

        private Vector3 movement;
        private bool jump;
        private GroundCheckResult groundCheckResult;

        private Vector3 lastUpdatePos;
        private Vector3 lastGroundPos;
        private Vector3 lastHitSteepNormal;

        private const float STUCK_CORRECTION_DISTANCE = 0.001f;
        private const float STUCK_CORRECTION_SPEED = 0.5f;

        private const string ANIM_MOVEMENT_ID = "Movement";
        private const string ANIM_IS_GROUNDED_ID = "IsGrounded";
        private const string ANIM_IS_SWIMMING_ID = "IsSwimming";
        private const string ANIM_JUMP_ID = "Jump";

        /// <summary>
        /// The movement freeze flag. Above zero if frozen.
        /// </summary>
        public int FreezeFlag { get; private set; }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            animator = GetComponentInChildren<Animator>();
            movementCollider = GetComponent<Collider>();

            defaultStaticFriction = movementCollider.material.staticFriction;
            defaultDynamicFriction = movementCollider.material.dynamicFriction;

            if (artificialGravity != Vector3.zero)
            {
                rb.useGravity = false;
            }

            lastGroundPos = transform.position;
            lastUpdatePos = transform.position;
            lastHitSteepNormal = Vector3.zero;
        }

        private void Update()
        {
            groundCheckResult = GroundCheck();
            UpdateAnimationVariables();
        }

        private void FixedUpdate()
        {
            // Stuck Correction
            bool inAir = groundCheckResult == GroundCheckResult.InAir;
            if (stuckCorrection && inAir && Vector3.Distance(transform.position, lastUpdatePos) <= STUCK_CORRECTION_DISTANCE)
            {
                Vector3 correctionDirection = (transform.position - lastGroundPos).normalized;
                rb.AddForce(speed * STUCK_CORRECTION_SPEED * correctionDirection, ForceMode.VelocityChange);
            }

            // Apply gravity
            if (artificialGravity != Vector3.zero)
            {
                rb.AddForce(artificialGravity, ForceMode.Force);
            }

            // Movement
            bool onGroundOrWater = (groundCheckResult == GroundCheckResult.OnGround || groundCheckResult == GroundCheckResult.InWater);
            bool notOnSteep = groundCheckResult != GroundCheckResult.OnSteep;
            bool canMove = FreezeFlag <= 0 && (onGroundOrWater || (moveInAir && notOnSteep));
            if (canMove)
            {
                InternalMove();
            }

            // Jump
            animator?.SetBool(ANIM_IS_GROUNDED_ID, groundCheckResult != GroundCheckResult.InAir);

            if (jump && groundCheckResult == GroundCheckResult.OnGround)
            {
                rb.velocity = new Vector3(rb.velocity.x, jumpForce, rb.velocity.z);
                animator?.SetTrigger(ANIM_JUMP_ID);
                jump = false;
            }
            else
            {
                jump = false;
            }

            // Steepness Correction
            if (steepnessCorrection && groundCheckResult == GroundCheckResult.OnSteep)
            {
                rb.AddForce(lastHitSteepNormal * speed, ForceMode.VelocityChange);
            }

            lastUpdatePos = rb.position;
        }

        private void InternalMove()
        {
            if (!(movement.magnitude > 0))
            {
                return;
            }

            // Movement
            rb.velocity = new Vector3(movement.x * speed, rb.velocity.y, movement.z * speed);

            // Steepness Stuck Correction
            rb.velocity += lastHitSteepNormal;

            // Rotation
            Vector3 forwardDirection = rb.velocity.normalized;
            forwardDirection.y = 0f;
            Quaternion targetRotation = Quaternion.LookRotation(forwardDirection, Vector3.up);
            if (Vector3.Angle(transform.forward, rb.velocity.normalized) < 180)
            {            
                rb.rotation = Quaternion.Lerp(rb.rotation, targetRotation, Time.deltaTime * rotationLerpSpeed);
            }
            else
            {
                rb.rotation = targetRotation;
            }
        }

        /// <summary>
        /// Sets the movement direction.
        /// </summary>
        /// <param name="movement">The movement direction.</param>
        public void Move(Vector3 movement)
        {
            this.movement = movement;
        }

        /// <summary>
        /// Updates -some- animation variables associated with the movement.
        /// </summary>
        public void UpdateAnimationVariables()
        {
            // Animator
            if (animator)
            {
                animator.SetFloat(ANIM_MOVEMENT_ID, movement.magnitude);
                animator.SetBool(ANIM_IS_SWIMMING_ID, isSwimming);
            }
        }

        /// <summary>
        /// Raycasts downwards to check if there is ground.
        /// </summary>
        /// <returns>The state of the movement.</returns>
        public GroundCheckResult GroundCheck()
        {
            lastHitSteepNormal = Vector3.zero;

            if (isSwimming)
            {
                return GroundCheckResult.InWater;
            }

            RaycastHit hit;
            if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out hit, groundCheckDist, groundMask))
            {
                if (hit.normal.y <= slopeSteepness)
                {
                    // Normal to steep, stop movement
                    movementCollider.material.staticFriction = 0;
                    movementCollider.material.dynamicFriction = 0;
                    lastHitSteepNormal = hit.normal;
                    return GroundCheckResult.OnSteep;
                }
                else
                {
                    movementCollider.material.staticFriction = defaultStaticFriction;
                    movementCollider.material.dynamicFriction = defaultDynamicFriction;

                    lastGroundPos = transform.position;

                    return GroundCheckResult.OnGround;
                }
            }

            // In air.
            movementCollider.material.staticFriction = 0;
            movementCollider.material.dynamicFriction = 0;

            return GroundCheckResult.InAir;
        }

        /// <summary>
        /// Adds or removes one freezeflag.
        /// </summary>
        /// <param name="value">True: add a freezeglag. False: remove a freeze flag.</param>
        public void SetFreezeFlag(bool value)
        {
            FreezeFlag += value ? 1 : -1;
        }

        /// <summary>
        /// Resets the freezeFlag to 0. This should not need to be used.
        /// </summary>
        public void ResetFreezeFlag()
        {
            FreezeFlag = 0;
        }

        /// <summary>
        /// Tells the movement to jump next physics update. Imidiate return if frozen.
        /// </summary>
        public void Jump()
        {
            if (jump || FreezeFlag > 0)
            {
                return;
            }

            jump = true;
        }
    }
}