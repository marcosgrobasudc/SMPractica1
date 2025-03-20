
using UnityEngine;

public class MetaScript : MonoBehaviour
{
    public GameObject winCanvas; // Canvas de victoria que aparecerá al final
    public GameObject player;    // El jugador para comprobar si tiene el tesoro

    private void Start()
    {
        // Asegúrate de que el Canvas de victoria esté desactivado al inicio
        winCanvas.SetActive(false);
    }

    // Este método se llama cuando el jugador toca la meta
    private void OnTriggerEnter(Collider other)
    {
        // Comprobar si el objeto que tocó la meta es el jugador
        if (other.CompareTag("Player"))
        {
            // Acceder al script de movimiento del jugador para ver si tiene el tesoro
            Movement playerMovement = other.GetComponent<Movement>();
            
            // Verificar si el jugador tiene el tesoro
            if (playerMovement.hasTreasure)
            {
                // Detener el juego (poner en pausa)
                Time.timeScale = 0f;
                
                // Mostrar el Canvas de victoria
                winCanvas.SetActive(true);
                
            
                Debug.Log("¡Has ganado!");
            }
            else
            {
                // Si el jugador no tiene el tesoro, mostrar un mensaje o hacer algo (opcional)
                Debug.Log("¡Aún no has recogido el tesoro!");
            }
        }
    }
}