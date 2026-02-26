using UnityEngine;
using TMPro;
using UnityEngine.UI; 

public class MyButtonHandler : MonoBehaviour
{   


    void Start(){
    
    }
    // Función que se llamará al pulsar el botón
    public void testClick(bool u)
    {
        // Aquí va el código que quieres ejecutar,
        // por ejemplo, iniciar la simulación o cambiar de menú.
        Debug.Log("¡Botón pulsado! Evento lanzado.");
    }
}
