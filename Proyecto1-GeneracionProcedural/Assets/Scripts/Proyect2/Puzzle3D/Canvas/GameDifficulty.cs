using UnityEngine;

/// <summary>
/// Configuracion editable de una dificultad: forma del puzzle, tiempo, monedas, pistas y desbloqueo.
/// </summary>
[System.Serializable]
public class DifficultyConfig
{
    public string displayName = "Facil";
    [TextArea(1, 3)] public string description = "";

    [Header("Puzzle (se sortea dentro de cada rango)")]
    public Vector2Int nodeRange = new Vector2Int(6, 8);
    public Vector2Int stepsRange = new Vector2Int(2, 3);
    [Tooltip("0 = puzzle plano, 1 = muy profundo en 3D")]
    [Range(0f, 1f)] public float depth = 0f;

    [Header("Temporizador (es un bonus, no una derrota)")]
    [Tooltip("Tiempo objetivo = baseTime + pasos optimos * secondsPerStep")]
    public float baseTime = 45f;
    public float secondsPerStep = 15f;

    [Header("Monedas ganadas al resolver")]
    public int baseReward = 10;
    [Tooltip("Se gana completo con clics <= optimo, y la mitad con clics <= optimo + 2")]
    public int efficiencyBonus = 5;
    [Tooltip("Se gana si se resuelve dentro del tiempo objetivo")]
    public int timeBonus = 5;

    [Header("Costo de las pistas (en monedas)")]
    public int cheapHintCost = 5;
    public int fullHintCost = 30;

    [Header("Desbloqueo")]
    [Tooltip("Puzzles que hay que resolver en la dificultad anterior para desbloquear esta")]
    public int requiredSolvedPrevious = 0;

    public int MaxReward => baseReward + efficiencyBonus + timeBonus;
}