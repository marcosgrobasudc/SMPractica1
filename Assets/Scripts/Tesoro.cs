
using UnityEngine;

public class Tesoro : MonoBehaviour
{
    // Este script maneja la recogida del tesoro por parte del jugador.
    public void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Cambiar el estado del jugador para que tenga el tesoro
            Movement playerMovement = other.GetComponent<Movement>();
            playerMovement.SetHasTreasure(true); // El jugador ahora tiene el tesoro

            // Desactivar el tesoro (o destruirlo si prefieres)
            gameObject.SetActive(false);
        }
    }
}
