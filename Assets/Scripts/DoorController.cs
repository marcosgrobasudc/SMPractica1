using UnityEngine;
using System.Collections;

public class DoorController : MonoBehaviour
{
    public Animator animator;                  // Controla la animación de la puerta
    public float doorCloseDelay = 3f;          // Segundos antes de cerrar la puerta
    private bool isOpen = false;
    private Coroutine closeCoroutine;

    private NoisyDoor noisyDoor;                // Referencia al script de la puerta ruidosa

    void Awake()
    {
        // Buscamos el componente NoisyDoor en el mismo GameObject o en uno de sus padres
        noisyDoor = GetComponent<NoisyDoor>();
        if (noisyDoor == null)
        {
            Debug.LogError("No se encontró el componente NoisyDoor en la puerta.");
        }
    }

    // Cuando el jugador o guardia entra en el Trigger de la puerta
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.CompareTag("Guard"))
        {
            // Abrimos la puerta
            OpenDoor();
        }
    }

    // Cuando el jugador o guardia sale del Trigger de la puerta
    private void OnTriggerExit(Collider other)
    {
        if ((other.CompareTag("Player") || other.CompareTag("Guard")) && isOpen)
        {
            // Detenemos la corutina de cerrar la puerta si la puerta está abierta
            if (closeCoroutine != null)
                StopCoroutine(closeCoroutine);

            // Iniciamos la corutina para cerrar la puerta
            closeCoroutine = StartCoroutine(CloseDoorAfterDelay());
        }
    }

    // Función para abrir la puerta
    void OpenDoor()
    {
        if (isOpen) return;  // Si ya está abierta, no hacemos nada

        isOpen = true;
        animator.SetBool("isOpen", true);  // Asumimos que tienes un parámetro "isOpen" en el Animator

        Debug.Log("Opening door and emitting sound.");
        noisyDoor.OpenDoor();
    }

    // Corutina para cerrar la puerta después de un retraso
    IEnumerator CloseDoorAfterDelay()
    {
        yield return new WaitForSeconds(doorCloseDelay);

        isOpen = false;
        animator.SetBool("isOpen", false);  // Cerramos la puerta
    }
}
