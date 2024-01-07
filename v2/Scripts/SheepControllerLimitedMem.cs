using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SheepControllerLimitedMem : MonoBehaviour
{
    private GameManager GM;
    private Rigidbody m_Rigidbody;

    public Enums.State state;
    private Quaternion desiredRotation;
    private Vector3 desiredRotationVec;

    private float headingNoise;

    private List<GameObject> dogList;
    private List<GameObject> visibleSheep;    // list of close sheep, TODO: still all sheep

    // Sheeps Animator Controller
    public Animator anim;

    // how often calculate new state
    private float stateCalcTimerWalk = 0;
    private float stateCalcTime = 3;

    // how often change rotation in walk
    private float rotationCalcTimerWalk = 0;
    private float rotationCalcTime = 1;

    // how often change rotation in run
    private float rotationCalcTimerRun = 0;
    private float rotationCalcRun = 0.1f;

    // memory - sheep pos, time
    private IDictionary<GameObject, float> sheepMem;
    private float stayInMem = 3;

    void Start()
    {
        GM = FindObjectOfType<GameManager>();
        m_Rigidbody = GetComponent<Rigidbody>();

        dogList = getVisibleAndHeardDogs(GM.dogList, GM.sheepVisionAngle, GM.sheepHearDist);
        visibleSheep = getVisibleAndHeardSheep(GM.sheepList, GM.sheepVisionAngle, GM.sheepHearDist);

        state = Enums.State.Idle;
        anim.SetBool("IsIdle", true);

        // to perform calculation at the beginning
        stateCalcTimerWalk = stateCalcTime;
        rotationCalcTimerWalk = rotationCalcTime;

        // init sheep memory
        sheepMem = new Dictionary<GameObject, float>();
    }

    void Update()
    {
        // timer for calculating idle/walk state
        stateCalcTimerWalk += Time.deltaTime;

        // copy sheep memory to be able to remove from main memory
        Dictionary<GameObject, float> sheepMemCopy = new Dictionary<GameObject, float>(sheepMem);

        // subtract time from memory and remove if necessary
        foreach (var s in sheepMemCopy)
        {
            sheepMem[s.Key] -= Time.deltaTime;
            if (sheepMem[s.Key] <= 0)
            {
                sheepMem.Remove(s.Key);
            }
        }

        // get visible dogs
        dogList = getVisibleAndHeardDogs(GM.dogList, GM.sheepVisionAngle, GM.sheepHearDist);

        // get visible sheep
        visibleSheep = getVisibleAndHeardSheep(GM.sheepList, GM.sheepVisionAngle, GM.sheepHearDist);
        // add sheep to memory
        visibleSheep = addToMemory(visibleSheep);

        // check if a dog is near
        bool dogClose = false;
        foreach (GameObject s in dogList)
        {
            if (Vector3.Distance(s.transform.position, transform.position) < GM.strombom.rs)
            {
                dogClose = true;
            }
        }

        // Decide next state: if dog close go to run state, otherwise calculate between idle and walk
        if (dogClose)
        {
            state = Enums.State.Run;
            anim.SetBool("IsIdle", false);
            anim.SetBool("IsRunning", true);
        }
        else
        {
            // new state (walk/idle) calculation
            if (stateCalcTimerWalk > stateCalcTime)
            {
                computeNewState();
                stateCalcTimerWalk = 0;
            }
        }

        // move the sheep according to state
        if (state == Enums.State.Walk)
        {
            Walk();
        }
        else if(state == Enums.State.Run)
        {
            Run();
        }
    }

    void Walk()
    {
		// TODO: move to update?
        rotationCalcTimerWalk += Time.deltaTime;
		
        if (rotationCalcTimerWalk > rotationCalcTime)
        {
            // calculate average rotation of neighbouring sheep
            Quaternion neighbourHeading = Quaternion.identity;
            float averageWeight = 1f / visibleSheep.Count;
            foreach (GameObject s in visibleSheep)
            {
                Quaternion heading = s.transform.rotation;
                neighbourHeading *= Quaternion.Slerp(Quaternion.identity, heading, averageWeight);
            }

            // rotate sheep
            float psi = Random.Range(-GM.ginelli.eta * 180, GM.ginelli.eta * 180); //[-23.4, 23.4]
            neighbourHeading = Quaternion.Euler(0, neighbourHeading.eulerAngles.y + psi, 0);
            desiredRotation = neighbourHeading;

            rotationCalcTimerWalk = 0;
        }
        
		// rotate sheep
        transform.rotation = Quaternion.Lerp(transform.rotation, desiredRotation, GM.sheepRotationSpeed * Time.deltaTime);

        // move sheep
        transform.Translate(transform.forward * Time.deltaTime * GM.ginelli.v1, Space.World);
    }

    void Run()
    {
		// TODO: move in update?
        rotationCalcTimerRun += Time.deltaTime;

        // if any dog is really close -> make sheep faster
        float dogVeryClose = 1;

        if (rotationCalcTimerRun > rotationCalcRun)
        {
            // get sum vector from each close dog
            Vector3 dogVector = new Vector3(0, 0, 0);
            foreach (GameObject s in dogList)
            {
                if (Vector3.Distance(s.transform.position, transform.position) < GM.strombom.rs)
                {
                    dogVector += Vector3.Normalize(transform.position - s.transform.position);

                    // check if dog very close
                    if (Vector3.Distance(s.transform.position, transform.position) <= 5)
                    {
                        dogVeryClose = 1.2f;
                    }
                }
            }
            dogVector = Vector3.Normalize(dogVector);

            // get sum vector from each close sheep and gcm vector of close sheep
            Vector3 sheepVector = new Vector3(0, 0, 0);
            Vector3 gcm_ = new Vector3(0, 0, 0);
            foreach (GameObject s in visibleSheep)
            {
                if (Vector3.Distance(s.transform.position, transform.position) < GM.strombom.ra)
                {
                    sheepVector += Vector3.Normalize(transform.position - s.transform.position);
                }

                gcm_ += s.transform.position;
            }
            sheepVector = Vector3.Normalize(sheepVector);
            gcm_ /= visibleSheep.Count;

            Vector3 gcmVector = Vector3.Normalize(gcm_ - transform.position);

            // get sum vector from each close obstacle (trees and fences)
            Vector3 obstacleVect = new Vector3(0, 0, 0);
            foreach (GameObject s in GM.treeList)
            {
                if (Vector3.Distance(s.transform.position, transform.position) < GM.sheepObstacleDist)
                {
                    obstacleVect += Vector3.Normalize(transform.position - s.transform.position);
                }
            }
            foreach (GameObject s in GM.fenceList)
            {
                if (Vector3.Distance(s.transform.position, transform.position) < GM.sheepObstacleDist)
                {
                    obstacleVect += Vector3.Normalize(transform.position - s.transform.position);
                }
            }
            obstacleVect = new Vector3(obstacleVect.x, 0, obstacleVect.z);
            obstacleVect = Vector3.Normalize(obstacleVect);

            // add ratio to sheep repulsion from shepherd according to distance
            // float distRatio = (dist - GM.minDistToSheep) / (GM.maxDistToSheep - GM.minDistToSheep);

            // calculate final vector, TODO: add inertia vector and error term
            Vector3 final = dogVector * GM.strombom.ro_s + sheepVector * GM.strombom.ro_a + gcmVector * GM.strombom.c + obstacleVect * GM.obstacleVectStrength;
            final = Vector3.Normalize(final);

            desiredRotationVec = final;
            desiredRotation = Quaternion.LookRotation(final, Vector3.up);
        }

        // rotate sheep
        // transform.rotation = Quaternion.Lerp(transform.rotation, desiredRotation, GM.sheepRotationSpeed * Time.deltaTime);
        transform.rotation = Quaternion.LookRotation(Vector3.RotateTowards(transform.forward, desiredRotationVec, GM.sheepRotationSpeed * Time.deltaTime, 0.0f));

        // move sheep
        transform.Translate(transform.forward * Time.deltaTime * GM.ginelli.v2 * dogVeryClose, Space.World);

        // force sheep to the ground
        transform.position = new Vector3(transform.position.x, 0, transform.position.z);
    }

    void computeNewState()
    {
        if (state == Enums.State.Run)
        {
            int idleState = 0;
            float meanDist = 0;
            float topologicalCount = 0;

            // go through all topological sheep
            foreach (GameObject s in visibleSheep)
            {
                if (Vector3.Distance(transform.position, s.transform.position) < GM.sheepHearDist)
                {
                    topologicalCount += 1;

                    // add mean dist
                    meanDist += Vector3.Distance(transform.position, s.transform.position);

                    if (s.GetComponent<SheepControllerLimitedMem>().state == Enums.State.Idle)
                    {
                        idleState += 1;
                    }
                }

            }
            meanDist = meanDist / topologicalCount;

            // calculate porbability
            float prob = (1 / topologicalCount) * ((GM.ginelli.dS / meanDist) * Mathf.Pow(1 + GM.ginelli.alpha * idleState, GM.ginelli.delta));

            // potentially change state
            if (Random.Range(0, 1) < prob)
            {
                state = Enums.State.Idle;
                anim.SetBool("IsRunning", false);
                anim.SetBool("IsIdle", true);
            }
        }
        else if (state == Enums.State.Idle)
        {
            // get state of each neighbour
            int walkState = 0;
            foreach (GameObject s in visibleSheep)
            {
                if (s.GetComponent<SheepControllerLimitedMem>().state == Enums.State.Walk)
                {
                    walkState += 1;
                }
            }
            // calculate porbability
            float prob = (1 + GM.ginelli.alpha * walkState) / GM.ginelli.tau0_1;

            // potentially change state
            if (Random.Range(0, 1) < prob) {
                state = Enums.State.Walk;
                anim.SetBool("IsIdle", false);
                return;
            }
        }
        else if (state == Enums.State.Walk)
        {
            // get state of each neighbour
            int idleState = 0;
            foreach (GameObject s in visibleSheep)
            {
                if (s.GetComponent<SheepControllerLimitedMem>().state == Enums.State.Idle)
                {
                    idleState += 1;
                }
            }
            // calculate porbability
            float prob = (1 + GM.ginelli.alpha * idleState) / GM.ginelli.tau1_0;

            // potentially change state
            if (Random.Range(0, 1) < prob)
            {
                state = Enums.State.Idle;
                anim.SetBool("IsIdle", true);
                return;
            }
        }

        if (state == Enums.State.Idle || state == Enums.State.Walk)
        {
            int runState = 0;
            float meanDist = 0;
            float topologicalCount = 0;

            // go through all topological sheep
            foreach (GameObject s in visibleSheep)
            {
                if (Vector3.Distance(transform.position, s.transform.position) < GM.sheepHearDist)
                {
                    topologicalCount += 1;

                    // add mean dist
                    meanDist += Vector3.Distance(transform.position, s.transform.position);

                    if (s.GetComponent<SheepControllerLimitedMem>().state == Enums.State.Run)
                    {
                        runState += 1;
                    }
                }

            }
            meanDist = meanDist / topologicalCount;

            // calculate porbability
            float prob = (1 / topologicalCount) * ((meanDist / GM.ginelli.dR) * Mathf.Pow(1 + GM.ginelli.alpha * runState, GM.ginelli.delta));

            // potentially change state
            if (Random.Range(0, 1) < prob)
            {
                state = Enums.State.Run;
                anim.SetBool("IsIdle", false);
                anim.SetBool("IsRunning", true);
            }
        }
    }

    // return list of sheep in sheepList that are in viewAngle and not occluded or around heardDist
    private List<GameObject> getVisibleAndHeardSheep(List<GameObject> sheepList, int viewAngle, float heardDist)
    {
        // create list
        List<GameObject> visibleSheep_ = new List<GameObject>();

        // objects in view array - length equal to viewAngle
        float[] viewDeg = new float[viewAngle];

        // TODO: filter sheep
        List<GameObject> closeSheep = new List<GameObject>(sheepList);

        // sort sheep according to distance
        closeSheep.Sort(SortListByDist);

        // decide for each sheep if visible
        foreach (GameObject s in closeSheep)
        {
            bool sheepVisible = false;

            // calculate amount of angle the sheep represents in dogs view, *2 is used later
            float sheepAngle = Mathf.Atan((GM.sheepLength / 2) / Vector3.Distance(this.transform.position, s.transform.position));
            // from rad to deg
            sheepAngle = sheepAngle * 180 / Mathf.PI;

            // calculate angle between sheep and dog
            float angle = Vector3.SignedAngle(this.transform.forward, s.transform.position - this.transform.position, Vector3.up);

            // get sheep start and stop angle
            int startAngle = (int)Mathf.Round(angle - sheepAngle);
            int stopAngle = (int)Mathf.Round(angle + sheepAngle);
            //print(startAngle + ", " + stopAngle);

            // if sheep to far
            if (startAngle == stopAngle)
            {

            }
            // else if sheep outside of viewAngle
            else if (Mathf.Abs(startAngle) > viewAngle / 2 || Mathf.Abs(stopAngle) > viewAngle / 2)
            {
                // add sheep if not visible but around heardDist
                if (Vector3.Distance(transform.position, s.transform.position) <= heardDist)
                {
                    visibleSheep_.Add(s);
                }
            }
            else
            {
                // check every degree if nothing is occluding it
                for (int i = startAngle; i < stopAngle; i++)
                {
                    // if atleast one degree is still empty -> sheep visible
                    if (viewDeg[90 + i] == 0)
                    {
                        sheepVisible = true;
                        viewDeg[90 + i] = 1;
                    }
                }

                // add visible sheep to the list
                if (sheepVisible)
                {
                    visibleSheep_.Add(s);
                }
            }
        }

        return visibleSheep_;
    }

    // return list of sheep in sheepList that are in viewAngle and not occluded or around heardDist
    private List<GameObject> getVisibleAndHeardDogs(List<GameObject> dogList, int viewAngle, float heardDist)
    {
        // create list
        List<GameObject> visibleDogs_ = new List<GameObject>();

        // objects in view array - length equal to viewAngle
        float[] viewDeg = new float[viewAngle];

        // TODO: filter sheep
        List<GameObject> closeDogs = new List<GameObject>(dogList);

        // sort sheep according to distance
        closeDogs.Sort(SortListByDist);

        // decide for each sheep if visible
        foreach (GameObject d in closeDogs)
        {
            bool dogVisible = false;

            // calculate amount of angle the sheep represents in dogs view, *2 is used later
            float dogAngle = Mathf.Atan((GM.dogLength / 2) / Vector3.Distance(this.transform.position, d.transform.position));
            // from rad to deg
            dogAngle = dogAngle * 180 / Mathf.PI;

            // calculate angle between sheep and dog
            float angle = Vector3.SignedAngle(this.transform.forward, d.transform.position - this.transform.position, Vector3.up);

            // get dog start and stop angle
            int startAngle = (int)Mathf.Round(angle - dogAngle);
            int stopAngle = (int)Mathf.Round(angle + dogAngle);
            //print(startAngle + ", " + stopAngle);

            // if dog to far
            if (startAngle == stopAngle)
            {
                
            }
            // else if dog outside of viewAngle
            else if (Mathf.Abs(startAngle) > viewAngle / 2 || Mathf.Abs(stopAngle) > viewAngle / 2)
            {
                // add dog if not visible but around heardDist
                if (Vector3.Distance(transform.position, d.transform.position) <= heardDist)
                {
                    visibleDogs_.Add(d);
                }
            }
            else
            {
                // check every degree if nothing is occluding it
                for (int i = startAngle; i < stopAngle; i++)
                {
                    // if atleast one degree is still empty -> sheep visible
                    if (viewDeg[90 + i] == 0)
                    {
                        dogVisible = true;
                        viewDeg[90 + i] = 1;
                    }
                }

                // add visible sheep to the list
                if (dogVisible)
                {
                    visibleDogs_.Add(d);
                }
            }
        }

        return visibleDogs_;
    }

    public List<GameObject> addToMemory(List<GameObject> newList)
    {
        IDictionary<GameObject, float> tempMem = new Dictionary<GameObject, float>();

        // add new list to temp mem
        foreach (GameObject s in newList)
        {
            tempMem.Add(s, stayInMem);
        }

        // add sheep from old mem to temp mem
        foreach (var oldMemKVP in sheepMem)
        {
            // check if sheep from old mem is also in the temp mem
            bool exists = false;
            foreach (var tempMemKVP in tempMem)
            {
                if (Vector3.Distance(oldMemKVP.Key.transform.position, tempMemKVP.Key.transform.position) < 0.5f)
                {
                    exists = true;
                    break;
                }
            }

            // if not in temp mem -> add sheep from old mem to temp mem
            if (!exists)
            {
                tempMem.Add(oldMemKVP.Key, oldMemKVP.Value);
            }
        }

        // override 
        sheepMem = tempMem;

        // transform mem to list and return
        List<GameObject> lst = new List<GameObject>(GM.sheepNumber);
        foreach (var kvp in sheepMem)
        {
            lst.Add(kvp.Key);
        }

        return lst;
    }

    private int SortListByDist(GameObject obj1, GameObject obj2)
    {
        float d1 = Vector3.Distance(this.transform.position, obj1.transform.position);
        float d2 = Vector3.Distance(this.transform.position, obj2.transform.position);
        if (d1 < d2)
        {
            return -1;
        }
        else
        {
            return 1;
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
}
