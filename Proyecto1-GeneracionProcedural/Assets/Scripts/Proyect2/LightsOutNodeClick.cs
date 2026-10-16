using UnityEngine;

public class LightsOutNodeClick : MonoBehaviour
{
    private LightsOutController controller;
    private int nodeIndex;

    public void Setup(LightsOutController ctrl, int index)
    {
        controller = ctrl;
        nodeIndex = index;
    }

    private void OnMouseDown()
    {
        if (controller != null)
        {
            controller.OnNodeClickedByPlayer(nodeIndex);
        }
    }
}