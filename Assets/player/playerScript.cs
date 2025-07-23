using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class playerScript : MonoBehaviour
{
    /* Machine Machine Variables */
    public float shrinkSpeed = 0.8f;
    public float growSpeed = 0.8f;

    /* All Machine Tags */
    private bool isInShrinker = false;
    private bool isInGrower = false;

    /* Maximum and Minimum Scale Variables */
    public float minimumScale = 0.1f; // Note altering these values in runtime will have no effect, they are implemented in Awake()
    public float maximumScale = 10f;
    private Vector3 minScale;
    private Vector3 maxScale;

    /* ========== Movement Variables ========== */
    /* ==== State Variables ==== */
    public enum MovementState {
        Grounded,  // On ground, can jump
        Jumping,   // Moving upward after jumping
        Falling    // Moving downward (can be after jump or falling off ledge)
    }
    [Header("Movement State")]
    [SerializeField] private MovementState currentState;
    private bool wasGrounded; // for detecting exact frame left the ground
    [SerializeField] private float groundCheckDistance = 0.05f;
    [SerializeField] private LayerMask groundLayer;
    private Vector2 groundCheckSize;

    /* ==== Movement Settings ==== */
    [Header("Movement Settings")]
    [Header("Horizontal Movement Settings")]
    /* Public Movement Variables */
    [Header("Horizontal Movement Settings")]
    [Tooltip("Maximum horizontal movement speed (in units per second)")]
    public float maxSpeed = 10f;
    [Tooltip("Ground acceleration (how quickly you reach max speed on ground)")]
    public float groundAccel = 50f;
    [Tooltip("Air acceleration (how quickly you can change direction in air)")]
    public float airAccel = 25f;
    [Tooltip("Ground deceleration (how quickly you stop on ground)")]
    public float groundDecel = 60f;
    [Tooltip("Air deceleration (how quickly you slow down in air)")]
    public float airDecel = 20f;
    
    [Header("Vertical Movement Settings")]
    [Tooltip("Base upward force when jumping")]
    public float jumpForce = 12f;  // Increased from 8f for higher jumps
    [Tooltip("Gravity multiplier when falling")]
    public float fallMultiplier = 3f;  // Increased for faster falling
    [Tooltip("Gravity multiplier when jumping but not holding the button")]
    public float lowJumpMultiplier = 2f;  // Increased for faster deceleration
    [Tooltip("Multiplier for extra height when holding jump button"), Range(1f, 2f)]
    public float highJumpMultiplier = 1.25f;
    [Tooltip("Time in seconds after leaving a platform when you can still jump")]
    public float coyoteTime = 0.1f;
    [Tooltip("Time in seconds before hitting the ground when jump input will be buffered")]
    public float jumpBufferTime = 0.1f;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    /* Private Movement Variables */
    [SerializeField] private Vector2 velocity = Vector2.zero; // Current Velocity
    private Rigidbody2D rb; // Rigidbody Component
    private BoxCollider2D boxCollider; // BoxCollider Component
    

    void Awake() {
        /* Gets Rigidbody and BoxCollider Components */
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        /* Permenantly Prevent player rotation */
        rb.freezeRotation = true;
        /* Sets minimum and maximum scale VECTORS based on minimum and maximum scale VALUES, leaving Z axis at 1f (not used) */
        minScale = new Vector3(minimumScale, minimumScale, 1f);
        maxScale = new Vector3(maximumScale, maximumScale, 1f);
        /* Sets ground check size based on BoxCollider size */
        groundCheckSize = boxCollider.size;
    }

    void Update()
    {
        /* update ground check size based on Player Scale */
        groundCheckSize = boxCollider.size;
        /* ==== Machine Logic ==== */
        // If in both machines at once, do nothing (maintain current scale)
        if (isInShrinker && isInGrower) {
            return;
        }
        
        // Handle shrinking (only if not in grower)
        if (isInShrinker) {
            // Only shrink if not at minimum scale
            if (transform.localScale.sqrMagnitude > minScale.sqrMagnitude * 1.1f) {
                // Calculate new scale, ensuring it doesn't go below minScale
                Vector3 newScale = transform.localScale * (1 - shrinkSpeed * Time.deltaTime);
                transform.localScale = Vector3.Max(newScale, minScale);
            }
        }
        // Handle growing (only if not in shrinker)
        else if (isInGrower) {
            // Only grow if not at maximum scale and there's space to grow
            if (transform.localScale.sqrMagnitude < maxScale.sqrMagnitude * 0.9f) {
                // Calculate potential new scale
                Vector3 potentialScale = transform.localScale * (1 + growSpeed * Time.deltaTime);
                potentialScale = Vector3.Min(potentialScale, maxScale);
                
                // Check if there's space to grow
                if (CanGrowToScale(potentialScale)) {
                    transform.localScale = potentialScale;
                }
            }
        }
    }
    /* Fixed Update for Physics based Changes */
    void FixedUpdate() {
        /* Updates the player's state */
        determineState();
        /* Handles Movement */
        handleMovement();
        
        // Apply terminal velocity (scaled from Celeste's -300)
        float terminalVelocity = -80f; // Scaled from Celeste's -300 (-300/30*8)
        if (rb.velocity.y < terminalVelocity) {
            rb.velocity = new Vector2(rb.velocity.x, terminalVelocity);
        }
    }
    /* ========== TRIGGER AND COLLISION FUNCTIONS ========== */
    // Enter Trigger for all objects
    void OnTriggerEnter2D(Collider2D col) {
        // Set the appropriate flag based on which machine we entered
        if (col.CompareTag("Shrinker")) {
            isInShrinker = true;
        }
        if (col.CompareTag("Grower")) {
            isInGrower = true;
        }
    }
    // Exit Trigger for all objects
    void OnTriggerExit2D(Collider2D col) {
        // Machine exit trigger Logic
        if (col.CompareTag("Shrinker")) {
            isInShrinker = false;
        }
        if (col.CompareTag("Grower")) {
            isInGrower = false;
        }
    }
    void OnCollisionEnter2D(Collision2D col) {
        if (col.gameObject.CompareTag("Spike")) {
            resetScene();
        }
    }
    /* ========== STATE CONTROL FUNCTIONS ========== */
    void determineState() {
        bool isGrounded = getIsGrounded();
        
        // Update state based on vertical velocity and ground check
        if (isGrounded) {
            currentState = MovementState.Grounded;
        } 
        else if (rb.velocity.y > 0.1f) {
            currentState = MovementState.Jumping;
        } 
        else {
            currentState = MovementState.Falling;
        }
        
        // Reset coyote time when landing (not grounded last frame but grounded this frame)
        if (!wasGrounded && isGrounded) {
            coyoteTimeCounter = 0f;
        }
        wasGrounded = isGrounded; // Update wasGrounded to current state AFTER its implementation
    }
    bool getIsGrounded() {
        // Calculate the origin point (bottom center of collider)
        Vector2 origin = boxCollider.bounds.center;
        origin.y = boxCollider.bounds.min.y + groundCheckDistance * 0.5f; // Start slightly above the bottom
        
        // Calculate the size (full width, groundCheckDistance height)
        Vector2 size = new Vector2(
            boxCollider.bounds.size.x,  // player width
            groundCheckDistance                 // Height of the check area
        );
        
        // Cast the box downward
        RaycastHit2D hit = Physics2D.BoxCast(
            origin,
            size,
            0f,                // No rotation
            Vector2.down,      // Direction
            groundCheckDistance * 0.6f, // Only check within this distance
            groundLayer
        );
        
        return hit.collider != null; // Return true if we hit something (groundLayer)
    }
    /* ========== MOVEMENT FUNCTIONS ========== */
    void handleMovement() {
        handleHorizontalMovement();
        handleVerticleMovement();
    }
    void handleVerticleMovement() {
        handleJumpInput();
        handleGravity();
    }
    void handleHorizontalMovement() {
        float targetSpeed = getHorizontalInput() * maxSpeed;
        float accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? groundAccel : groundDecel;
        
        // Apply acceleration or deceleration based on input
        if (currentState != MovementState.Grounded) {
            accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? airAccel : airDecel;
        }
        
        // Calculate force with acceleration and direction
        float speedDiff = targetSpeed - rb.velocity.x;
        float movementForce = Mathf.Pow(Mathf.Abs(speedDiff) * accelRate, 0.8f) * Mathf.Sign(speedDiff);
        
        // Apply the force
        rb.AddForce(Vector2.right * movementForce);
        
        // Clamp velocity
        if (Mathf.Abs(rb.velocity.x) > maxSpeed) {
            rb.velocity = new Vector2(Mathf.Sign(rb.velocity.x) * maxSpeed, rb.velocity.y);
        }
    }
    /* ========== JUMP FUNCTIONS ========== */
    void handleJumpInput() {
        updateCoyoteTime();
        updateJumpBuffer();
        
        if (canJump()) {
            performJump();
        }
        
        if (shouldReduceJumpHeight()) {
            reduceJumpHeight();
        }
    }
    
    void updateCoyoteTime() {
        coyoteTimeCounter = currentState == MovementState.Grounded ? coyoteTime : coyoteTimeCounter - Time.deltaTime; // coyote time counter is reset if we are grounded, otherwise it decrements
    }
    
    void updateJumpBuffer() {
        // Check for jump input in both Update and FixedUpdate for better responsiveness
        if (Input.GetButtonDown("Jump") || Input.GetButton("Jump")) {
            jumpBufferCounter = jumpBufferTime;
        } else if (jumpBufferCounter > 0) {
            jumpBufferCounter -= Time.deltaTime;
        }
    }
    
    bool canJump() {
        // Can jump if we're grounded or in coyote time
        bool canJumpFromState = currentState == MovementState.Grounded || (currentState == MovementState.Falling && coyoteTimeCounter > 0);
        // Can jump if we have a buffered jump input and are in a jumpable state
        return jumpBufferCounter > 0f && canJumpFromState;
    }
    
    void performJump() {
        // Apply jump force with potential high jump boost
        float calculatedJumpForce = jumpForce;
        if (Input.GetButton("Jump")) {
            calculatedJumpForce *= highJumpMultiplier;
        }
        // Cancel any downward velocity before jumping for more consistent height
        if (rb.velocity.y < 0) {
            rb.velocity = new Vector2(rb.velocity.x, 0);
        }
        rb.AddForce(Vector2.up * calculatedJumpForce, ForceMode2D.Impulse);
        jumpBufferCounter = 0f;
        currentState = MovementState.Jumping;
        
        // Optional: Play jump sound/effect here
        // AudioManager.Instance.Play("Jump");
    }
    
    bool shouldReduceJumpHeight() {
        return Input.GetButtonUp("Jump") && rb.velocity.y > 0f;
    }
    
    void reduceJumpHeight() {
        rb.velocity = new Vector2(rb.velocity.x, rb.velocity.y * 0.5f);
        coyoteTimeCounter = 0f;
    }
    
    void handleGravity() {
        // Skip gravity if we're in a cutscene or paused
        // if (GameManager.Instance.IsPaused) return;
        
        // Apply gravity based on current state
        switch (currentState) {
            case MovementState.Jumping:
                // Apply stronger gravity when moving upward but not holding jump
                if (isRisingAndNotHoldingJump()) {
                    rb.velocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
                }
                // Always apply some additional gravity during jump for snappier feel
                rb.velocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime * 0.5f;
                break;
                
            case MovementState.Falling:
                // Apply full fall gravity
                rb.velocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
                break;
                
            case MovementState.Grounded:
                // Apply slight downward force for better ground stickiness
                if (rb.velocity.y < 0) {
                    rb.velocity = new Vector2(rb.velocity.x, -2f);  // Slightly stronger ground stick
                }
                break;
        }
    }
    
    bool isRisingAndNotHoldingJump() {
        return rb.velocity.y > 0 && !Input.GetButton("Jump");
    }
    
    void applyFallGravity() {
        rb.velocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.deltaTime;
    }
    
    void applyLowJumpGravity() {
        rb.velocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.deltaTime;
    }
    /* ====== CALCULATION FUNCTIONS ====== */
    Vector2 clampVelocity(Vector2 velocity) {
        return new Vector2(Mathf.Clamp(velocity.x, -maxSpeed, maxSpeed), Mathf.Clamp(velocity.y, -maxSpeed, maxSpeed));
    }
    void applyMovementForce(float movementForce) {
        rb.AddForce(Vector2.right * movementForce, ForceMode2D.Force);
    }
    float getHorizontalInput() {
        /* Raw Input (-1/0/1) */
        return Input.GetAxisRaw("Horizontal");
    }
    float getAcceleration() {
        /* State based Acceleration Calculation */
        return currentState == MovementState.Grounded ? groundAccel : airAccel;
    }
    float getDeceleration() {
        /* State based Deceleration Calculation */
        return currentState == MovementState.Grounded ? groundDecel : airDecel;
    }
    float getActiveAccel(float horizontalInput) {
        /* Only Accelerate if Input is greater than 0.01f (to prevent floating point errors) */
        if (Mathf.Abs(horizontalInput) > 0.01f) {
            return getAcceleration();
        } else {
            return getDeceleration();
        }
    }
    float getTargetSpeed(float horizontalInput) {
        return horizontalInput * maxSpeed;
    }
    float getSpeedDiff(float targetSpeed) {
        return targetSpeed - rb.velocity.x;
    }
    
    bool CanGrowToScale(Vector3 targetScale) {
        // Calculate the size increase
        Vector2 sizeIncrease = (Vector2)(targetScale - transform.localScale);
        
        // Calculate the box size for overlap check (slightly smaller than player's collider)
        Vector2 checkSize = boxCollider.size * 0.9f;
        
        // Check all four directions for potential wall collisions
        // You might need to adjust the distance based on your game's scale
        float checkDistance = 0.1f;
        
        // Check for walls in all four directions
        bool canGrowRight = !Physics2D.BoxCast(
            (Vector2)transform.position + Vector2.right * (boxCollider.size.x * 0.5f + checkDistance * 0.5f),
            new Vector2(checkDistance, checkSize.y * 0.9f),
            0f, Vector2.zero, 0f, groundLayer);
            
        bool canGrowLeft = !Physics2D.BoxCast(
            (Vector2)transform.position + Vector2.left * (boxCollider.size.x * 0.5f + checkDistance * 0.5f),
            new Vector2(checkDistance, checkSize.y * 0.9f),
            0f, Vector2.zero, 0f, groundLayer);
            
        bool canGrowUp = !Physics2D.BoxCast(
            (Vector2)transform.position + Vector2.up * (boxCollider.size.y * 0.5f + checkDistance * 0.5f),
            new Vector2(checkSize.x * 0.9f, checkDistance),
            0f, Vector2.zero, 0f, groundLayer);
            
        bool canGrowDown = !Physics2D.BoxCast(
            (Vector2)transform.position + Vector2.down * (boxCollider.size.y * 0.5f + checkDistance * 0.5f),
            new Vector2(checkSize.x * 0.9f, checkDistance),
            0f, Vector2.zero, 0f, groundLayer);
            
        // Only allow growing if there's space in all directions
        // You might want to adjust this based on which directions you want to check
        return canGrowRight && canGrowLeft && canGrowUp && canGrowDown;
    }

    void resetScene() {
        Debug.Log("You died!");
        // Reload the current scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}