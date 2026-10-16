using System.Collections.Generic;
using UnityEngine;

public class BackwardFromGoalState : MonoBehaviour
{
    [Header("Parametros Backward From Goal State")]
    [Range(5, 40)] public int nodeCount = 14;
    [Range(2, 25)] public int difficultySteps = 6;

    // Direcciones ortogonales en cruz (arriba, abajo, izquierda, derecha)
    private readonly Vector2Int[] crossDirs = {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    // Random Walk
    // Genera la estructura de nodos en cruz y sus conexiones vecinas
    public void GenerateTopology(out List<Vector2Int> nodeCoords, out List<int>[] neighbors)
    {
        nodeCoords = new List<Vector2Int>();
        Dictionary<Vector2Int, int> coordToIndex = new Dictionary<Vector2Int, int>();

        // Primer nodo en el centro (0,0)
        nodeCoords.Add(Vector2Int.zero);
        coordToIndex[Vector2Int.zero] = 0;

        System.Random rand = new System.Random();

        // Expansion aleatoria en 4 direcciones
        while (nodeCoords.Count < nodeCount)
        {
            int baseIndex = rand.Next(nodeCoords.Count);
            Vector2Int dir = crossDirs[rand.Next(4)];
            Vector2Int newCoord = nodeCoords[baseIndex] + dir;

            // Solo agrega el nodo si esa coordenada estaba vacia
            if (!coordToIndex.ContainsKey(newCoord))
            {
                coordToIndex[newCoord] = nodeCoords.Count;
                nodeCoords.Add(newCoord);
            }
        }

        // Conectar caminos adyacentes al nodo
        neighbors = new List<int>[nodeCount];
        for (int i = 0; i < nodeCount; i++)
        {
            neighbors[i] = new List<int>();
            foreach (Vector2Int dir in crossDirs)
            {
                Vector2Int neighborCoord = nodeCoords[i] + dir;
                if (coordToIndex.TryGetValue(neighborCoord, out int neighborIndex))
                {
                    neighbors[i].Add(neighborIndex);
                }
            }
        }
    }

    // Backward from Goal State
    // Genera los clics aleatorios hacia atras partiendo desde el Goal State
    public List<int> GenerateBackwardSteps(int totalNodes)
    {
        List<int> steps = new List<int>();
        System.Random rand = new System.Random();

        for (int i = 0; i < difficultySteps; i++)
        {
            steps.Add(rand.Next(totalNodes));
        }

        return steps;
    }

    // Metodos para conectar a Sliders
    public void SetNodeCount(float val) => nodeCount = Mathf.RoundToInt(val);
    public void SetDifficultySteps(float val) => difficultySteps = Mathf.RoundToInt(val);
}