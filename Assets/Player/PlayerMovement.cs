using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float walkSpeed = 5f; // Speed of the player movement
    public float jumpForce = 5f; // Force applied when the player jumps
    public float runSpeed = 10f; // Speed of the player when running
    public Rigidbody rb;
    public bool isGrounded; // Check if the player is on the ground

    public Transform cameraTransform; // Reference to the camera
    public float mouseSensitivity = 100f; // Sensitivity of the mouse
    private float xRotation = 0f; // Rotation around the X-axis

    void Start()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>(); // Get the Rigidbody component attached to the player
        }

        // Lock the cursor to the center of the screen
        Cursor.lockState = CursorLockMode.Locked;

        // Prevent the capsule from falling over
        rb.freezeRotation = true;
    }

    void Update()
    {
        // Handle mouse input for camera rotation
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f); // Clamp vertical rotation to prevent flipping

        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f); // Rotate the camera vertically
        transform.Rotate(Vector3.up * mouseX); // Rotate the player horizontally

        // Handle movement input
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        Vector3 movement = transform.right * horizontalInput + transform.forward * verticalInput;
        float speed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;

        Vector3 velocity = movement * speed;
        velocity.y = rb.linearVelocity.y; // Maintain the current vertical velocity
        rb.linearVelocity = velocity;

        // Handle jumping
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    void OnCollisionStay(Collision collision)
    {
        // Check if the player is colliding with the ground
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
        }
    }

    void OnCollisionExit(Collision collision)
    {
        // Check if the player is no longer colliding with the ground
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = false;
        }
    }
}
