using UnityEngine;

public class SoundEmitter : MonoBehaviour
{
    public float soundRadius = 5f;
    public float soundDuration = 2f;
    public bool isPlayer = false;

    public float jumpSoundRadius = 5f;
    public float landingSoundRadius = 2f;

    private float soundTimer = 0f;
    private bool soundActive = false;
    private Movement playerMovement;

    void Start()
    {
        if (isPlayer)
        {
            playerMovement = GetComponent<Movement>();
            if (playerMovement == null)
            {
                Debug.LogError("No se encontró el componente Movement en el jugador");
            }
        }
    }

    private void Update()
    {
        if (soundActive)
        {
            soundTimer -= Time.deltaTime;
            if (soundTimer <= 0f)
            {
                soundActive = false;
            }
        }

        // Si es el jugador, emitir sonido al saltar
        if (isPlayer && playerMovement != null)
        {
            if (Input.GetButtonDown("Jump") && playerMovement.IsGrounded()) // Umbral de velocidad para hacer ruido
            {
                EmitSound(jumpSoundRadius);
            }

             // Sonido al aterrizar (opcional)
            if (!playerMovement.WasGroundedLastFrame() && playerMovement.IsGrounded())
            {
                EmitSound(landingSoundRadius); // Sonido más leve al aterrizar
            }
        }
    }

    public void EmitSound()
    {
        EmitSound(soundRadius);
    }

    // Emite sonido con un radio específico
    public void EmitSound(float customRadius)
    {
        soundActive = true;
        soundTimer = soundDuration;
        soundRadius = customRadius; // Temporalmente cambia el radio
    }

    // Visualizamos la esfera de sonido en el editor
    void OnDrawGizmosSelected()
    {
        if (soundActive)
        {
            Gizmos.color = new Color(1f, 0.92f, 0.16f, 0.5f); // Amarillo transparente
            Gizmos.DrawSphere(transform.position, soundRadius);
        }
    }

    public bool IsSoundActive()
    {
        return soundActive;
    }

    public float GetSoundRadius()
    {
        return soundRadius;
    }

    public Vector3 GetSoundPosition()
    {
        return transform.position;
    }
}