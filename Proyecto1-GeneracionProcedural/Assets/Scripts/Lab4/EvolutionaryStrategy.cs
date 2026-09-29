using System;
using System.Collections.Generic;
using UnityEngine;

public class EvolutionaryStrategy : MonoBehaviour
{
    [Header("(mu + lambda) Evolution Strategy")]

    /*
     * mu = cantidad de padres que sobreviven.
     *
     * mu = 15
     * parents.Count = 15
     *
     * parents = [
     *   [25, 131, 94, 212, 135, 445],
     *   [6, 150, 197, 376, 59, 248],
     *   ...
     *   [...] // padre 14
     * ]
     */
    [Min(1)]
    [SerializeField] private int mu = 15;

    /*
     * lambda = cantidad de hijos generados en cada generación.
     *
     * lambda = 45
     * offspring.Count = 45
     *
     * Cada hijo se obtiene copiando un padre y aplicando mutación.
     */
    [Min(1)]
    [SerializeField] private int lambda = 45;

    [Min(1)]
    [SerializeField] private int generations = 80;

    /*
     * mutationRate = probabilidad de mutar cada gen.
     *
     * mutationRate = 0.20
     *
     * Cada una de las 6 posiciones tiene 20% de probabilidad
     * de ser reemplazada por otro Pokémon.
     */
    [Range(0f, 1f)]
    [SerializeField] private float mutationRate = 0.20f;

    [Header("Debug")]
    [SerializeField] private bool logProgress = true;

    /*
     * ============================================================
     * (mu + lambda) EVOLUTION STRATEGY
     * ============================================================
     *
     * DIFERENCIA PRINCIPAL CON GA
     * ------------------------------------------------------------
     * - NO utilizamos crossover.
     * - Los hijos se crean desde un padre.
     * - La principal fuente de variación es la mutación.
     * - Padres e hijos compiten juntos.
     *
     * PSEUDOCÓDIGO GENERAL
     * ------------------------------------------------------------
     * 1: create mu random parents
     * 2: evaluate parents
     *
     * 3: repeat for N generations:
     * 4:      create empty offspring
     *
     * 5:      repeat lambda times:
     * 6:          choose random parent
     * 7:          child = copy(parent)
     * 8:          mutate(child)
     * 9:          evaluate(child)
     * 10:         add child to offspring
     *
     * 11:     combined = parents + offspring
     * 12:     sort combined by fitness
     * 13:     keep best mu individuals as new parents
     *
     * 14: select generated content from final parents
     *
     * EJEMPLO
     * ------------------------------------------------------------
     * mu = 2
     * lambda = 3
     *
     * parents:
     * A -> fitness 0.80
     * B -> fitness 0.72
     *
     * offspring:
     * C -> fitness 0.88
     * D -> fitness 0.65
     * E -> fitness 0.91
     *
     * combined = [A, B, C, D, E]
     *
     * ordenado:
     * E = 0.91
     * C = 0.88
     * A = 0.80
     * B = 0.72
     * D = 0.65
     *
     * Como mu = 2:
     * newParents = [E, C]
     */
    public PokemonTeamCandidate Generate(
        IReadOnlyList<PokemonData> dataset,
        PokemonTeamGenerationConfig config)
    {
        if (dataset == null || dataset.Count == 0) return null;
        System.Random random = new System.Random();

        // 1: create mu random parents
        List<PokemonTeamCandidate> parents = CreateInitialParents(mu, dataset.Count, config, random);

        // 2: evaluate parents
        EvaluatePopulation(parents, dataset, config);

        // 3: repeat for N generations:
        for (int gen = 0; gen < generations; gen++)
        {
            // 4: create empty offspring
            List<PokemonTeamCandidate> offspring = new List<PokemonTeamCandidate>();

            // 5: repeat lambda times:
            for (int i = 0; i < lambda; i++)
            {
                // 6: choose random parent
                PokemonTeamCandidate parent = parents[random.Next(parents.Count)];

                // 7: child = copy(parent)
                PokemonTeamCandidate child = parent.Clone();

                // 8: mutate(child)
                Mutate(child, dataset.Count, random);

                // 9: evaluate(child)
                PokemonTeamFitness.Evaluate(child, dataset, config);

                // 10: add child to offspring
                offspring.Add(child);
            }

            // 11: combined = parents + offspring
            List<PokemonTeamCandidate> combined = new List<PokemonTeamCandidate>(parents);
            combined.AddRange(offspring);

            // 12: sort combined by fitness
            combined.Sort((a, b) => b.fitness.CompareTo(a.fitness));

            // 13: keep best mu individuals as new parents
            parents.Clear();
            for (int i = 0; i < mu && i < combined.Count; i++)
            {
                parents.Add(combined[i]);
            }
        }

        // 14: select generated content from final parents
        return PokemonTeamFitness.SelectGeneratedContent(parents, config, random);
    }

    /*
     * ============================================================
     * CREAR PADRES INICIALES
     * ============================================================
     *
     * mu = 15
     * teamSize = 6
     *
     * parents.Count = 15
     *
     * Cada padre puede verse así:
     * [25, 131, 94, 212, 135, 445]
     *
     * PSEUDOCÓDIGO
     * ------------------------------------------------------------
     * 1: create empty parents
     * 2: repeat parentCount times:
     * 3:      create candidate with teamSize genes
     * 4:      assign random dataset index to every gene
     * 5:      add candidate to parents
     * 6: return parents
     */
    private List<PokemonTeamCandidate> CreateInitialParents(
        int parentCount,
        int datasetSize,
        PokemonTeamGenerationConfig config,
        System.Random random)
    {
        List<PokemonTeamCandidate> parents = new List<PokemonTeamCandidate>();

        for (int i = 0; i < parentCount; i++)
        {
            PokemonTeamCandidate candidate = new PokemonTeamCandidate(config.teamSize);
            for (int j = 0; j < config.teamSize; j++)
            {
                candidate.genes[j] = random.Next(datasetSize);
            }
            parents.Add(candidate);
        }

        return parents;
    }

    /*
     * ============================================================
     * MUTACIÓN
     * ============================================================
     *
     * Padre:
     * [25, 131, 94, 212, 135, 445]
     *
     * Copia:
     * [25, 131, 94, 212, 135, 445]
     *
     * Después de mutar:
     * [25, 131, 700, 212, 135, 445]
     *
     * PSEUDOCÓDIGO
     * ------------------------------------------------------------
     * 1: mutated = false
     * 2: for each gene:
     * 3:      if random < mutationRate:
     * 4:          replace gene with another random Pokemon
     * 5:          mutated = true
     *
     * 6: if no gene was mutated:
     * 7:      choose one random gene
     * 8:      replace it with another Pokemon
     *
     * Queremos asegurar que cada hijo tenga al menos una variación.
     */
    private void Mutate(
        PokemonTeamCandidate candidate,
        int datasetSize,
        System.Random random)
    {
        bool mutated = false;

        for (int i = 0; i < candidate.genes.Length; i++)
        {
            if (random.NextDouble() < mutationRate)
            {
                candidate.genes[i] = RandomDifferentPokemon(candidate.genes[i], datasetSize, random);
                mutated = true;
            }
        }

        if (!mutated && candidate.genes.Length > 0)
        {
            int randomIndex = random.Next(candidate.genes.Length);
            candidate.genes[randomIndex] = RandomDifferentPokemon(candidate.genes[randomIndex], datasetSize, random);
        }
    }

    /*
     * Retorna un índice diferente al actual.
     *
     * current = 94
     * datasetSize = 1032
     *
     * resultado posible = 700
     *
     * Debe estar entre 0 y datasetSize - 1
     * y no debe ser igual a current.
     */
    private int RandomDifferentPokemon(
        int current,
        int datasetSize,
        System.Random random)
    {
        int newPokemon = current;
        while (newPokemon == current)
        {
            newPokemon = random.Next(datasetSize);
        }
        return newPokemon;
    }

    private void EvaluatePopulation(
        List<PokemonTeamCandidate> population,
        IReadOnlyList<PokemonData> dataset,
        PokemonTeamGenerationConfig config)
    {
        foreach (PokemonTeamCandidate candidate in population)
        {
            PokemonTeamFitness.Evaluate(candidate, dataset, config);
        }
    }
}