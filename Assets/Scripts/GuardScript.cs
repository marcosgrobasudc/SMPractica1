using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using System.Linq;
using System.Globalization;

public class GuardScript : MultiAgentSystem
{
    // Variables de movimiento y sensores
    public Transform[] patrolPoints;
    // public Transform treasureLocation;
    // public Transform exitLocation;

    public float chaseSpeed = 6f;
    public float patrolSpeed = 3f;
    public float lostPlayerWaitTime = 3f;

    public GameObject gameOverCanvas;
    public GameObject winCanvas;

    public override bool IsGuard => true;

    private NavMeshAgent agent;
    private int currentPatrolIndex = 0;
    private Transform player;
    private bool chasingPlayer = false;
    // private Vector3 lastKnownPlayerPosition;
    private bool searchingLastPosition = false;
    private bool checkingTreasure = false;
    private bool sawPlayerWithTreasure = false;
    private bool hasReachedExit = false;
    private bool hasCheckedTreasure = false;
    private bool hasLoggedTreasureCheck = false;
    private bool hasCheckedLastPosition = false;

    private VisionSensor visionSensor;
    private HearingSensor hearingSensor;

    // Variables para los informs en chase
    private float informChaseInterval = 0.5f;
    private float lastInformTime = 0f;

    private bool guardingTreasure = false;
    private bool guardingBlockage = false;

    // Variables para la lógica de las puertas
    private bool justPassedDoor = false;
    private float doorPassGracePeriod = 2f;
    private int doorConfirmations = 0;
    private float doorResponseTimeout = 0.5f;

    public Transform GetPlayerPosition()
    {
        return player; // Devuelve la referencia al jugador
    }

    protected override (float playerDist, float treasureDist, float exitDist) CalculateDistances(Vector3 playerPosition)
    {
        float treasureDist = MultiAgentSystem.PlayerHasTreasure 
            ? Mathf.Infinity  // Si el tesoro fue robado, distancia infinita
            : Vector3.Distance(transform.position, TreasureLocation.position);

        return (
            Vector3.Distance(transform.position, playerPosition),
            treasureDist,
            Vector3.Distance(transform.position, ExitLocation.position)
        );
    }

    public override void SetTarget(Vector3 targetPosition) 
    {
        agent.SetDestination(targetPosition);
    }

    protected override void Start()
    {
        base.Start();
        
        agent = GetComponent<NavMeshAgent>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (player == null)
        {
            Debug.LogError("NO HAY JUGADOR CON TAG 'Player' EN LA ESCENA");
            enabled = false;
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
            enabled = false;
            return;
        }

        GoToNextPatrolPoint();        

        if (gameOverCanvas != null) gameOverCanvas.SetActive(false);
        if (winCanvas != null) winCanvas.SetActive(false);
    }

    void Update()
    {
        ProcessMailbox();

        if (player == null || visionSensor == null || hearingSensor == null)
        {
            Debug.LogError("Falta una referencia crítica: player, visionSensor o hearingSensor.");
            enabled = false;
            return;
        }

        // Escuchamos algo pero no vemos al jugador
        if (!visionSensor.CanSeePlayer() && hearingSensor.CanHearPlayer()
            && !rolesAssigned && !auctionStarted && currentCoordinator == null && !justPassedDoor)
        {
            // Comprobamos que no hemos pasado por una puerta
            Debug.Log($"{name} escuchó un sonido sospechoso, pero no pasó por una puerta.");
            // Preguntamos a mis compañeros si han pasado por alguna puerta
            BroadcastDoorInquiry();
            // Cambiamos de estado para no volver a preguntar inmediatamente
            rolesAssigned = true;
        }

        if ((visionSensor.CanSeePlayer() || hearingSensor.CanHearPlayer()) 
            && !auctionStarted 
            && !rolesAssigned
            && currentCoordinator == null)
        {
            lastKnownPlayerPosition = player.position;
            bool hasTreasure = player.GetComponent<Movement>().hasTreasure;
            TryBecomeCoordinator(hasTreasure);
        }
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
                case "blockage1":
                    if (!guardingBlockage)
                    {
                        // Llegó al punto, se queda vigilando
                        guardingBlockage = true;
                        agent.isStopped = true;
                    }
                    break;
                case "blockage2":
                    if (!guardingBlockage)
                    {
                        // Llegó al punto, se queda vigilando
                        guardingBlockage = true;
                        agent.isStopped = true;
                    }
                    break; 
                default:
                    Debug.LogWarning($"Rol desconocido: {role}");
                    break;
            }

            // Si estamos en un punto estratégico, vigilamos
            if (guardingBlockage && !visionSensor.CanSeePlayer() && !hearingSensor.CanHearPlayer())
            {
                // No hacemos nada, solo vigilamos
                return;
            }

            // Si estamos en un punto estratégico y vemos al jugador, vamos a perseguirlo
            if (guardingBlockage && (visionSensor.CanSeePlayer() || hearingSensor.CanHearPlayer()))
            {
                guardingBlockage = false;
                lastKnownPlayerPosition = player.position;
                AssignRole("chase");
                return;
            }
        }
    }

    public override void AssignRole(string newRole) 
    {
        base.AssignRole(newRole);
        agent.isStopped = false;
        
        switch (newRole)
        {
            case "chase":
                agent.speed = chaseSpeed;
                guardingBlockage = false;
                guardingTreasure = false;
                break;

            case "treasure":
                agent.speed = patrolSpeed;
                agent.SetDestination(TreasureLocation.position);
                guardingBlockage = false;
                break;

            case "exit":
                agent.speed = patrolSpeed;
                agent.SetDestination(ExitLocation.position);
                guardingBlockage = false;
                break;

            case "blockage1":
                agent.speed = patrolSpeed;
                guardingBlockage = true;
                break;

            case "blockage2":
                agent.speed = patrolSpeed;
                guardingBlockage = true;
                break;

            case "patrol":
                hasCheckedLastPosition = false; 
                agent.speed = patrolSpeed;
                GoToNextPatrolPoint();
                guardingBlockage = false;
                guardingTreasure = false;
                break;
        }
    }

    void Chase()
    {
        if (visionSensor.CanSeePlayer() || hearingSensor.CanHearPlayer())
        {
            chasingPlayer = true;
            hasCheckedLastPosition = false;
            lastKnownPlayerPosition = player.position;
            agent.speed = chaseSpeed;
            agent.SetDestination(player.position);

            // Verificar si el jugador tiene el tesoro
            Movement playerMovement = player.GetComponent<Movement>();
            if (playerMovement != null && playerMovement.hasTreasure && !MultiAgentSystem.PlayerHasTreasure)
            {
                Debug.Log($"{name} detectó al jugador con el tesoro. PlayerHasTreasure = true");
                MultiAgentSystem.PlayerHasTreasure = true;
                InformAgentsAboutPlayer(player.position);
            }

            if (Time.time - lastInformTime > informChaseInterval)
            {
                lastInformTime = Time.time;
                string content = $"{lastKnownPlayerPosition.x.ToString("F4").Replace(',', '.')};" +
                                $"{lastKnownPlayerPosition.y.ToString("F4").Replace(',', '.')};" +
                                $"{lastKnownPlayerPosition.z.ToString("F4").Replace(',', '.')}";
                // Informar a otros agentes
                foreach (MultiAgentSystem other in allAgents)
                {
                    if (other != this)
                    {
                        SendACLMessage(
                            receiver: other.gameObject,
                            performative: "inform",
                            content: content,
                            protocol: "chase_protocol"
                        );
                    }
                }
            }
        }
        else if (agent.remainingDistance < 1f)
        {
            auctionStarted = false;
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

    private void GoToStrategicPoint()
    {
        // Buscmmos la ultima posición conocida del jugador
        Vector3 lastPlayerPos = Vector3.zero;
        bool found = false;

        // Recorremos la mailbox de atrás a delante
        for (int i = mailbox.Count -1; i >= 0; i--)
        {
            var msg = mailbox[i];

            if (msg.Performative == "inform")
            {
                lastPlayerPos = ParsePosition(msg.Content);
                Debug.Log($"Guardia encontró la posición del jugador: {lastPlayerPos}.");
                found = true;
                break;
            }
        }

        // Mostramos los puntos de bloqueo
        foreach (var pt in blockages)
        {
            Debug.Log($"Punto de bloqueo: {pt.position}");
        }

        if (!found || blockages.Length == 0)
        {
            Debug.LogWarning("No se encontró la posición del jugador o no hay puntos de bloqueo");
            return;
        }

        // Buscamos el punto de bloqueo más cercano
        Transform closest = null;
        float minDist = Mathf.Infinity;

        foreach (var pt in blockages)
        {
            float d = Vector3.Distance(pt.position, lastPlayerPos);
            if (d < minDist)
            {
                minDist = d;
                closest = pt;
            }
        }

        // Nos movemos hasta ese punto y esperamos ahí vigilando
        if (closest != null)
        {
            Debug.Log($"{name} va al punto '{closest.name}' cerca de {lastPlayerPos}.");
            agent.isStopped = false;
            agent.SetDestination(closest.position);
            guardingBlockage = true;        
        }
    }

    IEnumerator SearchLastKnownPosition()
    {
        if(hasCheckedLastPosition) yield break; // Salir si ya verificamos
        
        searchingLastPosition = true;
        agent.SetDestination(lastKnownPlayerPosition);

        while(agent.pathPending || agent.remainingDistance > 1f)
        {
            yield return null;
        }

        Debug.Log($"{name} ha llegado a la última posición conocida del jugador.");
        hasCheckedLastPosition = true; // Marcar como verificado

        // if(!auctionStarted && currentCoordinator == null)
        // {
        //     bool hasTreasure = player.GetComponent<Movement>()?.hasTreasure ?? false;
        //     TryBecomeCoordinator(hasTreasure);
        // }

        searchingLastPosition = false;
        
        // Volver a patrullar después de verificar
        if(!visionSensor.CanSeePlayer() && !hearingSensor.CanHearPlayer())
        {
            AssignRole("patrol");
        }
    }

    void GoToExit()
    {
        // Si ya estamos en la salida, no hacer nada
        if (Vector3.Distance(transform.position, ExitLocation.position) < 1f)
        {
            if (!hasReachedExit) // Solo imprime una vez
            {
                Debug.Log("El guardia ha llegado a la salida.");
                hasReachedExit = true;
            }
            return;
        }

        // Si no ha llegado, reinicia el flag y sigue moviéndose
        hasReachedExit = false;
        agent.speed = patrolSpeed;
        agent.SetDestination(ExitLocation.position);
    }

    void CheckTreasure()
    {
        // Verificación continua incluso si está vigilando
        if (visionSensor.CanSeePlayer() || hearingSensor.CanHearPlayer())
        {
            Debug.Log("¡Jugador detectado cerca del tesoro! Iniciando subasta...");
            lastKnownPlayerPosition = player.position;
            TryBecomeCoordinator(player.GetComponent<Movement>().hasTreasure);
            return;
        }

        if (agent.pathPending || agent.remainingDistance >= 1f) return;

        // Lógica original de verificación del tesoro
        if (!hasCheckedTreasure)
        {
            Movement playerMovement = player.GetComponent<Movement>();
            bool treasureIsGone = !TreasureLocation.gameObject.activeInHierarchy;

            if (treasureIsGone || (playerMovement != null && playerMovement.hasTreasure))
            {
                if (!MultiAgentSystem.PlayerHasTreasure)
                {
                    MultiAgentSystem.PlayerHasTreasure = true;
                    Debug.Log("¡Tesoro robado! Iniciando subasta...");
                    TryBecomeCoordinator(true);
                }
            }
            else
            {
                Debug.Log("Vigilando tesoro...");
                agent.isStopped = true;
            }
            hasCheckedTreasure = true;
        }
    }

    private void HandlePlayerDetection()
    {
        if (!auctionStarted)
        {
            // 1. Verificar SI el jugador tiene el tesoro ANTES de iniciar subasta
            Movement playerMovement = player.GetComponent<Movement>();
            if (playerMovement != null && playerMovement.hasTreasure)
            {
                MultiAgentSystem.PlayerHasTreasure = true; // Actualizar estado global
                Debug.Log($"{name} vio al jugador con el tesoro - Ignorando tesoro en subasta");
            }

            // 2. Iniciar subasta con retraso de 1 frame (para asegurar sincronización)
            StartAuction(player.position);
        }
    }

    // Método para preguntar si alguien pasó por una puerta
    private void BroadcastDoorInquiry()
    {
        foreach (var other in allAgents.Where(a => a.IsGuard && a != this))
        {
            SendACLMessage(
                receiver: other.gameObject,
                performative: "ASK_DOOR",
                content: "¿Brother has pasado por alguna puerta?",
                protocol: "door_check"
            );
        }

        // Preparamos lista para recoger confirmaciones
        doorConfirmations = 0;
        StartCoroutine(WaitForDoorResponses());
    }

    void PlayerCaptured()
    {
        Time.timeScale = 0f;
        gameOverCanvas.SetActive(true);
    }

    void PlayerReachedExit()
    {
        Time.timeScale = 0f;
        if (player.GetComponent<Movement>().hasTreasure)
        {
            winCanvas.SetActive(true);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Door"))
        {
            Debug.Log($"{name} pasó por la puerta.");
            justPassedDoor = true;
            StartCoroutine(ResetJustPassedDoor());
        }

        if (other.CompareTag("Player"))
        {
            Debug.Log("¡El jugador fue capturado!");
            PlayerCaptured();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Movement playerMovement = other.GetComponent<Movement>();
            if (playerMovement.hasTreasure)
            {
                PlayerReachedExit();
            }
        }
    }

    private IEnumerator ResetJustPassedDoor()
    {
        yield return new WaitForSeconds(doorPassGracePeriod);
        justPassedDoor = false;
    }

    private IEnumerator WaitForDoorResponses()
    {
        float elapsed = 0f;
        while (elapsed < doorResponseTimeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (doorConfirmations > 0)
        {
            // Al menos uno respondió que sí, ignoramos este sonido
            Debug.Log($"{name}: sonido de puerta confirmado por compañero, lo ignoro.");
        }
        else
        {
            // Nadie pasó por puerta → tratar como sonido “sospechoso”
            // Debug.Log($"{name}: nadie pasó por puerta, ¡investigo!");
            // AssignRole("chase");   // o lanzo subasta, o voy a lastKnownPlayerPosition
        }
    }

    public override void ReceiveACLMessage(ACLMessage message)
    {
        base.ReceiveACLMessage(message);

        // Si me llega un inform de una camara, aviso al resto de los agentes
        if (message.Performative == "camera_alert" && message.Protocol == "camera_protocol")
        {
            try
            {
                // 1. Actualizar la última posición conocida
                lastKnownPlayerPosition = ParsePosition(message.Content);
                Debug.Log($"{name} recibió alerta de cámara. Posición: {lastKnownPlayerPosition}");

                // 2. Enviar INFORM a TODOS los agentes (igual que en chase)
                string content = $"{lastKnownPlayerPosition.x.ToString("F4", CultureInfo.InvariantCulture)};" +
                                $"{lastKnownPlayerPosition.y.ToString("F4", CultureInfo.InvariantCulture)};" +
                                $"{lastKnownPlayerPosition.z.ToString("F4", CultureInfo.InvariantCulture)}";

                foreach (MultiAgentSystem agent in allAgents)
                {
                    if (agent != this) // No nos enviamos a nosotros mismos
                    {
                        SendACLMessage(
                            receiver: agent.gameObject,
                            performative: "inform",
                            content: content,
                            protocol: "position_update"
                        );
                    }
                }

                // 3. Lógica de subasta solo si no hay nadie persiguiendo
                bool someoneChasing = allAgents.Any(a => a.CurrentRole == "chase");
                if (!someoneChasing && !auctionStarted && currentCoordinator == null)
                {
                    bool playerHasTreasure = player.GetComponent<Movement>().hasTreasure;
                    TryBecomeCoordinator(playerHasTreasure);
                }
            }
            catch (System.FormatException e)
            {
                Debug.LogError($"Error al parsear posición de cámara: {e.Message}");
            }
        }

        if (message.Performative == "ASK_DOOR" && message.Protocol == "door_check")
        {
            if (justPassedDoor)
            {
                // Respondo que sí pasé por puerta
                SendACLMessage(
                    receiver: message.Sender,
                    performative: "CONFIRM_DOOR",
                    content: "Si que pasé brother",
                    protocol: "door_check"
                );
            }
            // si justPassedDoor==false, no respondo nada
            return;
        }

        if (message.Performative == "CONFIRM_DOOR" && message.Protocol == "door_check")
        {
            // Un compañero confirmó que pasó por puerta
            var guard = message.Sender.GetComponent<MultiAgentSystem>();
            if (guard != null && guard.IsGuard)
            {
                ((GuardScript)this).doorConfirmations++;
            }
            return;
        }    
    }

}