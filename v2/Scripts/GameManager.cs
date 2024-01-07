using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    // #########################################
    //  SHEEP
    // #########################################
    [Header("Sheep settings")]
    public int sheepNumber;
    public float sheepRotationSpeed;
    public GameObject sheepPrefab;
    public float sheepObstacleDist;
    public float obstacleVectStrength;
    public float sheepLength;
    public int sheepVisionAngle;
    public float sheepHearDist;
    [HideInInspector]
    public List<GameObject> sheepList;

    // spawn boundaries
    private float minSpawnX = -20.0f;
    private float maxSpawnX = 30.0f;
    private float minSpawnZ = -30.0f;
    private float maxSpawnZ = 10.0f;

    // #########################################
    //  DOG
    // #########################################
    [Header("Dog settings")]
    public int dogNumber;
    public float dogRotationSpeed;
    public float dogSpeed;
    public float minDistToSheep;
    public float maxDistToSheep;
    public int dogVisionAngle;
    public float dogLength;
    public GameObject dogPrefab;
    [HideInInspector]
    public List<GameObject> dogList;

    // spawn boundaries
    private float minSpawnXDog = -50.0f;
    private float maxSpawnXDog = -40.0f;
    private float minSpawnZDog = -10.0f;
    private float maxSpawnZDog = 10.0f;

    // #########################################
    //  OBSTACLES
    // #########################################
    [Header("Obstacles settings")]
    public List<GameObject> treeList;
    public List<GameObject> fenceList;

    // #########################################
    //  MODELS
    // #########################################
    [HideInInspector]
    public Ginelli ginelli;
    [HideInInspector]
    public Strombom strombom;

    // #########################################
    //  OTHERS
    // #########################################
    [Header("Goal")]
    public GameObject goal;


    void Start()
    {
        sheepList = new List<GameObject>(sheepNumber);
        spawnSheep(sheepNumber);

        dogList = new List<GameObject>(dogNumber);
        spawnDogs(dogNumber);

        ginelli = new Ginelli(sheepNumber);
        strombom = new Strombom(sheepNumber);

        // find all tree game objects
        GameObject[] trees = GameObject.FindGameObjectsWithTag("Tree");
        foreach (GameObject t in trees) {
            treeList.Add(t);
        }

        // find all fence game objects
        GameObject[] fences = GameObject.FindGameObjectsWithTag("Fence");
        foreach (GameObject f in fences)
        {
            fenceList.Add(f);
        }
    }

    void Update()
    {
        
    }

    void spawnSheep(int amount)
    {
        int i = 0;
        while (i < amount)
        {
            Vector3 position = new Vector3(Random.Range(minSpawnX, maxSpawnX), .0f, Random.Range(minSpawnZ, maxSpawnZ));

            // check if some object already on this position
            if (!Physics.CheckSphere(position, 1.0f, 1 << 8))
            {
                sheepList.Add(Instantiate(sheepPrefab, position, Quaternion.identity));
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
                dogList.Add(Instantiate(dogPrefab, position, Quaternion.identity));
                i += 1;
            }
        }
    }
}

public class Ginelli {
    public float v1 = 0.15f;
    public float v2 = 1.5f;
    public float tau1_0 = 8; //τ
    public float tau0_1 = 35;
    public float tau01_2;
    public float tau2_0;
    public float dS = 6.3f;
    public float dR = 31.6f;
    public float re = 1;
    public float r0 = 1;
    public float alpha = 15; //[5,25]
    public float beta = 0.8f;
    public float delta = 4; //[2,5]
    public float eta = 0.13f; //η
    public Ginelli(int N)
    {
        tau01_2 = N;
        tau2_0 = N;
    }
}

public class Strombom
{
    public float ro_a = 2;  //ρ repulsion from other agents
    public float c = 1.05f; // attraction to n nearest neighbours 1.05
    public float ro_s = 1.1f;   // repulsion from shepherd
    public float h = 0.5f;
    public float e = 0.3f;  // relative strength of angular noise 
    public float rs = 22.5f;   // detection distance
    public float ra = 1;    // agent interaction distance
    public float dogMinDist;
    // public float Pd;    // driving position behind flock - calculated dynamically
    public float Pc;    // collecting position behind furthest agent
    public float beta = 90;     // blinf angle behind shepherd

    public Strombom(int N)
    {
        dogMinDist = 3 * ra;
        // Pd = ra * Mathf.Sqrt(N);
        Pc = ra;
    }
}
