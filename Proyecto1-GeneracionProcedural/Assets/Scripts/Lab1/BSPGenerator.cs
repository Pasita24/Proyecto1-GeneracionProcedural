using System.Collections.Generic;
using UnityEngine;

public class BSPLeaf
{
    public int x, y, width, length;
    public BSPLeaf leftChild, rightChild;

    public BSPLeaf(int x, int y, int width, int length)
    {
        this.x = x;
        this.y = y;
        this.width = width;
        this.length = length;
    }

    public bool Split(int minLeafSize)
    {
        if (leftChild != null || rightChild != null) return false;

        bool splitHorizontal = Random.value > 0.5f;
        if (width > length && (float)width / length >= 1.25f) splitHorizontal = false;
        else if (length > width && (float)length / width >= 1.25f) splitHorizontal = true;

        int max = (splitHorizontal ? length : width) - minLeafSize;
        if (max <= minLeafSize) return false;

        int split = Random.Range(minLeafSize, max);

        if (splitHorizontal)
        {
            leftChild = new BSPLeaf(x, y, width, split);
            rightChild = new BSPLeaf(x, y + split, width, length - split);
        }
        else
        {
            leftChild = new BSPLeaf(x, y, split, length);
            rightChild = new BSPLeaf(x + split, y, width - split, length);
        }
        return true;
    }
}

public class BSPGenerator : MonoBehaviour
{
    public enum GenerationContext { Valley, Mountain }

    [Header("Contexto de Generación")]
    public GenerationContext context = GenerationContext.Valley;
    public float mountainMinHeight = 10f;

    [Header("Configuración BSP")]
    public int mapWidth = 100;
    public int mapLength = 100;
    public int minLeafSize = 15;
    public int maxLeafSize = 40;

    [Header("Referencias")]
    public GameObject[] housePrefabs;
    public Terrain targetTerrain;

    private List<BSPLeaf> leaves = new List<BSPLeaf>();
    private List<GameObject> spawnedBuildings = new List<GameObject>();

    public void GenerateVillage()
    {
        ClearVillage();
        BSPLeaf root = new BSPLeaf(0, 0, mapWidth, mapLength);
        leaves.Add(root);

        bool didSplit = true;
        while (didSplit)
        {
            didSplit = false;
            for (int i = 0; i < leaves.Count; i++)
            {
                if (leaves[i].leftChild == null && leaves[i].rightChild == null)
                {
                    if (leaves[i].width > maxLeafSize || leaves[i].length > maxLeafSize || Random.value > 0.25f)
                    {
                        if (leaves[i].Split(minLeafSize))
                        {
                            leaves.Add(leaves[i].leftChild);
                            leaves.Add(leaves[i].rightChild);
                            didSplit = true;
                        }
                    }
                }
            }
        }
        PlaceBuildings();
    }

    private void PlaceBuildings()
    {
        if (housePrefabs == null || housePrefabs.Length == 0 || targetTerrain == null) return;

        Transform housesContainer = transform.Find("Houses");
        if (housesContainer == null)
        {
            GameObject containerObj = new GameObject("Houses");
            containerObj.transform.SetParent(this.transform);
            containerObj.transform.localPosition = Vector3.zero;
            housesContainer = containerObj.transform;
        }

        foreach (BSPLeaf leaf in leaves)
        {
            if (leaf.leftChild == null && leaf.rightChild == null)
            {
                bool validSpot = false;
                Vector3 spawnPosition = Vector3.zero;

                if (context == GenerationContext.Mountain)
                {
                    float highestY = float.MinValue;

                    // Muestrea 20 puntos en el lote para encontrar la cima local
                    for (int attempt = 0; attempt < 20; attempt++)
                    {
                        float rx = leaf.x + Random.Range(2f, leaf.width - 2f);
                        float rz = leaf.y + Random.Range(2f, leaf.length - 2f);
                        float hy = targetTerrain.SampleHeight(new Vector3(rx, 0, rz)) + targetTerrain.transform.position.y;

                        // Si este punto es más alto que el anterior evaluado, lo guarda
                        if (hy > highestY)
                        {
                            highestY = hy;

                            // Valida el punto solo si además supera la altura mínima exigida
                            if (highestY >= mountainMinHeight)
                            {
                                validSpot = true;
                                spawnPosition = new Vector3(rx, highestY, rz);
                            }
                        }
                    }
                }
                else // Lógica original para el contexto Valley
                {
                    float randomX = leaf.x + Random.Range(2f, leaf.width - 2f);
                    float randomZ = leaf.y + Random.Range(2f, leaf.length - 2f);
                    float heightY = targetTerrain.SampleHeight(new Vector3(randomX, 0, randomZ)) + targetTerrain.transform.position.y;

                    validSpot = true;
                    spawnPosition = new Vector3(randomX, heightY, randomZ);
                }

                if (!validSpot) continue;

                GameObject prefabToSpawn = housePrefabs[Random.Range(0, housePrefabs.Length)];
                GameObject newHouse = Instantiate(prefabToSpawn, spawnPosition, Quaternion.Euler(0, Random.Range(0, 360), 0));

                newHouse.transform.SetParent(housesContainer);
                spawnedBuildings.Add(newHouse);
            }
        }
    }

    public void ClearVillage()
    {
        leaves.Clear();
        spawnedBuildings.Clear();
        Transform housesContainer = transform.Find("Houses");
        if (housesContainer != null) DestroyImmediate(housesContainer.gameObject);
    }
}