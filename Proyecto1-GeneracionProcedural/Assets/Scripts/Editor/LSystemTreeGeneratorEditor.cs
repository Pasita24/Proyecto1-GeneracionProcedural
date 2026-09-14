using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LSystemTreeGenerator))]
public class LSystemTreeGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        LSystemTreeGenerator generator = (LSystemTreeGenerator)target;

        EditorGUILayout.Space(15);

        if (GUILayout.Button("Generar Bosque", GUILayout.Height(30)))
        {
            generator.GenerateForest();
            EditorUtility.SetDirty(generator);
        }

        if (GUILayout.Button("Limpiar Bosque"))
        {
            generator.ClearForest();
            EditorUtility.SetDirty(generator);
        }
    }
}