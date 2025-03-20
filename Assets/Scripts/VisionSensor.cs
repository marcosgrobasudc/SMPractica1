using UnityEngine;

public class VisionSensor : MonoBehaviour
{
    public float sightRange = 10f;
    public LayerMask playerLayer;

    private Transform player;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
    }

    public bool CanSeePlayer()
    {
        Vector3 directionToPlayer = player.position - transform.position;
        if (directionToPlayer.magnitude < sightRange)
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, directionToPlayer.normalized, out hit, sightRange, playerLayer))
            {
                if (hit.collider.CompareTag("Player"))
                {
                    return true;
                }
            }
        }
        return false;
    }
}