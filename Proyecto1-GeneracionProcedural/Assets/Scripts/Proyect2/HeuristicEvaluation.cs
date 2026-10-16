using System.Collections.Generic;

public static class HeuristicEvaluation
{
    private const float INFECTED_PENALTY = 10f;
    private const float CLICK_PENALTY = 1f;

    // Invierte el estado del nodo ( De Esfera a Cubo) y el de sus vecinos conectados
    public static void ToggleNode(bool[] state, int index, List<int>[] neighbors)
    {
        if (state == null || index < 0 || index >= state.Length) return;

        state[index] = !state[index];
        foreach (int neighborIndex in neighbors[index])
        {
            state[neighborIndex] = !state[neighborIndex];
        }
    }

    // Simula una secuencia completa de clics sobre una copia del tablero
    public static bool[] SimulateClicks(bool[] startState, bool[] clicks, List<int>[] neighbors)
    {
        bool[] sim = (bool[])startState.Clone();
        for (int i = 0; i < clicks.Length; i++)
        {
            if (clicks[i])
            {
                ToggleNode(sim, i, neighbors);
            }
        }
        return sim;
    }

    // HEURISTICA: Calcula el costo total (0 = solucion perfecta sin clics extra)
    public static float EvaluateCost(bool[] state, int clicksUsed)
    {
        int infectedCubes = CountInfected(state);
        return (infectedCubes * INFECTED_PENALTY) + (clicksUsed * CLICK_PENALTY);
    }

    // Cuenta cuantos nodos infectados quedan en la red
    public static int CountInfected(bool[] state)
    {
        int count = 0;
        for (int i = 0; i < state.Length; i++)
        {
            if (state[i]) count++;
        }
        return count;
    }

    // Cuenta cuantos clics activos (true) tiene una solucion candidata
    public static int CountClicks(bool[] clicks)
    {
        int count = 0;
        for (int i = 0; i < clicks.Length; i++)
        {
            if (clicks[i]) count++;
        }
        return count;
    }
}