using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class GuardScript : MonoBehaviour
{
    public Transform[] patrolPoints;  // Puntos de patrulla
    public Transform treasureLocation; // Ubicación del tesoro
    public Transform exitLocation; // Ubicación de la salida
    public float sightRange = 10f;
    public float hearingRange = 5f;
    public float chaseSpeed = 6f;
    public float patrolSpeed = 3f;
    public LayerMask playerLayer;

    private NavMeshAgent agent;
    private int currentPatrolIndex = 0;
    private Transform player;
    private bool playerHasTreasure = false;
    private bool chasingPlayer = false;
    private Vector3 lastKnownPlayerPosition;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        player = GameObject.FindGameObjectWithTag("Player").transform;
        GoToNextPatrolPoint();
    }

    void Update()
    {
        if (CanSeePlayer() || CanHearPlayer())
        {
            chasingPlayer = true;
            lastKnownPlayerPosition = player.position;
            agent.speed = chaseSpeed;
            agent.SetDestination(player.position);
        }
        else if (chasingPlayer)
        {
            if (agent.remainingDistance < 1f)
            {
                chasingPlayer = false;
                if (!playerHasTreasure)
                {
                    agent.SetDestination(treasureLocation.position);
                }
                else
                {
                    agent.SetDestination(exitLocation.position);
                }
            }
        }
        else
        {
            Patrol();
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

    bool CanSeePlayer()
    {
        Vector3 directionToPlayer = player.position - transform.position;
        if (directionToPlayer.magnitude < sightRange)
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, directionToPlayer.normalized, out hit, sightRange))
            {
                if (hit.collider.CompareTag("Player"))
                {
                    return true;
                }
            }
        }
        return false;
    }

    bool CanHearPlayer()
    {
        return Vector3.Distance(transform.position, player.position) < hearingRange;
    }

    public void SetPlayerHasTreasure(bool hasTreasure)
    {
        playerHasTreasure = hasTreasure;
    }
}
