using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class CameraAgent : MultiAgentSystem
{
    [Header("Visión")]
    public float viewDistance = 20f;
    [Range(0f, 180f)]
    public float viewAngle = 90f;
    public LayerMask obstructionMask;

    // Cada cuánto reevalúa
    public float checkInterval = 0.5f;

    private Transform playerTransform;
    private bool playerInSight = false;

    // Implementación abstracta (no relevante para camera)
    protected override (float, float, float) CalculateDistances(Vector3 playerPosition)
    {
        // Cameras no hacen pujjas
        float dist = Vector3.Distance(transform.position, playerPosition);
        return (dist, dist, dist);
    }

    protected override void Start()
    {
        base.Start();

        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            playerTransform = playerGO.transform;
            StartCoroutine(PeriodicCheck());
        }
        else
        {
            Debug.LogError("CameraAgent: No se encontró el objeto del jugador con tag 'Player'.");
            enabled = false;
        }
    }

    IEnumerator PeriodicCheck()
    {
        while (true)
        {
            yield return new WaitForSeconds(checkInterval);
            CheckForPlayer();
        }
    }

    void CheckForPlayer()
    {
        if (playerTransform == null) return;

        Vector3 dirToPlayer = (playerTransform.position - transform.position).normalized;
        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // Comprueba ángulo y distancia
        float angle = Vector3.Angle(transform.forward, dirToPlayer);
        if (angle > viewAngle * 0.5f || distToPlayer > viewDistance)
        {
            playerInSight = false;
            return;
        }

        // Comprueba obstrucción
        if (!Physics.Raycast(transform.position, dirToPlayer, distToPlayer, obstructionMask))
        {
            if (!playerInSight)
            {
                playerInSight = true;
                OnPlayerSpotted();
            }
        }
        else
        {
            playerInSight = false;
        }
    }

    void OnPlayerSpotted()
    {
        Vector3 pos = playerTransform.position;
        string content = $"{pos.x:F3};{pos.y:F3};{pos.z:F3}";

        // Usar SendACLMessage heredado para avisar a todos los agentes
        foreach (var agent in allAgents)
        {
            if (agent != this)
            {
                SendACLMessage(
                    receiver: agent.gameObject,
                    performative: "inform",
                    content: content,
                    protocol: "camera_alert"
                );
            }
        }

        Debug.Log($"({name}) vio al jugador y avisó a {allAgents.Count - 1} agentes.");
    }
}
