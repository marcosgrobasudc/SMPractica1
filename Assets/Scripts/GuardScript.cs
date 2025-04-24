using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class GuardScript : MonoBehaviour
{
    // Variables de movimiento y sensores
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
    private bool checkingTreasure = false;
    private bool sawPlayerWithTreasure = false;

    private VisionSensor visionSensor;
    private HearingSensor hearingSensor;
    private List<ACLMessage> mailbox = new List<ACLMessage>();
    private string role = "patrol";
    private bool auctionStarted = false;
    
    // Variables para coordinación de guardias
    [SerializeField] private bool isCoordinator;
    public bool IsCoordinator => isCoordinator;
    public bool IsAvailableForAssignment() 
    {
        return (role == "patrol" || role == "idle");
    }
    public void AssignRole(string newRole) 
    {
        role = newRole;
        
        switch (role)
        {
            case "chase":
                agent.speed = chaseSpeed;
                break;
            case "treasure":
                agent.speed = patrolSpeed;
                agent.SetDestination(treasureLocation.position);
                break;
            case "exit":
                agent.speed = patrolSpeed;
                agent.SetDestination(exitLocation.position);
                break;
            case "patrol":
                agent.speed = patrolSpeed;
                GoToNextPatrolPoint();
                break;
        }
        
        Debug.Log($"{name} asignado a rol: {role}");
    }
    public void SetTarget(Vector3 targetPosition) 
    {
        agent.SetDestination(targetPosition);
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform; // Buscar el jugador por tag

        if (player == null)
        {
            Debug.LogError("NO HAY JUGADOR CON TAG 'Player' EN LA ESCENA");
            enabled = false; // Desactiva el script
        }    

        visionSensor = GetComponent<VisionSensor>();
        hearingSensor = GetComponent<HearingSensor>();    

        if (visionSensor == null) Debug.LogError($"{name} no tiene componente VisionSensor.");
        if (hearingSensor == null) Debug.LogError($"{name} no tiene componente HearingSensor.");    
        
        if (player != null)
        {
            player = player.transform;
        }

        if (player == null)
        {
            Debug.LogError("Juagdor no encontrado.");
            enabled = false; // Desactivar el script si no se encuentra el jugador
            return;
        }
        
        // Registro en el coordinador (empieza pasivo)
        if (GuardCoordinator.Instance != null)
        {
            GuardCoordinator.Instance.RegisterGuard(this);
        }

        GoToNextPatrolPoint();        

        // Asegurarse de que el Canvas de Game Over y Win estén desactivados al inicio
        if (gameOverCanvas != null) gameOverCanvas.SetActive(false);
        if (winCanvas != null) winCanvas.SetActive(false);
    }

    void Update()
    {

        ProcessMailbox(); // Procesamos mensajes de la bandeja de entrada

        if (player == null || visionSensor == null || hearingSensor == null)
        {
            Debug.LogError("Falta una referencia crítica: player, visionSensor o hearingSensor.");
            enabled = false;
            return; // Detener la ejecución si falta alguna referencia
        }

        // **1. Prioridad: Comprobar si el guardia ve o escucha al jugador**
        if (visionSensor.CanSeePlayer() || hearingSensor.CanHearPlayer())
        {
            // Si no hay coordinador, este agente asume el rol
            if (GuardCoordinator.Instance == null || !GuardCoordinator.Instance.HasActiveCoordinator())
            {
                BecomeCoordinator();
            }

            // Si soy el coordinador, iniciio acciones
            if (isCoordinator)
            {
                HandlePlayerDetection();
            }

            else if (!isCoordinator && role == "chase")
            {
                // Si no soy el coordinador y tengo el rol de "chase", persigo al jugador
                Chase();
            }
        }

        // **2. Si no se está persiguiendo al jugador, proceder con otros roles**
        else
        {
            switch (role)
            {
                case "chase":
                    Chase();
                    break;

                case "patrol":
                    Patrol();
                    break;

                case "treasure":
                    CheckTreasure();
                    break;

                case "exit":
                    GoToExit();
                    break;

                default:
                    Debug.LogWarning($"Rol desconocido: {role}");
                    break;
            }
        }
    }

    private void BecomeCoordinator()
    {
        isCoordinator = true;
        GuardCoordinator.Instance.SetCurrentCoordinator(this);
        Debug.Log($"{name} ha sido ascendido a coordinador");
    }

    private void HandlePlayerDetection()
    {
        if (!auctionStarted)
        {
            auctionStarted = true;

            lastKnownPlayerPosition = player.position;
            sawPlayerWithTreasure = player.GetComponent<Movement>()?.hasTreasure ?? false;

            GuardCoordinator.Instance.StartAuction(
                caller: this,
                playerPosition: lastKnownPlayerPosition,
                treasureLoc: treasureLocation,
                exitLoc: exitLocation
            );

            // Auto-asignación del coordinador
            AssignRole("chase");
            SetTarget(lastKnownPlayerPosition);
        }

        else
        {
            // Ya hemos arrancado la subasta, solo actualizamos el destino
            SetTarget(player.position);
        }
    }

    void Chase()
    {
        // Si el guardia está persiguiendo al jugador
        if (visionSensor.CanSeePlayer() || hearingSensor.CanHearPlayer())
        {
            chasingPlayer = true;
            lastKnownPlayerPosition = player.position; // Actualizar la posición conocida del jugador
            agent.speed = chaseSpeed; // Establecer la velocidad de persecución
            agent.SetDestination(player.position); // Establecer la posición del jugador como destino

            // Verificar si el jugador tiene el tesoro
            Movement playerMovement = player.GetComponent<Movement>();
            if (playerMovement != null && playerMovement.hasTreasure)
            {
                // El guardia ha visto al jugador con el tesoro, informar a otros guardias
                InformGuardsAboutPlayer(player.position);
            }
        }
        else if (agent.remainingDistance < 1f)
        {
            auctionStarted = false;

            // Si el guardia ya no está cerca del jugador, iniciar búsqueda de la última posición conocida
            chasingPlayer = false;
            StartCoroutine(SearchLastKnownPosition());
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

    void GoToExit()
    {
        // Establecer la velocidad para ir a la salida (puede ser la misma que la de patrullaje o diferente)
        agent.speed = patrolSpeed;

        // Moverse a la ubicación de salida
        agent.SetDestination(exitLocation.position);

        // Si el guardia llega a la salida, podemos poner más lógica aquí si lo necesitas
        if (agent.remainingDistance < 1f)
        {
            Debug.Log("El guardia ha llegado a la salida.");
        }
    }

    void CheckTreasure()
    {
        // Si el guardia ha llegado a la ubicación del tesoro
        if (agent.remainingDistance < 1f)
        {
            checkingTreasure = false; // Dejar de revisar el tesoro

            // Verificar si el tesoro sigue allí
            Movement playerMovement = player.GetComponent<Movement>();
            if (playerMovement != null && playerMovement.hasTreasure)
            {
                // El tesoro ha sido robado, el guardia va a la salida
                Debug.Log("El guardia revisó el tesoro y no está allí. Va a la salida.");
                agent.SetDestination(exitLocation.position);
            }
            else
            {
                // El tesoro sigue en su lugar, retomar patrullaje
                Debug.Log("El guardia revisó el tesoro y sigue ahí. Retomando patrullaje.");
                Patrol();
            }
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

    public void ReceiveACLMessage(ACLMessage message)
    {
        if (message.Performative == "CALL_FOR_PROPOSAL")
        {
            // Parseamos la posición del juador
            var parts = message.Content.Split(',');
            Vector3 playerPos = new Vector3(
                float.Parse(parts[0]),
                float.Parse(parts[1]),
                float.Parse(parts[2])
            );

            // Calculamos distancias 
            float dPlayer   = Vector3.Distance(transform.position, playerPos);
            float dTreasure= Vector3.Distance(transform.position, treasureLocation.position);
            float dExit    = Vector3.Distance(transform.position, exitLocation.position);

            // Enviamos propuesta al coordinador
            string bidPayload = $"{dPlayer},{dTreasure},{dExit}";
            SendACLMessage(
                receiver: message.Sender,
                performative: "PROPOSE",
                content: bidPayload,
                protocol: "auction_protocol"
            );

            return;
        }

        if (message.Performative == "ASSIGN_ROLE")
        {
            // Asignar rol al guardia
            AssignRole(message.Content);
            if (message.Content == "chase")
            {
                SetTarget(lastKnownPlayerPosition); // Establecer la posición del jugador como destino
            }

            return;
        }

        mailbox.Add(message);
    }
    
    private void ProcessMailbox()
    {
        foreach (ACLMessage message in mailbox)
        {
            if (message.Performative == "inform")
            {
                // Extraer la posición del contenido (Content) del mensaje
                string[] positionData = message.Content.Split(',');
                if (positionData.Length == 3)
                {
                    float x = float.Parse(positionData[0]);
                    float y = float.Parse(positionData[1]);
                    float z = float.Parse(positionData[2]);

                    Vector3 playerPosition = new Vector3(x, y, z);

                    // Ahora puedes usar la posición del jugador como desees, por ejemplo, para seguirlo
                    Debug.Log($"Recibí la posición del jugador: {playerPosition}");
                }
            }
        }
        mailbox.Clear();
    }
    
    public void SendACLMessage(GameObject receiver, string performative, string content, string protocol)
    {
        ACLMessage message = new ACLMessage(performative, this.gameObject, receiver, content, protocol, "guard_communication");
        receiver.GetComponent<GuardScript>().ReceiveACLMessage(message);
    }

    private void SendBidToAuction()
    {
        // Calculo distancias para la oferta
        float distToPlayer = Vector3.Distance(transform.position, lastKnownPlayerPosition);
        float distToTreasure = Vector3.Distance(transform.position, treasureLocation.position);
        float distToExit = Vector3.Distance(transform.position, exitLocation.position);

        // Envio mensaje bid al coordinador
        ACLMessage bidMessage = new ACLMessage(
            performative: "bid",
            sender: gameObject,
            receiver: GuardCoordinator.Instance.CurrentCoordinator.gameObject,
            content: $"playerDist:{distToPlayer},treasureDist:{distToTreasure},exitDist:{distToExit}",
            protocol: "auction",
            ontology: "guard_roles"
        );

        GuardCoordinator.Instance.CurrentCoordinator.ReceiveACLMessage(bidMessage);
    }

    void InformGuardsAboutPlayer(Vector3 playerPosition)
    {
        // Enviar información solo a los guardias en modo "chase"
        foreach (GuardScript otherGuard in GuardCoordinator.Instance.Guards)
        {
            if (otherGuard != this && otherGuard.role == "chase")
            {
                otherGuard.ReceiveACLMessage(new ACLMessage("inform", this.gameObject, otherGuard.gameObject, playerPosition.ToString(), "chase_protocol", "guard_communication"));
            }
        }
    }
}