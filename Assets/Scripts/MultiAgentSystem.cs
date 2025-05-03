using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public abstract class MultiAgentSystem : MonoBehaviour
{
    // Variables de comunicación multiagente
    protected List<ACLMessage> mailbox = new List<ACLMessage>();
    protected string role = "patrol";
    protected bool auctionStarted = false;
    
    // Variables para coordinación de agentes
    protected static MultiAgentSystem currentCoordinator;
    protected bool isCoordinator;
    protected bool rolesAssigned = false;
    protected Dictionary<MultiAgentSystem, (float, float, float)> bids = new Dictionary<MultiAgentSystem, (float, float, float)>();
    protected static List<MultiAgentSystem> allAgents = new List<MultiAgentSystem>();
    protected Vector3 lastKnownPlayerPosition;

    public bool IsCoordinator => isCoordinator;

    public static Vector3 TreasurePosOnRobbery { get; private set; }

    // Variables estáticas compartidas por TODOS los agentes
    private static Transform _treasureLocation;
    private static Transform _exitLocation;

    public static Transform TreasureLocation 
    {
        get
        {
            // Intentamos encontrar el objeto en escena
            if (_treasureLocation == null)
            {
                var go = GameObject.FindWithTag("Treasure");
                if (go != null)
                {
                    _treasureLocation = go.transform;
                }
                else
                {
                    // No está en escena: creamos un Transform "virtual" en la última posición
                    GameObject dummy = new GameObject("TreasureDummy");
                    dummy.transform.position = TreasurePosOnRobbery;
                    _treasureLocation = dummy.transform;
                }
            }
            
            return _treasureLocation;
        }
    }

    public static Transform ExitLocation 
    {
        get
        {
            if (_exitLocation == null)
                _exitLocation = GameObject.FindWithTag("Exit").transform;
            return _exitLocation;
        }
    }

    protected abstract (float playerDist, float treasureDist, float exitDist) CalculateDistances(Vector3 playerPosition);

 
    protected virtual void Start()
    {
        if (!allAgents.Contains(this))
        {
            allAgents.Add(this);
        }
    }
    
    protected virtual void OnDestroy()
    {
        allAgents.Remove(this);
        if (currentCoordinator == this)
        {
            currentCoordinator = null;
        }
    }
    
    public bool IsAvailableForAssignment() 
    {
        return (role == "patrol" || role == "idle");
    }
    
    public virtual void AssignRole(string newRole) 
    {
        role = newRole;
        Debug.Log($"{name} asignado a rol: {role}");
    }
    
    public void SetAuctionStarted(bool status)
    {
        auctionStarted = status;
    }
    
    protected void TryBecomeCoordinator()
    {
        lock (allAgents)
        {
            if (currentCoordinator == null)
            {
                currentCoordinator = this;
                isCoordinator = true;
                AssignRole("chase");
                StartAuction(lastKnownPlayerPosition); 
            }
        }
    }
    

    protected void StartAuction(Vector3 playerLastKnownPos)
    {
        currentCoordinator = this;
        isCoordinator = true;
        auctionStarted = true;

        // string content = $"{playerLastKnownPos.x},{playerLastKnownPos.y},{playerLastKnownPos.z}";
        string content = $"{playerLastKnownPos.x.ToString("F4").Replace(',', '.')};" +
                        $"{playerLastKnownPos.y.ToString("F4").Replace(',', '.')};" +
                        $"{playerLastKnownPos.z.ToString("F4").Replace(',', '.')}";
        
        foreach (MultiAgentSystem agent in allAgents)
        {
            if (agent != this)
            {
                SendACLMessage(
                    receiver: agent.gameObject,
                    performative: "REQUEST_BID",
                    content: content,
                    protocol: "auction"
                );
            }
        }

        StartCoroutine(WaitForBids());
    }
        
    protected IEnumerator WaitForBids()
    {
        float timeout = 5f;
        float elapsed = 0f;
        int expectedBids = allAgents.Count - 1;

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
    
    public void ReceiveBid(MultiAgentSystem sender, float pDist, float tDist, float eDist)
    {
        if (isCoordinator && sender != null)
        {
            // Debug.Log($"Recibido bid de {sender.name}: {pDist}, {tDist}, {eDist}");
            Debug.Log(string.Format("Recibido bid de {0}:\n" +
                       "Distancia al Jugador: {1:F2}\n" +
                       "Distancia al Tesoro: {2:F2}\n" +
                       "Distancia a la Salida: {3:F2}",
                       sender.name, pDist, tDist, eDist));
            bids[sender] = (pDist, tDist, eDist);
        }
    }
    
    public void ReceiveACLMessage(ACLMessage message)
    {
        Debug.Log($"{name} recibió mensaje '{message.Performative}' de {(message.Sender != null ? message.Sender.name : "null")}");


        if (message.Performative == "REQUEST_BID" && !isCoordinator)
        {
            Vector3 playerPos = ParsePosition(message.Content);
            var distances = CalculateDistances(playerPos);
            
            var coord = message.Sender.GetComponent<MultiAgentSystem>();
            if (coord != null)
            {
                coord.ReceiveBid(this, distances.playerDist, distances.treasureDist, distances.exitDist);
            }
        }

        if (message.Performative == "ASSIGN_ROLE")
        {
            AssignRole(message.Content);
            return;
        }

        mailbox.Add(message);
    }
    
    
    protected Vector3 ParsePosition(string positionStr)
    {
        try
        {
            // Limpia la cadena: elimina paréntesis y espacios
            string cleaned = positionStr
                .Replace("(", "")
                .Replace(")", "")
                .Replace(" ", "");

            // Intenta dividir por punto y coma
            string[] parts = cleaned.Split(';');

            // Si no hay 3 partes, intenta dividir por coma
            if (parts.Length != 3)
            {
                parts = cleaned.Split(',');
            }

            // Si aún no hay 3 partes, lanza error
            if (parts.Length != 3)
            {
                throw new System.Exception($"Formato inválido. Se esperaban 3 valores. Recibido: {positionStr}");
            }

            // Convierte a float usando CultureInfo.InvariantCulture para evitar problemas con decimales
            float x = float.Parse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
            float y = float.Parse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
            float z = float.Parse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);

            return new Vector3(x, y, z);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error al analizar la posición: {positionStr}. Error: {e.Message}");
            return Vector3.zero; // Retorna un valor por defecto en caso de error
        }
    }

    protected void AssignRoles()
    {
        List<MultiAgentSystem> availableAgents = new List<MultiAgentSystem>(allAgents);
        availableAgents.Remove(this);

        AssignClosestRole("chase", availableAgents, g => bids[g].Item1);
        AssignClosestRole("treasure", availableAgents, g => bids[g].Item2);
        AssignClosestRole("exit", availableAgents, g => bids[g].Item3);

        foreach (MultiAgentSystem agent in availableAgents)
        {
            agent.AssignRole("patrol");
        }

        rolesAssigned = true;
        auctionStarted = false;
        currentCoordinator = null;
        isCoordinator = false;
    }
    
    protected void AssignClosestRole(string role, List<MultiAgentSystem> agents, Func<MultiAgentSystem, float> distanceSelector)
    {
        MultiAgentSystem closest = null;
        float minDistance = Mathf.Infinity;

        foreach (MultiAgentSystem agent in agents)
        {
            if (!bids.ContainsKey(agent))
            {
                Debug.LogWarning($"Agente {agent.name} no tiene una oferta registrada.");
                continue;
            }

            float dist = distanceSelector(agent);
            if (dist < minDistance)
            {
                minDistance = dist;
                closest = agent;
            }
        }

        if (closest != null)
        {
            closest.AssignRole(role);
            agents.Remove(closest);
        }
    }
    
    protected void ProcessMailbox()
    {
        foreach (ACLMessage message in mailbox)
        {
            if (message.Performative == "inform")
            {
                Vector3 playerPosition = ParsePosition(message.Content);
                // Debug.Log($"Recibí la posición del jugador: {playerPosition}");
            }
        }
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
        receiver.GetComponent<MultiAgentSystem>().ReceiveACLMessage(message);
    }
    
    protected void InformAgentsAboutPlayer(Vector3 playerPosition)
    {
        foreach (MultiAgentSystem otherAgent in allAgents)
        {
            if (otherAgent != this && otherAgent.role == "chase")
            {
                otherAgent.ReceiveACLMessage(new ACLMessage("inform", this.gameObject, otherAgent.gameObject, playerPosition.ToString(), "chase_protocol", "guard_communication"));
            }
        }
    }
}