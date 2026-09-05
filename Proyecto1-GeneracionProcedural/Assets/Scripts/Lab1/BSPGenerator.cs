using System.Collections.Generic;
using UnityEngine;

// Clase que representa cada subdivisión del terreno
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

    // Intenta dividir la hoja en dos
    public bool Split(int minLeafSize)
    {
        // Si ya está dividida, cancela
        if (leftChild != null || rightChild != null) return false;

        // Determina la dirección del corte (horizontal o vertical)
        bool splitHorizontal = Random.value > 0.5f;
        if (width > length && (float)width / length >= 1.25f) splitHorizontal = false;
        else if (length > width && (float)length / width >= 1.25f) splitHorizontal = true;

        // Determina el tamaño máximo que puede tener el corte
        int max = (splitHorizontal ? length : width) - minLeafSize;
        if (max <= minLeafSize) return false; // El área es muy pequeña para dividirla

        // Realiza el corte
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
    [Header("Configuración BSP")]
    public int mapWidth = 100;
    public int mapLength = 100;
    public int minLeafSize = 15;
    public int maxLeafSize = 40;

    [Header("Referencias")]
    public GameObject[] housePrefabs; // Asigna aquí tus modelos 3D de casas
    public Terrain targetTerrain;     // El terreno generado por el Perlin Noise

    private List<BSPLeaf> leaves = new List<BSPLeaf>();
    private List<GameObject> spawnedBuildings = new List<GameObject>();

    public void GenerateVillage()
    {
        ClearVillage();

        // 1. Iniciar la raíz del BSP
        BSPLeaf root = new BSPLeaf(0, 0, mapWidth, mapLength);
        leaves.Add(root);

        // 2. Ejecutar las divisiones recursivamente
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

        // 3. Instanciar las casas en los lotes finales
        PlaceBuildings();
    }
    private void PlaceBuildings()
    {
        if (housePrefabs == null || housePrefabs.Length == 0 || targetTerrain == null) return;

        // 1. Buscar o crear el objeto contenedor "Houses"
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
                float randomX = leaf.x + Random.Range(2f, leaf.width - 2f);
                float randomZ = leaf.y + Random.Range(2f, leaf.length - 2f);

                float heightY = targetTerrain.SampleHeight(new Vector3(randomX, 0, randomZ)) + targetTerrain.transform.position.y;

                Vector3 spawnPosition = new Vector3(randomX, heightY, randomZ);

                GameObject prefabToSpawn = housePrefabs[Random.Range(0, housePrefabs.Length)];
                GameObject newHouse = Instantiate(prefabToSpawn, spawnPosition, Quaternion.Euler(0, Random.Range(0, 360), 0));

                // 2. Asignar la nueva casa como hija del contenedor "Houses"
                newHouse.transform.SetParent(housesContainer);
                spawnedBuildings.Add(newHouse);
            }
        }
    }

    public void ClearVillage()
    {
        leaves.Clear();
        spawnedBuildings.Clear();

        // 3. Eliminar el contenedor completo de una sola vez
        Transform housesContainer = transform.Find("Houses");
        if (housesContainer != null)
        {
            DestroyImmediate(housesContainer.gameObject);
        }
    }
}