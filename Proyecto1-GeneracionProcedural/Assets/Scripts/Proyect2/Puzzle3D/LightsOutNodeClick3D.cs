using UnityEngine;

/// <summary>
/// Detecta el clic izquierdo sobre un nodo 3D (requiere Collider en el mismo objeto).
/// El clic derecho queda libre para rotar la camara/puzzle.
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

    private void OnMouseDown()
    {
        if (controller != null) controller.OnNodeClickedByPlayer(nodeIndex);
    }
}