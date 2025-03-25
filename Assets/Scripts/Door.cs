using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class NoisyDoor : MonoBehaviour
{
    public SoundEmitter soundEmitter;

    void Start()
    {
        soundEmitter = gameObject.AddComponent<SoundEmitter>();
        soundEmitter.soundRadius = 7f;
        soundEmitter.soundDuration = 2f;
    }

    public void OpenDoor()
    {
        // Lógica para abrir la puerta
        // ...
        soundEmitter.EmitSound();
    }
}