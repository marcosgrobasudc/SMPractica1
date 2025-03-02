using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class GuardScript : MonoBehaviour
{
    public Transform[] patrolPoints;
    public Transform treasureLocation;
    public Transform exitLocation;
    public float sightRange = 10f;
    public float hearingRange = 5f;
    public float chaseSpeed = 6f;
    public float patrolSpeed = 3f;
    public float lostPlayerWaitTime = 3f; // Tiempo que espera en la última posición conocida
    public LayerMask playerLayer;

    public GameObject gameOverCanvas; // Referencia al Canvas de Game Over
    public GameObject winCanvas;      // Referencia al Canvas de Win

    private NavMeshAgent agent;
    private int currentPatrolIndex = 0;
    private Transform player;
    private bool playerHasTreasure = false;
    private bool chasingPlayer = false;
    private Vector3 lastKnownPlayerPosition;
    private bool searchingLastPosition = false;
    private bool sawPlayerWithTreasure = false; // Se activa si el guardia ve al jugador con el tesoro
    private bool checkingTreasure = false; // Para saber si el guardia está verificando la ubicación del tesoro

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        player = GameObject.FindGameObjectWithTag("Player").transform;
        GoToNextPatrolPoint();
        
        // Asegurarse de que el Canvas de Game Over y Win estén desactivados al inicio
        gameOverCanvas.SetActive(false);
        winCanvas.SetActive(false);
    }

    void Update()
    {
        if (CanSeePlayer() || CanHearPlayer())
        {
            chasingPlayer = true;
            searchingLastPosition = false;
            checkingTreasure = false;
            lastKnownPlayerPosition = player.position;
            agent.speed = chaseSpeed;
            agent.SetDestination(player.position);

            // Verificar si el jugador tiene el tesoro
            Movement playerMovement = player.GetComponent<Movement>();
            if (playerMovement.hasTreasure)
            {
                sawPlayerWithTreasure = true;  // El guardia ha visto al jugador con el tesoro
            }
        }
        else if (chasingPlayer)
        {
            if (agent.remainingDistance < 1f)
            {
                chasingPlayer = false;
                StartCoroutine(SearchLastKnownPosition());
            }
        }
        else if (!searchingLastPosition && !checkingTreasure)
        {
            Patrol();
        }

        // Verificar si el guardia ha llegado a la ubicación del tesoro
        if (checkingTreasure && agent.remainingDistance < 1f)
        {
            CheckTreasureStatus();
        }
    }

    void Patrol()
    {
        agent.speed = patrolSpeed;
        if (!agent.pathPending && agent.remainingDistance < 1f)
        {
            GoToNextPatrolPoint();
        }
    }

    void GoToNextPatrolPoint()
    {
        if (patrolPoints.Length == 0)
            return;

        agent.SetDestination(patrolPoints[currentPatrolIndex].position);
        currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
    }

    bool CanSeePlayer()
    {
        Vector3 directionToPlayer = player.position - transform.position;
        if (directionToPlayer.magnitude < sightRange)
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, directionToPlayer.normalized, out hit, sightRange))
            {
                if (hit.collider.CompareTag("Player"))
                {
                    return true;
                }
            }
        }
        return false;
    }

    bool CanHearPlayer()
    {
        return Vector3.Distance(transform.position, player.position) < hearingRange;
    }

    IEnumerator SearchLastKnownPosition()
    {
        searchingLastPosition = true;
        agent.SetDestination(lastKnownPlayerPosition);

        yield return new WaitForSeconds(lostPlayerWaitTime);

        // Si el guardia vio al jugador con el tesoro y luego lo perdió, va a la salida
        if (sawPlayerWithTreasure)
        {
            Debug.Log("Guardia perdió de vista al jugador con el tesoro. Va a la salida.");
            agent.SetDestination(exitLocation.position);
        }
        else
        {
            // Si no lo vio con el tesoro, primero revisa la ubicación del tesoro
            Debug.Log("Guardia va a revisar el tesoro.");
            checkingTreasure = true;
            agent.SetDestination(treasureLocation.position);
        }

        searchingLastPosition = false;
    }

    void CheckTreasureStatus()
    {
        checkingTreasure = false; // Ya no está revisando el tesoro

        // Si el tesoro no está en su ubicación, el guardia decide ir a la salida
        Movement playerMovement = player.GetComponent<Movement>();
        if (playerMovement.hasTreasure)
        {
            Debug.Log("El guardia revisó el tesoro y ya no está. Ahora va a la salida.");
            agent.SetDestination(exitLocation.position);
        }
        else
        {
            // Si el tesoro sigue ahí, el guardia vuelve a patrullar
            Debug.Log("El guardia revisó el tesoro y sigue ahí. Retomando patrulla.");
            Patrol();
        }
    }

    // Este método se llama cuando el jugador es capturado por el guardia
    void PlayerCaptured()
    {
        // Pausar el juego
        Time.timeScale = 0f;
        
        // Mostrar el Canvas de Game Over
        gameOverCanvas.SetActive(true);
    }

    // Este método debe llamarse cuando el guardia toca al jugador
    void OnTriggerEnter(Collider other)
    {
        Debug.Log("Guardia ha tocado a algo: " + other.gameObject.name);

        if (other.CompareTag("Player"))
        {
            Debug.Log("¡El jugador fue capturado!");
            PlayerCaptured();
        }
    }

    // Este método se llama cuando el jugador llega a la meta con el tesoro
    void PlayerReachedExit()
    {
        // Pausar el juego
        Time.timeScale = 0f;
        
        // Mostrar el Canvas de Win solo al jugador
        if (player.GetComponent<Movement>().hasTreasure)
        {
            winCanvas.SetActive(true);
        }
    }

    // Este método debe llamarse cuando el jugador llega a la meta con el tesoro
    void OnTriggerExit(Collider other)
    {
        // Verificar si el jugador llega con el tesoro a la meta
        if (other.CompareTag("Player"))
        {
            Movement playerMovement = other.GetComponent<Movement>();
            if (playerMovement.hasTreasure)
            {
                PlayerReachedExit();
            }
        }
    }
}





