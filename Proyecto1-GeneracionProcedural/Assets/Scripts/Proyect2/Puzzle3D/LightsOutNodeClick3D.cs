using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Detecta el mouse sobre un nodo 3D (requiere Collider en el mismo objeto).
/// - Pasar el mouse: previsualiza el nodo y sus vecinos (los que cambiarian).
/// - Clic izquierdo: pulsa el nodo.
/// - El clic derecho queda libre para orbitar la camara.
/// </summary>
public class LightsOutNodeClick3D : MonoBehaviour
{
    private LightsOutController3D controller;
    private int nodeIndex;

    public void Setup(LightsOutController3D owner, int index)
    {
        controller = owner;
        nodeIndex = index;
    }

    private void OnMouseEnter()
    {
        if (controller == null || PointerOverUI()) return;
        controller.SetHoveredNode(nodeIndex);
    }

    private void OnMouseExit()
    {
        if (controller != null) controller.ClearHoveredNode(nodeIndex);
    }

    private void OnMouseDown()
    {
        // Si el cursor esta sobre la interfaz (panel, botones), no pulsar el nodo de atras
        if (controller == null || PointerOverUI()) return;
        controller.OnNodeClickedByPlayer(nodeIndex);
    }

    private static bool PointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}