using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class GuardScript : MonoBehaviour
{
    public Transform[] patrolPoints;
    public Transform treasureLocation;
    public Transform exitLocation;
    public float chaseSpeed = 6f;
    public float patrolSpeed = 3f;
    public float lostPlayerWaitTime = 3f; // Tiempo que espera en la última posición conocida

    public GameObject gameOverCanvas; // Referencia al Canvas de Game Over
    public GameObject winCanvas;      // Referencia al Canvas de Win

    private NavMeshAgent agent;
    private int currentPatrolIndex = 0;
    private Transform player;
    private bool chasingPlayer = false;
    private Vector3 lastKnownPlayerPosition;
    private bool searchingLastPosition = false;
    private bool sawPlayerWithTreasure = false; // Se activa si el guardia ve al jugador con el tesoro
    private bool checkingTreasure = false; // Para saber si el guardia está verificando la ubicación del tesoro

    private VisionSensor visionSensor;
    private HearingSensor hearingSensor;

    private float communicationRange = 20f; // Rango de comunicación entre guardias
    private float lastCommunicationTime = 0f; // Último tiempo de comunicación
    private float communicationCooldown = 2f; // evitamos spam de mensajes
    private List<ACLMessage> mailbox = new List<ACLMessage>();

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        // Buscar al jugador por tag
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
        }
        else
        {
            Debug.LogError("No se encontró un objeto con el tag 'Player'. Asegúrate de que el jugador tenga el tag correcto.");
        }

        // Obtener los componentes de los sensores
        visionSensor = GetComponent<VisionSensor>();
        hearingSensor = GetComponent<HearingSensor>();

        if (visionSensor == null || hearingSensor == null)
        {
            Debug.LogError("Falta el componente VisionSensor o HearingSensor en el guardia.");
        }

        GoToNextPatrolPoint();

        // Asegurarse de que el Canvas de Game Over y Win estén desactivados al inicio
        if (gameOverCanvas != null) gameOverCanvas.SetActive(false);
        if (winCanvas != null) winCanvas.SetActive(false);
    }

    void Update()
    {
        if (player == null || visionSensor == null || hearingSensor == null)
        {
            Debug.LogError("Falta una referencia crítica: player, visionSensor o hearingSensor.");
            return; // Detener la ejecución si falta alguna referencia
        }

        if (visionSensor.CanSeePlayer() || hearingSensor.CanHearPlayer())
        {
            chasingPlayer = true;
            searchingLastPosition = false;
            checkingTreasure = false;
            lastKnownPlayerPosition = player.position;
            agent.speed = chaseSpeed;
            agent.SetDestination(player.position);

            // Verificar si el jugador tiene el tesoro
            Movement playerMovement = player.GetComponent<Movement>();
            if (playerMovement != null && playerMovement.hasTreasure)
            {
                sawPlayerWithTreasure = true;  // El guardia ha visto al jugador con el tesoro
            }

            // Notificamos a otros guardias
            if (visionSensor.CanSeePlayer())
            {
                InformGuardsAboutPlayer(player.position);
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

    private void InformGuardsAboutPlayer(Vector3 playerPosition) {

        if (Time.time - lastCommunicationTime < communicationCooldown) return;

        GameObject[] guards = GameObject.FindGameObjectsWithTag("Guard");

        foreach (var guard in guards) 
        {
            if (guard != this.gameObject) 
            {

                float distance = Vector3.Distance(transform.position, guard.transform.position);

                if (distance <= comunicationRange) 
                {

                    SendMessage(
                        guard,
                        "inform",
                        $"player_detected: {playerPosition.x},{playerPosition.y},{playerPosition.z}",
                        "flipa-inform"
                    );
                }
            }
        }

        lastCommunicationTime = Time.time;
    }

    public void SendACLMessage(GameObject receiver, string performative, string content, string protocol)
    {

        ACLMessage message = new ACLMessage(
            performative,
            this.gameObject,
            receiver,
            content,
            protocol,
            "guard_communication"
        );
        receiver.GetComponent<GuardScript>().ReceiveACLMessage(message);
    }

    public void ReceiveACLMessage(ACLMessage message)
    {
        mailbox.Add(message);
        ProcessACLMessage(message);
    }

    private void ProcessACLMessage(ACLMessage message)
    {
        switch (message.Performative)
        {
            case "inform":

                if (message.Content.StartsWith("player_detected:"))
                {
                    string[] parts = message.Content.Split(":");
                    string[] coords = parts[1].Split(",");

                    vector3 reportedPosition = new vector3
                    (
                        float.Parse(coords[0]),
                        float.Parse(coords[1]),
                        float.Parse(coords[2])
                    );

                    if(!chasingPlayer)
                    {
                        chasingPlayer = true;
                        searchingLastPosition = false;
                        checkingTreasure = false;
                        lastKnownPlayerPosition = reportedPosition;
                        agent.speed = chaseSpeed;
                        agent.SetDestination(reportedPosition);
                        Debug.Log($"{name} recibió alerta: jugador en {reportedPositiom}");
                    }
                }
                break;
        }
    }

    
}