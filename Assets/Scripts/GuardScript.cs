using System.Collections;
using System.Collections.Generic;
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
    private bool checkingTreasure = false;

    private VisionSensor visionSensor;
    private HearingSensor hearingSensor;
    
    private List<ACLMessage> mailbox = new List<ACLMessage>();
    
    private string role = "patrol";
    
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
        }
        
        visionSensor = GetComponent<VisionSensor>();
        hearingSensor = GetComponent<HearingSensor>();
        
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
        if (player == null || visionSensor == null || hearingSensor == null)
        {
            Debug.LogError("Falta una referencia crítica: player, visionSensor o hearingSensor.");
            return; // Detener la ejecución si falta alguna referencia
        }

        // **1. Prioridad: Comprobar si el guardia ve o escucha al jugador**
        if (visionSensor.CanSeePlayer() || hearingSensor.CanHearPlayer())
        {
            // Cambiar a rol "chase" inmediatamente si detecta al jugador
            role = "chase";
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
            else if (message.Performative == "call_for_bids")
            {
                string[] positionData = message.Content.Split(',');
                if (positionData.Length == 3)
                {
                    float x = float.Parse(positionData[0]);
                    float y = float.Parse(positionData[1]);
                    float z = float.Parse(positionData[2]);

                    Vector3 playerPosition = new Vector3(x, y, z);
                    
                    // Ahora puedes calcular la distancia a la posición del jugador
                    float distance = Vector3.Distance(transform.position, playerPosition);

                    SendACLMessage(message.Sender, "bid", distance.ToString(), "auction_protocol");
                }            
            }
            else if (message.Performative == "assign_role")
            {
                role = message.Content;
            }
        }
        mailbox.Clear();
    }
    
    public void SendACLMessage(GameObject receiver, string performative, string content, string protocol)
    {
        ACLMessage message = new ACLMessage(performative, this.gameObject, receiver, content, protocol, "guard_communication");
        receiver.GetComponent<GuardScript>().ReceiveACLMessage(message);
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