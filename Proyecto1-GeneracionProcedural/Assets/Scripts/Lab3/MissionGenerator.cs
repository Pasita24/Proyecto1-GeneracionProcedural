using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class MissionGenerator : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // CONFIGURACIÓN DE LA GENERACIÓN
    // -------------------------------------------------------------------------

    [Header("Generation")]

    [Tooltip(
        "Semilla utilizada para seleccionar las reglas. " +
        "La misma configuración y la misma seed deben producir " +
        "el mismo resultado."
    )]
    [SerializeField]
    private int seed = 12345;


    [Tooltip(
        "Cantidad de expansiones realizadas antes de finalizar la misión."
    )]
    [Range(1, 10)]
    [SerializeField]
    private int expansionSteps = 4;


    [Header("Grammar")]

    [SerializeField]
    private string startSymbol = "M";


    [SerializeField]
    private string startProduction = "STG";


    [SerializeField]
    private string taskSymbol = "T";


    [SerializeField]
    private List<string> taskProductions =
        new List<string>()
        {
            "CT",
            "ET",
            "RT",
            "KTL"
        };


    [SerializeField]
    private string terminalProduction = "C";


    // -------------------------------------------------------------------------
    // GRAMÁTICA SECUENCIAL PARA MISIONES
    // -------------------------------------------------------------------------
    //
    // En el primer ejercicio del laboratorio se implementó una gramática con
    // reescritura PARALELA.
    //
    // En este caso se utilizará una estrategia diferente.
    //
    // La generación de la misión será SECUENCIAL.
    //
    //
    // -------------------------------------------------------------------------
    // DIFERENCIA CON LA EXPANSIÓN PARALELA
    // -------------------------------------------------------------------------
    //
    // En una expansión paralela se procesan todos los símbolos de una cadena
    // antes de obtener la siguiente iteración.
    //
    // En la misión se seleccionará una tarea pendiente y se aplicará una
    // producción sobre ella.
    //
    // Por ejemplo:
    //
    //      M
    //
    // aplicando:
    //
    //      M -> STG
    //
    // se obtiene:
    //
    //      STG
    //
    // El símbolo T representa una tarea todavía pendiente de definir.
    //
    //
    // -------------------------------------------------------------------------
    // PRODUCCIONES DE TAREA
    // -------------------------------------------------------------------------
    //
    // Las producciones disponibles inicialmente son:
    //
    //      T -> CT
    //      T -> ET
    //      T -> RT
    //      T -> KTL
    //
    // Cada una introduce una acción concreta y conserva un símbolo T,
    // permitiendo que la misión continúe creciendo.
    //
    // Por ejemplo:
    //
    //      STG
    //
    // utilizando:
    //
    //      T -> KTL
    //
    // produce:
    //
    //      SKTLG
    //
    // La siguiente expansión podrá volver a actuar sobre la T restante.
    //
    //
    // -------------------------------------------------------------------------
    // SELECCIÓN DE PRODUCCIONES
    // -------------------------------------------------------------------------
    //
    // En cada expansión se seleccionará una de las producciones disponibles.
    //
    // Para que el resultado sea reproducible se utilizará System.Random junto
    // al parámetro seed.
    //
    // De esta manera:
    //
    //      misma seed + mismos parámetros
    //
    // debe producir la misma misión.
    //
    //
    // -------------------------------------------------------------------------
    // FINALIZACIÓN
    // -------------------------------------------------------------------------
    //
    // Después de ejecutar expansionSteps, la cadena todavía puede contener T.
    //
    // Para completar la misión se utilizará:
    //
    //      terminalProduction
    //
    // cuya configuración inicial corresponde a:
    //
    //      T -> C
    //
    // Al finalizar no deberían permanecer tareas pendientes representadas por T.
    //
    //
    // -------------------------------------------------------------------------
    // INTERPRETACIÓN
    // -------------------------------------------------------------------------
    //
    // Al igual que en el L-System, generar la cadena simbólica y darle
    // significado son procesos diferentes.
    //
    // Por ejemplo:
    //
    //      S C K L G
    //
    // puede interpretarse como:
    //
    //      S -> Comienza la misión.
    //      C -> Derrota a los enemigos.
    //      K -> Obtén una llave.
    //      L -> Abre una cerradura.
    //      G -> Completa el objetivo principal.
    //
    // La función GetDescription() que realiza esta traducción ya se encuentra
    // implementada.
    //
    //
    public void GenerateMission()
    {
        if (!ValidateGrammar())
        {
            return;
        }

        // Lista para almacenar el registro de la derivación paso a paso
        List<string> derivation = new List<string>();

        // Inicializar el generador aleatorio con la semilla configurada
        System.Random random = new System.Random(seed);

        // 1. Comenzar desde el símbolo inicial y aplicar la producción inicial (M -> STG)
        string currentSequence = startProduction;
        derivation.Add(currentSequence);

        // 2 & 3 & 4. Expansiones secuenciales reemplazando solo UNA tarea por paso
        for (int step = 0; step < expansionSteps; step++)
        {
            // Buscar el índice del primer símbolo de tarea pendiente ('T')
            int index = currentSequence.IndexOf(taskSymbol);

            // Si ya no quedan tareas pendientes en la cadena, interrumpimos la expansión
            if (index == -1)
            {
                break;
            }

            // Seleccionar aleatoriamente una producción de taskProductions usando la semilla
            int chosenIndex = random.Next(0, taskProductions.Count);
            string chosenProduction = taskProductions[chosenIndex];

            // Reemplazar únicamente la PRIMERA ocurrencia de 'T' encontrada
            currentSequence = currentSequence.Substring(0, index) + chosenProduction + currentSequence.Substring(index + taskSymbol.Length);

            // Registrar el paso en la derivación
            derivation.Add(currentSequence);
        }

        // 5. Finalizar todos los símbolos taskSymbol ('T') restantes con terminalProduction ('C')
        while (currentSequence.Contains(taskSymbol))
        {
            int index = currentSequence.IndexOf(taskSymbol);
            currentSequence = currentSequence.Substring(0, index) + terminalProduction + currentSequence.Substring(index + taskSymbol.Length);
            derivation.Add(currentSequence);
        }

        // 6 & 7 & 8. Formatear y mostrar en consola las reglas, derivación, cadena final e interpretación
        PrintMissionResults(derivation, currentSequence);
    }

    // -------------------------------------------------------------------------
    // IMPRESIÓN Y FORMATO DE MISIONES
    // -------------------------------------------------------------------------

    private void PrintMissionResults(List<string> derivation, string finalSequence)
    {
        StringBuilder output = new StringBuilder();

        output.AppendLine("===== MISSION GENERATOR =====");
        output.AppendLine();

        // 1. Mostrar Reglas
        output.AppendLine("GRAMMAR RULES:");
        output.AppendLine($"{startSymbol} -> {startProduction}");
        foreach (string prod in taskProductions)
        {
            output.AppendLine($"{taskSymbol} -> {prod}");
        }
        output.AppendLine($"{taskSymbol} -> {terminalProduction}");
        output.AppendLine();

        // 2. Mostrar Derivación
        output.AppendLine("DERIVATION:");
        output.AppendLine($"Axiom ({startSymbol}): {startProduction}");
        for (int i = 1; i < derivation.Count; i++)
        {
            output.AppendLine($"Step {i}: {derivation[i]}");
        }
        output.AppendLine();

        // 3. Mostrar Cadena Final
        output.AppendLine("FINAL STRING:");
        output.AppendLine(finalSequence);
        output.AppendLine();

        // 4. Mostrar Misión Interpretada
        output.AppendLine("INTERPRETED MISSION:");
        int stepCounter = 1;
        foreach (char symbol in finalSequence)
        {
            string description = GetDescription(symbol);
            if (!string.IsNullOrEmpty(description))
            {
                output.AppendLine($"{stepCounter}. [{symbol}] {description}");
                stepCounter++;
            }
        }

        Debug.Log(output.ToString(), this);
    }

    // -------------------------------------------------------------------------
    // INTERPRETACIÓN DE SÍMBOLOS
    // -------------------------------------------------------------------------
    //
    // Esta sección se entrega implementada.
    //
    // Cada símbolo terminal posee un significado dentro de la misión.
    //
    private string GetDescription(
        char symbol)
    {
        switch (symbol)
        {
            case 'S':
                return
                    "Comienza la misión.";

            case 'C':
                return
                    "Derrota a los enemigos.";

            case 'E':
                return
                    "Explora la zona.";

            case 'R':
                return
                    "Recolecta el recurso solicitado.";

            case 'K':
                return
                    "Obtén una llave.";

            case 'L':
                return
                    "Utiliza la llave para abrir una cerradura.";

            case 'G':
                return
                    "Completa el objetivo principal.";

            default:
                return null;
        }
    }


    // -------------------------------------------------------------------------
    // VALIDACIÓN
    // -------------------------------------------------------------------------
    //
    // Esta sección se entrega implementada.
    //
    // Su objetivo es detectar configuraciones básicas inválidas antes
    // de comenzar la generación.
    //
    private bool ValidateGrammar()
    {
        if (string.IsNullOrEmpty(
            startSymbol))
        {
            Debug.LogError(
                "Start Symbol no puede estar vacío.",
                this
            );

            return false;
        }


        if (string.IsNullOrEmpty(
            startProduction))
        {
            Debug.LogError(
                "Start Production no puede estar vacía.",
                this
            );

            return false;
        }


        if (string.IsNullOrEmpty(
            taskSymbol))
        {
            Debug.LogError(
                "Task Symbol no puede estar vacío.",
                this
            );

            return false;
        }


        if (taskProductions == null ||
            taskProductions.Count == 0)
        {
            Debug.LogError(
                "Debe existir al menos una producción para Task.",
                this
            );

            return false;
        }


        if (string.IsNullOrEmpty(
            terminalProduction))
        {
            Debug.LogError(
                "Terminal Production no puede estar vacía.",
                this
            );

            return false;
        }


        if (terminalProduction.Contains(
            taskSymbol))
        {
            Debug.LogError(
                "La producción terminal no debe volver a generar " +
                "el símbolo '" +
                taskSymbol +
                "'.",
                this
            );

            return false;
        }


        return true;
    }
}