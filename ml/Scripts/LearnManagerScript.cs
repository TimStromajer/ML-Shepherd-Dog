using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LearnManagerScript : MonoBehaviour
{
    // #########################################
    //  SHEEP
    // #########################################
    public int sheepNumber;
    public float sheepRotationSpeed;
    [HideInInspector]
    public List<GameObject> sheepList;
    public GameObject sheepPrefab;
    public float sheepObstacleDist;
    public float obstacleVectStrength;
    public float sheepLength;

    // spawn boundaries
    private float minSpawnXSheep = -10.0f;
    private float maxSpawnXSheep = 10.0f;
    private float minSpawnZSheep = -10.0f;
    private float maxSpawnZSheep = 10.0f;

    // #########################################
    //  DOG
    // #########################################
    public int dogNumber;
    public float dogRotationSpeed;
    public float dogSpeed;
    public float minDistToSheep;
    public float maxDistToSheep;
    [HideInInspector]
    public List<GameObject> dogList;
    public GameObject dogPrefab;

    // spawn boundaries
    private float minSpawnXDog = -30.0f;
    private float maxSpawnXDog = -20.0f;
    private float minSpawnZDog = -10.0f;
    private float maxSpawnZDog = 10.0f;

    // #########################################
    //  OBSTACLES
    // #########################################
    [HideInInspector]
    public List<GameObject> treeList;
    public List<GameObject> fenceList;

    // #########################################
    //  MODELS
    // #########################################
    public Ginelli ginelli;
    public Strombom strombom;

    // #########################################
    //  OTHERS
    // #########################################
    public GameObject goal;
    public GameObject plane;
    private Mesh planeMesh;

    void Start()
    {
        // load models
        ginelli = new Ginelli(sheepNumber);
        strombom = new Strombom(sheepNumber);

        // calculate plane bounds
        planeMesh = plane.GetComponent<MeshFilter>().mesh;
        Bounds planeBounds = planeMesh.bounds;
        float sizeX = plane.transform.localScale.x * planeBounds.size.x;
        float sizeZ = plane.transform.localScale.z * planeBounds.size.z;

        // define sheep positions
        minSpawnXSheep = plane.transform.position.x - sizeX / 2 + 2;    //plane.transform.position.x + 1;
        maxSpawnXSheep = plane.transform.position.x + sizeX / 2 - 2;    //plane.transform.position.x + sizeX / 3;
        minSpawnZSheep = plane.transform.position.z + 5;                //plane.transform.position.z - sizeZ / 3;
        maxSpawnZSheep = plane.transform.position.z + sizeZ / 3;        //plane.transform.position.z + sizeZ / 4;

        // define dog positions
        minSpawnXDog = plane.transform.position.x - sizeX / 2 + 2;      //plane.transform.position.x - sizeX / 2 + 2;
        maxSpawnXDog = plane.transform.position.x + sizeX / 2 - 2;      //plane.transform.position.x - 1;
        minSpawnZDog = plane.transform.position.z - sizeZ / 2 + 2;      //plane.transform.position.z - sizeZ / 2 + 2;
        maxSpawnZDog = plane.transform.position.z - 5;                  //plane.transform.position.z + sizeZ / 3;

        // spawn sheep
        sheepList = new List<GameObject>(sheepNumber);
        spawnSheep(sheepNumber);

        // spawn dog
        dogList = new List<GameObject>(dogNumber);
        spawnDogs(dogNumber);
    }

    public void EpisodeBegin()
    {
        // disable all sheep
        foreach(GameObject s in sheepList)
        {
            s.SetActive(false);
        }

        // get new sheep position and enable them
        foreach (GameObject s in sheepList)
        {
            bool positionFound = false;

            while (!positionFound)
            {
                Vector3 position = new Vector3(Random.Range(minSpawnXSheep, maxSpawnXSheep), .0f, Random.Range(minSpawnZSheep, maxSpawnZSheep));

                // check if some object already on this position
                if (!Physics.CheckSphere(position, 1.0f, 1 << 8))
                {
                    positionFound = true;
                    s.transform.position = position;
                    s.SetActive(true);
                }
            }

        }

        // disable dogs?

        // get new dog position and enable them
        foreach (GameObject d in dogList)
        {
            bool positionFound = false;

            while (!positionFound)
            {
                Vector3 position = new Vector3(Random.Range(minSpawnXDog, maxSpawnXDog), .0f, Random.Range(minSpawnZDog, maxSpawnZDog));

                // check if some object already on this position
                if (!Physics.CheckSphere(position, 1.0f, 1 << 8))
                {
                    positionFound = true;
                    d.transform.position = position;
                    d.SetActive(true);
                }
            }

        }

    }

    void spawnSheep(int amount)
    {
        int i = 0;
        while (i < amount)
        {
            Vector3 position = new Vector3(Random.Range(minSpawnXSheep, maxSpawnXSheep), .0f, Random.Range(minSpawnZSheep, maxSpawnZSheep));

            // check if some object already on this position
            if (!Physics.CheckSphere(position, 1.0f, 1 << 8))
            {
                GameObject sheep = Instantiate(sheepPrefab, position, Quaternion.identity);
                sheep.GetComponent<SheepController>().GM = this;
                sheep.transform.parent = transform.parent;
                sheepList.Add(sheep);
                i += 1;
            }
        }
    }

    void spawnDogs(int amount)
    {
        int i = 0;
        while (i < amount)
        {
            Vector3 position = new Vector3(Random.Range(minSpawnXDog, maxSpawnXDog), .0f, Random.Range(minSpawnZDog, maxSpawnZDog));

            // check if some object already on this position
            if (!Physics.CheckSphere(position, 1.0f, 1 << 8))
            {
                GameObject dog = Instantiate(dogPrefab, position, Quaternion.identity);
                dog.GetComponent<DogControllerMl>().GM = this;
                dog.transform.parent = transform.parent;
                dogList.Add(dog);
                i += 1;
            }
        }
    }
}
