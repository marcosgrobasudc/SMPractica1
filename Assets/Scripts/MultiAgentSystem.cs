using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using System.Linq;

public abstract class MultiAgentSystem : MonoBehaviour
{
    // Variables de comunicación multiagente
    protected List<ACLMessage> mailbox = new List<ACLMessage>();
    protected string role = "patrol";
    public string CurrentRole => role;
    protected bool auctionStarted = false;
    
    // Variables para coordinación de agentes
    protected static MultiAgentSystem currentCoordinator;
    protected static bool playerHasTreasure = false;
    protected bool isCoordinator;
    protected bool rolesAssigned = false;
    protected Dictionary<MultiAgentSystem, (float, float, float)> bids = new Dictionary<MultiAgentSystem, (float, float, float)>();
    protected static List<MultiAgentSystem> allAgents = new List<MultiAgentSystem>();
    protected Vector3 lastKnownPlayerPosition;

    public bool IsCoordinator => isCoordinator;
    public virtual bool IsGuard => false;
    
    [Header("Puntos estratégicos para atrapar al jugador")]
    public static Transform[] blockages;
    // Variables estáticas compartidas por TODOS los agentes
    private static Transform _treasureLocation;
    private static Vector3 _lastKnownTreasurePosition;

    private static Transform _exitLocation;

    public abstract void SetTarget(Vector3 targetPosition);

    public static Transform TreasureLocation 
    {
        get
        {
            if (_treasureLocation == null || !_treasureLocation.gameObject.activeInHierarchy)
            {
                GameObject treasure = GameObject.FindWithTag("Treasure");
                
                if (treasure != null && treasure.activeInHierarchy)
                {
                    _treasureLocation = treasure.transform;
                    _lastKnownTreasurePosition = _treasureLocation.position;
                }
                else
                {
                    // Crear un objeto dummy en la última posición conocida
                    GameObject dummy = new GameObject("TreasureDummy (Inactive)");
                    dummy.transform.position = _lastKnownTreasurePosition;
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

    public static bool PlayerHasTreasure {
        get { return playerHasTreasure; }
        set {
            if (value && !playerHasTreasure) {
                Debug.Log("⚠️ Los guardias saben que el jugador tiene el tesoro!");
            }
            playerHasTreasure = value;
        }
    }
 
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
    
    protected void TryBecomeCoordinator(bool playerHasTreasure = false)
    {
        lock (allAgents)
        {
            if (currentCoordinator == null)
            {
                // Actualizar estado global si el jugador tiene el tesoro
                if (playerHasTreasure)
                {
                    PlayerHasTreasure = true;
                    // Debug.Log("Coordinador actualizó PlayerHasTreasure");
                }

                // Convertirse en coordinador e iniciar subasta
                currentCoordinator = this;
                isCoordinator = true;
                AssignRole("chase");
                StartAuction(lastKnownPlayerPosition); 
            }
        }
    }
    
    protected void StartAuction(Vector3 playerLastKnownPos)
    {
        bids.Clear(); // Limpiar ofertas anteriores
        rolesAssigned = false;
        currentCoordinator = this;
        isCoordinator = true;
        auctionStarted = true;

        string content = $"{playerLastKnownPos.x.ToString("F4").Replace(',', '.')};" +
                        $"{playerLastKnownPos.y.ToString("F4").Replace(',', '.')};" +
                        $"{playerLastKnownPos.z.ToString("F4").Replace(',', '.')}";
        
        foreach (MultiAgentSystem agent in allAgents.Where(a => a.IsGuard)) // Solo a guardias
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
        int expectedBids = allAgents.Count(a => a.IsGuard) - 1; // Solo guardias -1 (coordinador)

        Debug.Log($"⏳ Esperando {expectedBids} bids...");

        while (elapsed < timeout && bids.Count < expectedBids)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.Log($"✅ Recibidos {bids.Count}/{expectedBids} bids");
        AssignRoles(blockages);
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
    
    public virtual void ReceiveACLMessage(ACLMessage message)
    {
        Debug.Log($"{name} recibió mensaje '{message.Performative}' de {(message.Sender != null ? message.Sender.name : "null")}");

            // Las cámaras ignoran mensajes de subastas
        if (!IsGuard)
        {
            return;
        }
        else
        {
            if (message.Performative == "inform" && message.Protocol == "position_update")
            {
                // Actualizar última posición conocida (para todos los agentes)
                lastKnownPlayerPosition = ParsePosition(message.Content);
                return;
            }
            if (message.Performative == "REQUEST_BID" && !isCoordinator)
            {
                Vector3 playerPos = ParsePosition(message.Content);
                var distances = CalculateDistances(playerPos);
                
                // Si el jugador tiene el tesoro, ignorar distancia al tesoro (asignar valor infinito)
                float adjustedTreasureDist = PlayerHasTreasure ? Mathf.Infinity : distances.treasureDist;
                
                var coord = message.Sender.GetComponent<MultiAgentSystem>();
                if (coord != null)
                {
                    coord.ReceiveBid(this, distances.playerDist, adjustedTreasureDist, distances.exitDist);
                }
            }

            if (message.Performative == "ASSIGN_ROLE")
            {
                AssignRole(message.Content);
                return;
            }
        }


        mailbox.Add(message);
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

    protected void AssignRoles(Transform[] blockages)
    {
        // 1. Validación inicial
        if (allAgents == null || allAgents.Count == 0) return;

        List<MultiAgentSystem> availableAgents = allAgents
            .Where(a => a != this && a.IsGuard && a.IsAvailableForAssignment()) // <- Añadir a.IsGuard
            .ToList();

        // 2. Asignación priorizada
        // A. Siempre asignar chase primero (máx 2)
        int currentChasers = allAgents.Count(a => a.role == "chase");
        if (currentChasers < 2)
        {
            AssignClosestRole("chase", availableAgents, g => bids[g].Item1);
        }

        // B. Asignar treasure SOLO si no sabemos que fue robado
        if (!PlayerHasTreasure && availableAgents.Count > 0)
        {
            AssignClosestRole("treasure", availableAgents, g => bids[g].Item2);
        }

        // C. Asignar exit
        if (availableAgents.Count > 0)
        {
            AssignClosestRole("exit", availableAgents, g => bids[g].Item3);
        }

        // D. Asignar bloqueos estratégicos SOLO si el tesoro fue robado
        if (PlayerHasTreasure && blockages != null && blockages.Length >= 2 && availableAgents.Count >= 2)
        {
            var closestBlockages = blockages
                .OrderBy(b => Vector3.Distance(b.position, lastKnownPlayerPosition))
                .Take(2)
                .ToList();

            AssignClosestToPosition("blockage1", availableAgents, closestBlockages[0].position);
            AssignClosestToPosition("blockage2", availableAgents, closestBlockages[1].position);
        }

        // 3. Patrulla para el resto
        availableAgents.ForEach(a => a.AssignRole("patrol"));

        // Reset estado
        rolesAssigned = true;
        auctionStarted = false;
        currentCoordinator = null;
        isCoordinator = false;
    }

    // --- Métodos auxiliares ---
    private void AssignPriorityRoles(List<MultiAgentSystem> agents)
    {
        // 1. Chase (máx 2)
        if (allAgents.Count(a => a.role == "chase") < 2)
        {
            AssignClosestRole("chase", agents, g => bids[g].Item1);
        }

        // 2. Treasure (solo si no fue robado)
        if (!PlayerHasTreasure)
        {
            AssignClosestRole("treasure", agents, g => bids[g].Item2);
        }

        // 3. Exit
        AssignClosestRole("exit", agents, g => bids[g].Item3);
    }

    private void AssignBlockagePositions(List<MultiAgentSystem> agents, Transform[] blockages)
    {
        var closestBlockages = blockages
            .OrderBy(b => Vector3.Distance(b.position, lastKnownPlayerPosition))
            .Take(2)
            .ToList();

        AssignClosestToPosition("blockage1", agents, closestBlockages[0].position);
        AssignClosestToPosition("blockage2", agents, closestBlockages[1].position);
    }

    // Nuevo método para asignar agentes a posiciones específicas
    protected void AssignClosestToPosition(string roleName, List<MultiAgentSystem> agents, Vector3 targetPosition)
    {
        MultiAgentSystem closest = null;
        float minDistance = Mathf.Infinity;

        foreach (MultiAgentSystem agent in agents)
        {
            float dist = Vector3.Distance(agent.transform.position, targetPosition);
            if (dist < minDistance)
            {
                minDistance = dist;
                closest = agent;
            }
        }

        if (closest != null)
        {
            closest.AssignRole(roleName);
            closest.SetTarget(targetPosition);
            agents.Remove(closest);
        }
    }
      
    protected void ProcessMailbox()
    {
        foreach (ACLMessage message in mailbox.ToList())  // Usamos ToList() para evitar modificar la colección durante la iteración
        {
            if (message.Performative == "inform")
            {
                try 
                {
                    Vector3 playerPosition = ParsePosition(message.Content);
                    Debug.Log($"Posición del jugador recibida: {playerPosition}");
                    
                    // Procesar la posición (añade tu lógica aquí si es necesario)
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error al procesar mensaje: {message.Content}. Error: {e.Message}");
                }
            }
        }
        mailbox.Clear();
    }

    protected Vector3 ParsePosition(string positionStr)
    {
        // Primero intentar dividir por punto y coma (formato nuevo)
        string[] parts = positionStr.Split(';');
        
        // Si no hay 3 partes, intentar por coma (formato antiguo)
        if (parts.Length != 3)
        {
            parts = positionStr.Split(',');
        }

        if (parts.Length != 3)
        {
            throw new System.FormatException($"Formato de posición inválido. Se esperaban 3 valores. Recibido: {positionStr}");
        }

        try
        {
            return new Vector3(
                float.Parse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture)
            );
        }
        catch (System.Exception e)
        {
            throw new System.FormatException($"Error al convertir números en: {positionStr}. Error: {e.Message}");
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
        string content = $"{playerPosition.x:F4};{playerPosition.y:F4};{playerPosition.z:F4}";

        foreach (MultiAgentSystem otherAgent in allAgents.Where(a => a.IsGuard)) // Solo a guardias
        {
            if (otherAgent != this && otherAgent.role == "chase")
            {
                SendACLMessage(
                    otherAgent.gameObject,
                    "inform",
                    content,
                    "chase_protocol"
                );
            }
        }
    }
}
