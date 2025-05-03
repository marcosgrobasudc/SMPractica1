

using System.Collections;
using UnityEngine;
using UnityEngine.AI;

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

    private VisionSensor visionSensor;
    private HearingSensor hearingSensor;

    // Variables para los informs en chase
    private float informChaseInterval = 0.5f;
    private float lastInformTime = 0f;


    public Transform GetPlayerPosition()
    {
        return player; // Devuelve la referencia al jugador
    }
    protected override (float, float, float) CalculateDistances(Vector3 playerPos)
    {
        return (
            Vector3.Distance(transform.position, playerPos),
            Vector3.Distance(transform.position, TreasureLocation.position),
            Vector3.Distance(transform.position, ExitLocation.position)
        );
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

        if ((visionSensor.CanSeePlayer() || hearingSensor.CanHearPlayer())
            && !rolesAssigned && !auctionStarted && currentCoordinator == null)
        {
            // Guarda la última posición conocida
            lastKnownPlayerPosition = player.position;  

            if (!isCoordinator && currentCoordinator == null)
            {
                TryBecomeCoordinator();
            }

            if (isCoordinator && !auctionStarted)
            {
                HandlePlayerDetection();
            }
            else if (!isCoordinator && role == "chase")
            {
                Chase();
            }
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
                default:
                    Debug.LogWarning($"Rol desconocido: {role}");
                    break;
            }
        }
    }

    public override void AssignRole(string newRole) 
    {
        base.AssignRole(newRole);
        
        switch (role)
        {
            case "chase":
                agent.speed = chaseSpeed;
                break;
            case "treasure":
                agent.speed = patrolSpeed;
                agent.SetDestination(TreasureLocation.position);
                break;
            case "exit":
                agent.speed = patrolSpeed;
                agent.SetDestination(ExitLocation.position);
                break;
            case "patrol":
                agent.speed = patrolSpeed;
                GoToNextPatrolPoint();
                break;
        }
    }
    
    public void SetTarget(Vector3 targetPosition) 
    {
        agent.SetDestination(targetPosition);
    }

    void Chase()
    {
        if (visionSensor.CanSeePlayer() || hearingSensor.CanHearPlayer())
        {
            chasingPlayer = true;
            lastKnownPlayerPosition = player.position;
            agent.speed = chaseSpeed;
            agent.SetDestination(player.position);

            if (Time.time - lastInformTime > informChaseInterval)
            {
                lastInformTime = Time.time;

                // Informamos a los demás agentes sobre la posición del jugador
                string content = $"{lastKnownPlayerPosition.x.ToString("F4").Replace(',', '.')}," +
                                $"{lastKnownPlayerPosition.y.ToString("F4").Replace(',', '.')}," +
                                $"{lastKnownPlayerPosition.z.ToString("F4").Replace(',', '.')}";
                Debug.Log($"Aquí está el jugador!! {content}");

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

            Movement playerMovement = player.GetComponent<Movement>();
            if (playerMovement != null && playerMovement.hasTreasure)
            {
                InformAgentsAboutPlayer(player.position);
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

    IEnumerator SearchLastKnownPosition()
    {
        searchingLastPosition = true;
        agent.SetDestination(lastKnownPlayerPosition);

        yield return new WaitForSeconds(lostPlayerWaitTime);

        if (!hasLoggedTreasureCheck) // Solo imprime una vez
        {
            if (sawPlayerWithTreasure)
            {
                Debug.Log("Guardia perdió de vista al jugador con el tesoro. Va a la salida.");
                agent.SetDestination(ExitLocation.position);
            }
            else
            {
                Debug.Log("Guardia va a revisar el tesoro.");
                checkingTreasure = true;
                agent.SetDestination(TreasureLocation.position);
            }
            hasLoggedTreasureCheck = true;
        }

        searchingLastPosition = false;
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
        if (agent.remainingDistance < 1f && !agent.pathPending)
        {
            if (!hasCheckedTreasure) // Solo imprime una vez
            {
                Movement playerMovement = player.GetComponent<Movement>();
                if (playerMovement != null && playerMovement.hasTreasure)
                {
                    Debug.Log("El guardia revisó el tesoro y no está allí. Va a la salida.");
                    agent.SetDestination(ExitLocation.position);
                }
                else
                {
                    Debug.Log("El guardia revisó el tesoro y sigue ahí. Retomando patrullaje.");
                    Patrol();
                }
                hasCheckedTreasure = true; // Evita que se repita el mensaje
            }
        }
        else
        {
            hasCheckedTreasure = false; // Reinicia si se aleja del tesoro
        }
    }

    private void HandlePlayerDetection()
    {
        if (!auctionStarted)
        {
            auctionStarted = true;
            lastKnownPlayerPosition = player.position;
            sawPlayerWithTreasure = player.GetComponent<Movement>()?.hasTreasure ?? false;
            AssignRole("chase");
            SetTarget(lastKnownPlayerPosition);
        }
        else
        {
            SetTarget(player.position);
        }
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
}