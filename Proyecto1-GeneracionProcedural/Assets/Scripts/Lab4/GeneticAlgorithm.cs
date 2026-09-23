using System;
using System.Collections.Generic;
using UnityEngine;

public class GeneticAlgorithm : MonoBehaviour
{
    [Header("Genetic Algorithm")]

    /*
     * populationSize = cantidad de individuos de la población.
     *
     * Si populationSize = 50 y teamSize = 6:
     *
     * population = [
     *   [25, 131, 94, 212, 135, 445],   // individuo 0
     *   [6, 150, 197, 376, 59, 248],     // individuo 1
     *   [143, 448, 130, 214, 94, 445],   // individuo 2
     *   ...
     *   [...]                              // individuo 49
     * ]
     *
     * population.Count = 50
     * population[i].genes.Length = 6
     *
     * Cada gen es el índice de un Pokémon dentro del dataset.
     */
    [Min(2)]
    [SerializeField] private int populationSize = 50;

    /*
     * generations = cantidad de veces que repetimos el ciclo evolutivo.
     *
     * generations = 80
     *
     * Generation 0  -> población inicial
     * Generation 1  -> nueva población
     * ...
     * Generation 80 -> población final
     */
    [Min(1)]
    [SerializeField] private int generations = 80;

    /*
     * crossoverRate = probabilidad de cruzar dos padres.
     *
     * crossoverRate = 0.80  -> aproximadamente 80%.
     */
    [Range(0f, 1f)]
    [SerializeField] private float crossoverRate = 0.80f;

    /*
     * mutationRate = probabilidad de reemplazar cada gen.
     *
     * mutationRate = 0.12
     *
     * Cada una de las 6 posiciones del equipo tiene 12%
     * de probabilidad de ser reemplazada por otro Pokémon.
     */
    [Range(0f, 1f)]
    [SerializeField] private float mutationRate = 0.12f;

    /*
     * tournamentSize = cantidad de individuos que compiten
     * para seleccionar un padre.
     *
     * tournamentSize = 3
     *
     * candidatos:
     * A -> fitness 0.72
     * B -> fitness 0.91
     * C -> fitness 0.84
     *
     * seleccionado -> B
     */
    [Min(2)]
    [SerializeField] private int tournamentSize = 3;

    /*
     * elitism = mejores individuos que pasan directamente
     * a la siguiente generación.
     *
     * elitism = 2
     *
     * nextPopulation comienza con:
     * [bestIndividual, secondBestIndividual]
     */
    [Min(0)]
    [SerializeField] private int elitism = 2;

    [Header("Debug")]
    [SerializeField] private bool logProgress = true;

    public PokemonTeamCandidate Generate(
        IReadOnlyList<PokemonData> dataset,
        PokemonTeamGenerationConfig config)
    {
        if (dataset == null || dataset.Count == 0)
            return null;

        System.Random random = new System.Random();

        // 1: Crear population
        List<PokemonTeamCandidate> population = CreateInitialPopulation(dataset.Count, config, random);

        // 2: Evaluar la población inicial
        EvaluatePopulation(population, dataset, config);

        // 3: Repetir el ciclo evolutivo durante "generations"
        for (int gen = 0; gen < generations; gen++)
        {
            // ordenar por fitness (de mayor a menor)
            population.Sort((a, b) => b.fitness.CompareTo(a.fitness));

            List<PokemonTeamCandidate> nextPopulation = new List<PokemonTeamCandidate>();

            // conservar elite
            for (int i = 0; i < elitism && i < population.Count; i++)
            {
                nextPopulation.Add(population[i].Clone());
            }

            // completar nextPopulation
            while (nextPopulation.Count < populationSize)
            {
                // seleccionar padres
                PokemonTeamCandidate parentA = TournamentSelection(population, random);
                PokemonTeamCandidate parentB = TournamentSelection(population, random);

                PokemonTeamCandidate child;

                // realizar crossover
                if (random.NextDouble() < crossoverRate)
                {
                    child = Crossover(parentA, parentB, random);
                }
                else
                {
                    child = parentA.Clone();
                }

                // mutar
                Mutate(child, dataset.Count, random);

                // evaluar hijos
                PokemonTeamFitness.Evaluate(child, dataset, config);
                nextPopulation.Add(child);
            }

            // reemplazar population
            population = nextPopulation;
        }

        // select generated content from final population
        return PokemonTeamFitness.SelectGeneratedContent(population, config, random);
    }

    private List<PokemonTeamCandidate> CreateInitialPopulation(
        int datasetSize,
        PokemonTeamGenerationConfig config,
        System.Random random)
    {
        List<PokemonTeamCandidate> population = new List<PokemonTeamCandidate>();

        for (int i = 0; i < populationSize; i++)
        {
            PokemonTeamCandidate candidate = new PokemonTeamCandidate(config.teamSize);
            for (int j = 0; j < config.teamSize; j++)
            {
                candidate.genes[j] = random.Next(datasetSize);
            }
            population.Add(candidate);
        }

        return population;
    }

    private PokemonTeamCandidate TournamentSelection(
        List<PokemonTeamCandidate> population,
        System.Random random)
    {
        PokemonTeamCandidate best = null;

        for (int i = 0; i < tournamentSize; i++)
        {
            PokemonTeamCandidate candidate = population[random.Next(population.Count)];
            if (best == null || candidate.fitness > best.fitness)
            {
                best = candidate;
            }
        }

        return best;
    }

    private PokemonTeamCandidate Crossover(
        PokemonTeamCandidate parentA,
        PokemonTeamCandidate parentB,
        System.Random random)
    {
        PokemonTeamCandidate child = new PokemonTeamCandidate(parentA.genes.Length);

        // crossoverPoint between 1 and geneCount - 1
        int crossoverPoint = random.Next(1, parentA.genes.Length);

        for (int i = 0; i < parentA.genes.Length; i++)
        {
            if (i < crossoverPoint)
                child.genes[i] = parentA.genes[i];
            else
                child.genes[i] = parentB.genes[i];
        }

        return child;
    }

    private void Mutate(
        PokemonTeamCandidate candidate,
        int datasetSize,
        System.Random random)
    {
        for (int i = 0; i < candidate.genes.Length; i++)
        {
            if (random.NextDouble() < mutationRate)
            {
                candidate.genes[i] = random.Next(datasetSize);
            }
        }
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