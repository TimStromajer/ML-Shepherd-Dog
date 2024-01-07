using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

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
    private Vector3 sheepGcm;
    public bool sheepWithMemory;
    public bool sheepWithLimited;

    // spawn boundaries
    private float minSpawnX = -20.0f;
    private float maxSpawnX = 40.0f;
    private float minSpawnZ = -40.0f;
    private float maxSpawnZ = 20.0f;

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
    public bool dogWithMemory;
    public bool dogWithLimited;

    // spawn boundaries
    private float minSpawnXDog = -48.0f;
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
    //  GOAL
    // #########################################
    [Header("Goal")]
    public GameObject goal;

    // #########################################
    //  SIMULATION
    // #########################################
    public bool saveResults;
    public bool saveFPS;
    float simTime;
    float collectTime;
    bool herding;
    StreamWriter writer;
    StreamWriter FPSwriter;
    string path;
    string FPSpath;
    int simNum;
    int simCount;
    float maxSimTime;

    float timer;
    float frameCount;

    void Start()
    {
        timer = 0;
        frameCount = 0;
        simNum = 10;
        simCount = 0;
        maxSimTime = 1000;

        // check for ____
        if (!dogWithLimited && dogWithMemory)
        {
            print("Not limited dog cannot have memory.");
            dogWithMemory = false;
        }
        if (!sheepWithLimited && sheepWithMemory)
        {
            print("Not limited sheep cannot have memory.");
            sheepWithMemory = false;
        }

        // find all tree game objects
        GameObject[] trees = GameObject.FindGameObjectsWithTag("Tree");
        foreach (GameObject t in trees)
        {
            treeList.Add(t);
        }

        // find all fence game objects
        GameObject[] fences = GameObject.FindGameObjectsWithTag("Fence");
        foreach (GameObject f in fences)
        {
            fenceList.Add(f);
        }

        if (saveResults)
        {
            path = "Assets/Results/" + (sheepWithMemory ? "M" : "N") + (sheepWithLimited ? "L" : "A") + sheepNumber + "_" + (dogWithMemory ? "M" : "N") + (dogWithLimited ? "L" : "A") + dogNumber + ".txt";
            if (!System.IO.File.Exists(path))
            {
                writer = new StreamWriter(path, true);
                writer.WriteLine("sheep count; dog count; collection time; herding time");
                writer.Close();
            }
        }

        if (saveFPS)
        {
            FPSpath = "Assets/Results/" + (sheepWithMemory ? "M" : "N") + (sheepWithLimited ? "L" : "A") + sheepNumber + "_" + (dogWithMemory ? "M" : "N") + (dogWithLimited ? "L" : "A") + dogNumber + "_FPS.txt";
            FPSwriter = new StreamWriter(FPSpath, true);
            if (!System.IO.File.Exists(FPSpath))
            {
                FPSwriter.WriteLine("FPS");
            }
        }

        startSim();
    }

    void Update()
    {
        if (saveFPS)
        {
            FPScount();
        }

        simTime += Time.deltaTime;
        if (simTime >= maxSimTime)
        {
            if (saveResults)
            {
                writer.WriteLine(sheepList.Count + "; " + dogList.Count + "; /; /");
                writer.Close();
            }
                
            cleanup();
            startSim();
            return;
        }

        sheepGcm = calculateGCM(sheepList);

        float fN = strombom.ra * Mathf.Pow(sheepList.Count, 2f / 3f);

        if (!herding)
        {
            bool collecting = false;
            foreach (GameObject s in sheepList)
            {
                if (Vector3.Distance(s.transform.position, sheepGcm) > fN)
                {
                    collecting = true;
                }
            }
            if (!collecting)
            {
                herding = true;
                collectTime = simTime;
            }
        }

        if (Vector3.Distance(sheepGcm, goal.transform.position) < 10)
        {
            print("SIM END. Sheep num: " + sheepList.Count + ", dog num: " + dogList.Count + ", collection time: " + collectTime +  ", Time: " + simTime);

            if (saveResults)
            {
                writer.WriteLine(sheepList.Count + "; " + dogList.Count + "; " + collectTime + "; " + simTime);
                writer.Close();
            }
                
            cleanup();
            startSim();
        }

    }

    float FPSstep = 10;
    float FPSstepTimer = 0;
    private void FPScount()
    {
        timer += Time.deltaTime;
        FPSstepTimer += Time.deltaTime;
        float currentFPS = 1.0f / Time.deltaTime;
        currentFPS = Time.frameCount / Time.time;
        frameCount += 1;

        if (FPSstepTimer >= FPSstep)
        {
            FPSstepTimer = 0;
            FPSwriter.WriteLine(currentFPS);
        }

        if (timer >= 100 && timer < 101)
        {
            Debug.Log(frameCount / 100);
            FPSwriter.Close();
        }
    }

    private void startSim()
    {
        if (simCount == simNum)
        {
            newSimCount();
            simCount = 0;
        } else
        {
            simCount += 1;
        }

        sheepList = new List<GameObject>(sheepNumber);
        spawnSheep(sheepNumber);

        dogList = new List<GameObject>(dogNumber);
        spawnDogs(dogNumber);

        ginelli = new Ginelli(sheepNumber);
        strombom = new Strombom(sheepNumber);

        simTime = 0;
        herding = false;

        if (saveResults)
        {
            writer = new StreamWriter(path, true);
        }
    }

    private void cleanup()
    {
        foreach (GameObject g in sheepList)
        {
            Destroy(g);
        }

        foreach (GameObject g in dogList)
        {
            Destroy(g);
        }
    }

    private void newSimCount()
    {
        sheepNumber += 1;
        if (saveResults)
        {
            path = "Assets/Results/" + (sheepWithMemory ? "M" : "N") + (sheepWithLimited ? "L" : "A") + sheepNumber + "_" + (dogWithMemory ? "M" : "N") + (dogWithLimited ? "L" : "A") + dogNumber + ".txt";
            if (!System.IO.File.Exists(path))
            {
                writer = new StreamWriter(path, true);
                writer.WriteLine("sheep count; dog count; collection time; herding time");
                writer.Close();
            }
        }
    }

    // calculate GCM of the objects in the list
    public Vector3 calculateGCM(List<GameObject> lst)
    {
        Vector3 gcm_ = new Vector3(0, 0, 0);
        foreach (GameObject s in lst)
        {
            gcm_ += s.transform.position;
        }

        gcm_ /= lst.Count;

        return gcm_;
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
    public float r0 = 20f;
    public float alpha = 15; //[5,25]
    public float beta = 0.8f;
    public float delta = 4; //[2,5]
    public float eta = 0.13f; //η
    public int k = 8; // topological neighbours
    public Ginelli(int N)
    {
        tau01_2 = N;
        tau2_0 = N;
    }
}

public class Strombom
{
    public float ro_a = 1.3f;  //ρ repulsion from other agents
    public float c = 1.1f; // attraction to n nearest neighbours 1.05
    public float ro_s = 1.1f;   // repulsion from shepherd
    public float h = 0.3f;
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
