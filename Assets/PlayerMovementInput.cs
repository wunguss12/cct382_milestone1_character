using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerMovementInput : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";

    [Header("Movement Actions")]
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string lookActionName = "Look";
    [SerializeField] private string sprintActionName = "Sprint";
    [SerializeField] private string jumpActionName = "Jump";
    [SerializeField] private string crouchActionName = "Crouch";

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 6f;
    [SerializeField] private float crouchSpeed = 1.5f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float jumpHeight = 1.5f;

    [Header("Crouching")]
    [SerializeField] private float crouchHeight = 1.2f;

    [Header("Camera")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float gamepadLookSpeed = 120f;
    [SerializeField] private float maxLookAngle = 85f;

    [Header("Animation Parameters")]
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string crouchParameter = "IsCrouching";
    [SerializeField] private string jumpParameter = "Jump";
    [SerializeField] private float animationDampTime = 0.1f;

    private CharacterController controller;
    private Animator animator;

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction sprintAction;
    private InputAction jumpAction;
    private InputAction crouchAction;

    private float verticalVelocity;
    private float standingHeight;
    private Vector3 standingCenter;
    private float cameraPitch;

    private bool hasCrouchParameter;
    private bool hasJumpParameter;

    private Transform viewTransform;
    private bool cameraIsChild;
    private Vector3 cameraStandingLocalPosition;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        standingHeight = controller.height;
        standingCenter = controller.center;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        // Movement follows the camera that is actually rendering the view.
        viewTransform = Camera.main != null ? Camera.main.transform : transform;

        // Only a direct child of the player root is lowered by this script.
        // A target under a bone is already moved by the animation.
        cameraIsChild = cameraTransform != null
                        && cameraTransform.parent == transform;

        if (cameraIsChild)
            cameraStandingLocalPosition = cameraTransform.localPosition;

        if (inputActions == null)
        {
            Debug.LogError("Assign PlayerControls to the movement script.", this);
            enabled = false;
            return;
        }

        var map = inputActions.FindActionMap(
            actionMapName, throwIfNotFound: false
        );

        if (map == null)
        {
            Debug.LogError("Could not find the Player action map.", this);
            enabled = false;
            return;
        }

        moveAction = map.FindAction(moveActionName, false);
        sprintAction = map.FindAction(sprintActionName, false);
        jumpAction = map.FindAction(jumpActionName, false);
        crouchAction = map.FindAction(crouchActionName, false);

        // Optional: without a Look action the mouse is read directly
        // (no gamepad look).
        lookAction = map.FindAction(lookActionName, false);

        if (moveAction == null || sprintAction == null ||
            jumpAction == null || crouchAction == null)
        {
            Debug.LogError(
                "Check that Move, Sprint, Jump and Crouch exist " +
                "in the Player action map.", this
            );

            enabled = false;
            return;
        }

        hasCrouchParameter = HasAnimatorParameter(
            crouchParameter, AnimatorControllerParameterType.Bool
        );

        hasJumpParameter = HasAnimatorParameter(
            jumpParameter, AnimatorControllerParameterType.Trigger
        );

        if (!hasCrouchParameter)
            Debug.LogWarning($"Animator has no Bool parameter named '{crouchParameter}'.", this);

        if (!hasJumpParameter)
            Debug.LogWarning($"Animator has no Trigger parameter named '{jumpParameter}'.", this);
    }

    private void OnEnable()
    {
        moveAction?.Enable();
        lookAction?.Enable();
        sprintAction?.Enable();
        jumpAction?.Enable();
        crouchAction?.Enable();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        lookAction?.Disable();
        sprintAction?.Disable();
        jumpAction?.Disable();
        crouchAction?.Disable();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        if (moveAction == null)
            return;

        float dt = Time.deltaTime;

        Vector2 input = Vector2.ClampMagnitude(
            moveAction.ReadValue<Vector2>(), 1f
        );

        // Stay crouched while something is overhead (e.g. a low tunnel),
        // so the player can't stand up inside geometry.
        // The ray starts just above the top of the capsule so it can't
        // hit the player's own collider.
        float ceilingGap = standingHeight - controller.height;

        bool blockedAbove = ceilingGap > 0.05f && Physics.Raycast(
            transform.position + controller.center
                + Vector3.up * (controller.height / 2f + 0.05f),
            Vector3.up, ceilingGap, ~0, QueryTriggerInteraction.Ignore);

        bool isCrouching = crouchAction.IsPressed() || blockedAbove;

        bool isMoving = input.sqrMagnitude > 0.01f;
        bool isSprinting = sprintAction.IsPressed()
                           && isMoving && !isCrouching;

        // Look: yaw turns the body, pitch tilts the camera.
        // Mouse delta is per-frame (no dt); gamepad stick is per-second.
        Vector2 look = Vector2.zero;

        if (lookAction != null)
        {
            bool usingGamepad = lookAction.activeControl?.device is Gamepad;

            look = lookAction.ReadValue<Vector2>()
                   * (usingGamepad ? gamepadLookSpeed * dt : mouseSensitivity);
        }
        else if (Mouse.current != null)
        {
            look = Mouse.current.delta.ReadValue() * mouseSensitivity;
        }

        transform.Rotate(Vector3.up, look.x, Space.World);

        cameraPitch = Mathf.Clamp(
            cameraPitch - look.y, -maxLookAngle, maxLookAngle
        );

        if (cameraTransform != null)
        {
            cameraTransform.rotation = Quaternion.Euler(
                cameraPitch, transform.eulerAngles.y, 0f
            );
        }

        // Movement is relative to the direction the view is facing,
        // so W always moves toward what you see.
        Vector3 forward = Vector3.ProjectOnPlane(viewTransform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(viewTransform.right, Vector3.up).normalized;

        Vector3 moveDirection = Vector3.ClampMagnitude(
            forward * input.y + right * input.x, 1f
        );

        // Change the Character Controller height while crouching.
        float targetHeight = isCrouching
            ? Mathf.Min(crouchHeight, standingHeight)
            : standingHeight;

        controller.height = Mathf.MoveTowards(
            controller.height, targetHeight, 4f * dt
        );

        // Keep the bottom of the capsule near the same position.
        controller.center = standingCenter
            + Vector3.up * ((controller.height - standingHeight) / 2f);

        // Lower the eye height along with the capsule.
        if (cameraIsChild)
        {
            cameraTransform.localPosition = cameraStandingLocalPosition
                + Vector3.up * (controller.height - standingHeight);
        }

        // Apply gravity and jumping.
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        bool jumped = controller.isGrounded && !isCrouching
                      && jumpAction.WasPressedThisFrame();

        if (jumped)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * dt;

        // Move the player.
        float currentSpeed = isCrouching
            ? crouchSpeed
            : (isSprinting ? runSpeed : walkSpeed);

        controller.Move(
            (moveDirection * currentSpeed
             + Vector3.up * verticalVelocity) * dt
        );

        // Update the Animator.
        float animationSpeed = isMoving ? (isSprinting ? 1f : 0.5f) : 0f;

        animator.SetFloat(
            speedParameter,
            animationSpeed,
            animationDampTime,
            dt
        );

        if (hasCrouchParameter)
            animator.SetBool(crouchParameter, isCrouching);

        if (jumped && hasJumpParameter)
            animator.SetTrigger(jumpParameter);
    }

    private bool HasAnimatorParameter(
        string parameterName,
        AnimatorControllerParameterType parameterType)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName &&
                parameter.type == parameterType)
            {
                return true;
            }
        }

        return false;
    }
}