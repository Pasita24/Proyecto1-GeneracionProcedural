using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class RandomWalkGenerator : MonoBehaviour
{
    [Header("Configuración de Tilemap")]
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private TileBase floorTile;

    [Header("Visualización del Agente")]
    [SerializeField] private Transform agentTransform;

    [Header("Parámetros del Algoritmo")]
    [SerializeField] private int totalPasos = 500;
    [SerializeField] private Vector2Int startPosition = Vector2Int.zero;

    [Tooltip("Cuánto aumenta la probabilidad de cambiar de dirección en cada paso (Ej: 5 = 5%)")]
    [Range(0, 100)]
    [SerializeField] private int incrementoProbDireccion = 5;

    [Tooltip("Cuánto aumenta la probabilidad de crear una sala en cada paso (Ej: 5 = 5%)")]
    [Range(0, 100)]
    [SerializeField] private int incrementoProbSala = 5;

    [Header("Animación en Tiempo Real")]
    [SerializeField] private float stepDelay = 0.05f;

    private static readonly List<Vector2Int> cardinalDirections = new List<Vector2Int>
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    private void Start()
    {
        StartCoroutine(BuildDungeonOverTime());
    }

    private IEnumerator BuildDungeonOverTime()
    {
        tilemap.ClearAllTiles();

        // Iniciamos las probabilidades con el valor del incremento
        int Pc = incrementoProbDireccion;
        int Pr = incrementoProbSala;

        Vector2Int currentPosition = startPosition;
        Vector2Int currentDirection = cardinalDirections[Random.Range(0, cardinalDirections.Count)];

        if (agentTransform != null)
        {
            agentTransform.position = tilemap.GetCellCenterWorld((Vector3Int)currentPosition);
        }

        for (int i = 0; i < totalPasos; i++)
        {
            currentPosition += currentDirection;
            tilemap.SetTile((Vector3Int)currentPosition, floorTile);

            if (agentTransform != null)
            {
                agentTransform.position = tilemap.GetCellCenterWorld((Vector3Int)currentPosition);
            }

            // Evaluar cambio de dirección
            int Nc = Random.Range(0, 101);
            if (Nc < Pc)
            {
                currentDirection = cardinalDirections[Random.Range(0, cardinalDirections.Count)];
                Pc = 0; // Se reinicia
            }
            else
            {
                Pc += incrementoProbDireccion; // Se acumula
            }

            // Evaluar creación de sala
            int Nr = Random.Range(0, 101);
            if (Nr < Pr)
            {
                int roomWidth = Random.Range(3, 8);
                int roomHeight = Random.Range(3, 8);

                PlaceRoom(currentPosition, roomWidth, roomHeight);
                Pr = 0; // Se reinicia
            }
            else
            {
                Pr += incrementoProbSala; // Se acumula
            }

            yield return new WaitForSeconds(stepDelay);
        }
    }

    private void PlaceRoom(Vector2Int center, int width, int height)
    {
        int halfWidth = width / 2;
        int halfHeight = height / 2;

        for (int x = -halfWidth; x <= halfWidth; x++)
        {
            for (int y = -halfHeight; y <= halfHeight; y++)
            {
                Vector2Int tilePos = new Vector2Int(center.x + x, center.y + y);
                tilemap.SetTile((Vector3Int)tilePos, floorTile);
            }
        }
    }
}