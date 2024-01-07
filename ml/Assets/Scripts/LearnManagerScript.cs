using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;

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

    // spawn boundaries big pasture
    private float minSpawnXSheep = 0;
    private float maxSpawnXSheep = 30.0f;
    private float minSpawnZSheep = -20.0f;
    private float maxSpawnZSheep = 20.0f;

    //// test environment
    //private float minSpawnXSheep = -20;
    //private float maxSpawnXSheep = 40.0f;
    //private float minSpawnZSheep = -40.0f;
    //private float maxSpawnZSheep = 20.0f;

    //// spawn boundaries small pasture
    //private float minSpawnXSheep = -25;
    //private float maxSpawnXSheep = 25.0f;
    //private float minSpawnZSheep = -5.0f;
    //private float maxSpawnZSheep = 20.0f;

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

    // spawn boundaries big pasture
    private float minSpawnXDog = -40.0f;
    private float maxSpawnXDog = -30.0f;
    private float minSpawnZDog = -10.0f;
    private float maxSpawnZDog = 10.0f;

    // test environment
    //private float minSpawnXDog = -48.0f;
    //private float maxSpawnXDog = -40.0f;
    //private float minSpawnZDog = -10.0f;
    //private float maxSpawnZDog = 10.0f;

    //// spawn boundaries small pasture
    //private float minSpawnXDog = -25.0f;
    //private float maxSpawnXDog = 25.0f;
    //private float minSpawnZDog = -28.0f;
    //private float maxSpawnZDog = -25.0f;

    // #########################################
    //  OBSTACLES
    // #########################################
    [HideInInspector]
    public List<GameObject> treeList;
    public GameObject[] fenceList;

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
    public bool saveResults;
    private Mesh planeMesh;

    void Start()
    {
        sheepNumber = (int)Academy.Instance.EnvironmentParameters.GetWithDefault("sheep_number", sheepNumber);

        // load models
        ginelli = new Ginelli(sheepNumber);
        strombom = new Strombom(sheepNumber);

        // calculate plane bounds
        planeMesh = plane.GetComponent<MeshFilter>().mesh;
        Bounds planeBounds = planeMesh.bounds;
        float sizeX = plane.transform.localScale.x * planeBounds.size.x;
        float sizeZ = plane.transform.localScale.z * planeBounds.size.z;

        // define sheep positions
        //minSpawnXSheep = plane.transform.position.x - sizeX / 2 + 2;    //plane.transform.position.x + 1;
        //maxSpawnXSheep = plane.transform.position.x + sizeX / 2 - 2;    //plane.transform.position.x + sizeX / 3;
        //minSpawnZSheep = plane.transform.position.z + 5;                //plane.transform.position.z - sizeZ / 3;
        //maxSpawnZSheep = plane.transform.position.z + sizeZ / 3;        //plane.transform.position.z + sizeZ / 4;

        //// define dog positions
        //minSpawnXDog = plane.transform.position.x - sizeX / 2 + 2;      //plane.transform.position.x - sizeX / 2 + 2;
        //maxSpawnXDog = plane.transform.position.x + sizeX / 2 - 2;      //plane.transform.position.x - 1;
        //minSpawnZDog = plane.transform.position.z - sizeZ / 2 + 2;      //plane.transform.position.z - sizeZ / 2 + 2;
        //maxSpawnZDog = plane.transform.position.z - 5;                  //plane.transform.position.z + sizeZ / 3;

        // spawn sheep
        sheepList = new List<GameObject>(sheepNumber);
        spawnSheep(sheepNumber);

        // spawn dog
        dogList = new List<GameObject>(dogNumber);
        spawnDogs(dogNumber);

        // fences
        fenceList = GameObject.FindGameObjectsWithTag("Fence");
    }

    public void EpisodeBegin()
    {
        // change spawn sites
        if (Random.value >= 0.5)
        {
            float oldVal = minSpawnXSheep;
            minSpawnXSheep = -maxSpawnXSheep;
            maxSpawnXSheep = -oldVal;

            float oldVal2 = minSpawnXDog;
            minSpawnXDog = -maxSpawnXDog;
            maxSpawnXDog = -oldVal2;
        }

        // disable all sheep
        foreach (GameObject s in sheepList)
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
