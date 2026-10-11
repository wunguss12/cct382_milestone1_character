using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

// Attach to the Player. Requires Cinemachine 3.x (Unity 6 default).
[RequireComponent(typeof(CharacterController))]
public class CameraEffects : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private CinemachineImpulseSource recoilSource;

    [Header("Breathing / Head Bob (Perlin Noise)")]
    [SerializeField] private float maxSpeed = 6f;          // match runSpeed
    [SerializeField] private float idleAmplitude = 0.4f;   // slow breathing
    [SerializeField] private float runAmplitude = 1.8f;    // head bob
    [SerializeField] private float idleFrequency = 0.5f;
    [SerializeField] private float runFrequency = 2f;
    [SerializeField] private float blendSpeed = 5f;

    [Header("Sprint FOV")]
    [SerializeField] private float baseFov = 60f;
    [SerializeField] private float sprintFov = 70f;

    [Header("Recoil (demo: left mouse / right trigger)")]
    [SerializeField] private bool demoRecoilInput = false;
    [SerializeField] private float recoilForce = 0.5f;

    private CharacterController controller;
    private CinemachineBasicMultiChannelPerlin noise;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (cinemachineCamera == null)
        {
            Debug.LogError("Assign the CinemachineCamera.", this);
            enabled = false;
            return;
        }

        noise = cinemachineCamera.GetComponent<CinemachineBasicMultiChannelPerlin>();

        if (recoilSource == null)
            recoilSource = GetComponent<CinemachineImpulseSource>();
    }

    private void Update()
    {
        // 0 = standing still, 1 = full sprint.
        Vector3 v = controller.velocity;
        float speedT = Mathf.Clamp01(new Vector3(v.x, 0f, v.z).magnitude / maxSpeed);

        // Frame-rate independent smoothing.
        float k = 1f - Mathf.Exp(-blendSpeed * Time.deltaTime);

        if (noise != null)
        {
            noise.AmplitudeGain = Mathf.Lerp(
                noise.AmplitudeGain, Mathf.Lerp(idleAmplitude, runAmplitude, speedT), k);

            noise.FrequencyGain = Mathf.Lerp(
                noise.FrequencyGain, Mathf.Lerp(idleFrequency, runFrequency, speedT), k);
        }

        LensSettings lens = cinemachineCamera.Lens;
        lens.FieldOfView = Mathf.Lerp(
            lens.FieldOfView, Mathf.Lerp(baseFov, sprintFov, speedT), k);
        cinemachineCamera.Lens = lens;

        // Demo trigger - call Recoil() from your weapon script instead.
        bool fired = (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                     || (Gamepad.current != null && Gamepad.current.rightTrigger.wasPressedThisFrame);

        if (demoRecoilInput && fired)
            Recoil();
    }

    public void Recoil()
    {
        if (recoilSource != null)
            recoilSource.GenerateImpulseWithForce(recoilForce);
    }
}