using System.Collections.Generic;
using UnityEngine;

public class PathGenerator : MonoBehaviour
{
    [Header("Configuración de Caminos")]
    public Terrain targetTerrain;
    public GameObject pathPrefab;

    [Header("Parámetros Random Walk Dirigido")]
    [Tooltip("Probabilidad de dar un paso directo hacia la casa destino (0 a 100)")]
    [Range(0, 100)]
    public int probabilidadDirecta = 75;
    public int limitePasosPorCamino = 500;

    private List<GameObject> spawnedPaths = new List<GameObject>();
    // HashSet para evitar instanciar dos baldosas exactamente en la misma coordenada
    private HashSet<Vector2Int> posicionesOcupadas = new HashSet<Vector2Int>();

    private static readonly List<Vector2Int> cardinalDirections = new List<Vector2Int>
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    public void GeneratePaths()
    {
        ClearPaths();

        if (targetTerrain == null || pathPrefab == null) return;

        // 1. Buscar las casas generadas por el BSPGenerator
        Transform housesContainer = transform.Find("Houses");
        if (housesContainer == null || housesContainer.childCount < 2)
        {
            Debug.LogWarning("No hay suficientes casas para conectar. Genera la aldea primero.");
            return;
        }

        // 2. Extraer las coordenadas 2D de todas las casas
        List<Vector2Int> housePositions = new List<Vector2Int>();
        foreach (Transform house in housesContainer)
        {
            housePositions.Add(new Vector2Int(Mathf.RoundToInt(house.position.x), Mathf.RoundToInt(house.position.z)));
        }

        // 3. Crear el contenedor para los caminos
        Transform pathsContainer = transform.Find("Paths");
        if (pathsContainer == null)
        {
            GameObject containerObj = new GameObject("Paths");
            containerObj.transform.SetParent(this.transform);
            containerObj.transform.localPosition = Vector3.zero;
            pathsContainer = containerObj.transform;
        }

        // 4. Trazar un camino desde cada casa hasta la siguiente (cerrando el circuito al final)
        for (int i = 0; i < housePositions.Count; i++)
        {
            Vector2Int start = housePositions[i];
            Vector2Int target = housePositions[(i + 1) % housePositions.Count];
            WalkTowardsTarget(start, target, pathsContainer);
        }
    }

    private void WalkTowardsTarget(Vector2Int current, Vector2Int target, Transform container)
    {
        int pasos = 0;

        while (current != target && pasos < limitePasosPorCamino)
        {
            pasos++;

            // Decidir si damos un paso directo al objetivo o un paso aleatorio
            if (Random.Range(0, 100) < probabilidadDirecta)
            {
                // Paso inteligente: reducir la distancia en el eje con mayor diferencia
                if (current.x != target.x && current.y != target.y)
                {
                    if (Random.value > 0.5f) current.x += (target.x > current.x) ? 1 : -1;
                    else current.y += (target.y > current.y) ? 1 : -1;
                }
                else if (current.x != target.x) current.x += (target.x > current.x) ? 1 : -1;
                else if (current.y != target.y) current.y += (target.y > current.y) ? 1 : -1;
            }
            else
            {
                // Paso aleatorio puro para dar la forma orgánica del Random Walk
                current += cardinalDirections[Random.Range(0, cardinalDirections.Count)];
            }

            // Evitar que el camino se salga del terreno
            current.x = Mathf.Clamp(current.x, 1, (int)targetTerrain.terrainData.size.x - 1);
            current.y = Mathf.Clamp(current.y, 1, (int)targetTerrain.terrainData.size.z - 1);

            // Solo instanciar la baldosa si esa coordenada está libre
            if (!posicionesOcupadas.Contains(current))
            {
                posicionesOcupadas.Add(current);
                InstantiatePathPiece(current, container);
            }
        }
    }

    private void InstantiatePathPiece(Vector2Int pos, Transform container)
    {
        float normX = pos.x / targetTerrain.terrainData.size.x;
        float normZ = pos.y / targetTerrain.terrainData.size.z;
        Vector3 terrainNormal = targetTerrain.terrainData.GetInterpolatedNormal(normX, normZ);

        float heightY = targetTerrain.SampleHeight(new Vector3(pos.x, 0, pos.y)) + targetTerrain.transform.position.y;
        Vector3 spawnPos = new Vector3(pos.x, heightY + 0.05f, pos.y);

        Quaternion slopeRotation = Quaternion.FromToRotation(Vector3.up, terrainNormal);

        GameObject newPath = Instantiate(pathPrefab, spawnPos, slopeRotation);
        newPath.transform.SetParent(container);
        spawnedPaths.Add(newPath);
    }

    public void ClearPaths()
    {
        spawnedPaths.Clear();
        posicionesOcupadas.Clear();
        Transform pathsContainer = transform.Find("Paths");
        if (pathsContainer != null) DestroyImmediate(pathsContainer.gameObject);
    }
}