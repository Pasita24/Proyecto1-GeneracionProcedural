using UnityEditor;
using UnityEngine;

public class PCGControlWindow : EditorWindow
{
    private GameObject generador;

    // Enum global para sincronizar los 3 scripts a la vez
    private enum GlobalContext { Valley, Mountain }
    private GlobalContext currentContext = GlobalContext.Valley;

    // Crea una pestaña nueva en el menú superior de Unity
    [MenuItem("PCG/Panel de Control General")]
    public static void ShowWindow()
    {
        GetWindow<PCGControlWindow>("Interfaz PCG");
    }

    private void OnGUI()
    {
        GUILayout.Label("Panel de Control Procedural", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Casilla para vincular el objeto maestro una sola vez
        generador = (GameObject)EditorGUILayout.ObjectField("Objeto 'GeneradorAldea'", generador, typeof(GameObject), true);

        if (generador == null)
        {
            EditorGUILayout.HelpBox("Asigna el GameObject que contiene tus scripts para continuar.", MessageType.Warning);
            return;
        }

        BSPGenerator bsp = generador.GetComponent<BSPGenerator>();
        PathGenerator paths = generador.GetComponent<PathGenerator>();
        LSystemTreeGenerator trees = generador.GetComponent<LSystemTreeGenerator>();

        if (bsp == null || paths == null || trees == null)
        {
            EditorGUILayout.HelpBox("Faltan scripts en el objeto seleccionado.", MessageType.Error);
            return;
        }

        EditorGUI.BeginChangeCheck();

        // -- 1. CONTEXTO GLOBAL --
        EditorGUILayout.Space();
        GUILayout.Label("1. Contexto Global", EditorStyles.boldLabel);
        currentContext = (GlobalContext)EditorGUILayout.EnumPopup("Contexto Actual", currentContext);

        // -- 2. PARÁMETROS BSP --
        EditorGUILayout.Space();
        GUILayout.Label("2. Loteo de la Aldea (BSP)", EditorStyles.boldLabel);
        bsp.minLeafSize = EditorGUILayout.IntSlider("Tamaño Mínimo Lote", bsp.minLeafSize, 10, 50);
        bsp.maxLeafSize = EditorGUILayout.IntSlider("Tamaño Máximo Lote", bsp.maxLeafSize, 20, 100);
        if (currentContext == GlobalContext.Mountain)
            bsp.mountainMinHeight = EditorGUILayout.Slider("Altura Mínima (Montaña)", bsp.mountainMinHeight, 0f, 50f);

        // -- 3. PARÁMETROS CAMINOS --
        EditorGUILayout.Space();
        GUILayout.Label("3. Red de Caminos", EditorStyles.boldLabel);
        paths.probabilidadDirecta = EditorGUILayout.IntSlider("Probabilidad Directa", paths.probabilidadDirecta, 0, 100);

        // -- 4. PARÁMETROS VEGETACIÓN --
        EditorGUILayout.Space();
        GUILayout.Label("4. Vegetación (L-System)", EditorStyles.boldLabel);
        trees.numberOfTrees = EditorGUILayout.IntSlider("Cantidad de Árboles", trees.numberOfTrees, 0, 100);
        trees.iterations = EditorGUILayout.IntSlider("Iteraciones (Crecimiento)", trees.iterations, 1, 5);
        trees.angle = EditorGUILayout.Slider("Ángulo de Ramas", trees.angle, 10f, 90f);

        if (currentContext == GlobalContext.Mountain)
        {
            trees.minHillHeight = EditorGUILayout.Slider("Altura Mínima Árboles", trees.minHillHeight, 0f, 50f);
            trees.maxCliffHeight = EditorGUILayout.Slider("Altura Máxima Árboles", trees.maxCliffHeight, 0f, 100f);
        }

        // -- SINCRONIZAR CAMBIOS --
        if (EditorGUI.EndChangeCheck())
        {
            // Transforma el contexto global al contexto individual de cada script de tu compañero
            bsp.context = (currentContext == GlobalContext.Valley) ? BSPGenerator.GenerationContext.Valley : BSPGenerator.GenerationContext.Mountain;
            paths.context = (currentContext == GlobalContext.Valley) ? PathGenerator.GenerationContext.Valley : PathGenerator.GenerationContext.Mountain;
            trees.context = (currentContext == GlobalContext.Valley) ? LSystemTreeGenerator.GenerationContext.Valley : LSystemTreeGenerator.GenerationContext.Mountain;

            EditorUtility.SetDirty(bsp);
            EditorUtility.SetDirty(paths);
            EditorUtility.SetDirty(trees);
        }

        // -- BOTONES MAESTROS --
        EditorGUILayout.Space(20);
        GUILayout.Label("Acciones Globales", EditorStyles.boldLabel);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Generar Todo (Casas, Rutas, Bosque)", GUILayout.Height(30)))
        {
            bsp.GenerateVillage();
            paths.GeneratePaths();
            trees.GenerateForest();
        }
        if (GUILayout.Button("Limpiar Escena", GUILayout.Height(30)))
        {
            trees.ClearForest();
            paths.ClearPaths();
            bsp.ClearVillage();
        }
        GUILayout.EndHorizontal();
    }
}