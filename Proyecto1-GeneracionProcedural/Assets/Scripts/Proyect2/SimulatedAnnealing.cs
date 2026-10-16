using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimulatedAnnealing : MonoBehaviour
{
    [Header("Parametros Simulated Annealing (Lab 6)")]
    public float initialTemperature = 30.0f;
    [Range(0.50f, 0.999f)] public float coolingRate = 0.96f;
    public float minTemperature = 0.05f;
    [Range(1, 10)] public int neighborsPerStep = 2;

    // Ejecuta Simulated Annealing paso a paso reportando el progreso
    public IEnumerator SolveRoutine(
        bool[] initialBoard,
        List<int>[] neighbors,
        float stepDelay,
        Action<bool[], int, int, float, int> onStepUpdate,
        Action<bool[], int, int, float, float> onComplete)
    {
        int nodeCount = initialBoard.Length;
        float initialCost = HeuristicEvaluation.EvaluateCost(initialBoard, 0);

        // Estado actual (vector de clics aplicados sobre la instancia inicial)
        bool[] currentClicks = new bool[nodeCount];
        bool[] currentBoard = (bool[])initialBoard.Clone();
        float currentCost = initialCost;

        // Mejor estado historico encontrado durante la busqueda
        bool[] bestClicks = (bool[])currentClicks.Clone();
        bool[] bestBoard = (bool[])currentBoard.Clone();
        float bestCost = currentCost;

        float temp = initialTemperature;
        int iterations = 0;
        System.Random rand = new System.Random();

        while (temp > minTemperature && HeuristicEvaluation.CountInfected(bestBoard) > 0)
        {
            iterations++;

            bool[] candidateBoard = null;
            bool[] candidateClicks = null;
            float candidateCost = float.MaxValue;

            // Evaluar K vecinos por iteracion y seleccionar el mejor del paso
            for (int n = 0; n < neighborsPerStep; n++)
            {
                bool[] testClicks = GetNeighbor(currentClicks, rand);
                bool[] testBoard = HeuristicEvaluation.SimulateClicks(initialBoard, testClicks, neighbors);
                float testCost = HeuristicEvaluation.EvaluateCost(testBoard, HeuristicEvaluation.CountClicks(testClicks));

                if (testCost < candidateCost)
                {
                    candidateCost = testCost;
                    candidateBoard = testBoard;
                    candidateClicks = testClicks;
                }
            }

            float delta = candidateCost - currentCost;

            // Condicion de aceptacion mejora directa o probabilidad exp(-delta / T)
            if (delta < 0 || rand.NextDouble() < Mathf.Exp(-delta / temp))
            {
                currentClicks = candidateClicks;
                currentBoard = candidateBoard;
                currentCost = candidateCost;

                if (currentCost < bestCost)
                {
                    bestCost = currentCost;
                    bestBoard = (bool[])currentBoard.Clone();
                    bestClicks = (bool[])currentClicks.Clone();
                }

                int currentClicksCount = HeuristicEvaluation.CountClicks(currentClicks);
                int bestInfectedCount = HeuristicEvaluation.CountInfected(bestBoard);

                onStepUpdate?.Invoke(currentBoard, currentClicksCount, iterations, temp, bestInfectedCount);
                yield return new WaitForSeconds(stepDelay);
            }

            // Enfriamiento geometrico
            temp *= coolingRate;
        }

        int finalClicks = HeuristicEvaluation.CountClicks(bestClicks);
        onComplete?.Invoke(bestBoard, finalClicks, iterations, initialCost, bestCost);
    }

    // Invertir la decision de clic en un unico nodo al azar
    private bool[] GetNeighbor(bool[] currentClicks, System.Random rand)
    {
        bool[] neighbor = (bool[])currentClicks.Clone();
        int randomNode = rand.Next(neighbor.Length);
        neighbor[randomNode] = !neighbor[randomNode];
        return neighbor;
    }

    // Metodos para conectar a Sliders de la UI
    public void SetInitialTemp(float val) => initialTemperature = val;
    public void SetCoolingRate(float val) => coolingRate = val;
    public void SetMinTemp(float val) => minTemperature = val;
    public void SetNeighborsPerStep(float val) => neighborsPerStep = Mathf.RoundToInt(val);
}