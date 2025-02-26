using UnityEngine;

public class Movement : MonoBehaviour
{
    public CharacterController controller;                          // Controlador de personaje
    public Transform cameraTransform;
    public float speed = 1.0f;
    public float mouseSensitivity = 2f;

    void Update()
    {
        // Movimiento del personaje
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        Vector3 move = cameraTransform.right * moveX + cameraTransform.forward * moveZ;
        controller.Move(move * speed * Time.deltaTime);

        // Movimiento de la cámara
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        cameraTransform.Rotate(vector3.up * mouseX);
        cameraTransform.Rotate(vector3.left * mouseY);
    }
}
