// // // using UnityEngine;
// // // using System.Collections;

// // // public class DoorController : MonoBehaviour
// // // {
// // //     public Animator animator;
// // //     public float doorCloseDelay = 3f; // Segundos antes de cerrar la puerta
// // //     private bool isOpen = false;
// // //     private Coroutine closeCoroutine;

// // //     private void OnTriggerEnter(Collider other)
// // //     {
// // //         if (other.CompareTag("Player") || other.CompareTag("Guard"))
// // //         {
// // //             OpenDoor();
// // //         }
// // //     }

// // //     private void OnTriggerExit(Collider other)
// // //     {
// // //         if ((other.CompareTag("Player") || other.CompareTag("Guard")) && isOpen)
// // //         {
// // //             if (closeCoroutine != null)
// // //                 StopCoroutine(closeCoroutine);
// // //             closeCoroutine = StartCoroutine(CloseDoorAfterDelay());
// // //         }
// // //     }

// // //     void OpenDoor()
// // //     {
// // //         isOpen = true;
// // //         animator.SetBool("isOpen", true);
// // //     }

// // //     IEnumerator CloseDoorAfterDelay()
// // //     {
// // //         yield return new WaitForSeconds(doorCloseDelay);
// // //         isOpen = false;
// // //         animator.SetBool("isOpen", false);
// // //     }
// // // }


// // using UnityEngine;
// // using System.Collections;

// // public class DoorController : MonoBehaviour
// // {
// //     public Animator animator;                  // Controla la animación de la puerta
// //     public Collider physicalCollider;          // Collider físico (ej. MeshCollider que bloquea el paso)
// //     public float doorCloseDelay = 3f;          // Segundos antes de cerrar la puerta

// //     private bool isOpen = false;
// //     private Coroutine closeCoroutine;

// //     private void OnTriggerEnter(Collider other)
// //     {
// //         if (other.CompareTag("Player") || other.CompareTag("Guard"))
// //         {
// //             OpenDoor();
// //         }
// //     }

// //     private void OnTriggerExit(Collider other)
// //     {
// //         if ((other.CompareTag("Player") || other.CompareTag("Guard")) && isOpen)
// //         {
// //             if (closeCoroutine != null)
// //                 StopCoroutine(closeCoroutine);

// //             closeCoroutine = StartCoroutine(CloseDoorAfterDelay());
// //         }
// //     }

// //     void OpenDoor()
// //     {
// //         if (isOpen) return;

// //         isOpen = true;
// //         animator.SetBool("isOpen", true);

// //         if (physicalCollider != null)
// //         {
// //             physicalCollider.enabled = false; // Desactiva collider físico
// //         }
// //         else
// //         {
// //             Debug.LogWarning("¡El physicalCollider no está asignado!");
// //         }
// //     }

// //     IEnumerator CloseDoorAfterDelay()
// //     {
// //         yield return new WaitForSeconds(doorCloseDelay);

// //         isOpen = false;
// //         animator.SetBool("isOpen", false);

// //         if (physicalCollider != null)
// //         {
// //             physicalCollider.enabled = true; // Reactiva collider físico
// //         }
// //         else
// //         {
// //             Debug.LogWarning("¡El physicalCollider no está asignado!");
// //         }
// //     }
// // }


// using UnityEngine;
// using System.Collections;

// public class DoorController : MonoBehaviour
// {
//     public Animator animator;                  // Controla la animación de la puerta
//     public Collider physicalCollider;          // Collider físico (ej. MeshCollider que bloquea el paso)
//     public float doorCloseDelay = 3f;          // Segundos antes de cerrar la puerta

//     private bool isOpen = false;
//     private Coroutine closeCoroutine;

//     private void OnTriggerEnter(Collider other)
//     {
//         // Asegúrate de que tanto el jugador como los guardias tienen la etiqueta correcta.
//         if (other.CompareTag("Player") || other.CompareTag("Guard"))
//         {
//             OpenDoor();
//         }
//     }

//     private void OnTriggerExit(Collider other)
//     {
//         // Cuando el jugador o el guardia sale del área, cierra la puerta después de un retraso
//         if ((other.CompareTag("Player") || other.CompareTag("Guard")) && isOpen)
//         {
//             if (closeCoroutine != null)
//                 StopCoroutine(closeCoroutine);

//             closeCoroutine = StartCoroutine(CloseDoorAfterDelay());
//         }
//     }

//     void OpenDoor()
//     {
//         if (isOpen) return;

//         isOpen = true;
//         animator.SetBool("isOpen", true);

//         if (physicalCollider != null)
//         {
//             physicalCollider.enabled = false; // Desactiva collider físico
//         }
//         else
//         {
//             Debug.LogWarning("¡El physicalCollider no está asignado!");
//         }
//     }

//     IEnumerator CloseDoorAfterDelay()
//     {
//         yield return new WaitForSeconds(doorCloseDelay);

//         isOpen = false;
//         animator.SetBool("isOpen", false);

//         if (physicalCollider != null)
//         {
//             physicalCollider.enabled = true; // Reactiva collider físico
//         }
//         else
//         {
//             Debug.LogWarning("¡El physicalCollider no está asignado!");
//         }
//     }
// }
using UnityEngine;
using System.Collections;

public class DoorController : MonoBehaviour
{
    public Animator animator;                  // Controla la animación de la puerta
    public float doorCloseDelay = 3f;          // Segundos antes de cerrar la puerta
    private bool isOpen = false;
    private Coroutine closeCoroutine;

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
    }

    // Corutina para cerrar la puerta después de un retraso
    IEnumerator CloseDoorAfterDelay()
    {
        yield return new WaitForSeconds(doorCloseDelay);

        isOpen = false;
        animator.SetBool("isOpen", false);  // Cerramos la puerta
    }
}
