using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[Serializable]
public class LSystemRule
{
    [Tooltip("Símbolo que será reemplazado durante la expansión.")]
    public string predecessor = "A";

    [Tooltip("Cadena que reemplazará al símbolo.")]
    public string successor = "AB";
}


public class ParallelGrammarGenerator : MonoBehaviour
{
    [Header("Parallel Grammar")]

    [SerializeField]
    private bool autoUpdate = true;


    [Tooltip("Cadena inicial de la gramática.")]
    [SerializeField]
    private string axiom = "A";


    [Tooltip(
        "Reglas de producción utilizadas durante la expansión.\n\n" +
        "Configuración base:\n" +
        "A -> AB\n" +
        "B -> A"
    )]
    [SerializeField]
    private List<LSystemRule> rules =
        new List<LSystemRule>()
        {
            new LSystemRule()
            {
                predecessor = "A",
                successor = "AB"
            },

            new LSystemRule()
            {
                predecessor = "B",
                successor = "A"
            }
        };


    [Tooltip("Cantidad de iteraciones de expansión.")]
    [Range(0, 10)]
    [SerializeField]
    private int iterations = 4;


    [SerializeField]
    private bool showDerivation = true;


    // -------------------------------------------------------------------------
    // GENERACIÓN DE LA EXPANSIÓN
    // -------------------------------------------------------------------------
    //
    // Una gramática define reglas que permiten reemplazar símbolos por nuevas
    // cadenas.
    //
    // Para la configuración inicial del laboratorio:
    //
    //      Axiom: A
    //
    //      A -> AB
    //      B -> A
    //
    // la expansión esperada es:
    //
    //      Iteración 0: A
    //      Iteración 1: AB
    //      Iteración 2: ABA
    //      Iteración 3: ABAAB
    //      Iteración 4: ABAABABA
    //
    //
    // -------------------------------------------------------------------------
    // REESCRITURA PARALELA
    // -------------------------------------------------------------------------
    //
    // La característica importante de esta primera implementación es que las
    // reglas se aplican de manera PARALELA.
    //
    // Esto significa que todos los símbolos de una iteración son evaluados
    // utilizando la misma cadena de origen.
    //
    // Por ejemplo:
    //
    //      cadena actual:
    //
    //          ABA
    //
    //      reglas:
    //
    //          A -> AB
    //          B -> A
    //
    // Durante esa iteración se evalúa:
    //
    //          A      B      A
    //          |      |      |
    //          AB     A      AB
    //
    // y solamente después de procesar toda la cadena se obtiene:
    //
    //          ABAAB
    //
    // No se debe utilizar el resultado parcial de una sustitución para decidir
    // las sustituciones restantes de la misma iteración.
    //
    //
    // -------------------------------------------------------------------------
    // SÍMBOLOS SIN REGLA
    // -------------------------------------------------------------------------
    //
    // No todos los símbolos necesitan poseer una regla de producción.
    //
    // Si un símbolo no posee una regla asociada, debe conservarse sin cambios.
    //
    // Esto será especialmente importante posteriormente en los L-Systems,
    // donde símbolos como:
    //
    //      +  -  [  ]  &  ^  \  /
    //
    // pueden formar parte de la cadena sin ser necesariamente reemplazados.
    //
    //
    // -------------------------------------------------------------------------
    // DERIVACIÓN
    // -------------------------------------------------------------------------
    //
    // Además de obtener la cadena final, se almacenará el resultado de cada
    // iteración.
    //
    // Esto permite observar en la Console cómo evoluciona la gramática y
    // verificar que las reglas están siendo aplicadas correctamente.
    //

    public static string Generate(
        string axiom,
        List<LSystemRule> rules,
        int iterations,
        List<string> derivation = null)
    {
        // 1. Comenzar con la cadena inicial (axioma)
        string currentSequence = axiom;

        // Registrar la iteración 0 (estado inicial) si se solicita el historial de derivación
        if (derivation != null)
        {
            derivation.Add(currentSequence);
        }

        // Mapear las reglas en un diccionario para búsquedas eficientes O(1)
        Dictionary<string, string> ruleMap = new Dictionary<string, string>();
        if (rules != null)
        {
            foreach (LSystemRule rule in rules)
            {
                if (rule != null && !string.IsNullOrEmpty(rule.predecessor))
                {
                    // Evita errores si hay predecesores duplicados
                    if (!ruleMap.ContainsKey(rule.predecessor))
                    {
                        ruleMap.Add(rule.predecessor, rule.successor ?? "");
                    }
                }
            }
        }

        // 2. Aplicar la reescritura para cada iteración
        for (int i = 0; i < iterations; i++)
        {
            // Usar StringBuilder para construir la nueva cadena de forma eficiente
            StringBuilder nextSequence = new StringBuilder();

            // 3. Evaluar cada símbolo de forma paralela (sobre la cadena de origen actual)
            foreach (char c in currentSequence)
            {
                string symbolStr = c.ToString();

                // 4. Si existe una regla asociada, se reemplaza por su sucesor
                if (ruleMap.ContainsKey(symbolStr))
                {
                    nextSequence.Append(ruleMap[symbolStr]);
                }
                else
                {
                    // 5. Si no posee regla, se conserva el símbolo original (+, -, [, ], &, ^, etc.)
                    nextSequence.Append(c);
                }
            }

            // Actualizar la cadena para la siguiente iteración
            currentSequence = nextSequence.ToString();

            // 6. Registrar el resultado en la lista de derivación
            if (derivation != null)
            {
                derivation.Add(currentSequence);
            }
        }

        // 7. Retornar la cadena completamente expandida
        return currentSequence;
    }

    // -------------------------------------------------------------------------
    // EJECUCIÓN DESDE EL INSPECTOR
    // -------------------------------------------------------------------------
    //
    // Esta sección se entrega implementada.
    //
    // Su función es utilizar los parámetros configurados en el Inspector,
    // ejecutar Generate() y mostrar posteriormente la derivación.
    //

    public void GenerateExpansion()
    {
        List<string> derivation =
            new List<string>();


        string finalSequence =
            Generate(
                axiom,
                rules,
                iterations,
                derivation
            );


        if (showDerivation)
        {
            PrintDerivation(
                derivation,
                finalSequence
            );
        }
    }


    // -------------------------------------------------------------------------
    // VISUALIZACIÓN DE LA DERIVACIÓN
    // -------------------------------------------------------------------------
    //
    // Esta sección se entrega implementada.
    //

    private void PrintDerivation(
        List<string> derivation,
        string finalSequence)
    {
        StringBuilder output =
            new StringBuilder();


        output.AppendLine(
            "===== PARALLEL GRAMMAR ====="
        );


        output.AppendLine();


        output.AppendLine(
            "AXIOM:"
        );


        output.AppendLine(
            axiom
        );


        output.AppendLine();


        output.AppendLine(
            "RULES:"
        );


        if (rules == null ||
            rules.Count == 0)
        {
            output.AppendLine(
                "(sin reglas)"
            );
        }
        else
        {
            foreach (LSystemRule rule in rules)
            {
                if (rule == null)
                {
                    continue;
                }


                output.AppendLine(
                    rule.predecessor +
                    " -> " +
                    rule.successor
                );
            }
        }


        output.AppendLine();


        output.AppendLine(
            "DERIVATION:"
        );


        for (int i = 0;
             i < derivation.Count;
             i++)
        {
            output.AppendLine(
                "Iteration " +
                i +
                ": " +
                derivation[i]
            );
        }


        output.AppendLine();


        output.AppendLine(
            "FINAL STRING:"
        );


        output.AppendLine(
            finalSequence
        );


        Debug.Log(
            output.ToString(),
            this
        );
    }
}