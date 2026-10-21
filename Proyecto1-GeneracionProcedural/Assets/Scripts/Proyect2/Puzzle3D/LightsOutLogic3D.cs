using System.Collections.Generic;

/// <summary>
/// Funciones puras de la logica Lights Out sobre un grafo.
/// Reemplaza a HeuristicEvaluation para los scripts 3D (no modifica el original).
/// </summary>
public static class LightsOutLogic3D
{
    // Aplica un clic: invierte el nodo y todos sus vecinos
    public static void ToggleNode(bool[] board, int index, List<int>[] neighbors)
    {
        board[index] = !board[index];

        List<int> list = neighbors[index];
        for (int i = 0; i < list.Count; i++)
        {
            int n = list[i];
            board[n] = !board[n];
        }
    }

    // Igual que ToggleNode, pero devuelve cuanto cambio la cantidad de nodos "true"
    // (+1 por cada nodo que se prende, -1 por cada uno que se apaga).
    // Sirve para mantener el conteo de infectados sin recorrer todo el tablero.
    public static int ToggleNodeCounting(bool[] board, int index, List<int>[] neighbors)
    {
        int delta = FlipAndCount(board, index);

        List<int> list = neighbors[index];
        for (int i = 0; i < list.Count; i++)
            delta += FlipAndCount(board, list[i]);

        return delta;
    }

    private static int FlipAndCount(bool[] board, int i)
    {
        board[i] = !board[i];
        return board[i] ? 1 : -1;
    }

    // Cuenta cuantos valores "true" hay (sirve para infectados y para clics)
    public static int CountTrue(bool[] array)
    {
        int count = 0;
        for (int i = 0; i < array.Length; i++)
            if (array[i]) count++;
        return count;
    }

    // Aplica un vector de clics sobre una copia del tablero inicial
    public static bool[] SimulateClicks(bool[] initialBoard, bool[] clicks, List<int>[] neighbors)
    {
        bool[] result = (bool[])initialBoard.Clone();
        for (int i = 0; i < clicks.Length; i++)
        {
            if (clicks[i]) ToggleNode(result, i, neighbors);
        }
        return result;
    }
}