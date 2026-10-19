using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Manager del puzzle 3D estatico: genera la instancia (Backward from Goal State 3D), construye los nodos
/// en el espacio, resuelve con Evolution Strategy + SA y muestra en cuantos pasos se puede resolver.
/// El puzzle no se mueve: la camara orbita alrededor de el con clic derecho.
/// </summary>
public class LightsOutController3D : MonoBehaviour
{
    [Header("Referencias a Algoritmos")]
    public BackwardFromGoalState3D backwardGenerator;
    public EvolutionStrategySolver evolutionSolver;

    [Header("Prefabs Opcionales")]
    public GameObject spherePrefab;
    public GameObject cubePrefab;
    public GameObject tubePrefab;

    [Header("Configuracion Visual 3D")]
    public float nodeSpacing = 2.0f;
    public float animationDelay = 0.08f;
    public Color cleanSphereColor = new Color(0.2f, 0.85f, 0.4f);   // Nodo limpio (false)
    public Color virusCubeColor = new Color(0.95f, 0.2f, 0.25f);    // Nodo infectado (true)
    public Color hintColor = new Color(1f, 0.9f, 0.15f);           // Marcador de pista
    public bool showHints = true;

    [Header("Generacion")]
    [Tooltip("Si esta desactivado, el puzzle aparece directamente sin animar el Backward from Goal State")]
    public bool animateGeneration = false;

    [Header("Camara Orbital 3D")]
    [Tooltip("Ajusta la distancia de la camara al tamano del puzzle al generarlo")]
    public bool fitCameraOnGenerate = true;
    [Tooltip("Angulo horizontal inicial de la camara (grados)")]
    public float initialYaw = 20f;
    [Tooltip("Angulo vertical inicial de la camara (grados)")]
    public float initialPitch = 15f;
    [Tooltip("Sensibilidad al arrastrar con clic derecho")]
    public float orbitSensitivity = 4f;
    [Tooltip("Velocidad de zoom con la rueda del mouse")]
    public float zoomSpeed = 4f;
    public float minDistance = 3f;
    public float maxDistance = 80f;

    [Header("Interfaz (UI)")]
    public TMP_Text statsText;

    // Estado del puzzle (false = esfera limpia, true = cubo infectado)
    private bool[] board;
    private bool[] initialSavedBoard;
    private bool[] knownSolution;     // Clics del generador backward (una solucion valida, no necesariamente la optima)
    private bool[] solutionClicks;    // Mejor solucion encontrada por ES+SA
    private bool[] playerClicks;      // Clics aplicados actualmente sobre el estado inicial

    private List<Vector3Int> nodeCoords;
    private List<int>[] neighbors;

    private GameObject[] sphereVisuals;
    private GameObject[] cubeVisuals;
    private GameObject[] hintMarkers;
    private List<GameObject> spawnedObjects = new List<GameObject>();

    private bool isBusy = false;
    private int currentClicksUsed = 0;

    // Estado de la camara orbital
    private float orbitYaw;
    private float orbitPitch;
    private float orbitDistance = 15f;

    private void Start()
    {
        InitOrbitFromCamera();
        GenerateNewPuzzle();
    }

    // Entrada del usuario: clic derecho orbita, rueda hace zoom
    private void Update()
    {
        if (Camera.main == null) return;

        // Clic derecho + arrastrar: la camara gira alrededor del puzzle
        if (Input.GetMouseButton(1))
        {
            orbitYaw += Input.GetAxis("Mouse X") * orbitSensitivity;
            orbitPitch -= Input.GetAxis("Mouse Y") * orbitSensitivity;
            orbitPitch = Mathf.Clamp(orbitPitch, -89f, 89f);   // Evita que la camara se de vuelta
        }

        // Rueda: acercar / alejar
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            orbitDistance = Mathf.Clamp(orbitDistance - scroll * zoomSpeed * 10f, minDistance, maxDistance);
        }
    }

    // La camara se actualiza al final del frame, despues de procesar la entrada
    private void LateUpdate()
    {
        ApplyOrbitCamera();
    }

    // Coloca la camara en una esfera alrededor del puzzle, mirando siempre a su centro
    private void ApplyOrbitCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Quaternion rot = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
        cam.transform.position = transform.position + rot * new Vector3(0f, 0f, -orbitDistance);
        cam.transform.LookAt(transform.position);
    }

    // Si no se ajusta automaticamente, parte desde donde ya este la camara en la escena
    private void InitOrbitFromCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        orbitYaw = initialYaw;
        orbitPitch = initialPitch;

        if (!fitCameraOnGenerate)
        {
            Vector3 offset = cam.transform.position - transform.position;
            orbitDistance = Mathf.Clamp(offset.magnitude, minDistance, maxDistance);
        }
    }

    // ---------- Botones de UI ----------

    // Boton 1: generar nuevo puzzle con Backward from Goal State
    public void GenerateNewPuzzle()
    {
        if (isBusy || backwardGenerator == null) return;
        StartCoroutine(GeneratePuzzleRoutine());
    }

    // Boton 2: volver al estado inicial para comparar parametros
    public void ResetToInitialState()
    {
        if (isBusy || initialSavedBoard == null) return;
        RestoreInitialState();
        UpdateVisuals3D();
        UpdateUIText("Estado inicial restaurado (Listo para comparar otra configuracion)");
    }

    // Boton 3: calcular la solucion con Evolution Strategy + Simulated Annealing
    public void SolveWithEvolutionStrategy()
    {
        if (isBusy || initialSavedBoard == null || evolutionSolver == null) return;
        StartCoroutine(SolveRoutine());
    }

    // Boton 4: animar la solucion encontrada paso a paso
    public void PlaySolution()
    {
        if (isBusy || solutionClicks == null) return;
        StartCoroutine(PlaySolutionRoutine());
    }

    // Boton 5: mostrar/ocultar pistas
    public void ToggleHints()
    {
        showHints = !showHints;
        RefreshHints();
    }

    // Boton 6 (opcional): volver la camara a su posicion inicial
    public void ResetCamera()
    {
        orbitYaw = initialYaw;
        orbitPitch = initialPitch;
    }

    // ---------- Rutinas ----------

    private IEnumerator GeneratePuzzleRoutine()
    {
        isBusy = true;
        ClearVisuals();
        solutionClicks = null;

        // 1. Topologia 3D de la red
        backwardGenerator.GenerateTopology(out nodeCoords, out neighbors);
        int totalNodes = nodeCoords.Count;

        // 2. Objetos 3D en escena (tambien ajusta la distancia de la camara)
        Build3DSceneObjects(totalNodes);

        // 3. Goal State: todo limpio
        board = new bool[totalNodes];
        initialSavedBoard = new bool[totalNodes];
        knownSolution = new bool[totalNodes];
        playerClicks = new bool[totalNodes];
        currentClicksUsed = 0;
        UpdateVisuals3D();

        // 4. Backward from Goal State
        List<int> backwardSteps = backwardGenerator.GenerateBackwardSteps(totalNodes);

        if (animateGeneration)
        {
            // Version animada (opcional)
            UpdateUIText("Goal State: Red Limpia (0 Virus / 100% Esferas)");
            yield return new WaitForSeconds(0.8f);

            for (int i = 0; i < backwardSteps.Count; i++)
            {
                ApplyBackwardStep(backwardSteps[i]);
                UpdateVisuals3D();
                UpdateUIText($"Backward from Goal State (Paso {i + 1}/{backwardSteps.Count})");
                yield return new WaitForSeconds(animationDelay);
            }
        }
        else
        {
            // Version instantanea: aplica todos los pasos de golpe
            for (int i = 0; i < backwardSteps.Count; i++)
                ApplyBackwardStep(backwardSteps[i]);
            UpdateVisuals3D();
        }

        // Si los clics se cancelaron y quedo todo limpio, forzar un clic para que haya puzzle
        if (LightsOutLogic3D.CountTrue(board) == 0)
        {
            ApplyBackwardStep(0);
            UpdateVisuals3D();
        }

        System.Array.Copy(board, initialSavedBoard, totalNodes);
        UpdateUIText(
            $"Estado Inicial Listo | Nodos Infectados (Cubos): {LightsOutLogic3D.CountTrue(board)}\n" +
            $"El generador uso {LightsOutLogic3D.CountTrue(knownSolution)} clics (no es necesariamente el minimo)"
        );

        isBusy = false;
    }

    // Aplica un clic del backward y lo registra como parte de la solucion de referencia
    private void ApplyBackwardStep(int nodeIndex)
    {
        LightsOutLogic3D.ToggleNode(board, nodeIndex, neighbors);
        knownSolution[nodeIndex] = !knownSolution[nodeIndex];
    }

    private IEnumerator SolveRoutine()
    {
        isBusy = true;
        solutionClicks = null;
        RestoreInitialState();   // Siempre se resuelve desde el estado inicial
        UpdateVisuals3D();

        int initialInfected = LightsOutLogic3D.CountTrue(initialSavedBoard);
        ESResult finalResult = null;

        yield return StartCoroutine(evolutionSolver.SolveRoutine(
            initialSavedBoard,
            neighbors,
            knownSolution,
            // Progreso por generacion
            (gen, bestSteps, bestInfected) =>
            {
                string best = bestSteps >= 0 ? $"{bestSteps} pasos" : "sin solucion valida aun";
                UpdateUIText(
                    $"Evolution Strategy + SA... Generacion: {gen}/{evolutionSolver.maxGenerations}\n" +
                    $"Mejor solucion valida: {best}"
                );
            },
            // Resultado final
            (result) => { finalResult = result; }
        ));

        if (finalResult != null && finalResult.found)
        {
            solutionClicks = finalResult.clicks;
            string comparison = finalResult.steps < finalResult.knownSteps
                ? $"Mejora al generador: {finalResult.knownSteps} -> {finalResult.steps} clics"
                : $"Igual al generador ({finalResult.knownSteps} clics)";

            UpdateUIText(
                $"SE PUEDE RESOLVER EN {finalResult.steps} PASOS\n" +
                $"Infectados iniciales: {initialInfected} | {comparison}\n" +
                $"Generaciones: {finalResult.generations} | Evaluaciones: {finalResult.evaluations} | Tiempo: {finalResult.seconds:0.00}s\n" +
                $"Params: mu={evolutionSolver.parentCount}, lambda={evolutionSolver.offspringCount}, " +
                $"SA iter={evolutionSolver.saIterations}, T0={evolutionSolver.saInitialTemperature:0.0}, Cool={evolutionSolver.saCoolingRate:0.000}"
            );
        }
        else
        {
            UpdateUIText("No se encontro una solucion valida con estos parametros. Sube generaciones, hijos o iteraciones de SA.");
        }

        RefreshHints();
        isBusy = false;
    }

    private IEnumerator PlaySolutionRoutine()
    {
        isBusy = true;
        RestoreInitialState();
        UpdateVisuals3D();
        yield return new WaitForSeconds(0.5f);

        int total = LightsOutLogic3D.CountTrue(solutionClicks);
        int step = 0;

        for (int i = 0; i < solutionClicks.Length; i++)
        {
            if (!solutionClicks[i]) continue;

            LightsOutLogic3D.ToggleNode(board, i, neighbors);
            playerClicks[i] = true;
            currentClicksUsed++;
            step++;
            UpdateVisuals3D();
            UpdateUIText($"Aplicando solucion optima: paso {step}/{total}");
            yield return new WaitForSeconds(animationDelay * 5f);
        }

        UpdateUIText(
            LightsOutLogic3D.CountTrue(board) == 0
                ? $"SISTEMA DESINFECTADO en {total} pasos"
                : "La solucion no dejo el sistema limpio (revisar parametros)"
        );
        isBusy = false;
    }

    // ---------- Interaccion del jugador ----------

    public void OnNodeClickedByPlayer(int nodeIndex)
    {
        if (isBusy || board == null) return;

        LightsOutLogic3D.ToggleNode(board, nodeIndex, neighbors);
        playerClicks[nodeIndex] = !playerClicks[nodeIndex];
        currentClicksUsed++;
        UpdateVisuals3D();

        int infected = LightsOutLogic3D.CountTrue(board);
        string status = infected == 0 ? "SISTEMA DESINFECTADO" : $"Jugando | Infectados: {infected}";
        string remaining = solutionClicks != null && infected > 0
            ? $" | Pasos restantes sugeridos: {CountPendingHints()}"
            : "";
        UpdateUIText($"{status} | Clics: {currentClicksUsed}{remaining}");
    }

    // ---------- Construccion de la escena 3D ----------

    private void Build3DSceneObjects(int totalNodes)
    {
        sphereVisuals = new GameObject[totalNodes];
        cubeVisuals = new GameObject[totalNodes];
        hintMarkers = new GameObject[totalNodes];

        // Centro del grafo para centrar la red en el origen del controller
        Vector3 center = Vector3.zero;
        foreach (Vector3Int c in nodeCoords) center += (Vector3)c;
        center /= totalNodes;

        // Posiciones locales de todos los nodos (tambien sirve para calcular el tamano de la camara)
        Vector3[] positions = new Vector3[totalNodes];
        float radius = 0f;
        for (int i = 0; i < totalNodes; i++)
        {
            positions[i] = ((Vector3)nodeCoords[i] - center) * nodeSpacing;
            radius = Mathf.Max(radius, positions[i].magnitude);
        }

        for (int i = 0; i < totalNodes; i++)
        {
            // Contenedor del nodo (con collider para detectar el clic)
            GameObject holder = new GameObject($"Node_{i}");
            holder.transform.SetParent(transform);
            holder.transform.localPosition = positions[i];

            SphereCollider col = holder.AddComponent<SphereCollider>();
            col.radius = 0.6f;

            LightsOutNodeClick3D detector = holder.AddComponent<LightsOutNodeClick3D>();
            detector.Setup(this, i);
            spawnedObjects.Add(holder);

            // Esfera (nodo limpio)
            sphereVisuals[i] = CreateVisual(spherePrefab, PrimitiveType.Sphere, cleanSphereColor, holder.transform, 0.9f);

            // Cubo (nodo infectado)
            cubeVisuals[i] = CreateVisual(cubePrefab, PrimitiveType.Cube, virusCubeColor, holder.transform, 0.85f);

            // Marcador de pista: esferita amarilla sobre el nodo
            GameObject hint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Renderer hr = hint.GetComponent<Renderer>();
            if (hr != null) hr.material.color = hintColor;
            hint.transform.SetParent(holder.transform);
            hint.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            hint.transform.localScale = Vector3.one * 0.3f;
            if (hint.GetComponent<Collider>() != null) Destroy(hint.GetComponent<Collider>());
            hint.SetActive(false);
            hintMarkers[i] = hint;

            // Conexiones 3D (cada arista una sola vez)
            foreach (int nIndex in neighbors[i])
            {
                if (nIndex > i) CreateConnectionTube(positions[i], positions[nIndex]);
            }
        }

        // Camara: distancia segun el tamano del grafo y angulo inicial
        if (fitCameraOnGenerate)
        {
            orbitDistance = Mathf.Clamp((radius + 3f) * 1.8f, minDistance, maxDistance);
            orbitYaw = initialYaw;
            orbitPitch = initialPitch;
        }
    }

    // Crea la parte visual (prefab o primitiva) como hija del nodo, sin collider propio
    private GameObject CreateVisual(GameObject prefab, PrimitiveType fallback, Color color, Transform parent, float scale)
    {
        GameObject obj;
        if (prefab != null)
        {
            obj = Instantiate(prefab);
        }
        else
        {
            obj = GameObject.CreatePrimitive(fallback);
            Renderer r = obj.GetComponent<Renderer>();
            if (r != null) r.material.color = color;
        }

        obj.transform.SetParent(parent);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localScale = Vector3.one * scale;
        if (obj.GetComponent<Collider>() != null) Destroy(obj.GetComponent<Collider>());
        return obj;
    }

    // Tubo entre dos puntos 3D
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

        Vector3 dir = end - start;
        tube.transform.localPosition = (start + end) * 0.5f;
        tube.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
        // El cilindro de Unity mide 2 de alto, por eso la mitad de la distancia
        tube.transform.localScale = new Vector3(0.18f, dir.magnitude * 0.5f, 0.18f);

        spawnedObjects.Add(tube);
    }

    // ---------- Utilidades ----------

    private void RestoreInitialState()
    {
        System.Array.Copy(initialSavedBoard, board, board.Length);
        playerClicks = new bool[board.Length];
        currentClicksUsed = 0;
    }

    private void UpdateVisuals3D()
    {
        for (int i = 0; i < board.Length; i++)
        {
            bool isInfectedCube = board[i];
            if (sphereVisuals[i] != null) sphereVisuals[i].SetActive(!isInfectedCube);
            if (cubeVisuals[i] != null) cubeVisuals[i].SetActive(isInfectedCube);
        }
        RefreshHints();
    }

    // Pista: clics que faltan = solucion XOR clics del jugador
    private void RefreshHints()
    {
        if (hintMarkers == null) return;
        for (int i = 0; i < hintMarkers.Length; i++)
        {
            if (hintMarkers[i] == null) continue;
            bool pending = showHints && solutionClicks != null && playerClicks != null
                           && solutionClicks[i] != playerClicks[i];
            hintMarkers[i].SetActive(pending);
        }
    }

    private int CountPendingHints()
    {
        int count = 0;
        for (int i = 0; i < solutionClicks.Length; i++)
            if (solutionClicks[i] != playerClicks[i]) count++;
        return count;
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
        if (statsText != null) statsText.text = message;
    }
}