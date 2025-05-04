
using UnityEngine;

public class Movement : MonoBehaviour
{
    public CharacterController controller;                          // Controlador de personaje
    public Transform cameraTransform;
    public float speed = 5f;
    public float mouseSensitivity = 2f;
    public float jumpHeight = 1.5f;

    private float gravity = -9.81f;
    private Vector3 velocity;
    private bool isGrounded;
    private bool wasGroundedLastFrame;

    private GameObject currentTreasure;  // Referencia al tesoro actual
    private Vector3 lastTreasurePosition; // Última posición conocida del tesoro

    public bool hasTreasure = false;  // Variable para saber si el jugador tiene el tesoro

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;                   // Bloqueamos el cursor
        Cursor.visible = false;                                     // Ocultamos el cursor

        SoundEmitter soundEmitter = gameObject.GetComponent<SoundEmitter>();
        soundEmitter.soundRadius = 4f;
        soundEmitter.soundDuration = 1f;
        soundEmitter.isPlayer = true;
    }

    void Update()
    {
        // Movimiento del personaje
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        Vector3 move = transform.right * moveX + transform.forward * moveZ;
        controller.Move(move * speed * Time.deltaTime);

        // Movimiento de la cámara
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        transform.Rotate(Vector3.up * mouseX);

        // Aplicamos gravedad
        wasGroundedLastFrame = isGrounded;
        isGrounded = controller.isGrounded;

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    // Método para cambiar si el jugador tiene el tesoro o no
    public void SetHasTreasure(bool treasure)
    {
        hasTreasure = treasure;
    }

    // Método para verificar si está en el suelo
    public bool IsGrounded()
    {
        return isGrounded;
    }

    public bool WasGroundedLastFrame()
    {
        return wasGroundedLastFrame;
        }
    
    
    public void PickUpTreasure(GameObject treasure)
    {
        if (!hasTreasure)
        {
            hasTreasure = true;
            treasure.SetActive(false);
            MultiAgentSystem.PlayerHasTreasure = true;
        }
    }
}
