using UnityEditor;
using UnityEngine;

// Vinculamos este editor personalizado al script BSPGenerator
[CustomEditor(typeof(BSPGenerator))]
public class BSPGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Dibuja la interfaz por defecto (muestra las variables públicas como prefabs, tamaño, etc.)
        DrawDefaultInspector();

        // Obtenemos la referencia al script que tiene este componente
        BSPGenerator generator = (BSPGenerator)target;

        EditorGUILayout.Space(15);

        // Creamos el botón de Generar
        if (GUILayout.Button("Generate Village", GUILayout.Height(30)))
        {
            generator.GenerateVillage();

            // Marca el objeto como modificado para que Unity guarde los cambios en la escena
            EditorUtility.SetDirty(generator);
        }

        // Creamos el botón para limpiar la aldea
        if (GUILayout.Button("Clear Village"))
        {
            generator.ClearVillage();
            EditorUtility.SetDirty(generator);
        }
    }
}