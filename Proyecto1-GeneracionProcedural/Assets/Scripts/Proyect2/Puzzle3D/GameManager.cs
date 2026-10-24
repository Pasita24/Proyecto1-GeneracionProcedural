using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

public enum GameState { Menu, Playing, Developer }

/// <summary>
/// Vista de una carta de dificultad en el menu (se asignan los objetos de UI en el inspector).
/// </summary>
[System.Serializable]
public class DifficultyCardView
{
    public Button button;
    public TMP_Text titleText;
    public TMP_Text infoText;
    [Tooltip("Objeto que se muestra encima cuando la carta esta bloqueada (candado, oscurecido, etc.)")]
    public GameObject lockedOverlay;
}

/// <summary>
/// Controla el flujo del juego: menu, partida (dificultad + temporizador + monedas + pistas),
/// modo desarrollador (el sistema de sliders y botones que ya existia) y la musica de fondo.
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("Referencias de juego")]
    public LightsOutController3D controller;
    public BackwardFromGoalState3D generator;

    [Header("Dificultades (orden: Facil, Medio, Dificil)")]
    public DifficultyConfig[] difficulties =
    {
        new DifficultyConfig
        {
            displayName = "Facil", description = "Aprende la regla. Puzzle plano.",
            nodeRange = new Vector2Int(6, 8), stepsRange = new Vector2Int(2, 3), depth = 0f,
            baseTime = 45f, secondsPerStep = 15f,
            baseReward = 10, efficiencyBonus = 5, timeBonus = 5,
            cheapHintCost = 5, fullHintCost = 30, requiredSolvedPrevious = 0
        },
        new DifficultyConfig
        {
            displayName = "Medio", description = "Entra la tercera dimension.",
            nodeRange = new Vector2Int(10, 14), stepsRange = new Vector2Int(4, 6), depth = 0.3f,
            baseTime = 30f, secondsPerStep = 10f,
            baseReward = 20, efficiencyBonus = 10, timeBonus = 10,
            cheapHintCost = 8, fullHintCost = 60, requiredSolvedPrevious = 3
        },
        new DifficultyConfig
        {
            displayName = "Dificil", description = "Red grande y profunda.",
            nodeRange = new Vector2Int(18, 26), stepsRange = new Vector2Int(7, 10), depth = 0.5f,
            baseTime = 20f, secondsPerStep = 8f,
            baseReward = 35, efficiencyBonus = 15, timeBonus = 15,
            cheapHintCost = 15, fullHintCost = 100, requiredSolvedPrevious = 3
        }
    };
    [Tooltip("Monedas que recibe el jugador la primera vez que juega")]
    public int startingCoins = 10;
    [Tooltip("Para probar: desbloquea todas las dificultades y las pistas")]
    public bool unlockAllForTesting = false;
    [Tooltip("Para probar: al pulsar Play se borran monedas y progreso (todo vuelve a empezar). Apagar en la version final")]
    public bool resetProgressOnStart = false;

    [Header("Musica")]
    [Tooltip("Cancion en bucle del menu principal")]
    public AudioClip menuMusic;
    [Tooltip("Cancion en bucle de los niveles (tambien suena en el modo desarrollador)")]
    public AudioClip levelMusic;
    [Range(0f, 1f)] public float musicVolume = 0.6f;
    [Tooltip("Segundos de fundido al cambiar de cancion (0 = corte directo)")]
    [Range(0f, 2f)] public float musicFadeTime = 0.6f;
    [Tooltip("Activo: la cancion del nivel reinicia con cada puzzle nuevo. Inactivo: sigue sonando entre puzzles")]
    public bool restartMusicOnEachPuzzle = false;

    [Header("Menu principal")]
    public GameObject menuRoot;
    [Tooltip("Una carta por dificultad, en el mismo orden que el arreglo de arriba")]
    public DifficultyCardView[] cards = new DifficultyCardView[3];
    public Button developerButton;

    [Header("HUD de partida")]
    public GameObject hudRoot;
    public TMP_Text coinsText;
    public TMP_Text timerText;
    public TMP_Text parText;
    public Button cheapHintButton;
    public Button fullHintButton;
    public Button hudMenuButton;
    public Color timerNormalColor = Color.white;
    public Color timerOverColor = new Color(0.95f, 0.3f, 0.3f);

    [Header("Resultado")]
    public GameObject resultRoot;
    public TMP_Text resultText;
    public Button nextButton;
    public Button resultMenuButton;

    [Header("Modo desarrollador")]
    [Tooltip("Objeto padre con TODA la UI de desarrollador: icono, panel de sliders, textos y boton de volver")]
    public GameObject developerRoot;
    public Button developerBackButton;

    // Estado
    private GameState state = GameState.Menu;
    private int currentDifficulty;
    private float elapsed;
    private float targetTime;
    private bool timerRunning;
    private bool puzzleActive;

    // Parametros del generador guardados para el modo desarrollador
    private int devNodeCount;
    private int devDifficultySteps;
    private float devDepth;
    private bool devAvoidRepeated;
    private bool devFixedSeed;

    // Musica
    private AudioSource musicSource;
    private Coroutine musicRoutine;
    private AudioClip targetClip;      // Cancion que suena o esta por sonar

    private void Awake()
    {
        // Reproductor de musica: se usa el AudioSource de este objeto (o se crea uno)
        musicSource = GetComponent<AudioSource>();
        if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();

        musicSource.loop = true;            // Bucle
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;      // Sonido 2D
        musicSource.volume = 0f;
    }

    private void Start()
    {
        // Reinicio automatico del progreso para ir probando
        if (resetProgressOnStart) PlayerProgress.ResetAll();

        if (!PlayerProgress.IsInitialized) PlayerProgress.Initialize(startingCoins);

        // Botones de las cartas
        for (int i = 0; i < cards.Length && i < difficulties.Length; i++)
        {
            int index = i;   // copia local para el lambda
            if (cards[i] != null) Bind(cards[i].button, () => StartDifficulty(index));
        }

        Bind(developerButton, StartDeveloper);
        Bind(developerBackButton, ShowMenu);
        Bind(hudMenuButton, ShowMenu);
        Bind(resultMenuButton, ShowMenu);
        Bind(nextButton, NextPuzzle);
        Bind(cheapHintButton, BuyCheapHint);
        Bind(fullHintButton, BuyFullHint);

        if (controller != null)
        {
            controller.OnPuzzleReady += HandlePuzzleReady;
            controller.OnPuzzleSolved += HandlePuzzleSolved;
        }

        StashDeveloperParams();   // Valores de los sliders al iniciar
        ShowMenu();
    }

    private void OnDestroy()
    {
        if (controller != null)
        {
            controller.OnPuzzleReady -= HandlePuzzleReady;
            controller.OnPuzzleSolved -= HandlePuzzleSolved;
        }
    }

    private void Update()
    {
        if (!timerRunning) return;

        elapsed += Time.deltaTime;
        float left = targetTime - elapsed;

        if (timerText != null)
        {
            if (left >= 0f)
            {
                timerText.text = FormatTime(left);
                timerText.color = timerNormalColor;
            }
            else
            {
                timerText.text = "+" + FormatTime(-left);   // Tiempo extra: se pierde el bonus
                timerText.color = timerOverColor;
            }
        }
    }

    // ---------- Navegacion ----------

    public void ShowMenu()
    {
        if (state == GameState.Developer) StashDeveloperParams();

        if (controller != null) controller.ClearPuzzle();

        state = GameState.Menu;
        timerRunning = false;
        puzzleActive = false;

        SetActive(menuRoot, true);
        SetActive(hudRoot, false);
        SetActive(resultRoot, false);
        SetActive(developerRoot, false);

        RefreshCards();
        PlayMusic(menuMusic);          // Musica del menu
    }

    public void StartDifficulty(int index)
    {
        if (index < 0 || index >= difficulties.Length || !IsUnlocked(index)) return;

        currentDifficulty = index;
        state = GameState.Playing;

        SetActive(menuRoot, false);
        SetActive(resultRoot, false);
        SetActive(developerRoot, false);
        SetActive(hudRoot, true);

        PlayMusic(levelMusic);         // Musica de los niveles
        BeginPuzzle();
    }

    public void StartDeveloper()
    {
        state = GameState.Developer;
        timerRunning = false;
        puzzleActive = false;

        SetActive(menuRoot, false);
        SetActive(hudRoot, false);
        SetActive(resultRoot, false);
        SetActive(developerRoot, true);

        PlayMusic(levelMusic);
        RestoreDeveloperParams();
        controller.GenerateNewPuzzle();
    }

    // Boton "Siguiente" del resultado: otro puzzle de la misma dificultad
    public void NextPuzzle()
    {
        if (state != GameState.Playing) return;

        SetActive(resultRoot, false);
        SetActive(hudRoot, true);       // El HUD vuelve al empezar el siguiente puzzle

        // Por defecto la cancion sigue sonando; con la opcion activada reinicia en cada puzzle
        if (restartMusicOnEachPuzzle) PlayMusic(levelMusic, true);

        BeginPuzzle();
    }

    // ---------- Partida ----------

    private void BeginPuzzle()
    {
        DifficultyConfig cfg = difficulties[currentDifficulty];

        // El puzzle se genera con los parametros de la dificultad
        generator.nodeCount = Random.Range(cfg.nodeRange.x, cfg.nodeRange.y + 1);
        generator.difficultySteps = Random.Range(cfg.stepsRange.x, cfg.stepsRange.y + 1);
        generator.depthExpansion = cfg.depth;
        generator.avoidRepeatedClicks = true;
        generator.useFixedSeed = false;

        timerRunning = false;
        puzzleActive = false;
        elapsed = 0f;

        SetText(timerText, "--:--");
        SetText(parText, "Calculando...");
        if (timerText != null) timerText.color = timerNormalColor;
        RefreshHud();

        controller.GenerateNewPuzzle();
    }

    // El controller avisa cuando el puzzle esta listo y ya se calculo el optimo
    private void HandlePuzzleReady()
    {
        if (state != GameState.Playing) return;   // El modo desarrollador no usa temporizador

        DifficultyConfig cfg = difficulties[currentDifficulty];
        int par = Mathf.Max(1, controller.OptimalSteps);

        targetTime = cfg.baseTime + par * cfg.secondsPerStep;
        elapsed = 0f;
        timerRunning = true;
        puzzleActive = true;

        SetText(parText, $"Optimo: {par} pasos");
        RefreshHud();
    }

    // El controller avisa cuando el jugador deja el sistema limpio
    private void HandlePuzzleSolved()
    {
        if (state != GameState.Playing || !puzzleActive) return;

        puzzleActive = false;
        timerRunning = false;
        controller.InputLocked = true;   // Evita seguir clicando el puzzle ya resuelto

        DifficultyConfig cfg = difficulties[currentDifficulty];
        int par = Mathf.Max(1, controller.OptimalSteps);
        int clicks = controller.ClicksUsed;

        // Recompensa
        int effBonus = 0;
        if (clicks <= par) effBonus = cfg.efficiencyBonus;
        else if (clicks <= par + 2) effBonus = cfg.efficiencyBonus / 2;

        int timeBonusEarned = elapsed <= targetTime ? cfg.timeBonus : 0;
        int reward = cfg.baseReward + effBonus + timeBonusEarned;

        // Estado de desbloqueos antes y despues de sumar este puzzle
        bool nextWasUnlocked = currentDifficulty + 1 < difficulties.Length && IsUnlocked(currentDifficulty + 1);
        bool hintsWereUnlocked = HintsUnlocked;

        PlayerProgress.AddCoins(reward);
        PlayerProgress.AddSolved(currentDifficulty);

        string unlockLine = "";
        if (currentDifficulty + 1 < difficulties.Length && !nextWasUnlocked && IsUnlocked(currentDifficulty + 1))
            unlockLine += $"\nDesbloqueaste: {difficulties[currentDifficulty + 1].displayName}";
        if (!hintsWereUnlocked && HintsUnlocked)
            unlockLine += "\nDesbloqueaste las pistas";

        SetText(resultText,
            "SISTEMA DESINFECTADO\n" +
            $"Clics: {clicks} (optimo {par})\n" +
            $"Tiempo: {FormatTime(elapsed)} (objetivo {FormatTime(targetTime)})\n" +
            $"+{reward} monedas (base {cfg.baseReward} | eficiencia {effBonus} | tiempo {timeBonusEarned})\n" +
            $"Monedas totales: {PlayerProgress.Coins}" +   // El HUD queda oculto, asi que se muestra aqui
            unlockLine);

        RefreshHud();
        SetActive(resultRoot, true);
        SetActive(hudRoot, false);       // Al aparecer el resultado se oculta el HUD
    }

    // ---------- Pistas ----------

    // Pista barata: revela UN nodo del plan
    public void BuyCheapHint()
    {
        if (!CanUseHints()) return;

        int cost = difficulties[currentDifficulty].cheapHintCost;
        if (PlayerProgress.Coins < cost) return;
        if (controller.HasFullHints) return;   // Ya se ve todo el plan

        // Solo se cobra si realmente se mostro una pista
        if (controller.RevealOneHint())
        {
            PlayerProgress.TrySpend(cost);
            RefreshHud();
        }
    }

    // Pista cara: revela todos los nodos pendientes
    public void BuyFullHint()
    {
        if (!CanUseHints()) return;

        int cost = difficulties[currentDifficulty].fullHintCost;
        if (PlayerProgress.Coins < cost) return;

        if (controller.RevealFullPlan())
        {
            PlayerProgress.TrySpend(cost);
            RefreshHud();
        }
    }

    private bool CanUseHints()
    {
        return state == GameState.Playing && puzzleActive && HintsUnlocked && !controller.IsBusy;
    }

    // Las pistas se desbloquean junto con la dificultad Medio
    private bool HintsUnlocked => difficulties.Length > 1 && IsUnlocked(1);

    // ---------- Desbloqueo ----------

    private bool IsUnlocked(int index)
    {
        if (unlockAllForTesting || index == 0) return true;
        return PlayerProgress.GetSolved(index - 1) >= difficulties[index].requiredSolvedPrevious;
    }

    // ---------- Musica ----------

    // Cambia a la cancion indicada con fundido. Si ya esta sonando esa misma, no la reinicia
    // (salvo que forceRestart sea true).
    private void PlayMusic(AudioClip clip, bool forceRestart = false)
    {
        if (musicSource == null) return;
        if (!forceRestart && clip == targetClip) return;

        targetClip = clip;
        if (musicRoutine != null) StopCoroutine(musicRoutine);
        musicRoutine = StartCoroutine(SwitchMusicRoutine(clip));
    }

    private IEnumerator SwitchMusicRoutine(AudioClip clip)
    {
        // 1. Bajar el volumen de la cancion actual
        if (musicSource.isPlaying && musicFadeTime > 0f)
        {
            float startVolume = musicSource.volume;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / musicFadeTime)
            {
                musicSource.volume = Mathf.Lerp(startVolume, 0f, t);
                yield return null;
            }
        }

        // 2. Cambiar de cancion
        musicSource.Stop();
        musicSource.clip = clip;

        if (clip == null)
        {
            musicRoutine = null;
            yield break;
        }

        musicSource.volume = 0f;
        musicSource.Play();

        // 3. Subir el volumen de la nueva
        if (musicFadeTime > 0f)
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / musicFadeTime)
            {
                musicSource.volume = Mathf.Lerp(0f, musicVolume, t);
                yield return null;
            }
        }

        musicSource.volume = musicVolume;
        musicRoutine = null;
    }

    // Para conectar a un Slider de volumen (Dynamic float)
    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        if (musicRoutine == null && musicSource != null && musicSource.isPlaying)
            musicSource.volume = musicVolume;
    }

    // ---------- Interfaz ----------

    private void RefreshCards()
    {
        for (int i = 0; i < cards.Length && i < difficulties.Length; i++)
        {
            DifficultyCardView card = cards[i];
            if (card == null) continue;

            DifficultyConfig cfg = difficulties[i];
            bool unlocked = IsUnlocked(i);

            if (card.button != null) card.button.interactable = unlocked;
            SetActive(card.lockedOverlay, !unlocked);
            SetText(card.titleText, cfg.displayName);

            if (unlocked)
            {
                SetText(card.infoText,
                    $"{cfg.description}\nResueltos: {PlayerProgress.GetSolved(i)}\nHasta {cfg.MaxReward} monedas");
            }
            else
            {
                int have = PlayerProgress.GetSolved(i - 1);
                SetText(card.infoText,
                    $"Bloqueado\nResuelve {cfg.requiredSolvedPrevious} en {difficulties[i - 1].displayName} ({have}/{cfg.requiredSolvedPrevious})");
            }
        }
    }

    private void RefreshHud()
    {
        SetText(coinsText, $"Monedas: {PlayerProgress.Coins}");

        bool playing = state == GameState.Playing;
        bool hintsVisible = playing && HintsUnlocked;
        DifficultyConfig cfg = playing ? difficulties[currentDifficulty] : null;

        if (cheapHintButton != null)
        {
            cheapHintButton.gameObject.SetActive(hintsVisible);
            if (hintsVisible)
            {
                cheapHintButton.interactable = puzzleActive
                    && PlayerProgress.Coins >= cfg.cheapHintCost
                    && !controller.HasFullHints;
                SetButtonLabel(cheapHintButton, $"Pista (-{cfg.cheapHintCost})");
            }
        }

        if (fullHintButton != null)
        {
            fullHintButton.gameObject.SetActive(hintsVisible);
            if (hintsVisible)
            {
                fullHintButton.interactable = puzzleActive
                    && PlayerProgress.Coins >= cfg.fullHintCost
                    && !controller.HasFullHints;
                SetButtonLabel(fullHintButton, $"Plan completo (-{cfg.fullHintCost})");
            }
        }
    }

    // ---------- Parametros del modo desarrollador ----------

    private void StashDeveloperParams()
    {
        if (generator == null) return;
        devNodeCount = generator.nodeCount;
        devDifficultySteps = generator.difficultySteps;
        devDepth = generator.depthExpansion;
        devAvoidRepeated = generator.avoidRepeatedClicks;
        devFixedSeed = generator.useFixedSeed;
    }

    private void RestoreDeveloperParams()
    {
        if (generator == null) return;
        generator.nodeCount = devNodeCount;
        generator.difficultySteps = devDifficultySteps;
        generator.depthExpansion = devDepth;
        generator.avoidRepeatedClicks = devAvoidRepeated;
        generator.useFixedSeed = devFixedSeed;
    }

    // ---------- Utilidades de prueba ----------

    // Vuelve todo al inicio: dificultades bloqueadas, 0 puzzles resueltos y monedas iniciales.
    // Se puede usar con clic derecho sobre el componente en el inspector, o conectar a un boton.
    [ContextMenu("Reiniciar progreso")]
    public void ResetProgress()
    {
        PlayerProgress.ResetAll();
        PlayerProgress.Initialize(startingCoins);
        RefreshCards();
        RefreshHud();
    }

    [ContextMenu("Dar 100 monedas")]
    public void GiveTestCoins()
    {
        PlayerProgress.AddCoins(100);
        RefreshHud();
    }

    // ---------- Helpers ----------

    private static string FormatTime(float seconds)
    {
        int s = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        return $"{s / 60:00}:{s % 60:00}";
    }

    private static void Bind(Button button, UnityAction action)
    {
        if (button != null) button.onClick.AddListener(action);
    }

    private static void SetActive(GameObject obj, bool value)
    {
        if (obj != null) obj.SetActive(value);
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null) text.text = value;
    }

    private static void SetButtonLabel(Button button, string value)
    {
        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = value;
    }
}