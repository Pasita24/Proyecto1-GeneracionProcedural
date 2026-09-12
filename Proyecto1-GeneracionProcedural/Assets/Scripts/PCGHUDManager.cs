using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PCGHUDManager : MonoBehaviour
{
    [Header("Referencias a Scripts")]
    public TerrainGenerator terrainGen;
    public BSPGenerator bsp;
    public PathGenerator paths;
    public LSystemTreeGenerator trees;

    [Header("Elementos de la UI")]
    public TMP_Dropdown contextDropdown;
    public TMP_InputField seedInput;
    public Slider minLeafSlider;
    public Slider maxLeafSlider;
    public Slider pathProbSlider;
    public Slider treeCountSlider;
    public Button btnGenerate;
    public Button btnClear;

    void Start()
    {
        btnGenerate.onClick.AddListener(GenerateAll);
        btnClear.onClick.AddListener(ClearAll);
        contextDropdown.onValueChanged.AddListener(OnContextChanged);

        minLeafSlider.onValueChanged.AddListener(v => bsp.minLeafSize = (int)v);
        maxLeafSlider.onValueChanged.AddListener(v => bsp.maxLeafSize = (int)v);
        pathProbSlider.onValueChanged.AddListener(v => paths.probabilidadDirecta = (int)v);
        treeCountSlider.onValueChanged.AddListener(v => trees.numberOfTrees = (int)v);

        // Auto-completar la semilla inicial en el InputField
        if (terrainGen != null)
        {
            seedInput.text = terrainGen.seed.ToString();
        }

        // Actualizar el terreno en tiempo real al tipear
        seedInput.onValueChanged.AddListener(OnSeedChanged);
    }

    void OnSeedChanged(string newText)
    {
        if (terrainGen != null && int.TryParse(newText, out int newSeed))
        {
            terrainGen.seed = newSeed;
            terrainGen.GenerateTerrain();
        }
    }

    void OnContextChanged(int index)
    {
        bool isMountain = (index == 1);

        bsp.context = isMountain ? BSPGenerator.GenerationContext.Mountain : BSPGenerator.GenerationContext.Valley;
        paths.context = isMountain ? PathGenerator.GenerationContext.Mountain : PathGenerator.GenerationContext.Valley;
        trees.context = isMountain ? LSystemTreeGenerator.GenerationContext.Mountain : LSystemTreeGenerator.GenerationContext.Valley;

        if (terrainGen != null)
        {
            if (isMountain)
            {
                // Configuración Mountain
                terrainGen.generationMethod = TerrainGenerator.GenerationMethod.ValueNoise;
                terrainGen.lowThreshold = 0.327f;
                terrainGen.highThreshold = 0.533f;

                terrainGen.lowColor = new Color(0.2f, 0.6f, 0.2f);       // Verde
                terrainGen.middleColor = Color.gray;                     // Gris
                terrainGen.highColor = Color.white;                      // Blanco
            }
            else
            {
                // Configuración Valley
                terrainGen.generationMethod = TerrainGenerator.GenerationMethod.PerlinNoise;
                terrainGen.lowThreshold = 0.467f;
                terrainGen.highThreshold = 0.575f;

                terrainGen.lowColor = new Color(0.5f, 0.8f, 0.3f);       // Verde claro
                terrainGen.middleColor = new Color(0.15f, 0.4f, 0.15f);  // Verde oscuro
                terrainGen.highColor = new Color(0.45f, 0.3f, 0.15f);    // Café
            }
        }

        // Forzar la regeneración completa de la escena con los nuevos parámetros
        GenerateAll();
    }

    void GenerateAll()
    {
        ClearAll();
        if (terrainGen != null) terrainGen.GenerateTerrain();
        bsp.GenerateVillage();
        paths.GeneratePaths();
        trees.GenerateForest();
    }

    void ClearAll()
    {
        trees.ClearForest();
        paths.ClearPaths();
        bsp.ClearVillage();
    }
}