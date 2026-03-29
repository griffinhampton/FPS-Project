using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class FPSController : MonoBehaviour
{
    [Header("Movement Speeds")]
    [SerializeField] private float walkSpeed = 3.0f;
    [SerializeField] private float sprintMultiplier = 2.0f;

    [Header("Jump Parameters")]
    [SerializeField] private float jumpForce = 5.0f;
    [SerializeField] private float gravity = 9.81f;

    [Header("Look Sensitivity")]
    [SerializeField] private float mouseSensitivity = 2.0f;
    [SerializeField] private float upDownRange = 90.0f;

    private CharacterController characterController;
    private Camera mainCamera;
    private PlayInputHandler inputHandler;
    private Vector3 currentMovement;
    private float verticalRotation;

    LayerMask layerMask;
    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        mainCamera = Camera.main;
        layerMask = LayerMask.GetMask("Default"); 
    }

    private void Start()
    {
        inputHandler = PlayInputHandler.Instance;
    }

    private void Update()
    {
        HandleMovement();
        HandleRotation();
    }

    void HandleMovement()
    {
        float speed = walkSpeed * (inputHandler.SprintValue > 0 ? sprintMultiplier : 1f);

        Vector3 inputDirection = new Vector3(inputHandler.MoveInput.x, 0f, inputHandler.MoveInput.y);
        Vector3 worldDirection = transform.TransformDirection(inputDirection);
        worldDirection.Normalize();

        currentMovement.x = worldDirection.x * speed;
        currentMovement.z = worldDirection.z * speed;

        HandleJumping();

        characterController.Move(currentMovement * Time.deltaTime);
    }

    void HandleJumping()
    {
        Ray jumpray = new Ray(mainCamera.transform.position, Vector3.down);
        RaycastHit jumphit;
        if( Physics.Raycast(jumpray, out jumphit, 2.2f, layerMask ))
        {
            currentMovement.y -= gravity * Time.deltaTime;

            if (inputHandler.JumpTriggered)
            {
                currentMovement.y = jumpForce;
            }
        }
        else 
        {
            currentMovement.y -= gravity * Time.deltaTime;
        }

        inputHandler.ResetJump();
    }
    void HandleRotation()
    {
        float mouseXRotation = inputHandler.LookInput.x * mouseSensitivity;
        transform.Rotate(0, mouseXRotation, 0);

        verticalRotation -= inputHandler.LookInput.y * mouseSensitivity;
        verticalRotation = Mathf.Clamp(verticalRotation, -upDownRange, upDownRange);
        mainCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0,0);
    }

}
