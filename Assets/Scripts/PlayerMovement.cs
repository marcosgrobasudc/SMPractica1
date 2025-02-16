using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{

    public float Speed = 1.0f;                                      // Velocidad de movimiento
    public float RotationSpeed = 1.0f;                              // Velocidad de rotación

    private Rigidbody Physics;                                      // Rigidez del objeto
    
    // Start is called before the first frame update
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;                   // Bloqueamos el cursor
        Cursor.visible = false;                                     // Ocultamos el cursor

        Physics = GetComponent<Rigidbody>();                        // Obtenemos la rigidez de los objetos
    }

    // Update is called once per frame
    void Update()
    {
        float horizontal = Input.GetAxis("Horizontal");             // Nos devuelve 1 si pulsamos A y -1 si pulsamos D
        float vertical = Input.GetAxis("Vertical");                 // Nos devuelve 1 si pulsamos W y -1 si pulsamos S

        // Movemos el objeto en el eje X y Z
        transform.Translate(new Vector3(horizontal, 0, vertical) * Time.deltaTime * Speed);

        // Rotamos el objeto en el eje Y
        float rotationY = Input.GetAxis("Mouse X");
        transform.Rotate(new Vector3(0, rotationY * Time.deltaTime * RotationSpeed, 0));
    }
}