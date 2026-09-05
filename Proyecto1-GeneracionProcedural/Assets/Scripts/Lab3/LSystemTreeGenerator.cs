using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class LSystemTreeGenerator : MonoBehaviour
{
    public enum GenerationMode
    {
        TwoD,
        ThreeD
    }
    //

    // -------------------------------------------------------------------------
    // CONFIGURACIÓN
    // -------------------------------------------------------------------------

    [Header("Generation")]

    [SerializeField]
    private GenerationMode generationMode =
        GenerationMode.TwoD;

    [SerializeField]
    private bool autoUpdate = true;


    [Header("2D Configuration")]

    [SerializeField]
    private string axiom2D = "F";

    [SerializeField]
    private List<LSystemRule> rules2D =
        new List<LSystemRule>()
        {
            new LSystemRule()
            {
                predecessor = "F",
                successor = "F[+F]F[-F]F"
            }
        };

    [Range(0, 5)]
    [SerializeField]
    private int iterations2D = 3;

    [Range(1f, 90f)]
    [SerializeField]
    private float angle2D = 25f;


    [Header("3D Configuration")]

    [SerializeField]
    private string axiom3D = "F";

    [SerializeField]
    private List<LSystemRule> rules3D =
        new List<LSystemRule>()
        {
            new LSystemRule()
            {
                predecessor = "F",
                successor = "F[+F][-F][&F][^F]"
            }
        };

    [Range(0, 5)]
    [SerializeField]
    private int iterations3D = 3;

    [Range(1f, 90f)]
    [SerializeField]
    private float angle3D = 30f;


    [Header("Shared Visualization")]

    [Min(0.05f)]
    [SerializeField]
    private float segmentLength = 1f;

    [Min(0.005f)]
    [SerializeField]
    private float branchRadius = 0.06f;

    [SerializeField]
    private Material branchMaterial;


    [Header("Height Color")]

    [SerializeField]
    private bool useHeightColor = true;

    [SerializeField]
    private Color lowColor =
        new Color(
            0.35f,
            0.17f,
            0.06f,
            1f
        );

    [SerializeField]
    private Color highColor =
        new Color(
            0.15f,
            0.55f,
            0.18f,
            1f
        );

    [Min(0.1f)]
    [SerializeField]
    private float colorHeight = 8f;


    [Header("Debug")]

    [SerializeField]
    private bool showDerivation = true;


    [HideInInspector]
    [SerializeField]
    private Transform generatedRoot;

    private int branchCounter = 0;


    // -------------------------------------------------------------------------
    // ESTADO DE LA TORTUGA
    // -------------------------------------------------------------------------
    //
    // Turtle Graphics interpreta una cadena como una secuencia de instrucciones.
    //
    // Para poder recorrer dicha cadena es necesario mantener el estado actual
    // de la "tortuga".
    //
    // En este laboratorio el estado está compuesto por:
    //
    //      position
    //
    //          posición actual desde la cual continuará el crecimiento.
    //
    //      rotation
    //
    //          orientación actual que determina la dirección del siguiente
    //          movimiento.
    //
    // Ambos valores son importantes al trabajar con ramificaciones.
    //
    private struct TurtleState
    {
        public Vector3 position;
        public Quaternion rotation;

        public TurtleState(
            Vector3 position,
            Quaternion rotation)
        {
            this.position = position;
            this.rotation = rotation;
        }
    }


    // -------------------------------------------------------------------------
    // GENERACIÓN DEL L-SYSTEM
    // -------------------------------------------------------------------------
    //
    // Esta sección se entrega implementada.
    //
    // El proceso posee dos etapas diferentes:
    //
    //      1. GENERACIÓN SIMBÓLICA
    //
    //          ParallelGrammarGenerator.Generate(...)
    //
    //          produce una cadena mediante las reglas implementadas en
    //          el ejercicio anterior.
    //
    //
    //      2. INTERPRETACIÓN
    //
    //          Interpret(...)
    //
    //          transforma los símbolos de esa cadena en movimientos y
    //          rotaciones.
    //
    //
    // Esta separación es importante:
    //
    // La gramática NO genera directamente la geometría.
    //
    // Primero genera una representación simbólica y posteriormente dicha
    // representación es interpretada.
    //
    public void GenerateTree()
    {
        ClearGeneratedTree();
        EnsureGeneratedRoot();

        branchCounter = 0;

        string activeAxiom;
        List<LSystemRule> activeRules;
        int activeIterations;
        float activeAngle;


        if (generationMode ==
            GenerationMode.TwoD)
        {
            activeAxiom =
                axiom2D;

            activeRules =
                rules2D;

            activeIterations =
                iterations2D;

            activeAngle =
                angle2D;
        }
        else
        {
            activeAxiom =
                axiom3D;

            activeRules =
                rules3D;

            activeIterations =
                iterations3D;

            activeAngle =
                angle3D;
        }


        List<string> derivation =
            new List<string>();


        string sequence =
            ParallelGrammarGenerator.Generate(
                activeAxiom,
                activeRules,
                activeIterations,
                derivation
            );


        if (showDerivation)
        {
            PrintDerivation(
                activeAxiom,
                activeRules,
                derivation,
                sequence
            );
        }


        Interpret(
            sequence,
            activeAngle
        );
    }


    // -------------------------------------------------------------------------
    // TURTLE GRAPHICS
    // -------------------------------------------------------------------------
    //
    // La cadena generada anteriormente contiene símbolos que ahora deben ser
    // interpretados como instrucciones.
    //
    //
    // -------------------------------------------------------------------------
    // MOVIMIENTO
    // -------------------------------------------------------------------------
    //
    // F:
    //
    //      avanzar en la dirección actual y dibujar un segmento.
    //
    // f:
    //
    //      avanzar la misma distancia pero sin dibujar.
    //
    // La dirección depende de la orientación actual de la tortuga.
    //
    //
    // -------------------------------------------------------------------------
    // ROTACIÓN 2D
    // -------------------------------------------------------------------------
    //
    // + y - modifican la orientación utilizando el ángulo configurado.
    //
    // Ambos representan rotaciones opuestas.
    //
    // Esto permite que futuros movimientos F continúen en otra dirección.
    //
    //
    // -------------------------------------------------------------------------
    // RAMIFICACIONES
    // -------------------------------------------------------------------------
    //
    // Los símbolos [ y ] permiten generar estructuras ramificadas.
    //
    // Al encontrar:
    //
    //      [
    //
    // se debe recordar el estado actual.
    //
    // Posteriormente:
    //
    //      ]
    //
    // permite volver a ese estado y continuar desde allí.
    //
    // Debido a que pueden existir ramas dentro de otras ramas, los estados
    // deben almacenarse utilizando una pila:
    //
    //      Stack<TurtleState>
    //
    // El último estado almacenado debe ser el primero en recuperarse.
    //
    //
    // -------------------------------------------------------------------------
    // EXTENSIÓN 3D
    // -------------------------------------------------------------------------
    //
    // En modo 2D las rotaciones mantienen la estructura sobre un plano.
    //
    // En modo ThreeD se incorporan nuevas rotaciones:
    //
    //      & y ^       Pitch
    //
    //      \ y /       Roll
    //
    // Estas rotaciones permiten cambiar la orientación sobre ejes adicionales
    // y generar ramas fuera del plano original.
    //
    // Los operadores adicionales solamente deben actuar en modo ThreeD.
    //
    //
    // -------------------------------------------------------------------------
    // QUATERNIONS
    // -------------------------------------------------------------------------
    //
    // Unity representa la orientación utilizando Quaternion.
    //
    // Para aplicar las rotaciones puede utilizarse:
    //
    //      Quaternion.AngleAxis(...)
    //
    // y combinar la nueva rotación con la orientación actual.
    //
    // La figura incluida en la guía muestra como referencia los movimientos
    // yaw, pitch y roll.
    //
    //
   private void Interpret(
        string sequence,
        float activeAngle)
    {
        // 1. Estado inicial de la tortuga
        Vector3 currentPosition = Vector3.zero;
        Quaternion currentRotation = Quaternion.identity;

        // Pila para ramificaciones
        Stack<TurtleState> stateStack = new Stack<TurtleState>();

        // Recorrer los símbolos
        foreach (char symbol in sequence)
        {
            switch (symbol)
            {
                // 2. Movimiento
                case 'F': // Avanzar dibujando
                    {
                        // Calculamos la dirección actual del frente de la tortuga usando la rotación
                        Vector3 dir = currentRotation * Vector3.up;
                        Vector3 nextPosition = currentPosition + dir * segmentLength;

                        CreateBranch(currentPosition, nextPosition);
                        currentPosition = nextPosition;
                    }
                    break;

                case 'f': // Avanzar sin dibujar
                    {
                        Vector3 dir = currentRotation * Vector3.up;
                        currentPosition += dir * segmentLength;
                    }
                    break;

                // 3. Rotaciones 2D (Yaw / Z-axis)
                // Importante: multiplicar por la izquierda aplica la rotación localmente
                case '+':
                    currentRotation *= Quaternion.Euler(0f, 0f, activeAngle);
                    break;

                case '-':
                    currentRotation *= Quaternion.Euler(0f, 0f, -activeAngle);
                    break;

                // 4. Ramificaciones
                case '[':
                    stateStack.Push(new TurtleState(currentPosition, currentRotation));
                    break;

                case ']':
                    if (stateStack.Count > 0)
                    {
                        TurtleState savedState = stateStack.Pop();
                        currentPosition = savedState.position;
                        currentRotation = savedState.rotation;
                    }
                    break;

                // 5. Rotaciones 3D
                case '&': // Pitch down (+X)
                    if (generationMode == GenerationMode.ThreeD)
                    {
                        currentRotation *= Quaternion.Euler(activeAngle, 0f, 0f);
                    }
                    break;

                case '^': // Pitch up (-X)
                    if (generationMode == GenerationMode.ThreeD)
                    {
                        currentRotation *= Quaternion.Euler(-activeAngle, 0f, 0f);
                    }
                    break;

                case '\\': // Roll left (+Y)
                    if (generationMode == GenerationMode.ThreeD)
                    {
                        currentRotation *= Quaternion.Euler(0f, activeAngle, 0f);
                    }
                    break;

                case '/': // Roll right (-Y)
                    if (generationMode == GenerationMode.ThreeD)
                    {
                        currentRotation *= Quaternion.Euler(0f, -activeAngle, 0f);
                    }
                    break;

                default:
                    break;
            }
        }
    }
    
    // -------------------------------------------------------------------------
    // CREACIÓN DE SEGMENTOS
    // -------------------------------------------------------------------------
    //
    // Esta sección se entrega implementada.
    //
    // CreateBranch() recibe dos posiciones y crea un cilindro entre ellas.
    //
    // La lógica de visualización no forma parte de la implementación evaluada.
    //
    private void CreateBranch(
        Vector3 start,
        Vector3 end)
    {
        Vector3 direction =
            end - start;

        float length =
            direction.magnitude;

        if (length <= 0f)
        {
            return;
        }


        GameObject branch =
            GameObject.CreatePrimitive(
                PrimitiveType.Cylinder
            );

        branchCounter++;

        branch.name =
            "Branch_" +
            branchCounter.ToString("0000");


        branch.transform.SetParent(
            generatedRoot,
            false
        );


        Vector3 middlePoint =
            (start + end) *
            0.5f;


        branch.transform.localPosition =
            middlePoint;


        branch.transform.localRotation =
            Quaternion.FromToRotation(
                Vector3.up,
                direction.normalized
            );


        branch.transform.localScale =
            new Vector3(
                branchRadius,
                length * 0.5f,
                branchRadius
            );


        Renderer renderer =
            branch.GetComponent<Renderer>();


        if (renderer != null &&
            branchMaterial != null)
        {
            renderer.sharedMaterial =
                branchMaterial;
        }


        if (renderer != null &&
            useHeightColor)
        {
            ApplyHeightColor(
                renderer,
                middlePoint.y
            );
        }


        Collider collider =
            branch.GetComponent<Collider>();

        if (collider != null)
        {
            SafeDestroy(
                collider
            );
        }
    }


    // -------------------------------------------------------------------------
    // COLOR SEGÚN ALTURA
    // -------------------------------------------------------------------------
    //
    // Esta sección se entrega implementada.
    //
    private void ApplyHeightColor(
        Renderer renderer,
        float height)
    {
        float safeHeight =
            Mathf.Max(
                0.1f,
                colorHeight
            );


        float t =
            Mathf.InverseLerp(
                0f,
                safeHeight,
                height
            );


        Color branchColor =
            Color.Lerp(
                lowColor,
                highColor,
                t
            );


        MaterialPropertyBlock block =
            new MaterialPropertyBlock();


        renderer.GetPropertyBlock(
            block
        );


        Material material =
            renderer.sharedMaterial;


        if (material != null)
        {
            if (material.HasProperty(
                "_BaseColor"))
            {
                block.SetColor(
                    "_BaseColor",
                    branchColor
                );
            }


            if (material.HasProperty(
                "_Color"))
            {
                block.SetColor(
                    "_Color",
                    branchColor
                );
            }
        }


        renderer.SetPropertyBlock(
            block
        );
    }


    // -------------------------------------------------------------------------
    // INFRAESTRUCTURA DE VISUALIZACIÓN
    // -------------------------------------------------------------------------

    private void EnsureGeneratedRoot()
    {
        if (generatedRoot != null)
        {
            return;
        }


        Transform existing =
            transform.Find(
                "Generated Tree"
            );


        if (existing != null)
        {
            generatedRoot =
                existing;

            return;
        }


        GameObject root =
            new GameObject(
                "Generated Tree"
            );


        root.transform.SetParent(
            transform,
            false
        );


        root.transform.localPosition =
            Vector3.zero;

        root.transform.localRotation =
            Quaternion.identity;

        root.transform.localScale =
            Vector3.one;


        generatedRoot =
            root.transform;
    }


    public void ClearGeneratedTree()
    {
        if (generatedRoot == null)
        {
            Transform existing =
                transform.Find(
                    "Generated Tree"
                );


            if (existing != null)
            {
                generatedRoot =
                    existing;
            }
        }


        if (generatedRoot != null)
        {
            SafeDestroy(
                generatedRoot.gameObject
            );

            generatedRoot =
                null;
        }


        branchCounter = 0;
    }


    // -------------------------------------------------------------------------
    // DEBUG
    // -------------------------------------------------------------------------

    private void PrintDerivation(
        string activeAxiom,
        List<LSystemRule> activeRules,
        List<string> derivation,
        string finalSequence)
    {
        StringBuilder output =
            new StringBuilder();


        output.AppendLine(
            "===== L-SYSTEM TREE ====="
        );


        output.AppendLine();

        output.AppendLine(
            "MODE:"
        );

        output.AppendLine(
            generationMode.ToString()
        );


        output.AppendLine();

        output.AppendLine(
            "AXIOM:"
        );

        output.AppendLine(
            activeAxiom
        );


        output.AppendLine();

        output.AppendLine(
            "RULES:"
        );


        foreach (LSystemRule rule in activeRules)
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


    private void SafeDestroy(
        Object target)
    {
        if (target == null)
        {
            return;
        }


        if (Application.isPlaying)
        {
            Destroy(
                target
            );
        }
        else
        {
            DestroyImmediate(
                target
            );
        }
    }
}