
using UnityEngine;


public class Tesoro : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            var player = other.GetComponent<Movement>();
            if (player != null && !player.hasTreasure)
            {
                // Actualizar estado del jugador
                player.SetHasTreasure(true);
                // Desactivar el objeto
                gameObject.SetActive(false);
            }
        }
    }
}