using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.IO;

public class DogControllerMl : Agent
{
    // Dog Animator Controller
    public Animator anim;

    public LearnManagerScript GM;
    private Rigidbody m_Rigidbody;

    private Vector3 gcm;
    private List<GameObject> visibleSheep;

    private Vector3 previousGcm;
    private Vector3 previousDogPos;
    private float previousSummedDist;
    private float previousSummedDistGoal;
    private float eTime = 0;

    // testing
    int previousStepCount;
    StreamWriter writer;
    string path;

    public override void Initialize()
    {
        m_Rigidbody = GetComponent<Rigidbody>();
        previousStepCount = -1;
    }

    public override void OnEpisodeBegin()
    {
        eTime = 0;
        // print("EPISODE: " + CompletedEpisodes);
        if  (CompletedEpisodes == 100)
        {
            print("100 episodes completed.");
        }

        string testType = "vision_LI2";
        path = "Assets/Results/" + testType + "_" + GM.sheepNumber + "_" + GM.dogNumber + ".txt";

        if (GM.saveResults)
        {
            if (!System.IO.File.Exists(path))
            {
                writer = new StreamWriter(path, true);
                writer.WriteLine("Total time steps");
                writer.Close();
            }
        }

        GM.EpisodeBegin();

        // add visible sheep
        visibleSheep = new List<GameObject>();
        foreach (GameObject s in GM.sheepList)
        {
            visibleSheep.Add(s);
        }

        previousGcm = calculateGCM(visibleSheep);
        previousDogPos = transform.position;
        previousSummedDist = Mathf.Infinity;

        if (previousStepCount != -1 && GM.saveResults)
        {
            print("Previous steps: " + previousStepCount);
            writer = new StreamWriter(path, true);
            writer.WriteLine(previousStepCount);
            writer.Close();
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float move = 0;
        float rotate = 0;

        //move = actions.ContinuousActions[0];
        //rotate = actions.ContinuousActions[1];

        int moveA = actions.DiscreteActions[0];
        int rotateA = actions.DiscreteActions[1];

        if (moveA == 0)
        {
            move = 0;
        } else if (moveA == 1)
        {
            move = 1;
        }

        if (rotateA == 1)
        {
            rotate = -1;
        }
        else if (rotateA == 0)
        {
            rotate = 0;
        }
        else if (rotateA == 2)
        {
            rotate = 1;
        }

        // move shepherd
        transform.Translate(transform.forward * Time.deltaTime * GM.dogSpeed * move, Space.World);

        // rotate shepherd
        transform.Rotate(transform.up * GM.dogRotationSpeed * Time.deltaTime * rotate);

        // calculate GCM
        gcm = calculateGCM(visibleSheep);

        // calculate fN
        float fN = GM.strombom.ra * Mathf.Pow(visibleSheep.Count, 2f / 3f);

        // summed dist from sheep to gcm
        float summedDist = 0;
        foreach (GameObject s in GM.sheepList)
        {
            summedDist += Vector3.Distance(s.transform.position, gcm);
        }

        // summed dist from sheep to goal
        float summedDistGoal = 0;
        foreach (GameObject s in GM.sheepList)
        {
            summedDistGoal += Vector3.Distance(s.transform.position, GM.goal.transform.position);
        }

        // negative reward each time step
        AddReward(-0.004f);

        // if gcm close to any fence -> minus reward
        //foreach (GameObject f in GM.fenceList)
        //{
        //    if (Vector3.Distance(gcm, f.transform.position) <= 5)
        //    {
        //        AddReward(-0.05f);
        //        break;
        //    }
        //}

        // if dog closer to gcm add reward, if same reward is 0, if less negative reward
        float distDiff = Vector3.Distance(previousGcm, previousDogPos) - Vector3.Distance(gcm, transform.position);
        if (Vector3.Distance(gcm, transform.position) < Vector3.Distance(previousGcm, previousDogPos) && distDiff > 0.08f)
        {
            AddReward(0.01f);
        }
        else if (Vector3.Distance(gcm, transform.position) > Vector3.Distance(previousGcm, previousDogPos))
        {
            AddReward(-0.01f);
        }

        //// end episode if close to the gcm
        //if (Vector3.Distance(gcm, transform.position) < 2)
        //{
        //    AddReward(10f);
        //    EndEpisode();
        //}

        //// end episode if close to the gcm
        //if (summedDist < 3 * GM.sheepNumber)
        //{
        //    AddReward(10f);
        //    EndEpisode();
        //}

        if (!allInGCM(visibleSheep, gcm, 8))
        {
            // if sheep closer to gcm then before -> add positive reward else add negative reward
            float distSumDiff = previousSummedDist - summedDist;
            if (summedDist < previousSummedDist && distSumDiff > 0.02)
            {
                AddReward(0.02f);
            }
            else if (summedDist > previousSummedDist)
            {
                AddReward(-0.02f);
            }
        }
        else
        {
            // if sheep closer to goal then before -> add positive reward else add negative reward
            float distGoalDiff = previousSummedDistGoal - summedDistGoal;
            if (Vector3.Distance(gcm, GM.goal.transform.position) < Vector3.Distance(previousGcm, GM.goal.transform.position) && distGoalDiff > 0.02f)
            {
                AddReward(0.02f);
            }
            else if (Vector3.Distance(gcm, GM.goal.transform.position) > Vector3.Distance(previousGcm, GM.goal.transform.position))
            {
                AddReward(-0.02f);
            }
        }

        //// if sheep closer to goal then before -> add positive reward else add negative reward
        //float distGoalDiff = previousSummedDistGoal - summedDistGoal;
        //if (Vector3.Distance(gcm, GM.goal.transform.position) < Vector3.Distance(previousGcm, GM.goal.transform.position) && distGoalDiff > 0.02f)
        //{
        //    AddReward(0.02f);
        //}
        //else if (Vector3.Distance(gcm, GM.goal.transform.position) > Vector3.Distance(previousGcm, GM.goal.transform.position))
        //{
        //    AddReward(-0.02f);
        //}

        // end episode if gcm close to goal
        if (Vector3.Distance(GM.goal.transform.position, gcm) < 7)
        {
            AddReward(10f);
            EndEpisode();
        }

        // print(GetCumulativeReward());

        // update previous GCM
        previousGcm = gcm;

        // update previous summed dist sheep to goal
        previousSummedDistGoal = summedDistGoal;

        // update previous dog position
        previousDogPos = transform.position;

        // update previous summed distance
        previousSummedDist = summedDist;

        previousStepCount = StepCount;

    }

    // with fence (3+3+3+4) + sheepPart = 13 + 3x
    // (3+3+3) + sheepPart = 9 + 3x
    public override void CollectObservations(VectorSensor sensor)
    {
        // add dog position (3)
        sensor.AddObservation(this.transform.position);

        // add dog direction (3)
        sensor.AddObservation(this.transform.forward);

        // add goal position (3)
        sensor.AddObservation(GM.goal.transform.position);

        // add fence position small x, big x, small z, big z (4)
        //sensor.AddObservation(-30);    // GM.fenceList[120].transform.position.x
        //sensor.AddObservation(30);    // GM.fenceList[80].transform.position.x
        //sensor.AddObservation(-30);    // GM.fenceList[0].transform.position.z
        //sensor.AddObservation(30);    // GM.fenceList[40].transform.position.z

        //// choose one sheep with chance 1/d
        //// add sheep positons (3)
        //GameObject sheep = randomSheep(visibleSheep, transform.position);
        //sensor.AddObservation(sheep.transform.position);

        // choose one sheep with chance 1/d
        // add sheep positons (3)
        //GameObject sheep = randomSheepInverse(visibleSheep, transform.position);
        //sensor.AddObservation(sheep.transform.position);

        // add 7 closest sheep
        // add sheep positons (7 * 3 = 21)
        //List<GameObject> sheepList = nClosest(visibleSheep, transform.position, 3);
        //foreach (GameObject s in sheepList)
        //{
        //    sensor.AddObservation(s.transform.position);
        //}

        // add all sheep
        // add sheep positons(x * 3)
        //foreach (GameObject s in visibleSheep)
        //{
        //    sensor.AddObservation(s.transform.position);
        //}
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        if (v > 0)
        {
            actionsOut.DiscreteActions.Array[0] = 1;
        } else
        {
            actionsOut.DiscreteActions.Array[0] = 0;
        }

        if (h < 0)
        {
            actionsOut.DiscreteActions.Array[1] = 1;
        } else if (h > 0)
        {
            actionsOut.DiscreteActions.Array[1] = 2;
        } else
        {
            actionsOut.DiscreteActions.Array[1] = 0;
        }

        //actionsOut.ContinuousActions.Array[0] = v;
        //actionsOut.ContinuousActions.Array[1] = h;

        float space = Input.GetAxis("Jump");
        if (space != 0)
        {
            print(this.transform.position + ", " + this.transform.forward + ", " + GM.goal.transform.position + ", " + visibleSheep[0].transform.position);
        }
    }

    // check if all objects in lst are dist around gcm or return furthest object
    public bool allInGCM(List<GameObject> lst, Vector3 gcm_, float dist)
    {
        foreach (GameObject s in lst)
        {
            float fromGCM = Vector3.Distance(s.transform.position, gcm_);
            if (fromGCM > dist)
            {
                return false;
            }
        }

        return true;
    }

    // return n closest objects to the pos from the list 
    public List<GameObject> nClosest(List<GameObject> lst, Vector3 pos, int n)
    {
        // sort
        lst.Sort(SortListByDistFromThis);

        // create new list
        List<GameObject> newLst = new List<GameObject>();

        // put closest n in the new list
        for (int i = 0; i < n; i++)
        {
            newLst.Add(lst[i]);
        }

        return newLst;
    }

    private int SortListByDistFromThis(GameObject obj1, GameObject obj2)
    {
        float d1 = Vector3.Distance(transform.position, obj1.transform.position);
        float d2 = Vector3.Distance(transform.position, obj2.transform.position);
        if (d1 < d2)
        {
            return 1;
        }
        else
        {
            return -1;
        }
    }

    // randomly choose sheep from lst with probability 1/d from object pos
    public GameObject randomSheepInverse(List<GameObject> lst, Vector3 pos)
    {
        // return sheep if only one
        if (lst.Count == 1)
        {
            return lst[0];
        }

        // sum all distances
        float sumDist = 0;
        foreach (GameObject s in lst)
        {
            sumDist += Vector3.Distance(transform.position, s.transform.position);
        }

        // convert to probability
        List<float> distances = new List<float>();
        for (int i = 0; i < lst.Count; i++)
        {
            distances.Add(Vector3.Distance(transform.position, lst[i].transform.position) / sumDist);
        }

        // get random value from 0 to 1
        float rand = Random.Range(0f, 1f);

        // decide which value
        float summer = 0;
        GameObject chosenSheep = null;
        for (int i = 0; i < distances.Count; i++)
        {
            summer += distances[i];
            if (rand <= summer)
            {
                chosenSheep = lst[i];
                break;
            }
        }

        return chosenSheep;
    }

    // randomly choose sheep from lst with probability 1/d from object pos
    public GameObject randomSheep(List<GameObject> lst, Vector3 pos)
    {
        // return sheep if only one
        if (lst.Count == 1)
        {
            return lst[0];
        }

        // calculate distances and find max distance
        List<float> distances = new List<float>();
        float maxDist = 0;
        foreach (GameObject s in lst)
        {
            float dist = Vector3.Distance(pos, s.transform.position);
            distances.Add(dist);
            if (dist > maxDist)
            {
                maxDist = dist;
            }
        }

        // convert to probability
        float sumOfAll = 0;
        for (int i = 0; i < distances.Count; i++)
        {
            // avoid zero values
            if (distances[i] == maxDist)
            {
                distances[i] = 1;
            }
            else
            {
                distances[i] = maxDist - distances[i];
            }

            sumOfAll += distances[i];
        }

        // normalize
        for (int i = 0; i < distances.Count; i++)
        {
            distances[i] = distances[i] / sumOfAll;
        }

        // get random value from 0 to 1
        float rand = Random.Range(0f, 1f);

        // decide which value
        float summer = 0;
        GameObject chosenSheep = null;
        for (int i = 0; i < distances.Count; i++)
        {
            summer += distances[i];
            if (rand <= summer)
            {
                chosenSheep = lst[i];
                break;
            }
        }

        return chosenSheep;
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

}
