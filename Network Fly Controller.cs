// NetworkFlyController.cs
// Choose your networking stack by uncommenting ONE of these:
// #define USE_NGO
// #define USE_FUSION

using UnityEngine;
using UnityEngine.InputSystem;

#if USE_NGO
using Unity.Netcode;
#endif

#if USE_FUSION
using Fusion;
#endif

/// <summary>
/// Hold the configured "Fly Button" to enter fly mode.
/// Left stick = forward/strafe, Right stick Y = up/down while flying.
/// Only runs for the local player (owner / input authority).
/// Attach to your Player prefab (XR Origin).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class NetworkFlyController : 
#if USE_NGO
    NetworkBehaviour
#elif USE_FUSION
    NetworkBehaviour
#else
    MonoBehaviour
#endif
{
    [Header("References")]
    [Tooltip("Typically the XR camera (Main Camera). Used to get head-forward for movement.")]
    public Transform head; // assign Main Camera (XR)

    private CharacterController cc;

    [Header("Input (Input System)")]
    [Tooltip("Button to hold for fly mode (e.g., RightHand/PrimaryButton = A on Quest).")]
    public InputActionProperty flyButton;      // Bool
    [Tooltip("Left-hand primary 2D axis (move forward/strafe).")]
    public InputActionProperty move2D;         // Vector2
    [Tooltip("Right-hand primary 2D axis (up/down on Y while flying).")]
    public InputActionProperty vertical2D;     // Vector2 (use .y)

    [Header("Flight Tuning")]
    public float flySpeed = 4.5f;
    public float sprintMultiplier = 1.8f;
    [Tooltip("Grip held multiplies speed (optional). Leave unassigned to ignore.")]
    public InputActionProperty sprintGrip;     // Bool (optional)

    [Tooltip("If true, gravity is ignored while the fly button is held.")]
    public bool ignoreGravityWhileFlying = true;

    [Tooltip("Optional: layer mask to avoid pushing through colliders when moving fast.")]
    public LayerMask collisionMask = ~0;

    // Internal
    private bool isFlying;

    // --- Networking authority check helpers ---
    private bool HasLocalAuthority()
    {
#if USE_NGO
        return this is NetworkBehaviour nb ? nb.IsOwner : true;
#elif USE_FUSION
        return Object != null ? Object.HasInputAuthority : true;
#else
        return true;
#endif
    }

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (!head)
        {
            var cam = Camera.main;
            if (cam) head = cam.transform;
        }
    }

#if !USE_NGO && !USE_FUSION
    private void OnEnable()
    {
        // Enable actions if not enabled via an InputActionManager
        flyButton.action?.Enable();
        move2D.action?.Enable();
        vertical2D.action?.Enable();
        sprintGrip.action?.Enable();
    }

    private void OnDisable()
    {
        flyButton.action?.Disable();
        move2D.action?.Disable();
        vertical2D.action?.Disable();
        sprintGrip.action?.Disable();
    }
#endif

    private void Update()
    {
        if (!HasLocalAuthority()) return;

        bool flyHeld = flyButton.action != null && flyButton.action.IsPressed();
        isFlying = flyHeld;

        // Read movement sticks
        Vector2 move = move2D.action != null ? move2D.action.ReadValue<Vector2>() : Vector2.zero;
        float upDown = vertical2D.action != null ? vertical2D.action.ReadValue<Vector2>().y : 0f;

        // Head-relative planar basis
        Vector3 fwd = Vector3.forward;
        Vector3 right = Vector3.right;
        if (head)
        {
            Vector3 hf = head.forward;
            hf.y = 0f;
            hf.Normalize();
            fwd = hf.sqrMagnitude > 0.0001f ? hf : Vector3.forward;

            Vector3 hr = head.right;
            hr.y = 0f;
            hr.Normalize();
            right = hr.sqrMagnitude > 0.0001f ? hr : Vector3.right;
        }

        float speed = flySpeed;
        if (sprintGrip.action != null && sprintGrip.action.IsPressed()) speed *= sprintMultiplier;

        Vector3 velocity = Vector3.zero;

        if (isFlying)
        {
            // 6DoF flight: left stick = planar, right Y = ascend/descend
            Vector3 planar = (fwd * move.y + right * move.x) * speed;
            Vector3 vertical = Vector3.up * (upDown * speed);
            velocity = planar + vertical;

            if (ignoreGravityWhileFlying)
            {
                // Do not accumulate gravity while flying
                ApplyMove(velocity);
                return;
            }
        }

        // Not flying: just planar move, let your normal locomotion handle gravity,
        // or lightly apply gravity here if you want:
        if (!isFlying)
        {
            // Optional tiny assist to move when grounded (no jump here)
            Vector3 planar = (fwd * move.y + right * move.x) * (speed * 0.65f);
            velocity = planar;
        }

        // Minimal gravity (optional). Comment if your project already applies gravity elsewhere.
        Vector3 grav = Physics.gravity * Time.deltaTime;
        ApplyMove(velocity + grav);
    }

    private void ApplyMove(Vector3 worldVelocity)
    {
        // Simple collision-aware move through CharacterController
        cc.Move(worldVelocity * Time.deltaTime);
    }
}
