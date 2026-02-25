using UnityEngine;
using System.Collections.Generic;

public class PhaseGraph : MonoBehaviour
{
    public LineRenderer trajectoryLine;
    private List<Vector3> points = new List<Vector3>();

    public float graphWidth = 5.0f;
    public float graphHeight = 5.0f;

    public int maxPreysReached = 250;
    public int maxPredatorsReached = 250;

    //private int positionCount = 0;

    void Awake()
    {
        trajectoryLine.useWorldSpace = false;
        trajectoryLine.startWidth = 0.1f;
        trajectoryLine.endWidth = 0.1f;
        // Puedes agregar el material de tu LineRenderer aquí si lo tienes.
        // lineRenderer.material = myVRMaterial; 
    }

    public void AddPoint(int preyCount, int predatorCount)
    {
        float normalizedPrey = 0;
        if (maxPreysReached > 0)
        {
            normalizedPrey = (float)preyCount / maxPreysReached;
        }

        float normalizedPredator = 0;
        if (maxPredatorsReached > 0)
        {
            normalizedPredator = (float)predatorCount / maxPredatorsReached;
        }

        // Se mueve ligeramente en el eje Z para evitar el Z-fighting
        Vector3 newPosition = new Vector3(normalizedPrey * graphWidth, normalizedPredator * graphHeight, -0.01f);

        points.Add(newPosition);

        UpdateLine();
    }
    
    // Este método solo se encarga de redibujar la línea de forma eficiente
    private void UpdateLine()
    {
        // ... (el mismo código para suavizar la línea que te di antes)
        // La clave es no borrar la lista 'points' aquí
        
        // Genera los puntos intermedios...
        List<Vector3> smoothedPoints = new List<Vector3>();
        for (int i = 0; i < points.Count - 1; i++)
        {
            smoothedPoints.Add(points[i]);
            for (float t = 0.2f; t < 1.0f; t += 0.2f)
            {
                Vector3 interpolatedPoint = Vector3.Lerp(points[i], points[i + 1], t);
                smoothedPoints.Add(interpolatedPoint);
            }
        }
        smoothedPoints.Add(points[points.Count - 1]);

        trajectoryLine.positionCount = smoothedPoints.Count;
        trajectoryLine.SetPositions(smoothedPoints.ToArray());
    }

    // Asegúrate de que este método solo se llama al inicio de la simulación
    public void ResetGraph()
    {
        points.Clear();
        trajectoryLine.positionCount = 0;
    }
}