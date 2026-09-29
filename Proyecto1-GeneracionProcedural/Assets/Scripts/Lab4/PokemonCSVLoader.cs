using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

[Serializable]
public class PokemonData
{
    public string name;
    public string type1;
    public string type2;

    public int hp;
    public int offense;
    public int defense;
    public int speed;
    public int bst;
}

public class PokemonCSVLoader : MonoBehaviour
{
    [Header("CSV")]
    [SerializeField] private TextAsset csvFile;
    [SerializeField] private bool loadOnAwake = true;

    private readonly List<PokemonData> pokemon = new List<PokemonData>();

    public IReadOnlyList<PokemonData> Data => pokemon;
    public bool IsLoaded => pokemon.Count > 0;

    private void Awake()
    {
        if (loadOnAwake)
            Load();
    }

    /*
     * ============================================================
     * OBJETIVO
     * ============================================================
     *
     * El CSV contiene muchas columnas, pero para este laboratorio
     * solo necesitamos una representación reducida:
     *
     * Name, Type 1, Type 2, HP, Att, Spa, Def, Spd, Spe y BST.
     *
     * A partir de esos valores construiremos:
     *
     * offense = max(Att, Spa)
     * defense = max(Def, Spd)
     *
     * EJEMPLO
     * ------------------------------------------------------------
     * Una fila del CSV puede contener:
     *
     * Name = "Charizard"
     * Type 1 = "Fire"
     * Type 2 = "Flying"
     * HP = 78
     * Att = 84
     * Spa = 109
     * Def = 78
     * Spd = 85
     * Spe = 100
     * BST = 534
     *
     * El PokemonData resultante será:
     *
     * {
     *   name = "Charizard",
     *   type1 = "Fire",
     *   type2 = "Flying",
     *   hp = 78,
     *   offense = 109,   // max(84, 109)
     *   defense = 85,    // max(78, 85)
     *   speed = 100,
     *   bst = 534
     * }
     *
     * PSEUDOCÓDIGO
     * ------------------------------------------------------------
     * 1: clear pokemon list
     * 2: verify that csvFile exists
     * 3: split file into lines
     * 4: read first line as header
     * 5: create a map: column name -> column index
     * 6: for each data row:
     * 7:      parse row
     * 8:      read Name
     * 9:      read Type 1 and Type 2
     * 10:     read HP
     * 11:     read Att and Spa
     * 12:     read Def and Spd
     * 13:     read Spe
     * 14:     read BST
     * 15:     create PokemonData
     * 16:     offense = max(Att, Spa)
     * 17:     defense = max(Def, Spd)
     * 18:     add PokemonData to pokemon list
     *
     * RESULTADO ESPERADO
     * ------------------------------------------------------------
     * pokemon = [
     *   PokemonData("Bulbasaur", ...),
     *   PokemonData("Ivysaur", ...),
     *   PokemonData("Venusaur", ...),
     *   ...
     * ]
     *
     * pokemon.Count corresponde a la cantidad de filas válidas.
     */

    [ContextMenu("Load CSV")]
    public void Load()
    {
        // 1: clear pokemon list
        pokemon.Clear();

        // 2: verify that csvFile exists
        if (csvFile == null || string.IsNullOrWhiteSpace(csvFile.text))
        {
            Debug.LogError("[PokemonCSVLoader] El archivo CSV no está asignado o está vacío.");
            return;
        }

        // 3: split file into lines
        string[] lines = csvFile.text.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return;

        // 4: read first line as header
        List<string> headers = ParseCsvLine(lines[0]);
        // 5: create a map: column name -> column index
        Dictionary<string, int> colMap = BuildColumnMap(headers);

        // 6: for each data row:
        for (int i = 1; i < lines.Length; i++)
        {
            // 7: parse row
            List<string> row = ParseCsvLine(lines[i]);
            if (row.Count == 0) continue;

            // 15: create PokemonData
            PokemonData p = new PokemonData();

            // 8: read Name
            p.name = ReadString(row, colMap["Name"]);
            // 9: read Type 1 and Type 2
            p.type1 = ReadString(row, colMap["Type 1"]);

            if (colMap.ContainsKey("Type 2"))
                p.type2 = ReadString(row, colMap["Type 2"]);

            // 10: read HP
            p.hp = ReadInt(row, colMap["HP"]);

            // 11: read Att and Spa
            int att = ReadInt(row, colMap["Att"]);
            int spa = ReadInt(row, colMap["Spa"]);

            // 12: read Def and Spd
            int def = ReadInt(row, colMap["Def"]);
            int spd = ReadInt(row, colMap["Spd"]);

            // 13: read Spe
            p.speed = ReadInt(row, colMap["Spe"]);

            // 14: read BST
            p.bst = ReadInt(row, colMap["BST"]);

            // 16: offense = max(Att, Spa)
            p.offense = Mathf.Max(att, spa);

            // 17: defense = max(Def, Spd)
            p.defense = Mathf.Max(def, spd);

            // 18: add PokemonData to pokemon list
            pokemon.Add(p);
        }
    }

    // ============================================================
    // INFRAESTRUCTURA ENTREGADA
    // No es necesario modificar los métodos siguientes.
    // ============================================================

    private static Dictionary<string, int> BuildColumnMap(List<string> headers)
    {
        Dictionary<string, int> result =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < headers.Count; i++)
        {
            string header = headers[i].Trim();

            if (!result.ContainsKey(header))
                result.Add(header, i);
        }

        return result;
    }

    private static string ReadString(List<string> values, int index)
    {
        if (index < 0 || index >= values.Count)
            return string.Empty;

        return values[index].Trim();
    }

    private static int ReadInt(List<string> values, int index)
    {
        string raw = ReadString(values, index);

        if (int.TryParse(
            raw,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int value))
        {
            return value;
        }

        return 0;
    }

    /// <summary>
    /// Parser CSV entregado.
    /// Permite leer campos entre comillas y comas internas.
    /// </summary>
    private static List<string> ParseCsvLine(string line)
    {
        List<string> values = new List<string>();
        StringBuilder current = new StringBuilder();

        bool insideQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (insideQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    insideQuotes = !insideQuotes;
                }
            }
            else if (c == ',' && !insideQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        values.Add(current.ToString());

        return values;
    }
}