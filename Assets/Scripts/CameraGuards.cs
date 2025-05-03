using System.Collections;
using UnityEngine;

public class CameraGuards : MonoBehaviour
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

    void Start()
    {
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            playerTransform = playerGO.transform;
            StartCoroutine(PeriodicCheck());
        }
        else
        {
            Debug.LogError("CameraGuards: no se encontró ningún GameObject con tag 'Player'");
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
        Vector3 dirToPlayer = (playerTransform.position - transform.position).normalized;
        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // Está dentro del ángulo?
        float angle = Vector3.Angle(transform.forward, dirToPlayer);
        if (angle > viewAngle * 0.5f || distToPlayer > viewDistance)
        {
            playerInSight = false;
            return;
        }

        // Comprobamos obstrucción
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
        string content = $"{pos.x:F3},{pos.y:F3},{pos.z:F3}";

        foreach (var agent in FindObjectsOfType<MultiAgentSystem>())
        {
            ACLMessage msg = new ACLMessage(
                performative: "inform",
                sender: this.gameObject,          // <- Aquí va this.gameObject
                receiver: agent.gameObject,
                content: content,
                protocol: "camera_alert",
                ontology: "guard_communication"   // corrige typo si acaso
            );
            agent.ReceiveACLMessage(msg);
        }

        Debug.Log($"CameraGuards ({name}) vio al jugador y avisó a {FindObjectsOfType<MultiAgentSystem>().Length} guardias.");
    }
}
