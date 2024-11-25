using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.PostProcessing; // For visual effects like motion blur or vignette

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 2f;
    public float sprintSpeed = 4f;
    public float crouchSpeed = 1f;
    public float acceleration = 10f; // Speed ramp-up
    public float deceleration = 15f; // Slowdown responsiveness

    [Header("Stamina Settings")]
    public float maxStamina = 5f;
    public float staminaDrainRate = 1f;
    public float staminaRecoveryRate = 0.5f;
    private float currentStamina;
    private bool isExhausted = false;

    [Header("Breathing Settings")]
    public AudioSource breathingAudioSource;
    public AudioClip heavyBreathingClip;
    public AudioClip lightBreathingClip; // Recovery sound

    [Header("Mouse Look Settings")]
    public float mouseSensitivity = 100f;
    public Transform playerCamera;
    public float verticalLookLimit = 80f;

    [Header("Head Bobbing Settings")]
    public bool enableHeadBobbing = true;
    public float headBobFrequency = 1.5f;
    public float headBobAmplitude = 0.1f;
    private Vector3 originalCameraPosition;
    private float headBobTimer;

    [Header("Footstep Sounds")]
    public AudioSource footstepAudioSource;
    public AudioClip[] walkFootstepClips;
    public AudioClip[] runFootstepClips;
    public AudioClip[] crouchFootstepClips;
    public float footstepInterval = 0.5f;
    private float footstepTimer;

    [Header("Crouch Settings")]
    public float crouchHeight = 1.2f;
    public float standingHeight = 1.8f;

    [Header("Visual Effects")]
    public Image vignetteImage; // UI overlay for exhaustion effects
    public Color vignetteColor = new Color(0, 0, 0, 0.5f);
    public float vignetteFadeSpeed = 2f;

    public PostProcessVolume postProcessVolume; // For motion blur
    private MotionBlur motionBlur;

    [Header("Fatigue Effects")]
    public bool enableFatigueSway = true;
    public float swayIntensity = 0.05f;
    private bool fatigueSwayActive = false;

    private CharacterController characterController;
    private float verticalRotation = 0f;
    private bool isCrouching = false;
    private Vector3 currentVelocity; // For acceleration/deceleration

    private void Start()
    {
        characterController = GetComponent<CharacterController>();
        originalCameraPosition = playerCamera.localPosition;
        currentStamina = maxStamina;

        // Lock the cursor for gameplay
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (vignetteImage)
        {
            vignetteImage.color = new Color(0, 0, 0, 0); // Fully transparent at the start
        }

        if (postProcessVolume)
        {
            postProcessVolume.profile.TryGetSettings(out motionBlur);
            if (motionBlur)
            {
                motionBlur.enabled.value = false;
            }
        }
    }

    private void Update()
    {
        HandleStamina();
        HandleMovement();
        HandleMouseLook();
        HandleCrouch();
        HandleHeadBobbing();
        HandleFootsteps();
        UpdateVignette();
    }

    private void HandleMovement()
    {
        // Get movement input
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        Vector3 targetVelocity = (transform.right * moveX + transform.forward * moveZ).normalized;

        // Determine the movement speed
        float speed = walkSpeed;
        if (Input.GetKey(KeyCode.LeftShift) && canSprint() && !isExhausted && !isCrouching)
        {
            speed = sprintSpeed; // Sprinting
            DrainStamina();
        }
        else if (isCrouching)
        {
            speed = crouchSpeed; // Crouching
        }

        targetVelocity *= speed;
        currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, (targetVelocity.magnitude > 0 ? acceleration : deceleration) * Time.deltaTime);

        // Apply movement via CharacterController
        characterController.Move(currentVelocity * Time.deltaTime);
    }

    private void HandleMouseLook()
    {
        // Get mouse input
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // Rotate player horizontally
        transform.Rotate(Vector3.up * mouseX);

        // Rotate camera vertically
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -verticalLookLimit, verticalLookLimit);
        playerCamera.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }

    private void HandleCrouch()
    {
        if (Input.GetKeyDown(KeyCode.LeftControl) && canCrouch)
        {
            isCrouching = !isCrouching; // Toggle crouch

            // Adjust CharacterController height
            characterController.height = isCrouching ? crouchHeight : standingHeight;
        }
    }

    private void HandleHeadBobbing()
    {
        if (!enableHeadBobbing) return;

        // Head bobbing effect
        if (characterController.velocity.magnitude > 0 && characterController.isGrounded)
        {
            headBobTimer += Time.deltaTime * headBobFrequency;
            float offsetY = Mathf.Sin(headBobTimer) * headBobAmplitude;
            playerCamera.localPosition = originalCameraPosition + new Vector3(0, offsetY, 0);
        }
        else
        {
            // Reset camera position when not moving
            playerCamera.localPosition = Vector3.Lerp(playerCamera.localPosition, originalCameraPosition, Time.deltaTime * 8f);
        }
    }

    private void HandleFootsteps()
    {
        if (!characterController.isGrounded || characterController.velocity.magnitude <= 0) return;

        // Increment footstep timer
        footstepTimer += Time.deltaTime;

        // Determine footstep interval based on movement state
        float currentInterval = footstepInterval;
        AudioClip[] currentFootsteps = walkFootstepClips;

        if (isCrouching)
        {
            currentInterval *= 1.5f; // Slower footsteps when crouching
            currentFootsteps = crouchFootstepClips;
        }
        else if (Input.GetKey(KeyCode.LeftShift) && canSprint() && !isExhausted)
        {
            currentInterval /= 1.5f; // Faster footsteps when sprinting
            currentFootsteps = runFootstepClips;
        }

        if (footstepTimer >= currentInterval)
        {
            PlayFootstepSound(currentFootsteps);
            footstepTimer = 0;
        }
    }

    private void PlayFootstepSound(AudioClip[] clips)
    {
        if (clips.Length == 0) return;

        int index = Random.Range(0, clips.Length);
        footstepAudioSource.PlayOneShot(clips[index]);
    }

    private bool canSprint()
    {
        return Input.GetAxis("Vertical") > 0; // Only sprint when moving forward
    }

    private void HandleStamina()
    {
        if (isExhausted)
        {
            currentStamina += staminaRecoveryRate * Time.deltaTime;
            if (currentStamina >= maxStamina)
            {
                isExhausted = false;
                StopBreathing();
                DisableFatigueEffects();
            }
        }
        else if (currentStamina < maxStamina)
        {
            currentStamina += staminaRecoveryRate * Time.deltaTime;
        }
    }

    private void DrainStamina()
    {
        currentStamina -= staminaDrainRate * Time.deltaTime;

        if (currentStamina <= 0)
        {
            isExhausted = true;
            StartBreathing();
            EnableFatigueEffects();
        }
    }

    private void StartBreathing()
    {
        if (!breathingAudioSource.isPlaying)
        {
            breathingAudioSource.clip = heavyBreathingClip;
            breathingAudioSource.loop = true;
            breathingAudioSource.Play();
        }
    }

    private void StopBreathing()
    {
        if (breathingAudioSource.isPlaying)
        {
            breathingAudioSource.Stop();
        }
    }

    private void EnableFatigueEffects()
    {
        if (motionBlur)
        {
            motionBlur.enabled.value = true;
        }

        fatigueSwayActive = true;
    }

    private void DisableFatigueEffects()
    {
        if (motionBlur)
        {
            motionBlur.enabled.value = false;
        }

        fatigueSwayActive = false;
    }

    private void UpdateVignette()
    {
        if (!vignetteImage) return;

        float vignetteAlpha = isExhausted ? vignetteColor.a : 0;
        vignetteImage.color = Color.Lerp(vignetteImage.color, new Color(0, 0, 0, vignetteAlpha), vignetteFadeSpeed * Time.deltaTime);
    }
}
