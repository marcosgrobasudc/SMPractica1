using UnityEngine;

public class HearingSensor : MonoBehaviour
{
    public float hearingRadius = 10f;

    private Transform player;
    private SoundEmitter[] allSoundEmitters;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        allSoundEmitters = FindObjectsOfType<SoundEmitter>();
    }

    public bool CanHearPlayer()
    {
        foreach (var emitter in allSoundEmitters)
        {
            if (emitter == null || !emitter.IsSoundActive())
                continue;

            // Si este emitter pertenece a una puerta, lo ignoramos
            if (emitter.GetComponent<NoisyDoor>() != null)
                continue;

            float distance = Vector3.Distance(transform.position, emitter.GetSoundPosition());
            float combinedRadius = hearingRadius + emitter.GetSoundRadius();

            if (distance < combinedRadius)
                return true;
        }
        return false;
    }

    // Dibujamos el gizmo para visualizar el radio de audición
    void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, hearingRadius);
    }
}