using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PathGenerator))]
public class PathGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Muestra las variables públicas del script (Terrain, Prefab, etc.)
        DrawDefaultInspector();

        PathGenerator generator = (PathGenerator)target;

        EditorGUILayout.Space(15);

        // Botón para generar los caminos
        if (GUILayout.Button("Generate Paths", GUILayout.Height(30)))
        {
            generator.GeneratePaths();
            EditorUtility.SetDirty(generator);
        }

        // Botón para limpiar los caminos generados
        if (GUILayout.Button("Clear Paths"))
        {
            generator.ClearPaths();
            EditorUtility.SetDirty(generator);
        }
    }
}