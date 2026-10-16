using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class LightsOutController : MonoBehaviour
{
    [Header("Referencias a Algoritmos")]
    public BackwardFromGoalState backwardGenerator;
    public SimulatedAnnealing simulatedAnnealing;

    [Header("Prefabs Opcionales")]
    public GameObject spherePrefab;
    public GameObject cubePrefab;
    public GameObject tubePrefab;

    [Header("Configuracion Visual 3D")]
    public float nodeSpacing = 2.0f;
    public float animationDelay = 0.08f;
    public Color cleanSphereColor = new Color(0.2f, 0.85f, 0.4f); // Nodo Limpio de Viruz (false)
    public Color virusCubeColor = new Color(0.95f, 0.2f, 0.25f);  // Nodo Infectado de Viruz (true)

    [Header("Interfaz (UI)")]
    public TMP_Text statsText;

    // Representacion 1D del puzzle (false = Esfera Limpia, true = Cubo Infectado)
    private bool[] board;
    private bool[] initialSavedBoard;

    private List<Vector2Int> nodeCoords;
    private List<int>[] neighbors;

    private GameObject[] sphereVisuals;
    private GameObject[] cubeVisuals;
    private List<GameObject> spawnedObjects = new List<GameObject>();

    private bool isBusy = false;
    private int currentClicksUsed = 0;

    private void Start()
    {
        GenerateNewPuzzle();
    }

    // Boton 1 Generar nuevo puzzle con Backward from Goal State
    public void GenerateNewPuzzle()
    {
        if (isBusy || backwardGenerator == null) return;
        StartCoroutine(GeneratePuzzleRoutine());
    }

    // Boton 2 Restaurar la misma instancia inicial para comparar parametros (Lab 6)
    public void ResetToInitialState()
    {
        if (isBusy || initialSavedBoard == null) return;
        System.Array.Copy(initialSavedBoard, board, board.Length);
        currentClicksUsed = 0;
        UpdateVisuals3D();
        UpdateUIText("Estado inicial restaurado (Listo para comparar otra configuracion)");
    }

    // Boton 3 Ejecutar Busqueda Local (Simulated Annealing)
    public void RunSimulatedAnnealing()
    {
        if (isBusy || board == null || simulatedAnnealing == null) return;
        StartCoroutine(ExecuteSimulatedAnnealingRoutine());
    }

    private IEnumerator GeneratePuzzleRoutine()
    {
        isBusy = true;
        ClearVisuals();

        // 1 Generar topologia de la red
        backwardGenerator.GenerateTopology(out nodeCoords, out neighbors);
        int totalNodes = nodeCoords.Count;

        // 2 Construir objetos 3D en escena
        Build3DSceneObjects(totalNodes);

        // 3 Iniciar en Goal State (Todos limpios osea todo false)
        board = new bool[totalNodes];
        initialSavedBoard = new bool[totalNodes];
        currentClicksUsed = 0;
        UpdateVisuals3D();
        UpdateUIText("Goal State: Red Limpia (0 Virus / 100% Esferas)");

        yield return new WaitForSeconds(0.8f);

        // 4 Backward from Goal State animado
        List<int> backwardSteps = backwardGenerator.GenerateBackwardSteps(totalNodes);
        for (int i = 0; i < backwardSteps.Count; i++)
        {
            HeuristicEvaluation.ToggleNode(board, backwardSteps[i], neighbors);
            UpdateVisuals3D();
            UpdateUIText($"Backward from Goal State (Paso {i + 1}/{backwardSteps.Count})");
            yield return new WaitForSeconds(animationDelay);
        }

        if (HeuristicEvaluation.CountInfected(board) == 0)
        {
            HeuristicEvaluation.ToggleNode(board, 0, neighbors);
            UpdateVisuals3D();
        }

        // Guardar copia intacta del estado inicial
        System.Array.Copy(board, initialSavedBoard, totalNodes);
        UpdateUIText($"Estado Inicial Listo | Nodos Infectados (Cubos): {HeuristicEvaluation.CountInfected(board)}");

        isBusy = false;
    }

    private IEnumerator ExecuteSimulatedAnnealingRoutine()
    {
        isBusy = true;
        int initialInfected = HeuristicEvaluation.CountInfected(initialSavedBoard);

        yield return StartCoroutine(simulatedAnnealing.SolveRoutine(
            initialSavedBoard,
            neighbors,
            animationDelay * 0.5f,
            (stepBoard, clicks, iter, temp, bestInfected) =>
            {
                System.Array.Copy(stepBoard, board, board.Length);
                currentClicksUsed = clicks;
                UpdateVisuals3D();
                UpdateUIText(
                    $"Simulated Annealing... Iter: {iter} | Temp: {temp:0.00}\n" +
                    $"Infectados Actuales: {HeuristicEvaluation.CountInfected(stepBoard)} | Mejor Logrado: {bestInfected}"
                );
            },
            (finalBoard, finalClicks, totalIter, initCost, finalCost) =>
            {
                System.Array.Copy(finalBoard, board, board.Length);
                currentClicksUsed = finalClicks;
                UpdateVisuals3D();

                int finalInfected = HeuristicEvaluation.CountInfected(finalBoard);
                UpdateUIText(
                    $"RESULTADO SIMULATED ANNEALING\n" +
                    $"Estado Inicial -> Infectados: {initialInfected} (Costo: {initCost:0})\n" +
                    $"Estado Final   -> Infectados: {finalInfected} (Costo: {finalCost:0})\n" +
                    $"Clics Usados: {finalClicks} | Iteraciones: {totalIter}\n" +
                    $"Params: T0={simulatedAnnealing.initialTemperature:0.0}, Cool={simulatedAnnealing.coolingRate:0.000}, Vecinos={simulatedAnnealing.neighborsPerStep}"
                );
            }
        ));

        isBusy = false;
    }

    public void OnNodeClickedByPlayer(int nodeIndex)
    {
        if (isBusy) return;

        HeuristicEvaluation.ToggleNode(board, nodeIndex, neighbors);
        currentClicksUsed++;
        UpdateVisuals3D();

        int infected = HeuristicEvaluation.CountInfected(board);
        string status = infected == 0 ? "SISTEMA DESINFECTADO" : $"Jugando | Infectados: {infected}";
        UpdateUIText($"{status} | Clics: {currentClicksUsed}");
    }

    private void Build3DSceneObjects(int totalNodes)
    {
        sphereVisuals = new GameObject[totalNodes];
        cubeVisuals = new GameObject[totalNodes];

        Vector2 centerOffset = Vector2.zero;
        foreach (Vector2Int c in nodeCoords) centerOffset += (Vector2)c;
        centerOffset /= totalNodes;

        for (int i = 0; i < totalNodes; i++)
        {
            Vector3 pos = new Vector3(
                (nodeCoords[i].x - centerOffset.x) * nodeSpacing,
                (nodeCoords[i].y - centerOffset.y) * nodeSpacing,
                0f
            );

            GameObject holder = new GameObject($"Node_{i}");
            holder.transform.SetParent(transform);
            holder.transform.localPosition = pos;

            SphereCollider col = holder.AddComponent<SphereCollider>();
            col.radius = 0.6f;

            LightsOutNodeClick detector = holder.AddComponent<LightsOutNodeClick>();
            detector.Setup(this, i);
            spawnedObjects.Add(holder);

            GameObject sphere;
            if (spherePrefab != null)
            {
                sphere = Instantiate(spherePrefab);
            }
            else
            {
                sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Renderer r = sphere.GetComponent<Renderer>();
                if (r != null) r.material.color = cleanSphereColor;
            }
            sphere.transform.SetParent(holder.transform);
            sphere.transform.localPosition = Vector3.zero;
            sphere.transform.localScale = Vector3.one * 0.9f;
            if (sphere.GetComponent<Collider>() != null) Destroy(sphere.GetComponent<Collider>());
            sphereVisuals[i] = sphere;

            GameObject cube;
            if (cubePrefab != null)
            {
                cube = Instantiate(cubePrefab);
            }
            else
            {
                cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Renderer r = cube.GetComponent<Renderer>();
                if (r != null) r.material.color = virusCubeColor;
            }
            cube.transform.SetParent(holder.transform);
            cube.transform.localPosition = Vector3.zero;
            cube.transform.localScale = Vector3.one * 0.85f;
            if (cube.GetComponent<Collider>() != null) Destroy(cube.GetComponent<Collider>());
            cubeVisuals[i] = cube;

            foreach (int nIndex in neighbors[i])
            {
                if (nIndex > i)
                {
                    Vector3 neighborPos = new Vector3(
                        (nodeCoords[nIndex].x - centerOffset.x) * nodeSpacing,
                        (nodeCoords[nIndex].y - centerOffset.y) * nodeSpacing,
                        0f
                    );
                    CreateConnectionTube(pos, neighborPos);
                }
            }
        }
    }

    private void CreateConnectionTube(Vector3 start, Vector3 end)
    {
        GameObject tube;
        if (tubePrefab != null)
        {
            tube = Instantiate(tubePrefab);
        }
        else
        {
            tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Renderer r = tube.GetComponent<Renderer>();
            if (r != null) r.material.color = new Color(0.35f, 0.35f, 0.4f);
        }

        tube.transform.SetParent(transform);
        if (tube.GetComponent<Collider>() != null) Destroy(tube.GetComponent<Collider>());

        tube.transform.localPosition = (start + end) * 0.5f;
        Vector3 dir = end - start;
        tube.transform.up = dir.normalized;
        tube.transform.localScale = new Vector3(0.18f, dir.magnitude * 0.5f, 0.18f);

        spawnedObjects.Add(tube);
    }

    private void UpdateVisuals3D()
    {
        for (int i = 0; i < board.Length; i++)
        {
            bool isInfectedCube = board[i];
            if (sphereVisuals[i] != null) sphereVisuals[i].SetActive(!isInfectedCube);
            if (cubeVisuals[i] != null) cubeVisuals[i].SetActive(isInfectedCube);
        }
    }

    private void ClearVisuals()
    {
        foreach (GameObject obj in spawnedObjects)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedObjects.Clear();
    }

    private void UpdateUIText(string message)
    {
        if (statsText != null)
            statsText.text = message;
    }
}