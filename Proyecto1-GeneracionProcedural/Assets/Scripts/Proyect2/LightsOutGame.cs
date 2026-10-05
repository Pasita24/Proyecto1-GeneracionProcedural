using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LightsOutBoard : MonoBehaviour
{
    [Header("Configuracion del Tablero")]
    [Range(3, 50)] public int width = 5;
    [Range(3, 50)] public int height = 5;

    [Header("Generacion Procedural")]
    [Tooltip("Cantidad de clics aleatorios para generar el puzzle.")]
    [Min(1)] public int difficultySteps = 10;
    [Tooltip("Tiempo de espera entre cada clic durante la animacion.")]
    public float animationDelay = 0.1f;

    [Header("Interfaz Visual (UI)")]
    public GridLayoutGroup gridLayout;
    public GameObject cellPrefab;
    public Button generateButton;
    public Color lightOnColor = Color.yellow;
    public Color lightOffColor = Color.gray;

    private bool[] board;
    private Image[] cellImages;
    private Button[] cellButtons; // Array para guardar botones y bloquearlos
    private bool isGenerating = false;

    private void Start()
    {
        // Conecta la funcion de generar al boton de la UI
        if (generateButton != null)
        {
            generateButton.onClick.AddListener(OnGenerateButtonClicked);
        }
    }

    private void OnGenerateButtonClicked()
    {
        // Evita que se inicie otra generacion si ya hay una corriendo
        if (isGenerating) return;
        StartCoroutine(GenerateBackwardFromGoalAnimated());
    }

    private IEnumerator GenerateBackwardFromGoalAnimated()
    {
        isGenerating = true;
        board = new bool[width * height];

        SetupGridUI();

        // Apaga la interaccion para evitar bugs del jugador
        SetBoardInteractable(false);

        // Paso 1: Inicia Goal State (todas apagadas)
        for (int i = 0; i < board.Length; i++)
        {
            board[i] = false;
        }
        UpdateVisuals();

        yield return new WaitForSeconds(1.0f);

        // Paso 2: Retrocede dando clics al azar (Backward)
        System.Random random = new System.Random();
        for (int i = 0; i < difficultySteps; i++)
        {
            int randomX = random.Next(width);
            int randomY = random.Next(height);

            Toggle(randomX, randomY);
            UpdateVisuals();

            // Espera un instante para crear el efecto animado
            yield return new WaitForSeconds(animationDelay);
        }

        // Reactiva la interaccion al terminar de generar
        SetBoardInteractable(true);
        isGenerating = false;
    }

    public void OnCellClicked(int x, int y)
    {
        // Ignora el clic si el tablero se esta generando
        if (isGenerating) return;

        Toggle(x, y);
        UpdateVisuals();
    }

    public void Toggle(int x, int y)
    {
        // Detiene la funcion si el clic sale de los bordes
        if (x < 0 || x >= width || y < 0 || y >= height) return;

        // Invierte la cruz completa (centro, arriba, abajo, izq, der)
        ToggleCell(x, y);
        ToggleCell(x, y - 1);
        ToggleCell(x, y + 1);
        ToggleCell(x - 1, y);
        ToggleCell(x + 1, y);
    }

    private void ToggleCell(int x, int y)
    {
        // Verifica limites antes de invertir una celda unica
        if (x >= 0 && x < width && y >= 0 && y < height)
        {
            int index = y * width + x;
            board[index] = !board[index];
        }
    }

    private void SetupGridUI()
    {
        // Elimina los botones generados anteriormente
        foreach (Transform child in gridLayout.transform)
        {
            Destroy(child.gameObject);
        }

        // Fija la cantidad de columnas en el componente de Unity
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = width;

        // Calcula dinamicamente el tamano maximo para que entren las celdas
        RectTransform gridRect = gridLayout.GetComponent<RectTransform>();
        float availableWidth = gridRect.rect.width - (gridLayout.padding.left + gridLayout.padding.right) - (gridLayout.spacing.x * (width - 1));
        float availableHeight = gridRect.rect.height - (gridLayout.padding.top + gridLayout.padding.bottom) - (gridLayout.spacing.y * (height - 1));

        float cellSizeX = availableWidth / width;
        float cellSizeY = availableHeight / height;
        float finalCellSize = Mathf.Min(cellSizeX, cellSizeY);

        gridLayout.cellSize = new Vector2(finalCellSize, finalCellSize);

        cellImages = new Image[board.Length];
        cellButtons = new Button[board.Length];

        // Crea los botones nuevos y asigna las coordenadas
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                GameObject newCell = Instantiate(cellPrefab, gridLayout.transform);

                cellImages[index] = newCell.GetComponent<Image>();

                Button btn = newCell.GetComponent<Button>();
                if (btn != null)
                {
                    cellButtons[index] = btn;
                    int captureX = x; // Variables locales para el lambda
                    int captureY = y;
                    btn.onClick.AddListener(() => OnCellClicked(captureX, captureY));
                }
            }
        }
    }

    private void UpdateVisuals()
    {
        // Actualiza el color de cada boton leyendo el arreglo booleano
        if (cellImages == null || cellImages.Length != board.Length) return;

        for (int i = 0; i < board.Length; i++)
        {
            cellImages[i].color = board[i] ? lightOnColor : lightOffColor;
        }
    }

    private void SetBoardInteractable(bool state)
    {
        // Enciende o apaga la propiedad interactable de todos los botones
        if (cellButtons == null) return;

        foreach (Button btn in cellButtons)
        {
            if (btn != null)
            {
                btn.interactable = state;
            }
        }
    }
}