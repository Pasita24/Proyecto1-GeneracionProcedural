using System.Collections.Generic;
using UnityEngine;

public class PathGenerator : MonoBehaviour
{
    public enum GenerationContext { Valley, Mountain }

    [Header("Contexto de Generación")]
    public GenerationContext context = GenerationContext.Valley;

    [Header("Configuración de Caminos")]
    public Terrain targetTerrain;
    public GameObject pathPrefab;

    [Header("Parámetros Random Walk Dirigido")]
    [Range(0, 100)] public int probabilidadDirecta = 75;
    public int limitePasosPorCamino = 500;

    private List<GameObject> spawnedPaths = new List<GameObject>();
    private HashSet<Vector2Int> posicionesOcupadas = new HashSet<Vector2Int>();

    private static readonly List<Vector2Int> cardinalDirections = new List<Vector2Int>
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    public void GeneratePaths()
    {
        ClearPaths();
        if (targetTerrain == null || pathPrefab == null) return;

        Transform housesContainer = transform.Find("Houses");
        if (housesContainer == null || housesContainer.childCount < 2) return;

        // Extraer Vector3 para conservar la altura de cada casa
        List<Vector3> housePositions = new List<Vector3>();
        foreach (Transform house in housesContainer)
        {
            housePositions.Add(house.position);
        }

        Transform pathsContainer = transform.Find("Paths");
        if (pathsContainer == null)
        {
            GameObject containerObj = new GameObject("Paths");
            containerObj.transform.SetParent(this.transform);
            containerObj.transform.localPosition = Vector3.zero;
            pathsContainer = containerObj.transform;
        }

        for (int i = 0; i < housePositions.Count; i++)
        {
            Vector3 start = housePositions[i];
            Vector3 target = housePositions[(i + 1) % housePositions.Count];
            WalkTowardsTarget(start, target, pathsContainer);
        }
    }

    private void WalkTowardsTarget(Vector3 start3D, Vector3 target3D, Transform container)
    {
        Vector2Int current = new Vector2Int(Mathf.RoundToInt(start3D.x), Mathf.RoundToInt(start3D.z));
        Vector2Int target = new Vector2Int(Mathf.RoundToInt(target3D.x), Mathf.RoundToInt(target3D.z));

        Vector2Int start2D = current;
        float totalDist = Vector2Int.Distance(start2D, target);
        int pasos = 0;

        while (current != target && pasos < limitePasosPorCamino)
        {
            pasos++;

            if (Random.Range(0, 100) < probabilidadDirecta)
            {
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
                current += cardinalDirections[Random.Range(0, cardinalDirections.Count)];
            }

            current.x = Mathf.Clamp(current.x, 1, (int)targetTerrain.terrainData.size.x - 1);
            current.y = Mathf.Clamp(current.y, 1, (int)targetTerrain.terrainData.size.z - 1);

            // Calcular altura interpolada para los puentes
            float currentDist = Vector2Int.Distance(start2D, current);
            float t = totalDist == 0 ? 1 : currentDist / totalDist;
            float lerpedY = Mathf.Lerp(start3D.y, target3D.y, t);

            if (!posicionesOcupadas.Contains(current))
            {
                posicionesOcupadas.Add(current);
                InstantiatePathPiece(current, lerpedY, container);
            }
        }
    }

    private void InstantiatePathPiece(Vector2Int pos, float bridgeY, Transform container)
    {
        Vector3 spawnPos;
        Quaternion slopeRotation;

        if (context == GenerationContext.Mountain)
        {
            // Puente recto ignorando terreno
            spawnPos = new Vector3(pos.x, bridgeY, pos.y);
            slopeRotation = Quaternion.identity;
        }
        else
        {
            // Adaptado al terreno (Valle)
            float normX = pos.x / targetTerrain.terrainData.size.x;
            float normZ = pos.y / targetTerrain.terrainData.size.z;
            Vector3 terrainNormal = targetTerrain.terrainData.GetInterpolatedNormal(normX, normZ);
            float heightY = targetTerrain.SampleHeight(new Vector3(pos.x, 0, pos.y)) + targetTerrain.transform.position.y;

            spawnPos = new Vector3(pos.x, heightY + 0.05f, pos.y);
            slopeRotation = Quaternion.FromToRotation(Vector3.up, terrainNormal);
        }

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