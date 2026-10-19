using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resultado final de la busqueda.
/// </summary>
public class ESResult
{
    public bool found;          // Se encontro al menos una solucion valida
    public bool[] clicks;       // Mejor vector de clics encontrado (null si no hubo solucion)
    public int steps;           // Cantidad de clics de la mejor solucion
    public int knownSteps;      // Clics de la solucion que genero el backward (referencia)
    public int generations;     // Generaciones ejecutadas
    public int evaluations;     // Evaluaciones de costo realizadas
    public float seconds;       // Tiempo real que tomo
}

/// <summary>
/// Evolution Strategy (mu + lambda) con Simulated Annealing interno.
/// - ES: exploracion global con poblacion y mutacion auto-adaptativa (sigma).
/// - SA: refinamiento local de cada hijo; su "memoria" es el mejor estado visto en el enfriamiento.
/// Objetivo: encontrar el vector de clics VALIDO con la menor cantidad de clics.
/// </summary>
public class EvolutionStrategySolver : MonoBehaviour
{
    [Header("Evolution Strategy")]
    [Tooltip("mu: tamano de la poblacion de padres que sobrevive cada generacion")]
    [Range(2, 20)] public int parentCount = 6;
    [Tooltip("lambda: hijos generados por generacion")]
    [Range(4, 60)] public int offspringCount = 18;
    [Range(10, 500)] public int maxGenerations = 120;
    [Tooltip("Generaciones sin mejorar el mejor resultado valido antes de detenerse")]
    [Range(5, 100)] public int stagnationLimit = 25;
    [Tooltip("Cantidad inicial de clics que muta un individuo (se auto-adapta)")]
    [Range(1f, 10f)] public float initialSigma = 3f;
    [Tooltip("Tasa de aprendizaje de la auto-adaptacion de sigma")]
    [Range(0.05f, 1f)] public float tau = 0.3f;
    [Range(0f, 1f)] public float crossoverChance = 0.25f;
    [Tooltip("Densidad de clics de los individuos aleatorios iniciales")]
    [Range(0.05f, 0.8f)] public float initialDensity = 0.25f;
    [Tooltip("Incluye la solucion del generador como individuo inicial (garantiza una solucion valida)")]
    public bool seedWithKnownSolution = true;

    [Header("Simulated Annealing interno")]
    public float saInitialTemperature = 3f;
    [Range(0.5f, 0.999f)] public float saCoolingRate = 0.9f;
    [Range(5, 200)] public int saIterations = 40;
    [Range(1, 10)] public int saNeighborsPerStep = 2;

    [Header("Funcion de costo")]
    [Tooltip("Penalizacion por cada nodo infectado (costo = infectados * peso + clics)")]
    public float infectionWeight = 6f;

    [Header("Animacion")]
    [Tooltip("Pausa entre generaciones para que la UI pueda mostrar el progreso")]
    public float generationDelay = 0.02f;

    // Individuo de la poblacion
    private class Individual
    {
        public bool[] clicks;   // Decision de clic por nodo
        public bool[] board;    // Tablero resultante de aplicar esos clics al estado inicial
        public float sigma;     // Parametro de mutacion propio (auto-adaptativo)
        public int infected;
        public int clickCount;
        public float cost;
    }

    private int evaluations;

    // Ejecuta la busqueda. onProgress(generacion, mejorPasosValido (-1 si no hay), infectadosDelMejor)
    public IEnumerator SolveRoutine(
        bool[] initialBoard,
        List<int>[] neighbors,
        bool[] knownSolution,
        Action<int, int, int> onProgress,
        Action<ESResult> onComplete)
    {
        float startTime = Time.realtimeSinceStartup;
        evaluations = 0;
        int n = initialBoard.Length;
        System.Random rand = new System.Random();

        // ---------- Poblacion inicial ----------
        List<Individual> population = new List<Individual>();

        if (seedWithKnownSolution && knownSolution != null)
        {
            Individual seeded = new Individual { clicks = (bool[])knownSolution.Clone(), sigma = initialSigma };
            Evaluate(seeded, initialBoard, neighbors);
            population.Add(seeded);
        }

        while (population.Count < parentCount)
        {
            Individual random = new Individual { clicks = new bool[n], sigma = initialSigma };
            for (int i = 0; i < n; i++) random.clicks[i] = rand.NextDouble() < initialDensity;
            Evaluate(random, initialBoard, neighbors);
            population.Add(random);
        }

        // SA refina tambien a la poblacion inicial
        for (int i = 0; i < population.Count; i++)
            population[i] = RefineWithSA(population[i], neighbors, rand);

        population.Sort(CompareIndividuals);

        Individual bestValid = null;
        if (population[0].infected == 0) bestValid = Clone(population[0]);

        int stagnation = 0;
        int generationsUsed = 0;

        // ---------- Bucle evolutivo (mu + lambda) ----------
        for (int gen = 1; gen <= maxGenerations; gen++)
        {
            generationsUsed = gen;

            // Padres + hijos compiten juntos (elitismo)
            List<Individual> pool = new List<Individual>(population);

            for (int o = 0; o < offspringCount; o++)
            {
                Individual child = CreateChild(population, initialBoard, neighbors, rand);
                child = RefineWithSA(child, neighbors, rand);   // SA como refinamiento
                pool.Add(child);
            }

            pool.Sort(CompareIndividuals);

            // Seleccion de los mu mejores evitando clones identicos (mantiene diversidad)
            population.Clear();
            HashSet<string> seen = new HashSet<string>();
            foreach (Individual ind in pool)
            {
                if (seen.Add(ClicksKey(ind.clicks))) population.Add(ind);
                if (population.Count >= parentCount) break;
            }

            // Seguimiento del mejor valido
            Individual top = population[0];
            bool improved = false;
            if (top.infected == 0 && (bestValid == null || top.clickCount < bestValid.clickCount))
            {
                bestValid = Clone(top);
                improved = true;
            }
            stagnation = improved ? 0 : stagnation + 1;

            onProgress?.Invoke(gen, bestValid != null ? bestValid.clickCount : -1, top.infected);

            // Condiciones de parada: optimo trivial (1 clic) o estancamiento
            if (bestValid != null && bestValid.clickCount <= 1) break;
            if (stagnation >= stagnationLimit) break;

            if (generationDelay > 0f) yield return new WaitForSeconds(generationDelay);
            else yield return null;
        }

        // ---------- Resultado ----------
        ESResult result = new ESResult
        {
            found = bestValid != null,
            clicks = bestValid != null ? bestValid.clicks : null,
            steps = bestValid != null ? bestValid.clickCount : -1,
            knownSteps = knownSolution != null ? LightsOutLogic3D.CountTrue(knownSolution) : -1,
            generations = generationsUsed,
            evaluations = evaluations,
            seconds = Time.realtimeSinceStartup - startTime
        };
        onComplete?.Invoke(result);
    }

    // ---------- Operadores evolutivos ----------

    // Crea un hijo: (cruce opcional) + mutacion con sigma auto-adaptativo
    private Individual CreateChild(List<Individual> parents, bool[] initialBoard, List<int>[] neighbors, System.Random rand)
    {
        int n = initialBoard.Length;
        Individual pa = parents[rand.Next(parents.Count)];
        Individual child = Clone(pa);

        // Cruce uniforme con otro padre
        if (rand.NextDouble() < crossoverChance)
        {
            Individual pb = parents[rand.Next(parents.Count)];
            for (int i = 0; i < n; i++)
                if (rand.NextDouble() < 0.5) child.clicks[i] = pb.clicks[i];
            child.sigma = (pa.sigma + pb.sigma) * 0.5f;
        }

        // Auto-adaptacion: sigma' = sigma * exp(tau * N(0,1))
        child.sigma = Mathf.Clamp(child.sigma * Mathf.Exp(tau * Gaussian(rand)), 1f, Mathf.Max(1f, n * 0.5f));

        // Mutacion: invierte "sigma" decisiones de clic al azar
        int flips = Mathf.Clamp(Mathf.RoundToInt(child.sigma), 1, n);
        for (int f = 0; f < flips; f++)
        {
            int node = rand.Next(n);
            child.clicks[node] = !child.clicks[node];
        }

        Evaluate(child, initialBoard, neighbors);
        return child;
    }

    // Simulated Annealing corto sobre un individuo. Devuelve el mejor estado visto (memoria).
    private Individual RefineWithSA(Individual start, List<int>[] neighbors, System.Random rand)
    {
        Individual current = Clone(start);
        Individual best = Clone(start);
        int n = current.clicks.Length;
        float temp = saInitialTemperature;

        for (int it = 0; it < saIterations; it++)
        {
            // Evalua K vecinos (flip de un clic) y se queda con el mejor del paso
            int bestNode = -1;
            float bestCandidateCost = float.MaxValue;

            for (int k = 0; k < saNeighborsPerStep; k++)
            {
                int node = rand.Next(n);
                FlipClick(current, node, neighbors);
                float c = current.cost;
                FlipClick(current, node, neighbors);   // revertir

                if (c < bestCandidateCost)
                {
                    bestCandidateCost = c;
                    bestNode = node;
                }
            }

            // Criterio de aceptacion: mejora directa o probabilidad exp(-delta / T)
            float delta = bestCandidateCost - current.cost;
            if (delta < 0f || rand.NextDouble() < Math.Exp(-delta / temp))
            {
                FlipClick(current, bestNode, neighbors);
                if (CompareIndividuals(current, best) < 0) best = Clone(current);
            }

            // Enfriamiento geometrico
            temp = Mathf.Max(0.01f, temp * saCoolingRate);
        }

        return best;
    }

    // ---------- Utilidades ----------

    // Calcula tablero, infectados, clics y costo desde cero
    private void Evaluate(Individual ind, bool[] initialBoard, List<int>[] neighbors)
    {
        ind.board = LightsOutLogic3D.SimulateClicks(initialBoard, ind.clicks, neighbors);
        ind.infected = LightsOutLogic3D.CountTrue(ind.board);
        ind.clickCount = LightsOutLogic3D.CountTrue(ind.clicks);
        ind.cost = infectionWeight * ind.infected + ind.clickCount;
        evaluations++;
    }

    // Cambia la decision de clic de un nodo actualizando el tablero de forma incremental
    private void FlipClick(Individual ind, int node, List<int>[] neighbors)
    {
        ind.clicks[node] = !ind.clicks[node];
        LightsOutLogic3D.ToggleNode(ind.board, node, neighbors);
        ind.clickCount += ind.clicks[node] ? 1 : -1;
        ind.infected = LightsOutLogic3D.CountTrue(ind.board);
        ind.cost = infectionWeight * ind.infected + ind.clickCount;
        evaluations++;
    }

    // Orden de seleccion: validos primero; entre validos menos clics; entre invalidos menor costo
    private int CompareIndividuals(Individual a, Individual b)
    {
        bool va = a.infected == 0;
        bool vb = b.infected == 0;
        if (va != vb) return va ? -1 : 1;
        if (va) return a.clickCount.CompareTo(b.clickCount);
        return a.cost.CompareTo(b.cost);
    }

    private Individual Clone(Individual s)
    {
        return new Individual
        {
            clicks = (bool[])s.clicks.Clone(),
            board = (bool[])s.board.Clone(),
            sigma = s.sigma,
            infected = s.infected,
            clickCount = s.clickCount,
            cost = s.cost
        };
    }

    private string ClicksKey(bool[] clicks)
    {
        char[] chars = new char[clicks.Length];
        for (int i = 0; i < clicks.Length; i++) chars[i] = clicks[i] ? '1' : '0';
        return new string(chars);
    }

    // Numero aleatorio con distribucion normal (Box-Muller)
    private float Gaussian(System.Random rand)
    {
        double u1 = 1.0 - rand.NextDouble();
        double u2 = rand.NextDouble();
        return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
    }

    // Metodos para conectar a Sliders de la UI
    public void SetParentCount(float val) => parentCount = Mathf.RoundToInt(val);
    public void SetOffspringCount(float val) => offspringCount = Mathf.RoundToInt(val);
    public void SetMaxGenerations(float val) => maxGenerations = Mathf.RoundToInt(val);
    public void SetSAIterations(float val) => saIterations = Mathf.RoundToInt(val);
    public void SetSAInitialTemp(float val) => saInitialTemperature = val;
    public void SetSACoolingRate(float val) => saCoolingRate = val;
    public void SetSANeighborsPerStep(float val) => saNeighborsPerStep = Mathf.RoundToInt(val);
}