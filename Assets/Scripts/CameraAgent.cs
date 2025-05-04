using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using System.Globalization;
using System.Collections.Generic;

public class CameraAgent : MultiAgentSystem
{
    [Header("Visión")]
    public float viewDistance = 20f;
    [Range(0f, 180f)]
    public float viewAngle = 90f;
    public LayerMask obstructionMask;

    [Header("Guardia coordinador asignado a esta cámara")]
    public GuardScript assignedCoordinator;

    // Cada cuánto reevalúa
    public float checkInterval = 0.5f;

    private Transform playerTransform;
    private bool playerInSight = false;

    protected override (float, float, float) CalculateDistances(Vector3 playerPosition)
    {
        // Las cámaras no participan en subastas
        float dist = Vector3.Distance(transform.position, playerPosition);
        return (dist, dist, dist);
    }

    public override void SetTarget(Vector3 targetPosition)
    {
        playerTransform.LookAt(targetPosition);
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
        string content = string.Format(CultureInfo.InvariantCulture, "{0:F3};{1:F3};{2:F3}",
            pos.x, pos.y, pos.z);

        // Avisar primero al guardia asignado
        if (assignedCoordinator != null)
        {
            Debug.Log($"({name}) envía alerta a guardia asignado {assignedCoordinator.name}");
            SendACLMessage(
                receiver: assignedCoordinator.gameObject,
                performative: "camera_alert",
                content: content,
                protocol: "camera_protocol"
            );
            
        }
        else
        {
            Debug.LogWarning($"({name}) no tiene guardia asignado!");
        }
    }
}
