using System;
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
    private static GuardScript currentCoordinator;
    private bool isCoordinator;
    private bool rolesAssigned = false;
    private Dictionary<GuardScript, (float, float, float)> bids = new Dictionary<GuardScript, (float, float, float)>();
    private static List<GuardScript> allGuards = new List<GuardScript>();
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
    public void SetAuctionStarted(bool status)
    {
        auctionStarted = status;
    }

    void Start()
    {
        // Registramos cada guardia en una lista estática compartida
        // Evitar duplicados al recargar escena
        if (!allGuards.Contains(this))
        {
            allGuards.Add(this);
        }

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
        if (
            (visionSensor.CanSeePlayer() || hearingSensor.CanHearPlayer())
            && !rolesAssigned && !auctionStarted && currentCoordinator == null
        )
        {
            // Si no hay coordinador, este agente asume el rol
            if (!isCoordinator && currentCoordinator == null)
            {
                // Intentar convertirse en coordinador
                TryBecomeCoordinator();
            }

            // Si soy el coordinador, iniciio acciones
            if (isCoordinator && !auctionStarted)
            {
                // Si soy el coordinador y veo al jugador, iniciar subasta
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

    void OnDestroy()
    {
        allGuards.Remove(this); // Eliminar el guardia de la lista estática al destruir el objeto
        if (currentCoordinator == this)
        {
            currentCoordinator = null; // Si el guardia es el coordinador, eliminarlo de la referencia estática
        }
    }

    private void TryBecomeCoordinator()
    {
        lock (allGuards)                    // Bloqueamos para evitar condiciones de carrera
        {
            if (currentCoordinator == null)
            {
                currentCoordinator = this;
                isCoordinator = true;

                AssignRole("chase");
                SetTarget(player.position);
                
                StartAuction();
            }
        }
    }

    private void StartAuction()
    {
        currentCoordinator = this; // Asegurar referencia
        isCoordinator = true;
        auctionStarted = true; // Marcar subasta como iniciada

        Debug.Log($"🔄 {name} inició subasta. Enviando solicitudes a {allGuards.Count - 1} guardias");

        Vector3 pos = lastKnownPlayerPosition;
        string content = $"{pos.x},{pos.y},{pos.z}";

        // Enviamos mensaje a todos los guardias para recolectar bids
        foreach (GuardScript guard in allGuards)
        {
            if (guard != this)
            {
                SendACLMessage(
                    receiver: guard.gameObject,
                    performative: "REQUEST_BID",
                    content: content,
                    protocol: "auction"
                );
            }
        }

        StartCoroutine(WaitForBids());
    }

    private IEnumerator WaitForBids()
    {
        float timeout = 5f;
        float elapsed = 0f;
        int expectedBids = allGuards.Count - 1;

        Debug.Log($"⏳ Esperando {expectedBids} bids...");

        while (elapsed < timeout && bids.Count < expectedBids)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.Log($"✅ Recibidos {bids.Count}/{expectedBids} bids");

        AssignRoles();
        currentCoordinator = null;
        isCoordinator = false;
    }

    private void HandlePlayerDetection()
    {
        if (!auctionStarted)
        {
            auctionStarted = true;

            lastKnownPlayerPosition = player.position;
            sawPlayerWithTreasure = player.GetComponent<Movement>()?.hasTreasure ?? false;

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

    public void ReceiveBid(GuardScript sender, float pDist, float tDist, float eDist)
    {
        if (isCoordinator && sender != null)
        {
            Debug.Log($"Recibido bid de {sender.name}: {pDist}, {tDist}, {eDist}");
            bids[sender] = (pDist, tDist, eDist);
        }
    }

    public void ReceiveACLMessage(ACLMessage message)
    {

        Debug.Log($"{name} recibió mensaje '{message.Performative}' de {(message.Sender != null ? message.Sender.name : "null")}");

        if (message.Performative == "REQUEST_BID" && !isCoordinator)
        {
            Vector3 playerPos   = ParsePosition(message.Content);
            float playerDist    = Vector3.Distance(transform.position, playerPos);
            float treasureDist  = Vector3.Distance(transform.position, treasureLocation.position);
            float exitDist      = Vector3.Distance(transform.position, exitLocation.position);

            // Le mando mis distancias de vuelta al coordinador
            var coord = message.Sender.GetComponent<GuardScript>();
            if (coord != null)
            {
                Debug.Log($"{name} va a llamar a ReceiveBid en {coord.name}");
                coord.ReceiveBid(this, playerDist, treasureDist, exitDist);
            }

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
    
    private Vector3 ParsePosition(string positionStr)
    {
        string[] parts = positionStr.Split(',');
        
        return new Vector3(
            float.Parse(parts[0]),
            float.Parse(parts[1]),
            float.Parse(parts[2])
        );
    }

    private void AssignRoles()
    {
        List<GuardScript> availableGuards = new List<GuardScript>(allGuards);
        availableGuards.Remove(this); // El coordinador ya tiene rol

        // Auto-asignar rol de perseguidor
        AssignClosestRole("chase", availableGuards, g => bids[g].Item1);
        
        // Asignar guardián del tesoro
        AssignClosestRole("treasure", availableGuards, g => bids[g].Item2);
        
        // Asignar guardián de salida
        AssignClosestRole("exit", availableGuards, g => bids[g].Item3);

        // El resto patrulla
        foreach (GuardScript guard in availableGuards)
        {
            guard.AssignRole("patrol");
        }

        rolesAssigned = true;
        auctionStarted = false;
        currentCoordinator = null;
        isCoordinator = false;
    }

    private void AssignClosestRole(string role, List<GuardScript> guards, Func<GuardScript, float> distanceSelector)
    {
        GuardScript closest = null;
        float minDistance = Mathf.Infinity;

        foreach (GuardScript guard in guards)
        {
            if (!bids.ContainsKey(guard))
            {
                Debug.LogWarning($"Guardia {guard.name} no tiene una oferta registrada.");
                continue; // Si el guardia no tiene una oferta, lo ignoramos
            }

            float dist = distanceSelector(guard);
            if (dist < minDistance)
            {
                minDistance = dist;
                closest = guard;
            }
        }

        if (closest != null)
        {
            closest.AssignRole(role);
            guards.Remove(closest);
        }
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

        if (receiver == null)
        {
            Debug.LogError("Intentando enviar mensaje a receptor nulo");
            return;
        }

        Debug.Log(receiver.name);
        ACLMessage message = new ACLMessage(performative, this.gameObject, receiver, content, protocol, "guard_communication");
        receiver.GetComponent<GuardScript>().ReceiveACLMessage(message);
    }

    void InformGuardsAboutPlayer(Vector3 playerPosition)
    {
        // Enviar información solo a los guardias en modo "chase"
        foreach (GuardScript otherGuard in allGuards)
        {
            if (otherGuard != this && otherGuard.role == "chase")
            {
                otherGuard.ReceiveACLMessage(new ACLMessage("inform", this.gameObject, otherGuard.gameObject, playerPosition.ToString(), "chase_protocol", "guard_communication"));
            }
        }
    }
}