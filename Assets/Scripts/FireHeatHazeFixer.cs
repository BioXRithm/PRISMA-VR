using UnityEngine;

/// <summary>
/// Corrige la transparencia del Canvas World-Space causada por las partículas
/// de fuego (VFX). Las partículas y el Canvas comparten la cola de renderizado
/// "Transparent" con SortingOrder 0, lo que provoca que las partículas se
/// dibujen encima del Canvas haciéndolo parecer transparente.
///
/// Solución: al activar el fuego (OnEnable) se sube el SortingOrder de todos
/// los Canvas World-Space para que siempre se dibujen por encima de las
/// partículas. Al desactivar (OnDisable) se restauran los valores originales.
///
/// También desactiva los hijos "Heat Distortion" y "Burning Dark" que usan
/// el shader Heat Haze (SAMPLE_SCENE_COLOR) y generan artefactos adicionales.
///
/// Uso: colocar este script en el GameObject "FireContainer".
/// </summary>
public class FireHeatHazeFixer : MonoBehaviour
{
    [Tooltip("Nombres de los hijos a desactivar en cada fuego (usan Heat Haze shader)")]
    public string[] problematicChildren = { "Heat Distortion", "Burning Dark" };

    [Tooltip("SortingOrder alto para que el Canvas se dibuje encima de las partículas")]
    public int canvasSortingOrder = 100;

    // Para restaurar los valores originales al desactivar el fuego
    private struct CanvasState
    {
        public Canvas canvas;
        public bool originalOverrideSorting;
        public int originalSortingOrder;
    }
    private CanvasState[] savedStates;

    void OnEnable()
    {
        DisableHeatDistortion();
        BoostCanvasSorting();
    }

    void OnDisable()
    {
        RestoreCanvasSorting();
    }

    /// <summary>
    /// Sube el SortingOrder de todos los Canvas World-Space para que se
    /// rendericen después (encima) de las partículas transparentes del fuego.
    /// </summary>
    private void BoostCanvasSorting()
    {
        Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        var worldCanvases = new System.Collections.Generic.List<CanvasState>();

        foreach (Canvas c in allCanvases)
        {
            if (c != null && c.renderMode == RenderMode.WorldSpace)
            {
                worldCanvases.Add(new CanvasState
                {
                    canvas = c,
                    originalOverrideSorting = c.overrideSorting,
                    originalSortingOrder = c.sortingOrder
                });

                c.overrideSorting = true;
                c.sortingOrder = canvasSortingOrder;
            }
        }

        savedStates = worldCanvases.ToArray();
    }

    /// <summary>
    /// Restaura los valores originales de SortingOrder en los Canvas.
    /// </summary>
    private void RestoreCanvasSorting()
    {
        if (savedStates == null) return;

        foreach (var state in savedStates)
        {
            if (state.canvas != null)
            {
                state.canvas.overrideSorting = state.originalOverrideSorting;
                state.canvas.sortingOrder = state.originalSortingOrder;
            }
        }

        savedStates = null;
    }

    /// <summary>
    /// Recorre todos los hijos (fuegos) y desactiva los GameObjects
    /// que usan el shader Heat Haze.
    /// </summary>
    public void DisableHeatDistortion()
    {
        foreach (Transform fire in transform)
        {
            foreach (string childName in problematicChildren)
            {
                Transform child = fire.Find(childName);
                if (child != null)
                {
                    child.gameObject.SetActive(false);
                }
            }
        }
    }
}
