using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct TreeLSystemRule
{
    public char symbol;
    public string replacement;
}

public class LSystemTreeGenerator : MonoBehaviour
{
    [Header("Configuración de Posicionamiento")]
    public Terrain targetTerrain;
    public int numberOfTrees = 20;
    public float edgeMargin = 5f;

    [Header("Prefabs Low-Poly")]
    public GameObject branchPrefab;
    public GameObject leafPrefab;

    [Header("Parámetros del L-System")]
    public string axiom = "F";
    public int iterations = 3;
    public float angle = 25f;
    public float branchLength = 1f;
    public float branchWidth = 0.2f;
    [Range(0.5f, 1f)] public float lengthReduction = 0.8f;
    [Range(0.5f, 1f)] public float widthReduction = 0.7f;

    public List<TreeLSystemRule> rules = new List<TreeLSystemRule>();

    private List<GameObject> spawnedTrees = new List<GameObject>();

    private class TransformInfo
    {
        public Vector3 position;
        public Quaternion rotation;
        public float length;
        public float width;
    }

    [ContextMenu("Generar Bosque")]
    public void GenerateForest()
    {
        ClearForest();

        if (targetTerrain == null || branchPrefab == null)
        {
            Debug.LogWarning("Faltan referencias en LSystemTreeGenerator.");
            return;
        }

        Transform treesContainer = transform.Find("Trees");
        if (treesContainer == null)
        {
            GameObject containerObj = new GameObject("Trees");
            containerObj.transform.SetParent(this.transform);
            containerObj.transform.localPosition = Vector3.zero;
            treesContainer = containerObj.transform;
        }

        string lSystemString = GenerateLSystemString();

        for (int i = 0; i < numberOfTrees; i++)
        {
            float terrainWidth = targetTerrain.terrainData.size.x;
            float terrainLength = targetTerrain.terrainData.size.z;

            float randomX = Random.Range(edgeMargin, terrainWidth - edgeMargin);
            float randomZ = Random.Range(edgeMargin, terrainLength - edgeMargin);

            float heightY = targetTerrain.SampleHeight(new Vector3(randomX, 0, randomZ)) + targetTerrain.transform.position.y;
            Vector3 spawnPosition = new Vector3(randomX, heightY, randomZ);

            GameObject newTree = new GameObject("LSystemTree_" + i);
            newTree.transform.position = spawnPosition;
            newTree.transform.SetParent(treesContainer);
            // Rotación base aleatoria en el eje Y para variedad en el bosque
            newTree.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

            DrawTree(lSystemString, newTree.transform);
            spawnedTrees.Add(newTree);
        }
    }

    private string GenerateLSystemString()
    {
        string currentString = axiom;

        for (int i = 0; i < iterations; i++)
        {
            string nextString = "";
            foreach (char c in currentString)
            {
                bool ruleApplied = false;
                foreach (TreeLSystemRule rule in rules)
                {
                    if (c == rule.symbol)
                    {
                        nextString += rule.replacement;
                        ruleApplied = true;
                        break;
                    }
                }
                if (!ruleApplied) nextString += c;
            }
            currentString = nextString;
        }
        return currentString;
    }

    private void DrawTree(string lString, Transform treeRoot)
    {
        Stack<TransformInfo> transformStack = new Stack<TransformInfo>();

        Vector3 currentPosition = treeRoot.position;
        Quaternion currentRotation = treeRoot.rotation;
        float currentLength = branchLength;
        float currentWidth = branchWidth;

        foreach (char c in lString)
        {
            switch (c)
            {
                case 'F':
                    Vector3 initialPosition = currentPosition;
                    currentPosition += currentRotation * Vector3.up * currentLength;

                    GameObject branch = Instantiate(branchPrefab, initialPosition, currentRotation);
                    branch.transform.SetParent(treeRoot);
                    branch.transform.localScale = new Vector3(currentWidth, currentLength, currentWidth);
                    branch.transform.position = initialPosition + (currentRotation * Vector3.up * (currentLength / 2f));
                    break;

                case '+':
                    // Rotación 3D real: usa ejes locales combinados con un giro aleatorio en Corona (Roll)
                    currentRotation = currentRotation * Quaternion.Euler(angle, Random.Range(-20f, 20f), Random.Range(-40f, 40f));
                    break;

                case '-':
                    currentRotation = currentRotation * Quaternion.Euler(-angle, Random.Range(-20f, 20f), Random.Range(-40f, 40f));
                    break;

                case '\\':
                    currentRotation = currentRotation * Quaternion.Euler(0, 0, angle);
                    break;

                case '/':
                    currentRotation = currentRotation * Quaternion.Euler(0, 0, -angle);
                    break;

                case '[':
                    transformStack.Push(new TransformInfo
                    {
                        position = currentPosition,
                        rotation = currentRotation,
                        length = currentLength,
                        width = currentWidth
                    });
                    currentLength *= lengthReduction;
                    currentWidth *= widthReduction;
                    break;

                case ']':
                    if (leafPrefab != null)
                    {
                        GameObject leaf = Instantiate(leafPrefab, currentPosition, currentRotation);
                        leaf.transform.SetParent(treeRoot);
                    }

                    TransformInfo ti = transformStack.Pop();
                    currentPosition = ti.position;
                    currentRotation = ti.rotation;
                    currentLength = ti.length;
                    currentWidth = ti.width;
                    break;
            }
        }
    }

    [ContextMenu("Limpiar Bosque")]
    public void ClearForest()
    {
        spawnedTrees.Clear();
        Transform treesContainer = transform.Find("Trees");
        if (treesContainer != null) DestroyImmediate(treesContainer.gameObject);
    }
}