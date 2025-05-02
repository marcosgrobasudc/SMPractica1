using System.Collections;
using UnityEngine;

public class CameraGuards : MonoBehaviour
{
    public LayerMask obstructionMask;

    // Cada cuánto reevalúa
    public float checkInterval = 0.5f;

    private Transform playerTransform;
    private bool playerInsight = false;

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
            Debug.LogError("No se encontró el objeto del jugador con la etiqueta 'Player'.");
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

        // Choca con linea de visión?
        if (!Physics.Raycast(transform.position, dirToPlayer, distToPlayer, obstructionMask))
        {
            // Si no hay obstrucción, el jugador está a la vista
            if (!playerInsight)
            {
                playerInsight = true;
                Debug.Log("Jugador a la vista!");
                // lógica para lo que sucede cuando el jugador es visto
            }
            return;
        }

        playerInsight = false;
    }

    void OnPlayerSpotted()
    {
        Vector3 pos = playerTransform.position;
        string content = $"{pos.x:F3},{pos.y:F3},{pos.z:F3}";

        // Enviamos mensaje inform a todos los guardias
        foreach (var agent in FindObjectsOfType<MultiAgentSystem>())
        {
            // Creamos el mensaje y lo enviamos
            ACLMessage msg = new ACLMessage(
                performative: "inform",
                sender: this.gameObject,
                receiver: agent.gameObject,
                content: content,
                protocol: "camera_alert",
                ontology: "guard_comunication"
            );

            agent.ReceiveACLMessage(msg);
        }
        Debug.Log($"({name}) vio al jugador y avisó a {FindObjectsOfType<MultiAgentSystem>().Length} guardias.");
    }
}