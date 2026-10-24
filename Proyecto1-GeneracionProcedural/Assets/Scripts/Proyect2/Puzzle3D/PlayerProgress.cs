using UnityEngine;

/// <summary>
/// Progreso del jugador guardado en PlayerPrefs: monedas y puzzles resueltos por dificultad.
/// </summary>
public static class PlayerProgress
{
    private const string InitKey = "lo3d_initialized";
    private const string CoinsKey = "lo3d_coins";
    private const string SolvedKey = "lo3d_solved_";

    public static bool IsInitialized => PlayerPrefs.GetInt(InitKey, 0) == 1;

    // Primera vez que se juega: da las monedas iniciales
    public static void Initialize(int startingCoins)
    {
        PlayerPrefs.SetInt(InitKey, 1);
        PlayerPrefs.SetInt(CoinsKey, Mathf.Max(0, startingCoins));
        PlayerPrefs.Save();
    }

    public static int Coins => PlayerPrefs.GetInt(CoinsKey, 0);

    public static void AddCoins(int amount)
    {
        PlayerPrefs.SetInt(CoinsKey, Mathf.Max(0, Coins + amount));
        PlayerPrefs.Save();
    }

    // Descuenta monedas solo si alcanzan
    public static bool TrySpend(int amount)
    {
        if (Coins < amount) return false;
        PlayerPrefs.SetInt(CoinsKey, Coins - amount);
        PlayerPrefs.Save();
        return true;
    }

    public static int GetSolved(int difficulty)
    {
        return PlayerPrefs.GetInt(SolvedKey + difficulty, 0);
    }

    public static void AddSolved(int difficulty)
    {
        PlayerPrefs.SetInt(SolvedKey + difficulty, GetSolved(difficulty) + 1);
        PlayerPrefs.Save();
    }

    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(InitKey);
        PlayerPrefs.DeleteKey(CoinsKey);
        for (int i = 0; i < 10; i++) PlayerPrefs.DeleteKey(SolvedKey + i);
        PlayerPrefs.Save();
    }
}