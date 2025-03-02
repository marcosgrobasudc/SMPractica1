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

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;                   // Bloqueamos el cursor
        Cursor.visible = false;                                     // Ocultamos el cursor
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
}

// using UnityEngine;
// using UnityEngine.AI; // Necesario para NavMeshAgent

// public class Movement : MonoBehaviour
// {
//     public NavMeshAgent agent;          // Agente de navegación
//     public Transform cameraTransform;  
//     public float mouseSensitivity = 2f;
//     public float jumpHeight = 1.5f;

//     private float gravity = -9.81f;
//     private Vector3 velocity;
//     private bool isGrounded;

//     private void Start()
//     {
//         Cursor.lockState = CursorLockMode.Locked;
//         Cursor.visible = false;

//         agent = GetComponent<NavMeshAgent>();
//         agent.speed = 5f;  // Ajusta según lo necesites
//     }

//     void Update()
//     {
//         // Movimiento del jugador usando NavMeshAgent
//         float moveX = Input.GetAxis("Horizontal");
//         float moveZ = Input.GetAxis("Vertical");
//         Vector3 move = transform.right * moveX + transform.forward * moveZ;
        
//         if (move.magnitude > 0)
//         {
//             agent.SetDestination(transform.position + move);
//         }

//         // Movimiento de la cámara
//         float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
//         transform.Rotate(Vector3.up * mouseX);
//     }
// }



// using UnityEngine;

// public class Movement : MonoBehaviour
// {
//     public CharacterController controller;
//     public Transform cameraTransform;
//     public float speed = 5f;
//     public float mouseSensitivity = 2f;
//     public float jumpHeight = 1.5f;
//     private float gravity = -9.81f;
//     private Vector3 velocity;
//     private bool isGrounded;

//     private void Start()
//     {
//         Cursor.lockState = CursorLockMode.Locked;
//         Cursor.visible = false;
//     }

//     void Update()
//     {
//         // Comprobar si está en el suelo
//         isGrounded = Physics.Raycast(transform.position, Vector3.down, 1.1f);
//         if (isGrounded && velocity.y < 0)
//         {
//             velocity.y = -2f;
//         }

//         // Movimiento del personaje
//         float moveX = Input.GetAxis("Horizontal");
//         float moveZ = Input.GetAxis("Vertical");
//         Vector3 move = transform.right * moveX + transform.forward * moveZ;
//         controller.Move(move * speed * Time.deltaTime);

//         // Movimiento de la cámara
//         float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
//         transform.Rotate(Vector3.up * mouseX);

//         // Aplicar gravedad
//         velocity.y += gravity * Time.deltaTime;
//         controller.Move(velocity * Time.deltaTime);

//         // Saltar
//         if (Input.GetButtonDown("Jump") && isGrounded)
//         {
//             velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
//         }
//     }
// }
