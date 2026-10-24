using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manager del puzzle 3D estatico: genera la instancia (Backward from Goal State 3D), construye los nodos
/// en el espacio y resuelve con Evolution Strategy + SA desde el ESTADO ACTUAL del tablero.
/// - Todos los elementos visuales (nodos, tubos, pistas, previsualizacion) son PREFABS asignados en el inspector.
/// - Al generar un puzzle calcula en cuantos pasos se puede resolver.
/// - Cada clic del jugador actualiza las pistas al instante y relanza el calculo en segundo plano.
/// - Pistas: modo desarrollador (Resolver + ON/OFF) y modo juego (pista de un nodo / plan completo).
/// - Previsualizacion al pasar el mouse: resalta el nodo y sus vecinos.
/// La camara orbita alrededor del puzzle con clic derecho.
/// </summary>
public class LightsOutController3D : MonoBehaviour
{
    [Header("Referencias a Algoritmos")]
    public BackwardFromGoalState3D backwardGenerator;
    public EvolutionStrategySolver evolutionSolver;

    [Header("Prefabs (OBLIGATORIOS, arrastrar desde el Project)")]
    [Tooltip("Nodo limpio")]
    public GameObject spherePrefab;
    [Tooltip("Nodo infectado")]
    public GameObject cubePrefab;
    [Tooltip("Conexion entre nodos (su eje largo debe ser Y, como el cilindro de Unity)")]
    public GameObject tubePrefab;
    [Tooltip("Marcador de pista (aparece sobre los nodos que hay que pulsar)")]
    public GameObject hintPrefab;
    [Tooltip("Marcador de previsualizacion (aparece bajo el nodo en que esta el mouse y sus vecinos)")]
    public GameObject previewPrefab;

    [Header("Ajustes de prefabs")]
    [Tooltip("Posicion del marcador de pista respecto al nodo")]
    public Vector3 hintOffset = new Vector3(0f, 0.85f, 0f);
    [Tooltip("Posicion del marcador de previsualizacion respecto al nodo")]
    public Vector3 previewOffset = new Vector3(0f, -0.85f, 0f);
    [Tooltip("Grosor del tubo (escala X y Z)")]
    public float tubeThickness = 0.18f;
    [Tooltip("Largo del modelo del tubo en Y con escala 1 (el cilindro de Unity mide 2; si tu modelo mide 1, poner 1)")]
    public float tubeModelLength = 2f;

    [Header("Configuracion Visual 3D")]
    public float nodeSpacing = 2.0f;
    public float animationDelay = 0.08f;

    [Header("Generacion")]
    [Tooltip("Si esta desactivado, el puzzle aparece directamente sin animar el Backward from Goal State")]
    public bool animateGeneration = false;
    [Tooltip("Generar un puzzle al iniciar la escena (dejar apagado si se usa el GameManager)")]
    public bool generateOnStart = false;

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
    [Tooltip("Etiqueta fija (por ejemplo 'Pistas'): se muestra u oculta junto a los botones ON/OFF, su texto no se modifica")]
    public TMP_Text resolveText;

    [Header("Botones que dependen de Resolver (modo desarrollador)")]
    [Tooltip("Boton 'Ver solucion' (solo visible despues de Resolver)")]
    public Button playSolutionButton;
    [Tooltip("Boton 'Pistas ON' (solo visible despues de Resolver)")]
    public Button hintsOnButton;
    [Tooltip("Boton 'Pistas OFF' (solo visible despues de Resolver)")]
    public Button hintsOffButton;

    // ---------- Eventos y propiedades para el GameManager ----------
    public event System.Action OnPuzzleReady;     // Puzzle generado y optimo ya calculado
    public event System.Action OnPuzzleSolved;    // El jugador dejo el sistema limpio

    public bool InputLocked { get; set; }          // Bloquea los clics sobre los nodos
    public bool IsBusy => isBusy;
    public int ClicksUsed => currentClicksUsed;
    public int OptimalSteps => initialSteps;       // -1 si aun se esta calculando
    public bool HasFullHints => solutionRevealed;  // Ya se muestra el plan completo

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

    private bool solutionRevealed = false;   // true despues de Resolver (dev) o del plan completo (juego)
    private bool showHints = true;
    private bool[] revealedHints;            // Nodos del plan revelados por pistas individuales
    private bool isSolving = false;
    private Coroutine solveHandle;

    private List<Vector3Int> nodeCoords;
    private List<int>[] neighbors;

    // Escala base de cada prefab (la del propio prefab), para restaurarla tras la previsualizacion
    private Vector3 sphereBaseScale = Vector3.one;
    private Vector3 cubeBaseScale = Vector3.one;

    private GameObject[] sphereVisuals;
    private GameObject[] cubeVisuals;
    private GameObject[] hintMarkers;
    private GameObject[] previewMarkers;
    private int hoveredNode = -1;
    private List<GameObject> spawnedObjects = new List<GameObject>();

    private bool isBusy = false;    // Bloquea clics mientras se genera o se anima la solucion
    private int currentClicksUsed = 0;

    // Estado de la camara orbital
    private float orbitYaw;
    private float orbitPitch;
    private float orbitDistance = 15f;

#if UNITY_EDITOR
    // Avisa en el editor si a un campo de prefab se le arrastro un objeto de la escena
    private void OnValidate()
    {
        CheckIsPrefab(spherePrefab, "Sphere Prefab");
        CheckIsPrefab(cubePrefab, "Cube Prefab");
        CheckIsPrefab(tubePrefab, "Tube Prefab");
        CheckIsPrefab(hintPrefab, "Hint Prefab");
        CheckIsPrefab(previewPrefab, "Preview Prefab");
    }

    private void CheckIsPrefab(GameObject obj, string fieldName)
    {
        if (obj != null && !UnityEditor.PrefabUtility.IsPartOfPrefabAsset(obj))
        {
            Debug.LogWarning($"LightsOutController3D: '{fieldName}' debe ser un PREFAB de la carpeta Assets, " +
                             "no un objeto de la escena.", this);
        }
    }
#endif

    private void Start()
    {
        InitOrbitFromCamera();
        RefreshButtons();      // Ocultar botones hasta que se pulse Resolver
        if (generateOnStart) GenerateNewPuzzle();
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
        if (!PrefabsAssigned()) return;
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

    // ---------- Pistas del modo juego (las llama el GameManager) ----------

    // Revela UN nodo del plan que aun no se haya revelado. Devuelve false si no habia ninguno.
    public bool RevealOneHint()
    {
        if (isBusy || board == null || plan == null || revealedHints == null) return false;

        List<int> candidates = new List<int>();
        for (int i = 0; i < plan.Length; i++)
            if (plan[i] && !revealedHints[i]) candidates.Add(i);

        if (candidates.Count == 0) return false;

        revealedHints[candidates[Random.Range(0, candidates.Count)]] = true;
        showHints = true;
        RefreshHints();
        return true;
    }

    // Revela todos los nodos pendientes del plan. Devuelve false si ya estaban visibles o no hay nada que resolver.
    public bool RevealFullPlan()
    {
        if (isBusy || board == null || plan == null || solutionRevealed) return false;
        if (LightsOutLogic3D.CountTrue(board) == 0) return false;

        solutionRevealed = true;
        showHints = true;
        RefreshButtons();
        RefreshHints();
        RefreshInfoText();
        return true;
    }

    // ---------- Previsualizacion al pasar el mouse ----------

    public void SetHoveredNode(int index)
    {
        if (board == null || neighbors == null || isBusy || InputLocked) return;
        if (index < 0 || index >= neighbors.Length) return;

        ClearPreview();
        hoveredNode = index;
        ApplyPreview(index, true);
    }

    // Solo limpia si el nodo que se deja es el que estaba resaltado
    public void ClearHoveredNode(int index)
    {
        if (hoveredNode != index) return;
        ClearPreview();
    }

    private void ClearPreview()
    {
        if (hoveredNode >= 0 && neighbors != null && hoveredNode < neighbors.Length && sphereVisuals != null)
            ApplyPreview(hoveredNode, false);
        hoveredNode = -1;
    }

    // Resalta (o restaura) el nodo y sus vecinos: son los que cambian al hacer clic
    private void ApplyPreview(int node, bool on)
    {
        SetNodePreview(node, on, true);

        List<int> list = neighbors[node];
        for (int i = 0; i < list.Count; i++)
            SetNodePreview(list[i], on, false);
    }

    private void SetNodePreview(int i, bool on, bool isCenter)
    {
        float mult = on ? (isCenter ? 1.3f : 1.18f) : 1f;

        if (sphereVisuals != null && sphereVisuals[i] != null)
            sphereVisuals[i].transform.localScale = sphereBaseScale * mult;
        if (cubeVisuals != null && cubeVisuals[i] != null)
            cubeVisuals[i].transform.localScale = cubeBaseScale * mult;
        if (previewMarkers != null && previewMarkers[i] != null)
            previewMarkers[i].SetActive(on);
    }

    // ---------- Limpiar el puzzle (volver al menu) ----------

    public void ClearPuzzle()
    {
        StopAllCoroutines();
        solveHandle = null;
        isSolving = false;
        isBusy = false;
        hoveredNode = -1;

        ClearVisuals();
        sphereVisuals = null;
        cubeVisuals = null;
        hintMarkers = null;
        previewMarkers = null;

        board = null;
        initialSavedBoard = null;
        knownSolution = null;
        playerClicks = null;
        plan = null;
        planSteps = 0;
        initialPlan = null;
        initialSteps = -1;
        revealedHints = null;
        solutionRevealed = false;
        showHints = true;
        currentClicksUsed = 0;

        RefreshButtons();
        RefreshInfoText();
        UpdateUIText("");
    }

    // ---------- Rutinas ----------

    private IEnumerator GeneratePuzzleRoutine()
    {
        isBusy = true;
        InputLocked = false;
        hoveredNode = -1;
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
        revealedHints = new bool[totalNodes];

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
        OnPuzzleReady?.Invoke();   // Avisa al GameManager
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
        if (isBusy || InputLocked || board == null) return;

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
            OnPuzzleSolved?.Invoke();   // Avisa al GameManager
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

    // ---------- Construccion de la escena 3D (todo con prefabs) ----------

    // Comprueba que los 5 prefabs esten asignados. Si falta alguno, no se genera el puzzle.
    private bool PrefabsAssigned()
    {
        List<string> missing = new List<string>();
        if (spherePrefab == null) missing.Add("Sphere Prefab");
        if (cubePrefab == null) missing.Add("Cube Prefab");
        if (tubePrefab == null) missing.Add("Tube Prefab");
        if (hintPrefab == null) missing.Add("Hint Prefab");
        if (previewPrefab == null) missing.Add("Preview Prefab");

        if (missing.Count == 0) return true;

        string message = "Faltan prefabs en LightsOutController3D: " + string.Join(", ", missing);
        Debug.LogError(message, this);
        UpdateUIText(message);
        return false;
    }

    private void Build3DSceneObjects(int totalNodes)
    {
        sphereVisuals = new GameObject[totalNodes];
        cubeVisuals = new GameObject[totalNodes];
        hintMarkers = new GameObject[totalNodes];
        previewMarkers = new GameObject[totalNodes];

        // La escala que tenga cada prefab es su tamano base
        sphereBaseScale = spherePrefab.transform.localScale;
        cubeBaseScale = cubePrefab.transform.localScale;

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
            // Contenedor del nodo (con el unico collider, para detectar el mouse)
            GameObject holder = new GameObject($"Node_{i}");
            holder.transform.SetParent(transform);
            holder.transform.localPosition = positions[i];

            SphereCollider col = holder.AddComponent<SphereCollider>();
            col.radius = 0.6f;

            LightsOutNodeClick3D detector = holder.AddComponent<LightsOutNodeClick3D>();
            detector.Setup(this, i);
            spawnedObjects.Add(holder);

            sphereVisuals[i] = SpawnFromPrefab(spherePrefab, holder.transform, Vector3.zero);   // Nodo limpio
            cubeVisuals[i] = SpawnFromPrefab(cubePrefab, holder.transform, Vector3.zero);       // Nodo infectado

            hintMarkers[i] = SpawnFromPrefab(hintPrefab, holder.transform, hintOffset);         // Pista
            hintMarkers[i].SetActive(false);

            previewMarkers[i] = SpawnFromPrefab(previewPrefab, holder.transform, previewOffset); // Previsualizacion
            previewMarkers[i].SetActive(false);

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

    // Instancia un prefab como hijo del nodo, respetando su escala y sin colliders propios
    private GameObject SpawnFromPrefab(GameObject prefab, Transform parent, Vector3 localPosition)
    {
        GameObject obj = Instantiate(prefab, parent);
        obj.transform.localPosition = localPosition;
        obj.transform.localRotation = Quaternion.identity;
        DisableColliders(obj);
        return obj;
    }

    // El unico collider de un nodo es el del contenedor; los de los prefabs se quitan
    private void DisableColliders(GameObject obj)
    {
        foreach (Collider c in obj.GetComponentsInChildren<Collider>(true))
        {
            c.enabled = false;   // Deja de recibir raycasts de inmediato (Destroy se aplica al final del frame)
            Destroy(c);
        }
    }

    // Tubo entre dos puntos 3D (prefab estirado al largo de la conexion)
    private void CreateConnectionTube(Vector3 start, Vector3 end)
    {
        GameObject tube = Instantiate(tubePrefab, transform);
        DisableColliders(tube);

        Vector3 dir = end - start;
        tube.transform.localPosition = (start + end) * 0.5f;
        tube.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
        tube.transform.localScale = new Vector3(
            tubeThickness,
            dir.magnitude / Mathf.Max(0.01f, tubeModelLength),
            tubeThickness);

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

    // Pistas: nodos del plan (clics que faltan desde el estado ACTUAL) que ya fueron revelados.
    // Revelado = plan completo (Resolver / pista cara) o pista individual comprada.
    private void RefreshHints()
    {
        if (hintMarkers == null) return;
        for (int i = 0; i < hintMarkers.Length; i++)
        {
            if (hintMarkers[i] == null) continue;

            bool planned = plan != null && i < plan.Length && plan[i];
            bool revealed = solutionRevealed
                            || (revealedHints != null && i < revealedHints.Length && revealedHints[i]);

            hintMarkers[i].SetActive(showHints && planned && revealed);
        }
    }

    // Muestra u oculta los botones (y la etiqueta de pistas) segun el estado del puzzle
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

        // Etiqueta "Pistas": aparece y desaparece junto con los botones ON/OFF, sin cambiar su texto
        if (resolveText != null)
            resolveText.gameObject.SetActive(active);
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