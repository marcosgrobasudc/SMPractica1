using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tesoro : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        // Verificamos si el objeto que colisiona es el jugador
        if (other.CompareTag("Player"))
        {
            Debug.Log("¡Tesoro recogido!");
            gameObject.SetActive(false); // Hace desaparecer el tesoro
        }
    }
}