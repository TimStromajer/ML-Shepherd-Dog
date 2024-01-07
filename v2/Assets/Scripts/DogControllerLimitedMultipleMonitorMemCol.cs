using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DogControllerLimitedMultipleMonitorMemCol : MonoBehaviour
{

    // Dog Animator Controller
    public Animator anim;

    private GameManager GM;
    private Rigidbody m_Rigidbody;

    private Vector3 gcm;
    private List<GameObject> visibleSheep;
    private List<GameObject> dogList;
    private float fN;
    private float Pd;
    private GameObject goal;
    private Vector3 goingDirection;
    private Vector3 destinationPosition;
    private Enums.dogState state;
    private float collectAngle = 35;

    // monitor
    private float monitorSpeed;

    // memory - sheep pos, time
    private IDictionary<GameObject, float> sheepMem;
    private float stayInMem = 3;

    private GameObject chosenSheepCollect;
    private int maxSheep = 0;

    void Start()
    {
        GM = FindObjectOfType<GameManager>();
        m_Rigidbody = GetComponent<Rigidbody>();
        goal = GM.goal;
        state = Enums.dogState.observing;

        // get visible sheep
        visibleSheep = getVisibleSheep(GM.sheepList, GM.dogVisionAngle);

        // get dog list
        dogList = GM.dogList;

        // start with monitor speed 1
        monitorSpeed = 1;

        // init sheep memory
        sheepMem = new Dictionary<GameObject, float>();
    }

    // Update is called once per frame
    void Update()
    {
        if (GM.dogWithMemory)
        {
            // copy dog memory to be able to remove from main memory
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
        }

        // get visible sheep
        visibleSheep = getVisibleSheep(GM.sheepList, 180);

        if (GM.dogWithMemory)
        {
            // add sheep to memory
            visibleSheep = addToMemory(visibleSheep);
        }

        if (visibleSheep.Count > maxSheep)
        {
            maxSheep = visibleSheep.Count;
            print(maxSheep);
        }

        if (Input.GetKeyDown("space"))
        {
            print(visibleSheep.Count);
        }

        // if no sheep visible and dog has no destination -> only rotate
        if (visibleSheep.Count <= 0 && state != Enums.dogState.hasDestination)
        {
            transform.rotation = Quaternion.LookRotation(Vector3.RotateTowards(transform.forward, Vector3.Cross(transform.forward, Vector3.up), GM.dogRotationSpeed * Time.deltaTime, 0.0f));
            return;
        }

        // calculate GCM
        gcm = calculateGCM(visibleSheep);

        // calculate f(N) - how far sheep can be from gcm
        fN = GM.strombom.ra * Mathf.Pow(visibleSheep.Count, 3f / 5f);

        // check if any sheep not in f(N) = ra_N^2/3 from GCM
        // Vector3? furthestSheepPos = allInGCM(visibleSheep, gcm, fN);
        // Vector3? furthestSheepPos = allInGCMAngle(visibleSheep, gcm, fN, goal.transform.position, collectAngle);
        List<GameObject> furthestSheepPos = allNotInGCMAngle(visibleSheep, gcm, fN, goal.transform.position, collectAngle);

        // reset monitor speed
        monitorSpeed = 1;

        // do collect
        if (furthestSheepPos.Count > 0 || state == Enums.dogState.collect)
        {
            Vector3 desiredPosition = new Vector3();
            Vector3 direction = new Vector3();

            // if dog has already chosen a sheep to herd
            if (state == Enums.dogState.collect)
            {
                // if sheep is close to gcm change state
                if (Vector3.Distance(chosenSheepCollect.transform.position, gcm) < fN)
                {
                    state = Enums.dogState.observing;
                }
                else
                {
                    // get desired location
                    direction = Vector3.Normalize(chosenSheepCollect.transform.position - gcm);
                    desiredPosition = chosenSheepCollect.transform.position + direction * GM.strombom.Pc;

                    goingDirection = Vector3.Normalize(desiredPosition - transform.position);
                }
            }
            // else search for a sheep to collect
            else
            {

                // change state
                state = Enums.dogState.observing;

                // if no sheep to collect is found dog will go around gcm and try and find some
                bool goAround = true;

                // go through all sheep away from gcm and find one to collect
                foreach (GameObject s in furthestSheepPos)
                {
                    // get desired location
                    direction = Vector3.Normalize(s.transform.position - gcm);
                    desiredPosition = s.transform.position + direction * GM.strombom.Pc;

                    // check if some other dog is closer to that sheep
                    float myDist = Vector3.Distance(desiredPosition, transform.position);
                    bool closer = false;
                    foreach (GameObject d in dogList)
                    {
                        if (Vector3.Distance(desiredPosition, d.transform.position) < myDist)
                        {
                            closer = true;
                        }
                    }

                    // if this dog is closest -> break and choose this location
                    if (!closer)
                    {
                        goAround = false;
                        chosenSheepCollect = s;
                        break;
                    }
                }

                Debug.DrawLine(transform.position, desiredPosition, Color.black);

                // if some other dog closer, go around the gcm and check for other out of gcm sheep
                if (goAround)
                {
                    // get desired location on the other side of gcm
                    Pd = GM.strombom.ra * Mathf.Sqrt(visibleSheep.Count);
                    direction = Vector3.Normalize(gcm - transform.position);

                    // decide to which direction to run based on angle with gcm and goal
                    Vector3 perpendicularLeft = Vector3.Cross(direction, Vector3.up).normalized;
                    Vector3 perpendicularRight = -perpendicularLeft;
                    if (Vector3.Angle(perpendicularLeft, Vector3.Normalize(GM.goal.transform.position - gcm)) < Vector3.Angle(perpendicularRight, Vector3.Normalize(GM.goal.transform.position - gcm)))
                    {
                        desiredPosition = gcm + (direction + perpendicularRight * 3f) * Pd;
                    }
                    else
                    {
                        desiredPosition = gcm + (direction + perpendicularLeft * 3f) * Pd;
                    }

                    goingDirection = Vector3.Normalize(desiredPosition - transform.position);
                    Debug.DrawLine(transform.position, desiredPosition, Color.red);
                }
                // else if this dog is the closes, go to that location
                else
                {
                    state = Enums.dogState.collect;
                    goingDirection = Vector3.Normalize(desiredPosition - transform.position);
                }
            }
        }
        // do herd
        else if (state != Enums.dogState.hasDestination)
        {
            // get desired location
            Pd = GM.strombom.ra * Mathf.Sqrt(visibleSheep.Count);
            Vector3 direction = Vector3.Normalize(gcm - goal.transform.position);
            Vector3 desiredPosition = gcm + direction * Pd;

            // check if some other dog is closer and get its position
            float myDist = Vector3.Distance(desiredPosition, transform.position);
            bool closer = false;
            float smallestDist = Mathf.Infinity;
            Vector3 herderPos = new Vector3();
            foreach (GameObject d in dogList)
            {
                if (Vector3.Distance(desiredPosition, d.transform.position) < myDist)
                {
                    closer = true;
                    if (Vector3.Distance(desiredPosition, d.transform.position) < smallestDist)
                    {
                        herderPos = d.transform.position;
                    }
                }
            }

            // if this dog is closest -> go to that position
            if (!closer)
            {
                goingDirection = Vector3.Normalize(desiredPosition - transform.position);
            }
            // else get destination for monitoring
            else
            {
                // decide to which direction to run based on angle with gcm and goal
                Vector3 perpendicularLeft = Vector3.Cross(direction, Vector3.up).normalized;
                Vector3 perpendicularRight = -perpendicularLeft;
                if (Vector3.Angle(perpendicularLeft, Vector3.Normalize(herderPos - gcm)) < Vector3.Angle(perpendicularRight, Vector3.Normalize(herderPos - gcm)))
                {
                    desiredPosition += (direction + perpendicularRight) * Pd * 3;
                }
                else
                {
                    desiredPosition += (direction + perpendicularLeft) * Pd * 3;
                }

                // if destination is futher from gcm than shepherd -> move towards herderpos (do not go back); else go to that position
                if (Vector3.Distance(transform.position, gcm) < Vector3.Distance(desiredPosition, gcm))
                {
                    goingDirection = Vector3.Normalize(herderPos - transform.position);
                    monitorSpeed = 0.1f;
                } else
                {
                    goingDirection = Vector3.Normalize(desiredPosition - transform.position);

                    state = Enums.dogState.hasDestination;
                    destinationPosition = desiredPosition;
                }
            }
        }
        // go to destination
        else
        {
            goingDirection = Vector3.Normalize(destinationPosition - transform.position);
            monitorSpeed = 0.2f;

            if (state == Enums.dogState.hasDestination && Vector3.Distance(transform.position, destinationPosition) < 2)
            {
                state = Enums.dogState.herd;
            }

            // check if close to fence -> stop movement to destination
            foreach (GameObject f in GM.fenceList)
            {
                if (Vector3.Distance(transform.position, f.transform.position) < 2)
                {
                    state = Enums.dogState.herd;
                }
            }

            Debug.DrawLine(transform.position, destinationPosition, Color.magenta);
        }

        // distort rotation by gcm - so it goes around the sheep
        Vector3 perpLeftGoingDir = Vector3.Cross(goingDirection, Vector3.up).normalized;
        Vector3 perpRightGoingDir = -perpLeftGoingDir;
        Vector3 finalDirection = new Vector3();
        if (Vector3.Angle(perpLeftGoingDir, Vector3.Normalize(transform.position - gcm)) < Vector3.Angle(perpRightGoingDir, Vector3.Normalize(transform.position - gcm)))
        {
            finalDirection = Vector3.Normalize(perpLeftGoingDir * 0.7f + goingDirection);
        }
        else
        {
            finalDirection = Vector3.Normalize(perpRightGoingDir * 0.7f + goingDirection);
        }
        // Vector3 finalDirection = Vector3.Normalize(Vector3.Normalize(transform.position - gcm) * 0.5f + goingDirection);
        Debug.DrawLine(transform.position, gcm, Color.white);

        // rotate shepherd
        transform.rotation = Quaternion.LookRotation(Vector3.RotateTowards(transform.forward, finalDirection, GM.dogRotationSpeed * Time.deltaTime, 0.0f));

        // add ratio to shepherd speed according to distance to closest sheep
        float distRatio;
        if (visibleSheep.Count > 0)
        {
            Vector3 sheepPos = closestSheep(visibleSheep);
            float dist = Vector3.Distance(transform.position, sheepPos);
            distRatio = (dist - GM.minDistToSheep) / (GM.maxDistToSheep - GM.minDistToSheep);
            if (distRatio > 1)
            {
                distRatio = 1;
            }
        } else
        {
            distRatio = 1;
        }

        // move shepherd
        transform.Translate(transform.forward * Time.deltaTime * GM.dogSpeed * distRatio * monitorSpeed, Space.World);

        // reset y position to 0
        transform.position = new Vector3(transform.position.x, 0, transform.position.z);

        // choose animation depending on speed
        //if (GM.dogSpeed * distRatio * monitorSpeed > GM.dogSpeed / 2)
        //{
        //    anim.SetBool("IsRunning", true);
        //    anim.SetBool("IsWalking", false);
        //}
        //else
        //{
        //    anim.SetBool("IsRunning", false);
        //    anim.SetBool("IsWalking", true);
        //}
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

    // check if all objects in lst are dist around gcm or return furthest object
    public Vector3? allInGCM(List<Vector3> lst, Vector3 gcm_, float dist)
    {
        float furthestDist = 0;
        Vector3? furthestPoint = null;
        foreach (Vector3 s in lst)
        {
            float fromGCM = Vector3.Distance(s, gcm_);
            if (fromGCM > dist && fromGCM > furthestDist)
            {
                furthestDist = fromGCM;
                furthestPoint = s;
            }
        }

        return furthestPoint;
    }

    // check if all objects in lst are dist around gcm or if not they are not in the way of gcm, otherwise return furthest object
    public Vector3? allInGCMAngle(List<Vector3> lst, Vector3 gcm_, float dist, Vector3 goal_, float angle)
    {
        float furthestDist = 0;
        Vector3? furthestPoint = null;
        foreach (Vector3 s in lst)
        {
            // get distances and angle
            float distSheepGCM = Vector3.Distance(s, gcm_);
            float distGoalGCM = Vector3.Distance(goal_, gcm_);
            float distGoalSheep = Vector3.Distance(goal_, s);
            Vector3 goalSheepVec = Vector3.Normalize(s - goal_);
            Vector3 gcmSheepVec = Vector3.Normalize(s - gcm_);
            float vecAngle = Vector3.Angle(goalSheepVec, gcmSheepVec);

            if (distSheepGCM > dist && distSheepGCM > furthestDist && (vecAngle > angle || distGoalSheep > distGoalGCM))
            {
                furthestDist = distSheepGCM;
                furthestPoint = s;
            }
        }

        return furthestPoint;
    }

    // check if all objects in lst are dist around gcm or if not they are not in the way of gcm, otherwise return all objects not in gcm
    public List<GameObject> allNotInGCMAngle(List<GameObject> lst, Vector3 gcm_, float dist, Vector3 goal_, float angle)
    {
        List<GameObject> furtherSheep = new List<GameObject>();
        foreach (GameObject s in lst)
        {
            // get distances and angle
            float distSheepGCM = Vector3.Distance(s.transform.position, gcm_);
            float distGoalGCM = Vector3.Distance(goal_, gcm_);
            float distGoalSheep = Vector3.Distance(goal_, s.transform.position);
            Vector3 goalSheepVec = Vector3.Normalize(s.transform.position - goal_);
            Vector3 gcmSheepVec = Vector3.Normalize(gcm_ - s.transform.position);
            float vecAngle = Vector3.Angle(goalSheepVec, gcmSheepVec);

            if (distSheepGCM > dist && (vecAngle > angle || distGoalSheep > distGoalGCM))
            {
                furtherSheep.Add(s);
            }
        }

        // sort in order from furthest to closest to GCM
        furtherSheep.Sort(SortListByDistFromGCM);

        return furtherSheep;
    }

    // returns position of closest object in lst
    Vector3 closestSheep(List<GameObject> lst)
    {
        if (lst.Count == 0)
        {
            print("ERROR: no elements in closestSheep lst");
        }

        Vector3 closest = new Vector3(Mathf.Infinity, Mathf.Infinity, Mathf.Infinity);
        float dist = Mathf.Infinity;
        foreach (GameObject s in lst)
        {
            float currentDist = Vector3.Distance(transform.position, s.transform.position);
            if (currentDist < dist)
            {
                dist = currentDist;
                closest = s.transform.position;
            }
        }

        return closest;
    }

    // return list of sheep in sheepList that are in viewAngle and not occluded
    private List<GameObject> getVisibleSheep(List<GameObject> sheepList, int viewAngle)
    {
        // return all sheep if not limited
        if (!GM.dogWithLimited)
        {
            return GM.sheepList;
        }

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
                
            } else
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

    private int SortListByDistFromGCM(GameObject obj1, GameObject obj2)
    {
        float d1 = Vector3.Distance(gcm, obj1.transform.position);
        float d2 = Vector3.Distance(gcm, obj2.transform.position);
        if (d1 < d2)
        {
            return 1;
        }
        else
        {
            return -1;
        }
    }

    private int SortListByDist(GameObject obj1, GameObject obj2)
    {
        float d1 = Vector3.Distance(this.transform.position, obj1.transform.position);
        float d2 = Vector3.Distance(this.transform.position, obj2.transform.position);
        if (d1 < d2)
        {
            return -1;
        } else
        {
            return 1;
        }
    }
}
