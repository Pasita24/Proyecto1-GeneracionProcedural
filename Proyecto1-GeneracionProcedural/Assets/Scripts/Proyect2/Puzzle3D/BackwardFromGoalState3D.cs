using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Version 3D del generador: construye la red de nodos en el espacio (random walk
/// con 6 direcciones) y genera el estado inicial con clics hacia atras desde el Goal State.
/// </summary>
public class BackwardFromGoalState3D : MonoBehaviour
{
    // Limites reales (los sliders por codigo no respetan [Range], por eso se aplican en los setters)
    public const int MinNodes = 5;
    public const int MaxNodes = 60;
    public const int MinDifficulty = 2;
    public const int MaxDifficulty = 30;

    [Header("Parametros Backward From Goal State 3D")]
    [Range(MinNodes, MaxNodes)] public int nodeCount = 16;
    [Range(MinDifficulty, MaxDifficulty)] public int difficultySteps = 7;

    [Header("Forma 3D")]
    [Tooltip("Probabilidad de que la red crezca en el eje Z (0 = plano 2D, 1 = muy profunda)")]
    [Range(0f, 1f)] public float depthExpansion = 0.4f;

    [Header("Jugabilidad")]
    [Tooltip("Si esta activo, el generador nunca repite un clic (los clics no se cancelan entre si)")]
    public bool avoidRepeatedClicks = true;

    [Header("Reproducibilidad (comparar parametros)")]
    public bool useFixedSeed = false;
    public int seed = 12345;

    // Direcciones del plano XY y del eje de profundidad Z
    private readonly Vector3Int[] planarDirs =
    {
        Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down
    };
    private readonly Vector3Int[] depthDirs =
    {
        Vector3Int.forward, Vector3Int.back
    };

    // Todas las 6 direcciones, para calcular vecinos
    private readonly Vector3Int[] allDirs =
    {
        Vector3Int.right, Vector3Int.left, Vector3Int.up,
        Vector3Int.down, Vector3Int.forward, Vector3Int.back
    };

    private System.Random CreateRng(int offset)
    {
        return useFixedSeed ? new System.Random(seed + offset) : new System.Random();
    }

    // Random Walk 3D: genera coordenadas de nodos y la lista de vecinos
    public void GenerateTopology(out List<Vector3Int> nodeCoords, out List<int>[] neighbors)
    {
        // Valor seguro aunque algo externo haya dejado nodeCount fuera de rango
        int targetCount = Mathf.Clamp(nodeCount, MinNodes, MaxNodes);

        nodeCoords = new List<Vector3Int>();
        Dictionary<Vector3Int, int> coordToIndex = new Dictionary<Vector3Int, int>();
        System.Random rand = CreateRng(0);

        // Primer nodo en el origen
        nodeCoords.Add(Vector3Int.zero);
        coordToIndex[Vector3Int.zero] = 0;

        // Expansion aleatoria: elige un nodo existente y crece hacia una direccion libre
        while (nodeCoords.Count < targetCount)
        {
            int baseIndex = rand.Next(nodeCoords.Count);

            // Decide si crece en profundidad (Z) o dentro del plano (XY)
            Vector3Int dir = (rand.NextDouble() < depthExpansion)
                ? depthDirs[rand.Next(depthDirs.Length)]
                : planarDirs[rand.Next(planarDirs.Length)];

            Vector3Int newCoord = nodeCoords[baseIndex] + dir;

            if (!coordToIndex.ContainsKey(newCoord))
            {
                coordToIndex[newCoord] = nodeCoords.Count;
                nodeCoords.Add(newCoord);
            }
        }

        // Conectar nodos adyacentes (distancia 1 en algun eje).
        // El tamano sale de nodeCoords.Count para que SIEMPRE coincida con los nodos creados.
        int total = nodeCoords.Count;
        neighbors = new List<int>[total];
        for (int i = 0; i < total; i++)
        {
            neighbors[i] = new List<int>();
            foreach (Vector3Int dir in allDirs)
            {
                if (coordToIndex.TryGetValue(nodeCoords[i] + dir, out int neighborIndex))
                    neighbors[i].Add(neighborIndex);
            }
        }
    }

    // Backward from Goal State: clics aleatorios aplicados desde el estado "todo apagado"
    public List<int> GenerateBackwardSteps(int totalNodes)
    {
        List<int> steps = new List<int>();
        System.Random rand = CreateRng(1);
        int stepsToApply = Mathf.Clamp(difficultySteps, MinDifficulty, MaxDifficulty);

        if (avoidRepeatedClicks)
        {
            // Mezcla Fisher-Yates y toma los primeros N: clics todos distintos
            int[] indices = new int[totalNodes];
            for (int i = 0; i < totalNodes; i++) indices[i] = i;
            for (int i = totalNodes - 1; i > 0; i--)
            {
                int j = rand.Next(i + 1);
                int tmp = indices[i]; indices[i] = indices[j]; indices[j] = tmp;
            }

            int amount = Mathf.Min(stepsToApply, totalNodes);
            for (int i = 0; i < amount; i++) steps.Add(indices[i]);
        }
        else
        {
            for (int i = 0; i < stepsToApply; i++) steps.Add(rand.Next(totalNodes));
        }

        return steps;
    }

    // Metodos para conectar a Sliders de la UI (aplican los limites reales)
    public void SetNodeCount(float val) => nodeCount = Mathf.Clamp(Mathf.RoundToInt(val), MinNodes, MaxNodes);
    public void SetDifficultySteps(float val) => difficultySteps = Mathf.Clamp(Mathf.RoundToInt(val), MinDifficulty, MaxDifficulty);
    public void SetDepthExpansion(float val) => depthExpansion = Mathf.Clamp01(val);

    // Para Toggles
    public void SetUseFixedSeed(bool val) => useFixedSeed = val;
    public void SetAvoidRepeatedClicks(bool val) => avoidRepeatedClicks = val;

    // Para un InputField (TMP) donde escribir la semilla
    public void SetSeed(string val)
    {
        if (int.TryParse(val, out int parsed)) seed = parsed;
    }
}