using UnityEngine;
using System.Collections;

public class DoorController : MonoBehaviour
{
    public Animator animator;
    public float doorCloseDelay = 3f; // Segundos antes de cerrar la puerta
    private bool isOpen = false;
    private Coroutine closeCoroutine;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.CompareTag("Guard"))
        {
            OpenDoor();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if ((other.CompareTag("Player") || other.CompareTag("Guard")) && isOpen)
        {
            if (closeCoroutine != null)
                StopCoroutine(closeCoroutine);
            closeCoroutine = StartCoroutine(CloseDoorAfterDelay());
        }
    }

    void OpenDoor()
    {
        isOpen = true;
        animator.SetBool("isOpen", true);
    }

    IEnumerator CloseDoorAfterDelay()
    {
        yield return new WaitForSeconds(doorCloseDelay);
        isOpen = false;
        animator.SetBool("isOpen", false);
    }
}
