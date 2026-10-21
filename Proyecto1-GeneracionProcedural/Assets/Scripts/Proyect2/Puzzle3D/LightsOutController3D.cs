using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manager del puzzle 3D estatico: genera la instancia (Backward from Goal State 3D), construye los nodos
/// en el espacio y resuelve con Evolution Strategy + SA desde el ESTADO ACTUAL del tablero.
/// - Al generar un puzzle calcula y muestra en cuantos pasos se puede resolver.
/// - Cada clic del jugador actualiza las pistas al instante y relanza el calculo en segundo plano.
/// - Los botones de pistas / ver solucion solo aparecen despues de pulsar "Resolver".
/// La camara orbita alrededor del puzzle con clic derecho.
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

    [Header("Generacion")]
    [Tooltip("Si esta desactivado, el puzzle aparece directamente sin animar el Backward from Goal State")]
    public bool animateGeneration = false;

    [Header("Camara Orbital 3D")]
    [Tooltip("Ajusta la distancia de la camara al tamano del puzzle al generarlo")]
    public bool fitCameraOnGenerate = true;
    public float initialYaw = 20f;
    public float initialPitch = 15f;
    public float orbitSensitivity = 4f;
    public float zoomSpeed = 4f;
    public float minDistance = 3f;
    public float maxDistance = 80f;

    [Header("Interfaz (UI)")]
    [Tooltip("Mensajes generales (estado, progreso, clics)")]
    public TMP_Text statsText;
    [Tooltip("Texto fijo: 'Se puede resolver en X pasos' y pasos desde el estado actual (opcional)")]
    public TMP_Text optimalText;

    [Header("Botones que dependen de Resolver")]
    [Tooltip("Boton 'Ver solucion' (solo visible despues de Resolver)")]
    public Button playSolutionButton;
    [Tooltip("Boton 'Pistas ON' (solo visible despues de Resolver)")]
    public Button hintsOnButton;
    [Tooltip("Boton 'Pistas OFF' (solo visible despues de Resolver)")]
    public Button hintsOffButton;

    // ---------- Estado del puzzle ----------
    private bool[] board;               // Estado actual (false = esfera limpia, true = cubo infectado)
    private bool[] initialSavedBoard;   // Estado inicial del puzzle
    private bool[] knownSolution;       // Clics del generador backward (valida desde el estado inicial)
    private bool[] playerClicks;        // Clics aplicados sobre el estado inicial

    // PLAN: clics que hay que pulsar DESDE EL ESTADO ACTUAL para dejarlo limpio.
    // Siempre es una solucion valida; ES+SA lo va acortando.
    private bool[] plan;
    private int planSteps;

    // Mejor solucion conocida desde el estado inicial (para el texto "Se puede resolver en X pasos")
    private bool[] initialPlan;
    private int initialSteps = -1;

    private bool solutionRevealed = false;   // true despues de pulsar Resolver
    private bool showHints = true;
    private bool isSolving = false;
    private Coroutine solveHandle;

    private List<Vector3Int> nodeCoords;
    private List<int>[] neighbors;

    private GameObject[] sphereVisuals;
    private GameObject[] cubeVisuals;
    private GameObject[] hintMarkers;
    private List<GameObject> spawnedObjects = new List<GameObject>();

    private bool isBusy = false;    // Bloquea clics mientras se genera o se anima la solucion
    private int currentClicksUsed = 0;

    // Estado de la camara orbital
    private float orbitYaw;
    private float orbitPitch;
    private float orbitDistance = 15f;

    private void Start()
    {
        InitOrbitFromCamera();
        RefreshButtons();      // Ocultar botones hasta que se pulse Resolver
        GenerateNewPuzzle();
    }

    // Entrada del usuario: clic derecho orbita, rueda hace zoom
    private void Update()
    {
        if (Camera.main == null) return;

        if (Input.GetMouseButton(1))
        {
            orbitYaw += Input.GetAxis("Mouse X") * orbitSensitivity;
            orbitPitch -= Input.GetAxis("Mouse Y") * orbitSensitivity;
            orbitPitch = Mathf.Clamp(orbitPitch, -89f, 89f);
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            orbitDistance = Mathf.Clamp(orbitDistance - scroll * zoomSpeed * 10f, minDistance, maxDistance);
        }
    }

    private void LateUpdate()
    {
        ApplyOrbitCamera();
    }

    private void ApplyOrbitCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Quaternion rot = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
        cam.transform.position = transform.position + rot * new Vector3(0f, 0f, -orbitDistance);
        cam.transform.LookAt(transform.position);
    }

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

    // Boton 1: generar nuevo puzzle (y calcular en cuantos pasos se puede resolver)
    public void GenerateNewPuzzle()
    {
        if (isBusy || backwardGenerator == null) return;
        StartCoroutine(GeneratePuzzleRoutine());
    }

    // Boton 2: volver al estado inicial para comparar parametros
    public void ResetToInitialState()
    {
        if (isBusy || initialSavedBoard == null) return;

        StopSolve();
        RestoreInitialState();

        // El plan del estado inicial ya se conoce (o, si aun no, el del generador)
        plan = (bool[])(initialPlan != null ? initialPlan : knownSolution).Clone();
        planSteps = LightsOutLogic3D.CountTrue(plan);

        UpdateVisuals3D();
        RefreshButtons();
        RefreshInfoText();
        UpdateUIText("Estado inicial restaurado (Listo para comparar otra configuracion)");

        if (initialPlan == null) StartSolve(false);
    }

    // Boton 3: Resolver. Muestra los botones de pistas / solucion y calcula desde el estado ACTUAL
    public void SolveWithEvolutionStrategy()
    {
        if (isBusy || board == null || plan == null || evolutionSolver == null) return;

        if (LightsOutLogic3D.CountTrue(board) == 0)
        {
            UpdateUIText("El puzzle ya esta resuelto");
            return;
        }

        solutionRevealed = true;
        showHints = true;
        RefreshButtons();
        RefreshHints();     // El plan actual ya es valido: las pistas aparecen al instante
        RefreshInfoText();

        StartSolve(true);   // ES+SA con progreso visible, desde el estado actual
    }

    // Boton 4: animar la solucion desde el estado ACTUAL
    public void PlaySolution()
    {
        if (isBusy || !solutionRevealed || plan == null || planSteps <= 0) return;
        StartCoroutine(PlaySolutionRoutine());
    }

    // Boton 5a: Pistas ON
    public void HintsOn()
    {
        showHints = true;
        RefreshHints();
        RefreshButtons();
    }

    // Boton 5b: Pistas OFF
    public void HintsOff()
    {
        showHints = false;
        RefreshHints();
        RefreshButtons();
    }

    // Opcional: un solo boton que alterna
    public void ToggleHints()
    {
        showHints = !showHints;
        RefreshHints();
        RefreshButtons();
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
        StopSolve();
        ClearVisuals();

        // Reiniciar todo el estado del puzzle anterior
        board = null;
        plan = null;
        planSteps = 0;
        initialPlan = null;
        initialSteps = -1;
        solutionRevealed = false;
        showHints = true;
        RefreshButtons();
        RefreshInfoText();

        // 1. Topologia 3D de la red
        backwardGenerator.GenerateTopology(out nodeCoords, out neighbors);
        int totalNodes = nodeCoords.Count;

        // 2. Objetos 3D en escena
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

        // 5. Plan inicial = solucion del generador (valida, pero no necesariamente la minima)
        plan = (bool[])knownSolution.Clone();
        planSteps = LightsOutLogic3D.CountTrue(plan);
        int infected = LightsOutLogic3D.CountTrue(board);
        UpdateUIText(
            $"Estado Inicial Listo | Nodos Infectados (Cubos): {infected}\n" +
            "Calculando la cantidad minima de pasos..."
        );
        RefreshInfoText();

        // 6. Buscar la solucion mas corta con ES+SA (rapido). Los clics siguen bloqueados mientras tanto.
        yield return SolveCurrentStateRoutine(false);

        UpdateUIText(
            $"Estado Inicial Listo | Nodos Infectados (Cubos): {infected}\n" +
            $"SE PUEDE RESOLVER EN {initialSteps} PASOS " +
            $"(el generador uso {LightsOutLogic3D.CountTrue(knownSolution)})"
        );

        isBusy = false;
    }

    // Aplica un clic del backward y lo registra como parte de la solucion de referencia
    private void ApplyBackwardStep(int nodeIndex)
    {
        LightsOutLogic3D.ToggleNode(board, nodeIndex, neighbors);
        knownSolution[nodeIndex] = !knownSolution[nodeIndex];
    }

    // Lanza (o relanza) el calculo ES+SA desde el estado actual, cancelando el anterior
    private void StartSolve(bool verbose)
    {
        StopSolve();
        solveHandle = StartCoroutine(SolveCurrentStateRoutine(verbose));
    }

    private void StopSolve()
    {
        if (solveHandle != null) StopCoroutine(solveHandle);
        solveHandle = null;
        isSolving = false;
    }

    // Busca la solucion mas corta desde el tablero ACTUAL, sembrando ES con el plan vigente.
    // verbose = true: modo visible (muestra progreso y resultado en statsText).
    // verbose = false: modo rapido en segundo plano (solo actualiza pistas y texto de pasos).
    private IEnumerator SolveCurrentStateRoutine(bool verbose)
    {
        if (board == null || plan == null || evolutionSolver == null) yield break;

        // Tablero ya limpio: no hay nada que resolver
        if (LightsOutLogic3D.CountTrue(board) == 0)
        {
            plan = new bool[board.Length];
            planSteps = 0;
            RefreshButtons();
            RefreshInfoText();
            yield break;
        }

        isSolving = true;
        RefreshInfoText();

        bool[] snapshot = (bool[])board.Clone();
        bool atInitial = BoardsEqual(board, initialSavedBoard);
        int stepsBefore = planSteps;
        ESResult finalResult = null;

        yield return evolutionSolver.SolveRoutine(
            snapshot,
            neighbors,
            plan,           // Semilla: solucion valida para este mismo tablero
            !verbose,       // verbose -> modo lento con progreso; si no, modo rapido
            verbose
                ? (System.Action<int, int, int>)((gen, bestSteps, bestInfected) =>
                {
                    string best = bestSteps >= 0 ? $"{bestSteps} pasos" : "sin solucion valida aun";
                    UpdateUIText(
                        $"Evolution Strategy + SA... Generacion: {gen}/{evolutionSolver.maxGenerations}\n" +
                        $"Mejor solucion valida desde aqui: {best}"
                    );
                })
                : null,
            (result) => { finalResult = result; }
        );

        isSolving = false;
        solveHandle = null;

        // Si el tablero cambio mientras calculaba, el resultado ya no sirve
        if (!BoardsEqual(board, snapshot)) yield break;

        // Solo se reemplaza el plan si ES+SA encontro uno mas corto (el plan siempre sigue siendo valido)
        if (finalResult != null && finalResult.found && finalResult.steps < planSteps)
        {
            plan = (bool[])finalResult.clicks.Clone();
            planSteps = finalResult.steps;
        }

        if (atInitial)
        {
            initialPlan = (bool[])plan.Clone();
            initialSteps = planSteps;
        }

        RefreshHints();
        RefreshButtons();
        RefreshInfoText();

        if (verbose)
        {
            string comparison = planSteps < stepsBefore
                ? $"Mejora: {stepsBefore} -> {planSteps} clics"
                : $"Sin mejora ({stepsBefore} clics)";

            UpdateUIText(
                $"SE PUEDE RESOLVER EN {planSteps} PASOS DESDE ESTE ESTADO\n" +
                $"Infectados actuales: {LightsOutLogic3D.CountTrue(board)} | {comparison}\n" +
                (finalResult != null
                    ? $"Generaciones: {finalResult.generations} | Evaluaciones: {finalResult.evaluations} | Tiempo: {finalResult.seconds:0.00}s\n"
                    : "") +
                $"Params: mu={evolutionSolver.parentCount}, lambda={evolutionSolver.offspringCount}, " +
                $"SA iter={evolutionSolver.saIterations}, T0={evolutionSolver.saInitialTemperature:0.0}, Cool={evolutionSolver.saCoolingRate:0.000}"
            );
        }
    }

    // Anima el plan actual desde el estado actual
    private IEnumerator PlaySolutionRoutine()
    {
        isBusy = true;
        StopSolve();

        // Lista fija de nodos a pulsar (el plan se va modificando al aplicar cada clic)
        List<int> steps = new List<int>();
        for (int i = 0; i < plan.Length; i++)
            if (plan[i]) steps.Add(i);

        int total = steps.Count;
        yield return new WaitForSeconds(0.3f);

        for (int s = 0; s < total; s++)
        {
            ApplyClick(steps[s]);
            UpdateVisuals3D();
            UpdateUIText($"Aplicando solucion: paso {s + 1}/{total}");
            yield return new WaitForSeconds(animationDelay * 5f);
        }

        if (LightsOutLogic3D.CountTrue(board) == 0)
        {
            plan = new bool[board.Length];
            planSteps = 0;
            UpdateUIText($"SISTEMA DESINFECTADO | Clics: {currentClicksUsed}");
        }
        else
        {
            UpdateUIText("La solucion no dejo el sistema limpio (revisar parametros)");
        }

        RefreshHints();
        RefreshButtons();
        RefreshInfoText();
        isBusy = false;
    }

    // ---------- Interaccion del jugador ----------

    public void OnNodeClickedByPlayer(int nodeIndex)
    {
        if (isBusy || board == null) return;

        ApplyClick(nodeIndex);
        UpdateVisuals3D();

        int infected = LightsOutLogic3D.CountTrue(board);

        // Puzzle resuelto por el jugador
        if (infected == 0)
        {
            StopSolve();
            plan = new bool[board.Length];
            planSteps = 0;
            RefreshHints();
            RefreshButtons();
            RefreshInfoText();
            UpdateUIText($"SISTEMA DESINFECTADO | Clics: {currentClicksUsed}");
            return;
        }

        string remaining = solutionRevealed ? $" | Pasos restantes: {planSteps}" : "";
        UpdateUIText($"Jugando | Infectados: {infected} | Clics: {currentClicksUsed}{remaining}");

        // Las pistas ya estan al dia (ApplyClick actualizo el plan);
        // ahora se busca en segundo plano un camino aun mas corto desde este nuevo estado
        RefreshHints();
        RefreshButtons();
        StartSolve(false);
    }

    // Aplica un clic sobre el tablero ACTUAL y mantiene el plan valido:
    // si el nodo estaba en el plan sale (un paso menos); si no, entra (hay que deshacerlo).
    private void ApplyClick(int nodeIndex)
    {
        LightsOutLogic3D.ToggleNode(board, nodeIndex, neighbors);
        playerClicks[nodeIndex] = !playerClicks[nodeIndex];
        currentClicksUsed++;

        if (plan != null)
        {
            plan[nodeIndex] = !plan[nodeIndex];
            planSteps += plan[nodeIndex] ? 1 : -1;
        }
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

    // ---------- Utilidades de interfaz ----------

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

    // Pistas: los nodos del plan (clics que faltan desde el estado ACTUAL)
    private void RefreshHints()
    {
        if (hintMarkers == null) return;
        for (int i = 0; i < hintMarkers.Length; i++)
        {
            if (hintMarkers[i] == null) continue;
            bool pending = showHints && solutionRevealed && plan != null && i < plan.Length && plan[i];
            hintMarkers[i].SetActive(pending);
        }
    }

    // Muestra u oculta los botones segun el estado del puzzle
    private void RefreshButtons()
    {
        bool solved = board != null && LightsOutLogic3D.CountTrue(board) == 0;
        bool active = solutionRevealed && !solved;

        if (playSolutionButton != null)
            playSolutionButton.gameObject.SetActive(active && planSteps > 0);

        if (hintsOnButton != null)
        {
            hintsOnButton.gameObject.SetActive(active);
            hintsOnButton.interactable = !showHints;     // El activo queda deshabilitado
        }

        if (hintsOffButton != null)
        {
            hintsOffButton.gameObject.SetActive(active);
            hintsOffButton.interactable = showHints;
        }
    }

    // Texto fijo: pasos del puzzle y, tras Resolver, pasos desde el estado actual
    private void RefreshInfoText()
    {
        if (optimalText == null) return;

        string line1 = initialSteps >= 0
            ? $"Se puede resolver en {initialSteps} pasos"
            : "Calculando pasos minimos...";

        string line2 = "";
        if (solutionRevealed && initialSteps >= 0 && board != null
            && LightsOutLogic3D.CountTrue(board) > 0 && !BoardsEqual(board, initialSavedBoard))
        {
            line2 = $"\nDesde tu estado actual: {planSteps} pasos" + (isSolving ? " (recalculando...)" : "");
        }

        optimalText.text = line1 + line2;
    }

    private static bool BoardsEqual(bool[] a, bool[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
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