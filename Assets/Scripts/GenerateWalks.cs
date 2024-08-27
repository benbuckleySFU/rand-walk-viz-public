using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using System.Diagnostics;
using System.IO;
using UnityEngine.SocialPlatforms;
using System.Text.RegularExpressions;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Windows;
using System.Globalization;

public class GenerateWalks : MonoBehaviour
{
    // Start is called before the first frame update
    public GameObject baseObject;
    private GameObject walkParent;
    public Material baseMaterial;
    public GameObject parent;
    private float scale;
    public int numWalks = 10;
    public int numSteps = 50;
    public int minSteps = 0;
    public int maxSteps = 50;
    private Vector3[] stepset;
    private Vector3 position = Vector3.zero;
    private Color stepColour = new Color(126 * 1.0f / 255, 126 * 1.0f / 255, 0 * 1.0f / 255, 1.0f / 10);

    private int xPlusWeight = 1;
    private int yPlusWeight = 1;
    private int zPlusWeight = 1;
    private int xMinusWeight = 1;
    private int yMinusWeight = 1;
    private int zMinusWeight = 1;

    // Translucent sphere to indicate average radius (currently just sqrt(length))
    public GameObject radiusSphere;

    // Needed to submit Maple input
    public TMP_InputField mapleInputField;

    // Variables needed to show the number of steps in each walk
    public GameObject titleParent;
    private GameObject numStepsTextParent;
    public GameObject numStepsTextCanvas;
    public TextMeshProUGUI numStepsText;

    // The variables needed for doing Boltzmann Generation:
    private System.Random random = new System.Random();
    private Dictionary<string, Dictionary<string, object>> generators;
    private Dictionary<string, Vector3> atomDictionary = new Dictionary<string, Vector3>();
    private double rhoApprox;

    // Keep track of current set of walks
    private List<Vector3[]> currentWalks = new List<Vector3[]>();
    private List<Vector3[]> currentWalksNormalized = new List<Vector3[]>();
    private List<GameObject[]> currentWalkObjects = new List<GameObject[]>();

    // Streamwriter for logging data:
    private StreamWriter swLog;
    public bool loggingWalks;
    private StreamWriter swStats;

    // Variables used for animation
    public Boolean animated = false;
    private int currentAnimationFrame = 0;
    private float interval = 1.0f / 24.0f; // Default: 1.0f / 24.0f, as in 24 frames per second
    private float nextFrameTime = 0;
    private int animTrail = 1;

    // Variables for counting rejection rates and other data collection
    private int numWalksGenerated = 0;
    private int numWalksRejectedLength = 0;
    private int numWalksRejectedTooShort = 0;
    private int numWalksRejectedTooLong = 0;
    private int numWalksRejectedOctant = 0;
    private bool currentMethodBoltzmann = false;
    private long timeToGenerate;

    // Variables for importing walks
    public TMP_InputField importInputField;

    // Determines whether the colour of cubes represents position in XYZ or position in the walk
    private bool colourXYZ = false;

    // Variables used for displaying convex hulls
    private List<Mesh> convexHulls = new List<Mesh>();
    private bool displayConvexHulls = false;
    public GameObject convexHullParent;

    // Variables needed for timed runs
    private int lengthTimedRun = 10;

    // Variables needed for sound
    bool soundOn = false;
    public GameObject soundObject;
    public GameObject singleWalkSoundObjectTemplate;

    void Start()
    {
        // Set size of sphere
        radiusSphere.transform.localScale = new Vector3(20, 20, 20);

        scale = 50f / numSteps;
        baseObject.transform.localScale = new Vector3(scale, scale, scale);
        // Create stepset
        stepset = new Vector3[6];
        stepset[0] = scale * new Vector3(1, 0, 0);
        stepset[1] = scale * new Vector3(0, 1, 0);
        stepset[2] = scale * new Vector3(0, 0, 1);
        stepset[3] = scale * new Vector3(-1, 0, 0);
        stepset[4] = scale * new Vector3(0, -1, 0);
        stepset[5] = scale * new Vector3(0, 0, -1);

        // Adjust material alpha
        Renderer renderer = baseObject.GetComponent<Renderer>();
        renderer.sharedMaterial.SetColor("_Color", new Color(255 * 1.0f / 255, 126 * 1.0f / 255, 0 * 1.0f / 255, 1.0f / numWalks));

        stepColour = new Color(255 * 1.0f / 255, 126 * 1.0f / 255, 0 * 1.0f / 255, 1.0f / numWalks);

        logInit();

        // displayThreeWalks();
        /*
        // Uniformity test
        int numToTest = 10;
        for (int i =  0; i < 5; i++)
        {
            testUniformity(10, numToTest);
            numToTest *= 10;
        }
        */

        /*
        // Testing excursion frequency
        swStats.WriteLine("Walk target length\tNum Walks Generated\tNum walks that return to origin\tNum Excursions");
        int walkLength = 20;
        for (int i = 0; walkLength <= 200; i++)
        {
            testExcursions(walkLength, 1000);
            walkLength += 20;
        }
        */

        // Goal: Get batch statistics for walks in increments of 10 for lengths 10 to 1000-ish.
        // For Boltzmann, +- 5.
        /*
        loggingWalks = false;
        for (int i = 10; i < 101; i = i + 10)
        {
            numSteps = i;
            // Just use default weights
            displayWalkCloud();
        }
        // Now, Boltzmann:
        createGenerator("[evaluations = {D = 5.715403245, P = 119.5345480, P_aux = 20.91445571, L_1 = .9521861810, R_1 = .9521861810, a_1 = .1666, b_1 = .1666, c_1 = .1666, c_2 = .1666, c_3 = .1666, c_4 = .1666}, grammar = {D = Union(Epsilon,Prod(c_1,D),Prod(c_2,D),Prod(c_3,D),Prod(c_4,D),Prod(L_1,R_1)), P = Prod(D,P_aux), P_aux = Union(Epsilon,Prod(L_1,P_aux)), L_1 = Union(Prod(a_1,D)), R_1 = Union(Prod(b_1,D)), a_1 = Atom, b_1 = Atom, c_1 = Atom, c_2 = Atom, c_3 = Atom, c_4 = Atom}, rho_approx = {.1666}, atomSet = {a_1 = [0, 0, 1], b_1 = [0, 0, -1], c_1 = [1, 0, 0], c_2 = [0, 1, 0], c_3 = [-1, 0, 0], c_4 = [0, -1, 0]}]");
        for (int i = 10; i < 101; i = i + 10)
        {
            minSteps = i - 5;
            maxSteps = i + 5;
            // Just use default weights
            displayWalkCloudBoltzmann();
        }
        */
    }

    // Update is called once per frame
    void Update()
    {
        if (Time.time >= nextFrameTime)
        {
            nextFrameTime += interval;
            //UnityEngine.Debug.Log("Updating time!");
            if (animated)
            {
                animateOneFrame();
            }

        }


    }

    public void setTrailLength (float trailLength)
    {
        animTrail = Math.Max(1, (int)trailLength);
    }

    public void togglePositionColour(bool positionalColour)
    {
        colourXYZ = positionalColour;
        if (colourXYZ)
        {
            for (int i = 0; i < currentWalkObjects.Count; i++)
            {
                for (int j = 0; j < currentWalkObjects[i].Length; j++)
                {
                    Color currentColor = currentWalkObjects[i][j].GetComponent<Renderer>().material.color;
                    // Get position.
                    Vector3 currentPosition = currentWalkObjects[i][j].transform.position;
                    // Determine the colour for this position in terms of RGB.
                    // x axis = red, y axis = green, z axis = blue
                    float vecLength = currentPosition.magnitude;
                    if (vecLength > 0)
                    {
                        currentColor.r = Math.Abs(currentPosition[0] / vecLength);
                        currentColor.g = Math.Abs(currentPosition[1] / vecLength);
                        currentColor.b = Math.Abs(currentPosition[2] / vecLength);
                    }
                    else
                    {
                        currentColor.r = 0;
                        currentColor.b = 0;
                        currentColor.g = 0;
                    }
                    currentWalkObjects[i][j].GetComponent<Renderer>().material.color = currentColor;
                }
            }
        }
        else
        {
            // Colour the cubes according to their position within the walk.
            for (int i = 0; i < currentWalkObjects.Count; i++)
            {
                for (int j = 0; j < currentWalkObjects[i].Length; j++)
                {
                    Color currentColor = Color.HSVToRGB(0.9f * j / currentWalkObjects[i].Length, 1.0f, 1.0f);
                    currentColor.a = 1.0f / numWalks;
                    currentWalkObjects[i][j].GetComponent<Renderer>().material.color = currentColor;
                }
            }
        }

    }

    public void toggleConvexHulls(bool showConvexHulls)
    {
        displayConvexHulls = showConvexHulls;
        if (displayConvexHulls)
        {
            // First, calculate the convex hulls if we haven't done so already.
            // Need to go through all of the walks and collect the current points.
            if (convexHulls.Count == 0) // Only generate the convex hulls if they haven't been generated already
            {
                bool stepsLeft = true;
                int currentStep = 0;
                do
                {
                    stepsLeft = false;
                    List<Vector3> currentPointList = new List<Vector3>();
                    for (int i = 0; i < currentWalkObjects.Count; i++)
                    {
                        if (currentWalkObjects[i].Length > currentStep)
                        {
                            stepsLeft = true;
                            currentPointList.Add(currentWalkObjects[i][currentStep].transform.position);
                        }
                    }
                    convexHulls.Add(findConvexHull(currentPointList));
                    currentStep++;
                } while (stepsLeft);
            }
        }
        else
        {
            convexHullParent.GetComponent<MeshFilter>().mesh = new Mesh();
        }
    }

    public void toggleSound(bool soundOnIn)
    {
        if (soundOnIn)
        {
            UnityEngine.Debug.Log("Sound turned on!");
            soundOn = true;
        }
        else
        {
            UnityEngine.Debug.Log("Sound turned off!");
            soundOn = false;
        }
    }

    void animateOneFrame()
    {
        // Let's see if I can get one cube visible at a time.
        //UnityEngine.Debug.Log("Animation code will go here!");
        Boolean walksChanged = false;

        if (displayConvexHulls)
        {
            // Display the convex hull associated with currentAnimationFrame
            //UnityEngine.Debug.Log("Current animation frame: " + currentAnimationFrame);
            //UnityEngine.Debug.Log("convexHulls.Count: " + convexHulls.Count);
            if (convexHulls.Count > currentAnimationFrame) // Should always be true, but sometimes messes up when generating new walks
            {
                convexHullParent.GetComponent<MeshFilter>().mesh = convexHulls[currentAnimationFrame];
            }

        }

        if (soundOn)
        {
            //UnityEngine.Debug.Log("soundObject.transform.childCount = " + soundObject.transform.childCount);

            for (int i = 0; i < soundObject.transform.childCount; i++)
            {
                soundObject.transform.GetChild(i).transform.GetChild(0).GetComponent<AudioSource>().pitch = 1 + currentWalksNormalized[0][currentAnimationFrame].x;
                soundObject.transform.GetChild(i).transform.GetChild(0).GetComponent<AudioSource>().Play();
                soundObject.transform.GetChild(i).transform.GetChild(1).GetComponent<AudioSource>().pitch = 2 + currentWalksNormalized[0][currentAnimationFrame].y;
                soundObject.transform.GetChild(i).transform.GetChild(1).GetComponent<AudioSource>().Play();
                soundObject.transform.GetChild(i).transform.GetChild(2).GetComponent<AudioSource>().pitch = 3 + currentWalksNormalized[0][currentAnimationFrame].z;
                soundObject.transform.GetChild(i).transform.GetChild(2).GetComponent<AudioSource>().Play();
            }
            
        }

        if (currentAnimationFrame > animTrail)
        {
            for (int i = 0; i < currentWalkObjects.Count; i++)
            {
                // Set the currentAnimationFrame-th cube of the walk to have alpha = 0.5
                // ... and set the previous cube to have alpha = 0
                if (currentAnimationFrame < currentWalkObjects[i].Length)
                {
                    for (int j = 0; j <= animTrail; j++)
                    {
                        Color currentColor = currentWalkObjects[i][currentAnimationFrame - j].GetComponent<Renderer>().material.color;
                        currentColor.a = 0.5f * (animTrail - j) / (animTrail);
                        currentWalkObjects[i][currentAnimationFrame - j].GetComponent<Renderer>().material.color = currentColor;
                    }

                    walksChanged = true;
                }
            }
            currentAnimationFrame++;
        }
        else // currentAnimationFrame <= animTrail
        {
            // Assume animation has just started. Set the initial cubes to have alpha = 0.5.
            for (int i = 0; i < currentWalkObjects.Count; i++)
            {
                // Set the currentAnimationFrame-th cube of the walk to have alpha = 0.5
                if (currentAnimationFrame < currentWalkObjects[i].Length)
                {
                    for (int j = 0; currentAnimationFrame - j >= 0; j++)
                    {
                        Color currentColor = currentWalkObjects[i][currentAnimationFrame - j].GetComponent<Renderer>().material.color;
                        currentColor.a = 0.5f * (animTrail - j) / (animTrail);
                        currentWalkObjects[i][currentAnimationFrame - j].GetComponent<Renderer>().material.color = currentColor;
                    }
                    //Color currentColor = currentWalkObjects[i][currentAnimationFrame].GetComponent<Renderer>().material.color;
                    //currentColor.a = 0.5f;
                    //currentWalkObjects[i][currentAnimationFrame].GetComponent<Renderer>().material.color = currentColor;
                    walksChanged = true;
                }
            }
            currentAnimationFrame++;
        }

        if (!walksChanged)
        {
            // Restart animation
            setAnimated(true);
        }

    }


    public void setNumWalks(String numWalksInput)
    {
        // numWalks = Math.Max(0, int.Parse(numWalksInput));
        int newVal = 0;
        bool isValidInput = int.TryParse(numWalksInput, out newVal);
        if (isValidInput)
        {
            numWalks = newVal;
        }
        UnityEngine.Debug.Log("numWalks: " + numWalks);

        while (soundObject.transform.childCount < numWalks)
        {
            // Make a bunch of copies of the template
            GameObject newSoundObject = Instantiate(singleWalkSoundObjectTemplate, soundObject.transform);
        }
    }

    public void setNumSteps(String numStepsInput)
    {
        //numSteps = Math.Max(0, int.Parse(numStepsInput));
        int newVal = 0;
        bool isValidInput = int.TryParse(numStepsInput, out newVal);
        UnityEngine.Debug.Log("numSteps: " + numSteps);
        if (isValidInput)
        {
            numSteps = newVal;
            // Change size of translucent sphere
            float newRadius = (float)Math.Sqrt(1.0 * numSteps);
            //radiusSphere.transform.localScale = new Vector3(newRadius, newRadius, newRadius);
            // For now, just have a constant radius and show it for scale purposes
            radiusSphere.transform.localScale = new Vector3(20,20,20);
            //UnityEngine.Debug.Log("Updated radius to " + newRadius);
        }
    }

    public void setMinSteps(String minStepsInput)
    {
        int newVal = 0;
        bool isValidInput = int.TryParse(minStepsInput, out newVal);
        if (isValidInput)
        {
            minSteps = newVal;
        }
    }

    public void setMaxSteps(String maxStepsInput)
    {
        int newVal = 0;
        bool isValidInput = int.TryParse(maxStepsInput, out newVal);
        if (isValidInput)
        {
            maxSteps = newVal;
        }
    }

    public void setAnimated(Boolean animatedInput)
    {
        animated = animatedInput;
        if (animated)
        {
            // Restart the animation
            currentAnimationFrame = 0;
            // Reset alpha values
            for (int i = 0; i < currentWalkObjects.Count; i++)
            {
                for (int j = 0; j < currentWalkObjects[i].Length; j++)
                {
                    Color currentColor = currentWalkObjects[i][j].GetComponent<Renderer>().material.color;
                    currentColor.a = 0f;
                    currentWalkObjects[i][j].GetComponent<Renderer>().material.color = currentColor;
                }
            }

        }
        else
        {
            // Reset alpha values
            for (int i = 0; i < currentWalkObjects.Count; i++)
            {
                for (int j = 0; j < currentWalkObjects[i].Length; j++)
                {
                    Color currentColor = currentWalkObjects[i][j].GetComponent<Renderer>().material.color;
                    currentColor.a = 1.0f / numWalks;
                    currentWalkObjects[i][j].GetComponent<Renderer>().material.color = currentColor;
                }
            }
        }
    }

    public void setXPlusWeight(String xPlusWeightInput)
    {
        int newVal = 0;
        bool isValidInput = int.TryParse(xPlusWeightInput, out newVal);
        if (isValidInput)
        {
            xPlusWeight = newVal;
        }
        //xPlusWeight = Math.Max(0, int.Parse(xPlusWeightInput));
    }
    public void setYPlusWeight(String yPlusWeightInput)
    {
        int newVal = 0;
        bool isValidInput = int.TryParse(yPlusWeightInput, out newVal);
        if (isValidInput)
        {
            yPlusWeight = newVal;
        }
        //yPlusWeight = Math.Max(0, int.Parse(yPlusWeightInput));
    }

    public void setZPlusWeight(String zPlusWeightInput)
    {
        int newVal = 0;
        bool isValidInput = int.TryParse(zPlusWeightInput, out newVal);
        if (isValidInput)
        {
            zPlusWeight = newVal;
        }
        //zPlusWeight = Math.Max(0, int.Parse(zPlusWeightInput));
    }

    public void setXMinusWeight(String xMinusWeightInput)
    {
        int newVal = 0;
        bool isValidInput = int.TryParse(xMinusWeightInput, out newVal);
        if (isValidInput)
        {
            xMinusWeight = newVal;
        }
        //xMinusWeight = Math.Max(0, int.Parse(xMinusWeightInput));
    }
    public void setYMinusWeight(String yMinusWeightInput)
    {
        int newVal = 0;
        bool isValidInput = int.TryParse(yMinusWeightInput, out newVal);
        if (isValidInput)
        {
            yMinusWeight = newVal;
        }
        //yMinusWeight = Math.Max(0, int.Parse(yMinusWeightInput));
    }
    public void setZMinusWeight(String zMinusWeightInput)
    {
        int newVal = 0;
        bool isValidInput = int.TryParse(zMinusWeightInput, out newVal);
        if (isValidInput)
        {
            zMinusWeight = newVal;
        }
        //zMinusWeight = Math.Max(0, int.Parse(zMinusWeightInput));
    }

    public void setBoltzmannInput()
    {
        currentMethodBoltzmann = true;
        createGenerator(mapleInputField.text);
        //String toPrint = mapleInputField.text;
        //UnityEngine.Debug.Log("Got this string: " + toPrint);
    }

    public void setLengthTimedRun(String timedRunInput)
    {
        // numWalks = Math.Max(0, int.Parse(numWalksInput));
        int newVal = 0;
        bool isValidInput = int.TryParse(timedRunInput, out newVal);
        if (isValidInput)
        {
            lengthTimedRun = newVal;
        }
        UnityEngine.Debug.Log("Timed run length (seconds): " + lengthTimedRun);
    }

    public void importWalks()
    {
        resetWalks();

        //UnityEngine.Debug.Log(importInputField.text);
        string walksText = importInputField.text;

        // The format we're looking for is a set of square brackets enclosing a list of triplets of integers separeated by commas e.g. [(0,0,1),(0,0,-1)]
        // First, let's capture the lists with square brackets.

        string listPattern = @"\[([^\[\]]*)\]";
        Regex rxGetLists = new Regex(listPattern, RegexOptions.Compiled);

        MatchCollection matches = rxGetLists.Matches(walksText);

        foreach (Match match in matches)
        {
            string currentList = Regex.Replace(match.Groups[1].Value, @"\s", "");
            UnityEngine.Debug.Log(currentList);
            // Now get the individual steps.
            //string vec3Pattern = @"(\(-?\d+(\.\d+)?,-?\d+(\.\d+)?,-?\d+(\.\d+)?\))";
            string vec3Pattern = @"(\((-?\d+(?:\.\d+)?),(-?\d+(?:\.\d+)?),(-?\d+(?:\.\d+)?)\))";
            Regex rxGetVectors = new Regex(vec3Pattern, RegexOptions.Compiled);
            MatchCollection vecMatches = rxGetVectors.Matches(currentList);

            Vector3[] newWalk = new Vector3[vecMatches.Count];
            int index = 0;
            foreach (Match match1 in vecMatches)
            {
                UnityEngine.Debug.Log("New step: " + match1.Groups[1].Value);
                // We can assume that this is a properly formatted step.
                // Groups 2, 3 and 4 will be the respective values.
                float newXVal = 0;
                float newYVal = 0;
                float newZVal = 0;
                bool dummy = float.TryParse(match1.Groups[2].Value, out newXVal);
                dummy = float.TryParse(match1.Groups[3].Value, out newYVal);
                dummy = float.TryParse(match1.Groups[4].Value, out newZVal);

                newWalk[index] = new Vector3(newXVal, newYVal, newZVal);
                index++;
            }
            // Add the new walk to currentWalks
            currentWalks.Add(newWalk);
        }
        numWalks = currentWalks.Count;
        displayWalks();
    }

    public void confirmWeights()
    {
        currentMethodBoltzmann = false;
        int total = xPlusWeight + yPlusWeight + zPlusWeight + xMinusWeight + yMinusWeight + zMinusWeight;
        stepset = new Vector3[total];
        int index = 0;
        for (var i = 0; i < xPlusWeight; i++)
        {
            stepset[index] = scale * new Vector3(1, 0, 0);
            index++;
        }
        for (var i = 0; i < xMinusWeight; i++)
        {
            stepset[index] = scale * new Vector3(-1, 0, 0);
            index++;
        }
        for (var i = 0; i < yPlusWeight; i++)
        {
            stepset[index] = scale * new Vector3(0, 1, 0);
            index++;
        }
        for (var i = 0; i < yMinusWeight; i++)
        {
            stepset[index] = scale * new Vector3(0, -1, 0);
            index++;
        }
        for (var i = 0; i < zPlusWeight; i++)
        {
            stepset[index] = scale * new Vector3(0, 0, 1);
            index++;
        }
        for (var i = 0; i < zMinusWeight; i++)
        {
            stepset[index] = scale * new Vector3(0, 0, -1);
            index++;
        }
        logStepset("Naive");
    }

    static Vector3[] genStepSeriesNaive(Vector3[] stepSetInput, int numStepsInput)
    {
        Vector3[] toReturn = new Vector3[numStepsInput];
        for (var i = 0; i < numStepsInput; i++)
        {
            int randStep = UnityEngine.Random.Range(0, stepSetInput.Length);
            toReturn[i] = stepSetInput[randStep];
        }
        return toReturn;
    }

    Vector3[] genStepSeriesBoltzmann()
    {
        int currentLength = 0;
        //minSteps = Convert.ToInt32(numSteps - numSteps * 0.1d);
        //maxSteps = Convert.ToInt32(numSteps + numSteps * 0.1d);
        //numSteps = 1000;
        Vector3[] toReturn;
        do
        {
            toReturn = generate();
            numWalksGenerated++;
            currentLength = toReturn.Length;
            //UnityEngine.Debug.Log("currentLength: " + currentLength);
        } while (currentLength < minSteps | currentLength > maxSteps);

        return toReturn;
    }

    public void resetWalks()
    {
        // Reset the list of walks
        currentWalks = new List<Vector3[]>();
        currentWalksNormalized = new List<Vector3[]>();
        currentWalkObjects = new List<GameObject[]>();

        // Reset rejection counts
        numWalksGenerated = 0;
        numWalksRejectedLength = 0;
        numWalksRejectedTooShort = 0;
        numWalksRejectedTooLong = 0;
        numWalksRejectedOctant = 0;

        // Get rid of previous walk step counts
        Destroy(numStepsTextParent);
        numStepsTextParent = new GameObject("numStepsTextParent");
        numStepsTextParent.transform.parent = titleParent.transform;

        // Create a new empty game object at (0,0,0) to be the parent of the new walk cloud.
        Destroy(walkParent);
        walkParent = new GameObject("walkParent");
        walkParent.transform.parent = parent.transform;

        // Reset convex hulls
        convexHulls = new List<Mesh>();
        convexHullParent.GetComponent<MeshFilter>().mesh = new Mesh();
    }

    public void logInit()
    {
        string directoryPath = "TextOutputs";
        string newFilename = "TextOutputs/WalkLog-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".txt";
        if (!System.IO.Directory.Exists(directoryPath))
        {
            System.IO.Directory.CreateDirectory(directoryPath);
        }
        swLog = new StreamWriter(newFilename);
        swLog.AutoFlush = true;
        UnityEngine.Debug.Log("New log file: " + newFilename);
        swLog.WriteLine("New log file: " + newFilename);
        logStepset("Naive");

        // Same thing for statistics outputs

        string statsDirectoryPath = "StatsOutputs";
        string statsNewFilename = "StatsOutputs/StatLog-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".txt";
        if (!System.IO.Directory.Exists(statsDirectoryPath))
        {
            System.IO.Directory.CreateDirectory(statsDirectoryPath);
        }
        swStats = new StreamWriter(statsNewFilename);
        swStats.AutoFlush = true;
        UnityEngine.Debug.Log("New stats file: " + statsNewFilename);
        swStats.WriteLine("New stats file: " + statsNewFilename);
        // ORDER:
        // Current Method, Stepset, Drift, Accepted, Rejected (Length), Rejected (Short), Rejected (Long), Rejected (Octant), # Generated, Gen Time, Target Length, Min Length, Max Length
        // If the order changes in printReport it must change in logInit, and vice versa!
        swStats.WriteLine("Current Method\tStepset\tDrift\tAccepted\tRejected (Length) (Boltzmann only)\tRejected (Short) (Boltzmann only)\tRejected (Long) (Boltzmann only)\tRejected (Octant)\tNum Walks Generated\tGeneration Time (ms)\tTarget Length (Naive only)\tMin Length (Boltzmann only)\tMax Length (Boltzmann only)");
    }
    public void logCurrentWalks()
    {
        
        if (loggingWalks)
        {
            UnityEngine.Debug.Log("currentWalks:");
            for (int i = 0; i < currentWalks.Count; i++)
            {
                // Unity log
                UnityEngine.Debug.Log("Length: " + currentWalks[i].Length);
                //UnityEngine.Debug.Log("[" + string.Join(", ", currentWalks[i]) + "]");
                // File log
                swLog.WriteLine("Length: " + currentWalks[i].Length);
                swLog.WriteLine("[" + string.Join(", ", currentWalks[i]) + "]");

            }

        }

    }

    public void normalizeWalks()
    {
        // Find the largest x, y and z values in all of the current walks
        float currentMaxX = 1.0f;
        float currentMaxY = 1.0f;
        float currentMaxZ = 1.0f;
        // We need the POSITION SERIES in order to do this.
        for (int i = 0; i < currentWalks.Count; i++)
        {
            currentWalksNormalized.Add(new Vector3[currentWalks[i].Length + 1]);
            currentWalksNormalized[i][0] = Vector3.zero;
            for (int j = 1; j < currentWalks[i].Length + 1; j++)
            {
                currentWalksNormalized[i][j] = currentWalksNormalized[i][j - 1] + currentWalks[i][j-1];
                currentMaxX = Mathf.Max(currentMaxX, currentWalksNormalized[i][j].x);
                currentMaxY = Mathf.Max(currentMaxY, currentWalksNormalized[i][j].y);
                currentMaxZ = Mathf.Max(currentMaxZ, currentWalksNormalized[i][j].z);
            }
        }

        UnityEngine.Debug.Log("currentMaxX:" + currentMaxX + "  currentMaxY: " + currentMaxY + "  currentMaxZ: " + currentMaxZ);
        Vector3 normalizingVector = new Vector3(1 / currentMaxX,1 / currentMaxY,1 / currentMaxZ);
        UnityEngine.Debug.Log("Normalizing Vector: " + normalizingVector);

        // Store the normalized walks in currentWalksNormalized
        // Use Vector3.scale to multiply vectors componentwise
        for (int i = 0; i < currentWalksNormalized.Count; i++)
        {
            for (int j = 0; j < currentWalksNormalized[i].Length; j++)
            {
                currentWalksNormalized[i][j] = Vector3.Scale(currentWalksNormalized[i][j], normalizingVector);
            }
            // UnityEngine.Debug.Log("Normalized walk: [" + String.Join(", ", currentWalksNormalized[i]) + "]");
        }

        
    }

    public void logStepset(string genMethod = "Naive")
    {
        // Unity Log
        UnityEngine.Debug.Log("Current Generation Method: " + genMethod);
        UnityEngine.Debug.Log("Current Stepset: [" + string.Join(", ", stepset) + "]");
        // File Log
        swLog.WriteLine("Current Generation Method: " + genMethod);
        swLog.WriteLine("Current Stepset: [" + string.Join(", ", stepset) + "]");
    }

    public void displayWalkCloudBoltzmann()
    {

        currentMethodBoltzmann = true;
        resetWalks();
        var watch = new System.Diagnostics.Stopwatch();
        watch.Start();
        for (var j = 0; j < numWalks; j++)
        {
            var newStepSeries = genStepSeriesOctantBoltzmann();
            //UnityEngine.Debug.Log("Number of steps: " + newStepSeries.Length);
            //UnityEngine.Debug.Log(string.Join(", ", newStepSeries));
            //displayWalk(newStepSeries);

        }
        watch.Stop();
        timeToGenerate = watch.ElapsedMilliseconds;
        
        displayWalks();
        numWalksRejectedTooShort = numWalksRejectedLength - numWalksRejectedTooLong;
        logCurrentWalks();
        normalizeWalks();
        printReport();
    }

    public void displayWalkCloud()
    {
        currentMethodBoltzmann = false;
        resetWalks();
        var watch = new System.Diagnostics.Stopwatch();
        watch.Start();
        for (var j = 0; j < numWalks; j++)
        {
            var newStepSeries = genStepSeriesOctantNaive(stepset, numSteps);
            UnityEngine.Debug.Log("New walk: [" + String.Join(", ", newStepSeries) + "]");
            //displayWalk(newStepSeries);
        }
        watch.Stop();
        timeToGenerate = watch.ElapsedMilliseconds;

        displayWalks();
        numWalksRejectedTooShort = numWalksRejectedLength - numWalksRejectedTooLong;
        logCurrentWalks();
        normalizeWalks();
        printReport();
    }

    public void timedRun()
    {
        // Get the time first
        UnityEngine.Debug.Log("lengthTimedRun: " + lengthTimedRun);
        int lengthMillis = lengthTimedRun * 1000;
        // Different procedure depending on whether settings are Boltzmann or Naive
        resetWalks();
        maxSteps = 10000000;
        var watch = new System.Diagnostics.Stopwatch();
        watch.Start();
        for (var j = 0; watch.ElapsedMilliseconds < lengthMillis; j++)
        {
            int minWalkSize = 0;
            Vector3[] newStepSeries;
            if (currentMethodBoltzmann)
            {
                newStepSeries = genStepSeriesBoltzmann();
            }
            else
            {
                newStepSeries = genStepSeriesOctantNaiveNoAdd(stepset, j);
            }
            
            //UnityEngine.Debug.Log("New walk: [" + String.Join(", ", newStepSeries) + "]");
            // Now we keep the longest walks and remove the shortest one if necessary.
            if (currentWalks.Count < numWalks)
            {
                currentWalks.Add(newStepSeries);
            }
            else if (newStepSeries.Length > minWalkSize)
            {
                // Find the length and index of the shortest walk in currentWalks
                int currentMin = currentWalks[0].Length;
                int currentIndex = 0;
                for (int i = 1; i < numWalks; i++)
                {
                    if (currentWalks[i].Length < currentMin)
                    {
                        currentMin = currentWalks[i].Length;
                        currentIndex = i;
                    }
                }
                minWalkSize = currentMin;
                if (newStepSeries.Length > currentMin)
                {
                    currentWalks[currentIndex] = newStepSeries;
                }
            }
        }
        displayWalks();
        logCurrentWalks();
        normalizeWalks();
        printReport();

    }

    private void printReport()
    {
        // ORDER:
        // Current Method, Stepset, Drift, Rejected (Length), Rejected (Short), Rejected (Long), Rejected (Octant), # Generated, Gen Time, Target Length, Min Length, Max Length
        // If the order changes in printReport it must change in logInit, and vice versa!
        string statString = "";

        string methodString;
        if (currentMethodBoltzmann)
        {
            methodString = "Boltzmann";
        }
        else
        {
            methodString = "Naive";
        }
        UnityEngine.Debug.Log("Current method: " + methodString);
        statString += methodString + "\t";
        UnityEngine.Debug.Log("Stepset: " + stepset); // Will have to change this to make it readable
        statString += "[" + String.Join(", ", stepset) + "]\t";

        // Calculate drift. Just the sum of all the vectors in the stepset
        Vector3 drift = Vector3.zero;
        for (int i = 0; i < stepset.Length; i++)
        {
            drift = drift + stepset[i];
        }
        UnityEngine.Debug.Log("Drift: " + drift);
        statString += drift + "\t";
        UnityEngine.Debug.Log("# Accepted: " + (numWalksGenerated - numWalksRejectedLength - numWalksRejectedOctant));
        statString += (numWalksGenerated - numWalksRejectedLength - numWalksRejectedOctant) + "\t";
        UnityEngine.Debug.Log("# Walks Rejected for Length  (Boltzmann only): " + numWalksRejectedLength); // Itemize too long and too short?
        statString += numWalksRejectedLength + "\t";
        UnityEngine.Debug.Log("# Walks Rejected for Length (Too Short) (Boltzmann only): " + numWalksRejectedTooShort);
        statString += numWalksRejectedTooShort + "\t";
        UnityEngine.Debug.Log("# Walks Rejected for Length (Too Long) (Boltzmann only): " + numWalksRejectedTooLong);
        statString += numWalksRejectedTooLong + "\t";
        UnityEngine.Debug.Log("# Walks Rejected for Leaving Octant: " + numWalksRejectedOctant);
        statString += numWalksRejectedOctant + "\t";
        UnityEngine.Debug.Log("# Walks Generated in Total: " + numWalksGenerated);
        statString += numWalksGenerated + "\t";
        UnityEngine.Debug.Log("Generation time in milliseconds: " + timeToGenerate);
        statString += timeToGenerate + "\t";
        UnityEngine.Debug.Log("Target length (Naive only): " + numSteps);
        statString += numSteps + "\t";
        UnityEngine.Debug.Log("Min length (Boltzmann only): " + minSteps);
        statString += minSteps + "\t";
        UnityEngine.Debug.Log("Max length (Boltzmann only): " + maxSteps);
        statString += maxSteps;

        swStats.WriteLine(statString);
    }

    void displayWalks()
    {
        for (var i = 0; i < numWalks; i++)
        {
            displayWalk(currentWalks[i]);
        }
        togglePositionColour(colourXYZ);
        toggleConvexHulls(displayConvexHulls);
    }

    void displayWalk(Vector3[] stepSeriesInput)
    {
        // Create a prefab of the necessary color?
        // Must also be the right scale!
        Material material = Instantiate(baseMaterial);
        //material.color = colorInput;
        GameObject displayObject = Instantiate(baseObject);
        Renderer renderer = displayObject.GetComponent<Renderer>();
        renderer.material = material;
        Color nextColor = Color.HSVToRGB(0.0f, 1.0f, 1.0f);
        position = Vector3.zero;
        float alphaValue = 1.0f / numWalks;

        // Keep track of the cubes on an array:
        GameObject[] newWalkCubes = new GameObject[stepSeriesInput.Length];
        for (var i = 0; i < stepSeriesInput.Length; i++)
        {
            // Want colour to range from Red to Violet.

            //renderer.sharedMaterial.SetColor("_Color", new Color(255 * 1.0f / 255, 126 * 1.0f / 255, 0 * 1.0f / 255, alphaValue));
            //renderer.material.SetColor("_Color", new Color(nextColor.r, nextColor.g, nextColor.b, alphaValue));

            GameObject newCube = Instantiate(displayObject, position, Quaternion.identity, walkParent.transform);
            //newCube.GetComponent<Renderer>().material.SetColor("_Color", new Color(nextColor.r, nextColor.g, nextColor.b, alphaValue));
            newWalkCubes[i] = newCube;

            position = position + stepSeriesInput[i];
            //nextColor = Color.HSVToRGB(0.9f * i / stepSeriesInput.Length, 1.0f, 1.0f);
            
            //UnityEngine.Debug.Log("Red value: " + nextColor.r);
            //UnityEngine.Debug.Log("Blue value: " + nextColor.b);
            //UnityEngine.Debug.Log("Green value: " + nextColor.g);

        }
        currentWalkObjects.Add(newWalkCubes);

        //UnityEngine.Debug.Log("Last position in walk: " + position);
        GameObject newTitleCanvas = Instantiate(numStepsTextCanvas, position, Quaternion.identity, numStepsTextParent.transform);
        newTitleCanvas.transform.localScale = new Vector3(scale / 30.0f, scale / 30.0f, scale / 30.0f);
        TextMeshProUGUI newTitleText = newTitleCanvas.transform.GetChild(0).gameObject.GetComponent<TextMeshProUGUI>();
        newTitleText.color = new Color(0, 0, 0, 0.5f);
        //UnityEngine.Debug.Log(newTitleText.name);
        ((TextMeshProUGUI)newTitleText).SetText("Length: " + stepSeriesInput.Length);

        //TextMeshProUGUI newTitleText = Instantiate(numStepsText, position, Quaternion.identity, newTitleCanvas.transform);
        //newTitleText.SetText("New Message");
        Destroy(displayObject);
    }

    public void displayThreeWalks()
    {
        resetWalks();
        Vector3[] newStepSeries = genStepSeriesOctantNaive(stepset, 200);
        displayWalkSolid(newStepSeries, Color.blue);

        newStepSeries = genStepSeriesOctantNaive(stepset, 200);
        displayWalkSolid(newStepSeries, Color.red);

        newStepSeries = genStepSeriesOctantNaive(stepset, 200);
        displayWalkSolid(newStepSeries, Color.green);


    }

    void displayWalkSolid(Vector3[] stepSeriesInput, Color colorInput)
    {
        // Create a prefab of the necessary color?
        // Must also be the right scale!
        Material material = Instantiate(baseMaterial);
        material.color = colorInput;
        GameObject displayObject = Instantiate(baseObject);
        Renderer renderer = displayObject.GetComponent<Renderer>();
        renderer.material = material;
        position = Vector3.zero;

        // Keep track of the cubes on an array:
        GameObject[] newWalkCubes = new GameObject[stepSeriesInput.Length];
        for (var i = 0; i < stepSeriesInput.Length; i++)
        {
            // Want colour to range from Red to Violet.

            //renderer.sharedMaterial.SetColor("_Color", new Color(255 * 1.0f / 255, 126 * 1.0f / 255, 0 * 1.0f / 255, alphaValue));
            //renderer.material.SetColor("_Color", new Color(nextColor.r, nextColor.g, nextColor.b, alphaValue));

            GameObject newCube = Instantiate(displayObject, position, Quaternion.identity, walkParent.transform);
            // newCube.GetComponent<Renderer>().material.SetColor("_Color", new Color(nextColor.r, nextColor.g, nextColor.b, alphaValue));
            newWalkCubes[i] = newCube;

            position = position + stepSeriesInput[i];
            //UnityEngine.Debug.Log("Red value: " + nextColor.r);
            //UnityEngine.Debug.Log("Blue value: " + nextColor.b);
            //UnityEngine.Debug.Log("Green value: " + nextColor.g);

        }
        currentWalkObjects.Add(newWalkCubes);

        UnityEngine.Debug.Log("Last position in walk: " + position);
        GameObject newTitleCanvas = Instantiate(numStepsTextCanvas, position, Quaternion.identity, numStepsTextParent.transform);
        newTitleCanvas.transform.localScale = new Vector3(scale / 30.0f, scale / 30.0f, scale / 30.0f);
        TextMeshProUGUI newTitleText = newTitleCanvas.transform.GetChild(0).gameObject.GetComponent<TextMeshProUGUI>();
        //UnityEngine.Debug.Log(newTitleText.name);
        ((TextMeshProUGUI)newTitleText).SetText("Length: " + stepSeriesInput.Length);

        //TextMeshProUGUI newTitleText = Instantiate(numStepsText, position, Quaternion.identity, newTitleCanvas.transform);
        //newTitleText.SetText("New Message");
        Destroy(displayObject);
    }

    static Boolean confinedToOctant(Vector3[] stepSeriesInput)
    {
        var position = Vector3.zero;
        for (var i = 0; i < stepSeriesInput.Length; i++)
        {
            position = position + stepSeriesInput[i];
            if (position.x < 0 | position.y < 0 | position.z < 0)
            {
                return false;
            }
        }
        return true;
    }

    Vector3[] genStepSeriesOctantBoltzmann()
    {
        // I've adjusted the generator so that it rejects walks that leave the octant early on.
        Vector3[] currentWalk = genStepSeriesBoltzmann();
        currentWalks.Add(currentWalk);
        return currentWalk;
    }

    Vector3[] genStepSeriesOctantBoltzmannLegacy()
    {
        Boolean walkFound = false;
        Vector3[] currentWalk = genStepSeriesBoltzmann();
        //numWalksGenerated++;
        while (!walkFound)
        {
            if (confinedToOctant(currentWalk))
            //if (true) // use this line to debug and just get half-plane walks
            {
                walkFound = true;
                currentWalks.Add(currentWalk);
                return currentWalk;
            }
            else
            {
                numWalksRejectedOctant++;
            }
            currentWalk = genStepSeriesBoltzmann();
            //numWalksGenerated++;
        }
        return currentWalk;
    }

    Vector3[] genStepSeriesOctantNaive(Vector3[] stepSetInput, int numStepsInput)
    {
        Boolean walkFound = false;
        Vector3[] currentWalk = new Vector3[0];  // = genStepSeriesNaive(stepSetInput, numStepsInput);
        while (!walkFound)
        {

            // New strategy: Use the same procedure as genStepSeriesNaive, but reject as soon as it leaves the octant

            currentWalk = new Vector3[numStepsInput];
            Vector3 position = Vector3.zero;
            walkFound = true;
            for (var i = 0; i < numStepsInput && walkFound; i++)
            {
                int randStep = UnityEngine.Random.Range(0, stepSetInput.Length);
                currentWalk[i] = stepSetInput[randStep];
                position += currentWalk[i];
                if (position.x < 0 || position.y < 0 || position.z < 0)
                {
                    // The walk left the octant and must be rejected.
                    numWalksRejectedOctant++;
                    walkFound = false;
                }
            }
            numWalksGenerated++;
            if (walkFound)
            {
                currentWalks.Add(currentWalk);
                return currentWalk;
            }
        }
        return currentWalk;
    }

    Vector3[] genStepSeriesOctantNaiveNoAdd(Vector3[] stepSetInput, int numStepsInput)
    {
        Boolean walkFound = false;
        Vector3[] currentWalk = new Vector3[0];  // = genStepSeriesNaive(stepSetInput, numStepsInput);
        while (!walkFound)
        {

            // New strategy: Use the same procedure as genStepSeriesNaive, but reject as soon as it leaves the octant

            currentWalk = new Vector3[numStepsInput];
            Vector3 position = Vector3.zero;
            walkFound = true;
            for (var i = 0; i < numStepsInput && walkFound; i++)
            {
                int randStep = UnityEngine.Random.Range(0, stepSetInput.Length);
                currentWalk[i] = stepSetInput[randStep];
                position += currentWalk[i];
                if (position.x < 0 || position.y < 0 || position.z < 0)
                {
                    // The walk left the octant and must be rejected.
                    numWalksRejectedOctant++;
                    walkFound = false;
                }
            }
            numWalksGenerated++;
            if (walkFound)
            {
                return currentWalk;
            }
        }
        return currentWalk;
    }

    public void createGenerator(string mapleInput)
    {
        UnityEngine.Debug.Log("Input string: " + mapleInput);

        string pattern = @"\[evaluations = \{(.*)\}, grammar = \{(.*)\}, rho_approx = \{(.*)\}, atomSet = \{(.*)\}\]"; // Capture whatever is in the curly brackets.


        // Get EVALUATIONS, GRAMMAR, RHO_APPROX and ATOMSET In that order
        // I'm looking for strings that are surrounded by {} curly brackets.
        // Should use Matches method.

        Regex rx = new Regex(pattern, RegexOptions.Compiled);
        var match = rx.Match(mapleInput);
        UnityEngine.Debug.Log(match);

        GroupCollection groups = match.Groups;

        string evaluationsString = groups[1].Value;
        UnityEngine.Debug.Log("Evaluations: {0}" + evaluationsString);
        string grammarString = groups[2].Value;
        UnityEngine.Debug.Log("Grammar: {0}" + grammarString);
        string rhoApproxString = groups[3].Value;
        UnityEngine.Debug.Log("Rho approximation: {0}" + rhoApproxString);
        string atomSetString = groups[4].Value;
        UnityEngine.Debug.Log("Atom Set = {0}" + atomSetString);


        // Some easy things I can do right away:
        // Covert rho_approx to a numerical value
        double rhoApprox = Convert.ToDouble(rhoApproxString);

        // Get the names of the symbols.
        // Use a Regex on GrammarString to get the left hand side of every equation.
        string symbolPattern = @"([^\s]+) = ([^\s]+)(?:,|$)";
        Regex rxGetSymbols = new Regex(symbolPattern, RegexOptions.Compiled);

        MatchCollection matches = rxGetSymbols.Matches(grammarString);

        UnityEngine.Debug.Log("Now to see if we can read the grammar.");
        UnityEngine.Debug.Log("{0} matches found." + matches.Count);

        // See if we can capture the keys for the dictionary:
        Dictionary<string, Dictionary<string, object>> newGenerators = new Dictionary<string, Dictionary<string, object>>();
        foreach (Match m in matches)
        {
            var newKey = m.Groups[1].Value;
            UnityEngine.Debug.Log(newKey);
            newGenerators[newKey] = new Dictionary<string, object>();
            var symbolGrammarString = m.Groups[2].Value;
            newGenerators[newKey]["grammarString"] = (string)symbolGrammarString;

            UnityEngine.Debug.Log(symbolGrammarString);
        }
        // Add Epsilon separately:
        newGenerators["Epsilon"] = new Dictionary<string, object>();
        newGenerators["Epsilon"]["grammarString"] = "Epsilon";

        // Make a dictionary of atoms!
        string atomPattern = @"(\S+) = (\[[^\]]*\])";
        Dictionary<string, Vector3> newAtomDictionary = new Dictionary<string, Vector3>();
        Regex rxGetAtoms = new Regex(atomPattern, RegexOptions.Compiled);
        MatchCollection atomMatches = rxGetAtoms.Matches(atomSetString);
        UnityEngine.Debug.Log("Get the atoms:");
        foreach (Match m in atomMatches)
        {
            var newKey = m.Groups[1].Value;
            UnityEngine.Debug.Log(newKey);
            var atomValueString = m.Groups[2].Value;
            UnityEngine.Debug.Log(atomValueString);
            // Ultimately, we want to turn atomValueString into a Vector3
            string getVectorPattern = @"([^\s\[\],]+)"; // Anything that isn't a space, a bracked or a comma must be a number.
            Regex rxGetVectors = new Regex(getVectorPattern, RegexOptions.Compiled);
            MatchCollection vectorMatches = rxGetVectors.Matches(atomValueString);
            // Vector3 atomVector = scale * new Vector3(float.Parse(vectorMatches[0].Groups[1].Value),
            //                                    float.Parse(vectorMatches[1].Groups[1].Value),
            //                                    float.Parse(vectorMatches[2].Groups[1].Value));
            Vector3 atomVector = new Vector3(scale * float.Parse(vectorMatches[0].Groups[1].Value),
                                             scale * float.Parse(vectorMatches[1].Groups[1].Value),
                                             scale * float.Parse(vectorMatches[2].Groups[1].Value));
            newAtomDictionary[newKey] = atomVector;
        }
        stepset = newAtomDictionary.Values.ToArray();

        // Get the evaluations

        //var evaluationsList = evaluationsString.Split(new char[] { ',' });

        // Use a Regex on evaluationsString to find the key and the value
        string evalPattern = @"([^\s]+) = ([0-9.]+)";
        Regex rxGetEvaluations = new Regex(evalPattern, RegexOptions.Compiled);

        MatchCollection evalMatches = rxGetEvaluations.Matches(evaluationsString);
        UnityEngine.Debug.Log("Now to see if we can read the evaluations.");
        UnityEngine.Debug.Log("{0} matches found." + evalMatches.Count);

        foreach (Match m in evalMatches)
        {
            newGenerators[m.Groups[1].Value]["evaluation"] = Convert.ToDouble(m.Groups[2].Value);
            UnityEngine.Debug.Log("newGenerators[" + m.Groups[1].Value + "]['evaluation'] = " + newGenerators[m.Groups[1].Value]["evaluation"]);
        }
        // Add Epsilon separately:
        newGenerators["Epsilon"]["evaluation"] = (double)1;


        // Precompute union summands and evaluations, and items in products.
        var keys = newGenerators.Keys.ToList();
        foreach (string k in keys)
        {
            string currentGrammar = (string)newGenerators[k]["grammarString"];
            if (currentGrammar.Contains("Union") && !newGenerators.ContainsKey(currentGrammar))
            {
                string summandPattern = @"((?:Epsilon|Prod\([^\(\)]+\)))";
                Regex rxGetSummands = new Regex(summandPattern, RegexOptions.Compiled);
                MatchCollection summandMatches = rxGetSummands.Matches(currentGrammar);

                int numSummands = 0;

                string[] newSummandList = new string[summandMatches.Count];
                double[] newSummandEvals = new double[summandMatches.Count];
                double currentEval = 0;

                UnityEngine.Debug.Log("newSummandList.Length: " + newSummandList.Length);
                foreach (Match m in summandMatches)
                {
                    string newSummand = m.Groups[1].Value;
                    newSummandList[numSummands] = newSummand;

                    // newSummand must either be a product, or a symbol.
                    if (newSummand.Contains("Prod"))
                    {
                        // Get the items in the product
                        string[] productSymbols;
                        if (!newGenerators.ContainsKey(newSummand))
                        {
                            productSymbols = newSummand.Substring(5, newSummand.IndexOf(")") - 5).Split(',');
                            newGenerators[newSummand] = new Dictionary<string, object>();
                            newGenerators[newSummand]["productSymbols"] = productSymbols;
                        }
                        else
                        {
                            productSymbols = (string[])newGenerators[newSummand]["productSymbols"];
                        }

                        // From the end to the beginning, push them into the stack.
                        double newEval = 1;
                        for (int i = 0; i < productSymbols.Length; i++)
                        {
                            UnityEngine.Debug.Log(productSymbols[i]);
                            newEval *= (double)newGenerators[productSymbols[i]]["evaluation"];
                            newSummandEvals[numSummands] = currentEval;
                        }
                        currentEval += newEval;
                        newSummandEvals[numSummands] = currentEval;
                    }
                    else
                    {
                        UnityEngine.Debug.Log("newSummand: " + newSummand);
                        currentEval += (double)newGenerators[newSummand]["evaluation"];
                        newSummandEvals[numSummands] = currentEval;
                    }
                    numSummands++;

                }
                UnityEngine.Debug.Log(numSummands);
                UnityEngine.Debug.Log(string.Join(", ", newSummandList));
                UnityEngine.Debug.Log(string.Join(", ", newSummandEvals));
                if (numSummands == 1)
                {
                    newGenerators[k]["grammarString"] = newSummandList[0];
                }
                newGenerators[currentGrammar] = new Dictionary<string, object>();
                newGenerators[currentGrammar]["summandList"] = newSummandList;
                newGenerators[currentGrammar]["summandEvals"] = newSummandEvals;

            }
            else if (currentGrammar.Contains("Prod") && !newGenerators.ContainsKey(currentGrammar))
            {
                string[] productSymbols = currentGrammar.Substring(5, currentGrammar.IndexOf(")") - 5).Split(',');
                newGenerators[currentGrammar] = new Dictionary<string, object>();
                newGenerators[currentGrammar]["productSymbols"] = productSymbols;
            }
        }

        atomDictionary = newAtomDictionary;
        generators = newGenerators;
        logStepset("Boltzmann");
    }

    public Vector3[] generate()
    {
        // First, start with a stack that just contains the initial nonterminal symbol.
        Stack<string> stringWalk = new Stack<string>();

        if (generators.ContainsKey("P_pointed"))
        {
            stringWalk.Push("P_pointed");
        }
        else
        {
            stringWalk.Push("P");
        }

        // Keep track of position!
        Vector3 position = Vector3.zero;

        List<Vector3> outList = new List<Vector3>();

        while (stringWalk.Count > 0)
        {
            string currentSymbol = stringWalk.Pop();
            //UnityEngine.Debug.Log(currentSymbol);



            if (currentSymbol.Equals("Epsilon"))
            {
                // No action. Nothing gets added to the output or to the stack.
            }
            else
            {
                string currentGrammar = (string)generators[currentSymbol]["grammarString"];
                if (currentGrammar.Contains("Union"))
                {
                    string[] newSummandList = (string[])generators[currentGrammar]["summandList"];
                    double[] newSummandEvals = (double[])generators[currentGrammar]["summandEvals"];
                    int numSummands = newSummandList.Length;

                    // Now, randomize a value
                    double u = random.NextDouble() * newSummandEvals[numSummands - 1];
                    // Then go through the eval list. Depending on what value we get, that determines what we push to the stack
                    for (int i = 0; i < numSummands; i++)
                    {
                        if (u < newSummandEvals[i])
                        {
                            // Must check whether it's a product or a symbol
                            string toAdd = newSummandList[i];
                            if (toAdd.Contains("Prod"))
                            {
                                // Push the items in the product to the stack
                                string[] productSymbols = (string[])generators[toAdd]["productSymbols"];
                                for (int j = productSymbols.Length - 1; j >= 0; j--)
                                {
                                    if (!productSymbols[j].Equals("Epsilon"))
                                    {
                                        stringWalk.Push(productSymbols[j]);
                                    }
                                }
                                break;
                            }
                            else
                            {
                                if (!toAdd.Equals("Epsilon"))
                                {
                                    stringWalk.Push(toAdd);
                                }
                                break;
                            }
                        }
                    }
                }
                else if (currentGrammar.Contains("Prod")) // Products will not contain Unions, just symbols
                {
                    // Get the items in the product
                    string[] productSymbols = (string[])generators[currentGrammar]["productSymbols"];
                    // From the end to the beginning, push them into the stack.
                    for (int i = productSymbols.Length - 1; i >= 0; i--)
                    {
                        if (!productSymbols[i].Equals("Epsilon"))
                        {
                            stringWalk.Push(productSymbols[i]);
                        }
                    }
                }
                else if (currentGrammar.Contains("Atom"))
                {
                    Vector3 newStep = atomDictionary[currentSymbol];
                    outList.Add(newStep);
                    // Reject walks that exceed maxSteps
                    if (outList.Count > maxSteps)
                    {
                        numWalksRejectedTooLong += 1;
                        numWalksRejectedLength += 1;
                        return new Vector3[] { };
                    }
                    // Reject walks that leave the octant
                    position += newStep;
                    if (position.x < 0 || position.y < 0 || position.z < 0)
                    {
                        numWalksRejectedOctant += 1;
                        return new Vector3[] { };
                    }
                }
                else
                {
                    // Some error must have occurred.
                    return new Vector3[] { };
                }
            }
        }
        if (outList.Count < minSteps)
        {
            // Too short
            numWalksRejectedTooShort += 1;
            numWalksRejectedLength += 1;
            return new Vector3[] { };
        }

        return outList.ToArray();

    }

    private void testUniformity(int testWalkLength, int testNumWalks)
    {
        // Use the stepset with drift (-1,-1,-1)
        string grammarString = "[evaluations = {D = 1.592592638, P = 2.820845434, P_aux = 1.772750416, L_1 = .2040693826, L_2 = .03889145064, L_3 = .05530660054, L_4 = .1388679150, R_1 = .3987497634, R_2 = .1224121716, R_3 = .3113246277, R_4 = 1.527547068, a_1 = .08720000787, a_2 = .08720000787, b_1 = .08720000787, b_2 = .08720000787, b_3 = .08720000787, b_4 = .08720000787, b_5 = .08720000787, b_6 = .08720000787, b_7 = .08720000787, b_8 = .08720000787, b_9 = .08720000787, b_10 = .08720000787, b_11 = .08720000787, b_12 = .08720000787, b_13 = .08720000787, c_1 = .08720000787, c_2 = .08720000787}, grammar = {D = Union(Epsilon,Prod(c_1,D),Prod(c_2,D),Prod(L_1,R_1),Prod(L_2,R_2),Prod(L_3,R_3),Prod(L_4,R_4)), P = Prod(D,P_aux), P_aux = Union(Epsilon,Prod(L_1,P_aux),Prod(L_2,P_aux),Prod(L_3,P_aux),Prod(L_4,P_aux)), L_1 = Union(Prod(a_2,D),Prod(L_2,R_1),Prod(L_3,R_2),Prod(L_4,R_3)), L_2 = Union(Prod(L_3,R_1),Prod(L_4,R_2)), L_3 = Union(Prod(L_4,R_1)), L_4 = Union(Prod(a_1,D)), R_1 = Union(Prod(b_12,D),Prod(b_13,D),Prod(L_1,R_2),Prod(L_2,R_3),Prod(L_3,R_4)), R_2 = Union(Prod(L_1,R_3),Prod(L_2,R_4)), R_3 = Union(Prod(L_1,R_4)), R_4 = Union(Prod(b_1,D),Prod(b_2,D),Prod(b_3,D),Prod(b_4,D),Prod(b_5,D),Prod(b_6,D),Prod(b_7,D),Prod(b_8,D),Prod(b_9,D),Prod(b_10,D),Prod(b_11,D)), a_1 = Atom, a_2 = Atom, b_1 = Atom, b_2 = Atom, b_3 = Atom, b_4 = Atom, b_5 = Atom, b_6 = Atom, b_7 = Atom, b_8 = Atom, b_9 = Atom, b_10 = Atom, b_11 = Atom, b_12 = Atom, b_13 = Atom, c_1 = Atom, c_2 = Atom}, rho_approx = {.8720000787e-1}, atomSet = {a_1 = [1, 0, 0], a_2 = [0, 1, 0], b_1 = [-1, 0, 0], b_2 = [-1, 0, 0], b_3 = [-1, 0, 0], b_4 = [-1, 0, 0], b_5 = [-1, 0, 0], b_6 = [-1, 0, 0], b_7 = [-1, 0, 0], b_8 = [-1, 0, 0], b_9 = [-1, 0, 0], b_10 = [-1, 0, 0], b_11 = [-1, 0, 0], b_12 = [0, -1, 0], b_13 = [0, -1, 0], c_1 = [0, 0, 1], c_2 = [0, 0, -1]}]";
        createGenerator(grammarString);
        minSteps = testWalkLength;
        maxSteps = testWalkLength;
        int dimArray = testWalkLength + 1;
        int[,,] numWalksTable = new int[dimArray, dimArray, dimArray];
        for (int i = 0; i < testNumWalks; i++)
        {
            // Generate a walk
            Vector3[] newStepSeries = genStepSeriesOctantBoltzmann();
            // Find the final position
            Vector3 finalPosition = Vector3.zero;
            for (int j = 0; j < newStepSeries.Length; j++)
            {
                finalPosition = finalPosition + newStepSeries[j];
            }
            int x = (int)(finalPosition[0]);
            int y = (int)(finalPosition[1]);
            int z = (int)(finalPosition[2]);
            // Increment that element in the array by 1.
            UnityEngine.Debug.Log("(x,y,z) = (" + x + "," + y + "," + z + ")");
            numWalksTable[x, y, z] += 1;

        }
        string mapleList = "[";
        for (int i = 0; i < dimArray ;i++)
        {
            mapleList = mapleList + "[";
            for (int j = 0; j < dimArray; j++)
            {
                mapleList = mapleList + "[";
                for (int k = 0; k < dimArray - 1; k++)
                {
                    mapleList += numWalksTable[i, j, k] + ",";
                }
                mapleList += numWalksTable[i, j, dimArray - 1]; // No comma for the last entry
                mapleList = mapleList + "]";
                if (j < dimArray - 1)
                {
                    mapleList = mapleList + ",";
                }
            }
            mapleList = mapleList + "]";
            if (i < dimArray - 1)
            {
                mapleList = mapleList + ",";
            }
        }
        mapleList = mapleList + "]";
        UnityEngine.Debug.Log("Array to output: " + mapleList);
        string listName = "unityTest" + testNumWalks;
        string newFilename = "TextOutputs/" + listName + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".txt";
        StreamWriter testOutput = new StreamWriter(newFilename);
        testOutput.AutoFlush = true;
        testOutput.WriteLine(listName + " := " + mapleList + ":");
        testOutput.WriteLine(listName + "Normalized := " + listName + "/(1.0 * getSumFromList(" + listName + ")):");
    }

    private void testExcursions(int testWalkLength, int testNumWalks)
    {
        // Use the stepset with drift (-1,-1,-1)
        string grammarString = "[evaluations = {D = 1.592592638, P = 2.820845434, P_aux = 1.772750416, L_1 = .2040693826, L_2 = .03889145064, L_3 = .05530660054, L_4 = .1388679150, R_1 = .3987497634, R_2 = .1224121716, R_3 = .3113246277, R_4 = 1.527547068, a_1 = .08720000787, a_2 = .08720000787, b_1 = .08720000787, b_2 = .08720000787, b_3 = .08720000787, b_4 = .08720000787, b_5 = .08720000787, b_6 = .08720000787, b_7 = .08720000787, b_8 = .08720000787, b_9 = .08720000787, b_10 = .08720000787, b_11 = .08720000787, b_12 = .08720000787, b_13 = .08720000787, c_1 = .08720000787, c_2 = .08720000787}, grammar = {D = Union(Epsilon,Prod(c_1,D),Prod(c_2,D),Prod(L_1,R_1),Prod(L_2,R_2),Prod(L_3,R_3),Prod(L_4,R_4)), P = Prod(D,P_aux), P_aux = Union(Epsilon,Prod(L_1,P_aux),Prod(L_2,P_aux),Prod(L_3,P_aux),Prod(L_4,P_aux)), L_1 = Union(Prod(a_2,D),Prod(L_2,R_1),Prod(L_3,R_2),Prod(L_4,R_3)), L_2 = Union(Prod(L_3,R_1),Prod(L_4,R_2)), L_3 = Union(Prod(L_4,R_1)), L_4 = Union(Prod(a_1,D)), R_1 = Union(Prod(b_12,D),Prod(b_13,D),Prod(L_1,R_2),Prod(L_2,R_3),Prod(L_3,R_4)), R_2 = Union(Prod(L_1,R_3),Prod(L_2,R_4)), R_3 = Union(Prod(L_1,R_4)), R_4 = Union(Prod(b_1,D),Prod(b_2,D),Prod(b_3,D),Prod(b_4,D),Prod(b_5,D),Prod(b_6,D),Prod(b_7,D),Prod(b_8,D),Prod(b_9,D),Prod(b_10,D),Prod(b_11,D)), a_1 = Atom, a_2 = Atom, b_1 = Atom, b_2 = Atom, b_3 = Atom, b_4 = Atom, b_5 = Atom, b_6 = Atom, b_7 = Atom, b_8 = Atom, b_9 = Atom, b_10 = Atom, b_11 = Atom, b_12 = Atom, b_13 = Atom, c_1 = Atom, c_2 = Atom}, rho_approx = {.8720000787e-1}, atomSet = {a_1 = [1, 0, 0], a_2 = [0, 1, 0], b_1 = [-1, 0, 0], b_2 = [-1, 0, 0], b_3 = [-1, 0, 0], b_4 = [-1, 0, 0], b_5 = [-1, 0, 0], b_6 = [-1, 0, 0], b_7 = [-1, 0, 0], b_8 = [-1, 0, 0], b_9 = [-1, 0, 0], b_10 = [-1, 0, 0], b_11 = [-1, 0, 0], b_12 = [0, -1, 0], b_13 = [0, -1, 0], c_1 = [0, 0, 1], c_2 = [0, 0, -1]}]";
        createGenerator(grammarString);
        minSteps = (int)Math.Round(testWalkLength * 0.95);
        maxSteps = (int)Math.Round(testWalkLength * 1.05);
        int dimArray = testWalkLength + 1;
        int numExcursions = 0;
        int numReturnToOrigin = 0;

        int x, y, z = 0;
        for (int i = 0; i < testNumWalks; i++)
        {
            // Generate a walk
            Vector3[] newStepSeries = genStepSeriesOctantBoltzmann();
            // Find the final position
            bool returnsHome = false;
            Vector3 finalPosition = Vector3.zero;
            for (int j = 0; j < newStepSeries.Length; j++)
            {
                finalPosition = finalPosition + newStepSeries[j];
                x = (int)(finalPosition[0]);
                y = (int)(finalPosition[1]);
                z = (int)(finalPosition[2]);
                if (x == 0 && y == 0 && z == 0)
                {
                    returnsHome = true;
                }
            }
            if (returnsHome)
            {
                numReturnToOrigin++;
            }
            // Now check whether it's an excursion (or one step away from an excursion)
            x = (int)(finalPosition[0]);
            y = (int)(finalPosition[1]);
            z = (int)(finalPosition[2]);
            if ((x == 0 && y == 0 && z == 0 ) || (x == 1 && y == 0 && z == 0) || (x == 0 && y == 1 && z == 0) || (x == 0 && y == 0 && z == 1))
            {
                numExcursions++;
            }
            // Increment that element in the array by 1.
            //UnityEngine.Debug.Log("(x,y,z) = (" + x + "," + y + "," + z + ")");

        }
        UnityEngine.Debug.Log("Walk target length: " + testWalkLength + "\tNum Walks Generated: " + testNumWalks + "\tNum walks that return to origin: " + numReturnToOrigin + "\tNum Excursions (or one step away): " + numExcursions);
        swStats.WriteLine(testWalkLength + "\t" + testNumWalks + "\t" + numReturnToOrigin + "\t" + numExcursions);
        //string listName = "unityTest" + testNumWalks;
        //string newFilename = "TextOutputs/" + listName + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".txt";
        //StreamWriter testOutput = new StreamWriter(newFilename);
        //testOutput.AutoFlush = true;
    }

    private Mesh findConvexHull(List<Vector3> inputPointList)
    {
        Mesh hullMesh = new Mesh();
        List<Vector3> currentPointList = (new HashSet<Vector3>(inputPointList)).ToList(); // Remove duplicates
        // First, need to check if all the points are on the same line.
        // Point sets to test: (0,0,0)
        // (0,0,0), (0,2,1)
        // (1,1,1), (0,0,0), (2,2,2), (5,5,5), (-1,-1,-1)
        // (1,1,1), (0,0,0), (2,2,2), (5,5,5), (-1,-1,-1), (0,0,1)
        //UnityEngine.Debug.Log("Finding convex hull for points: " + String.Join(", ", currentPointList));
        if (currentPointList.Count < 3)
        {
            //UnityEngine.Debug.Log("Point or simple linear hull");
            return hullMesh; // Either a point or a line
        }
        bool couldBeLinear = true;
        // Keep track of the two points farthest from each other -- these form the hull for a straight line
        Vector3 point1 = currentPointList[0];
        Vector3 point2 = currentPointList[1];
        Vector3 point3 = new Vector3();
        // If we do find a non-linear plane, it will be useful for the next step.
        List<Vector3> currentPlane = new List<Vector3>();
        // Use currentIndex to keep track of which points we've checked so far
        int currentIndex = 0;
        for (int i = 2; i < currentPointList.Count && couldBeLinear; i++)
        {
            point3 = currentPointList[i];
            if (!threePointsLinear(point1, point2, point3))
            {
                couldBeLinear = false;
                currentIndex = i + 1;
            }
            else
            {
                // Check if point3 is at the outside of the hull so far
                float dist12 = (point2 - point1).sqrMagnitude;
                float dist23 = (point3 - point2).sqrMagnitude;
                float dist13 = (point3 - point1).sqrMagnitude;
                // If dist23 and dist13 < dist 12, then point1 and point2 are still the best hull.
                if (dist12 < dist13)
                {
                    // point3 should be part of the new hull. But which point should be reassigned?
                    if (dist23 < dist13)
                    {
                        // point2 is in the middle and should be replaced with point3
                        point2 = point3;
                    }
                    else // dist23 > dist13
                    {
                        // point1 is in the middle and should be replaced with point3
                        point1 = point3;
                    }
                }
                else // dist12 > dist13
                {
                    // point3 might still be part of the new hull if point1 is in the middle
                    if (dist12 < dist23)
                    {
                        // point1 is in the middle and should be replaced with point3
                        point1 = point3;

                    }
                    else // dist12 > dist23
                    {
                        // point3 is in the middle. Do nothing
                    }
                }
            }
        }
        if (couldBeLinear)
        {
            //UnityEngine.Debug.Log("Linear hull: (" + String.Join(", ", point1) + "), (" + String.Join(", ", point2) + ")");
            //return new List<Vector3> { point1, point2 };
            return hullMesh;
        }
        // If we've gotten this far, we know the hull is not linear. Then points 1, 2 and 3 must form a plane (that isn't a line).
        currentPlane = new List<Vector3> { point1, point2, point3 };

        // To do the incremental hull algorithm, we need to think in terms of facets.
        // For 2D hulls, a facet is a line with two points.
        // In 2D, we don't have to worry too much about clockwise or counterclockwiseness.
        // Assume hull is 2D until we find a facet that isn't on the plane.
        List<Vector3> facet1 = new List<Vector3> { point1, point2 };
        List<Vector3> facet2 = new List<Vector3> { point2, point3 };
        List<Vector3> facet3 = new List<Vector3> { point3, point1 };

        List<List<Vector3>> facetList = new List<List<Vector3>> { facet1, facet2, facet3 };

        // The incremental hull algorithm involves determining whether new points are "above" or "below" the facets.
        // In 2D, this means determining whether the point is on the same side of the facet as the centre of the polytope.
        // We can calculate a "centre" now. It will always be inside the polytope, since we will expand it further to find the hull.
        Vector3 centre = (point1 + point2 + point3) * 1.0f / 3.0f;

        // Next, need to check if all the points are on the same plane
        // Note that we conveniently have the list of points currentPlane to help us.
        // Also, we know that every point with an index lower than currentIndex is either in currentPlane, or is definitely not part of the convex hull.

        // Test: // (1,1,1), (0,0,0), (2,2,2), (5,5,5), (-1,-1,-1), (0,0,1)
        // (0,0,0), (0, 1, 1), (0, 0, 1), (0, 1, 0), (0, -1, 0)
        // (0,0,0), (0, 1, 1), (0, 0, 1), (1,0,1)
        bool isPlanar = true;

        Vector3 point4 = new Vector3();
        // We'll eventually need a set of vertices in the hull, and we'll make it into a list
        HashSet<Vector3> hullSet = new HashSet<Vector3>();
        List<Vector3> hullList = hullSet.ToList();

        // We'll also need to consider the mesh:
        List<Vector3> hullVertices = new List<Vector3>();
        List<int> hullTriangles = new List<int>();

        for (int i = currentIndex; i < currentPointList.Count && isPlanar; i++)
        {
            //UnityEngine.Debug.Log("isPlanar: " + isPlanar);
            //UnityEngine.Debug.Log("current index: " + i);
            point4 = currentPointList[i];
            if (!fourPointsPlanar(point1, point2, point3, point4))
            {
                isPlanar = false;
                // currentIndex = i + 1;
            }
            else
            {
                // Check whether point4 is "above" any of the current facets.
                // If it is, remove those facets and add new facets that include point4
                List<Vector3> visiblePoints = new List<Vector3>();
                List<int> toRemove = new List<int>();
                for (int j = 0; j < facetList.Count; j++)
                {
                    // Check whether point4 is above facet j. Do this with a cross product.
                    Vector3 AB = facetList[j][1] - facetList[j][0];
                    Vector3 AC = centre - facetList[j][0];
                    Vector3 AP = point4 - facetList[j][0];

                    // If the centre and point4 are on opposite sides of the line, then:
                    // The cross product AB x AC will point in the opposite direction of AB X AP
                    // In which case, (AB x AC) dot (AB x AP) will be negative.
                    float dotProduct = Vector3.Dot(Vector3.Cross(AB, AC), Vector3.Cross(AB, AP));
                    //UnityEngine.Debug.Log("Testing visibility. dotProduct = " + dotProduct);
                    if (dotProduct <= 0)
                    {
                        // The points in this facet are visible to point4.
                        visiblePoints.Add(facetList[j][0]);
                        visiblePoints.Add(facetList[j][1]);
                        // Mark that this facet must be removed from facetList
                        toRemove.Add(j);
                    }
                }
                // Remove unnecesary facets from facetList.
                for (int j = toRemove.Count - 1; j >= 0; j--)
                {
                    facetList.RemoveAt(toRemove[j]);
                }
                // Now I have a list of points visible to point4.
                // In principle, if a point appears in the list twice, then it is part of a CORNER that is visible to point4, and should not be used.
                //UnityEngine.Debug.Log("visiblePoints.Count (before removing duplicates) = " + visiblePoints.Count);
                visiblePoints = RemoveAllDuplicates(visiblePoints);
                // Then we will be left with two points.
                //UnityEngine.Debug.Log("visiblePoints.Count (after removing duplicates) = " + visiblePoints.Count);
                if (visiblePoints.Count > 0)  // Note: This count can be 0 if the new point is inside the current polyhedron
                {
                    facetList.Add(new List<Vector3> { visiblePoints[0], point4 });
                    facetList.Add(new List<Vector3> { point4, visiblePoints[1] });
                }
                
            }
        }
        if (isPlanar)
        {
            //UnityEngine.Debug.Log("Planar hull!");
            //UnityEngine.Debug.Log("facetList.Count: " + facetList.Count);
            // Flatten out facetList
            int triangleCount = 0;
            for (int i = 0; i < facetList.Count; i++)
            {
                for (int j = 0; j < facetList[i].Count; j++)
                {
                    hullSet.Add(facetList[i][j]);
                    hullVertices.Add(facetList[i][j]);
                    hullTriangles.Add(triangleCount++);
                }
                hullVertices.Add(centre);
                hullTriangles.Add(triangleCount++);
            }
            hullList = hullSet.ToList();
            for (int i = 0; i < hullList.Count; i++)
            {
                UnityEngine.Debug.Log("hullList[" + i + "] = (" + String.Join(", ", hullList[i]) + ")");
            }
            int numVertices2D = hullVertices.Count;
            for (int i = 0; i < numVertices2D; i++)
            {
                //hullVertices.Add(hullVertices[i] + new Vector3(0.001f, 0.001f, 0.001f));
                hullTriangles.Add(numVertices2D - 1 - i);
            }
            //UnityEngine.Debug.Log("hullVertices.Count: " + hullVertices.Count);
            //UnityEngine.Debug.Log("hullTriangles.Count: " + hullTriangles.Count);

            hullMesh = new Mesh();
            hullMesh.vertices = hullVertices.ToArray();
            hullMesh.triangles = hullTriangles.ToArray();
            //hullMesh.RecalculateNormals();


            return hullMesh;
        }

        // If we've gotten this far, we know that there is a 3D convex hull. We also have an initial simplex:
        List<Vector3> currentHull = new List<Vector3> { point1, point2, point3, point4 };

        // We start with a polytope with 4 faces. Picture a stretched-out tetrahedron.
        facet1 = new List<Vector3> { point1, point2, point3 };
        facet2 = new List<Vector3> { point1, point2, point4 };
        facet3 = new List<Vector3> { point1, point3, point4 };
        List<Vector3> facet4 = new List<Vector3> { point2, point3, point4 };
        facetList = new List<List<Vector3>> { facet1, facet2, facet3, facet4 };

        // Again, we need to find the centre of these four points:
        centre = (point1 + point2 + point3 + point4) * 1.0f / 4.0f;
        Vector3 point5 = new Vector3();
        // Any points we've looked at are already in the current hull, or have been rejected.
        for (int i = currentIndex; i < currentPointList.Count; i++)
        {
            point5 = currentPointList[i];
            //UnityEngine.Debug.Log("3D hull testing current point: " + point5);

            // At this point, we know all the points are in a 3D space (and not 4D space), so we don't have to check that.
            // For each point, we just have to go through the current list of facets and see if point5 is "above" any of them.
            // We DO have to keep track of visible edges. We can represent each edge as a set, since we don't need to care about direction.
            List<List<Vector3>> visibleEdges = new List<List<Vector3>>();
            // We also have to keep track of which facets to remove.
            List<int> toRemove = new List<int>();
            for (int j = 0; j < facetList.Count; j++)
            {
                //
                Vector3 AB = facetList[j][1] - facetList[j][0];
                Vector3 AC = facetList[j][2] - facetList[j][0];
                // Find the normal vector to the facet. It doesn't matter whether it's inward or outward.
                Vector3 N = Vector3.Cross(AB, AC);
                // Find vectors from any point on the plane (say, facetList[j][0]) to point5 and to centre.
                Vector3 PA = point5 - facetList[j][0];
                Vector3 DA = centre - facetList[j][0];
                // If they are on opposite sides of the plane, then their projections onto the normal vector will point in opposite directions.
                // Then the product of the following two dot products will be less than zero:
                float PA_n = Vector3.Dot(PA, N);
                float DA_n = Vector3.Dot(DA, N);
                if (PA_n * DA_n < 0.0f)
                {
                    //UnityEngine.Debug.Log("Facet (" + String.Join(", ", facetList[j]) + ") is below the current point!");
                    // Then the current facet is below point5.
                    // All 3 edges on the current facet are visible to point5:
                    visibleEdges.Add(new List<Vector3> { facetList[j][0], facetList[j][1] });
                    visibleEdges.Add(new List<Vector3> { facetList[j][0], facetList[j][2] });
                    visibleEdges.Add(new List<Vector3> { facetList[j][1], facetList[j][2] });
                    // Note that we must remove this facet.
                    toRemove.Add(j);
                }

            }
            // Remove unnecesary facets from facetList.
            for (int j = toRemove.Count - 1; j >= 0; j--)
            {
                facetList.RemoveAt(toRemove[j]);
            }
            // Remove any edge that is visible to point5 in more than one facet, i.e. appears in visibleEdges twice.
            visibleEdges = RemoveAllDuplicateEdges(visibleEdges);
            // Create new facets composed from the remaining edges and point5
            for (int j = 0; j < visibleEdges.Count; j++)
            {
                List<Vector3> newFacet = visibleEdges[j];
                newFacet.Add(point5);
                facetList.Add(newFacet);
            }

        }
        //UnityEngine.Debug.Log("3D HULL!!!!");
        //UnityEngine.Debug.Log("facetList.Count: " + facetList.Count);
        // Flatten out facetList
        //hullSet = new HashSet<Vector3>();


        // Make sure to deal with the NORMALS
        // Note: The normals should be perpendicular to the respective triangles, facing outward.
        // In order to make sure they're facing outward, we must find the "center" of the polytope.
        // Used this page for help with "Aggregate": https://stackoverflow.com/questions/33170643/finding-the-average-of-vectors-in-a-list
        Vector3 average = inputPointList.Aggregate(new Vector3(0, 0, 0), (s, v) => s + v) / (float)inputPointList.Count;

        List<Vector3> hullNormals = new List<Vector3>();
        for (int i = 0; i < facetList.Count; i++)
        {
            // Note: For any given facet, all three vertices will have the same normal vector.
            // Should be perpendicular to any two vectors made from the three vertices.
            Vector3 newNormal = Vector3.Cross(facetList[i][1] - facetList[i][0], facetList[i][2] - facetList[i][0]);
            newNormal = Vector3.Normalize(newNormal);
            // Need to make sure they're pointing OUTWARD. Need to use the "center of gravity"?
            // So: I need to make sure newNormal is pointing in roughly the same direction as any point on the facet minus "average". Use DOT PRODUCT
            bool reverseTriangle = false;
            if (Vector3.Dot(newNormal, facetList[i][0] - average) < 0)
            {
                newNormal = -newNormal;
                // This tells me that the points are in the wrong order, so I need to reverse the triangle order later:
                reverseTriangle = true;
            }
            for (int j = 0; j < facetList[i].Count; j++)
            {
                //hullSet.Add(facetList[i][j]);
                hullVertices.Add(facetList[i][j]);
                if (reverseTriangle)
                {
                    hullTriangles.Add(i * facetList[i].Count + facetList[i].Count - 1 - j);
                }
                else
                {
                    hullTriangles.Add(i * facetList[i].Count + j);
                }
                hullNormals.Add(newNormal);
            }
            
        }
        //hullList = hullSet.ToList();
        //for (int i = 0; i < hullList.Count; i++)
        //{
            //UnityEngine.Debug.Log("hullList[" + i + "] = (" + String.Join(", ", hullList[i]) + ")");
        //}

        // Add extra faces so visible from both sides:
        /*
         * int numVertices = hullVertices.Count;
        for (int i = 0; i < numVertices; i++)
        {
            //hullVertices.Add(hullVertices[i] + new Vector3(0.001f, 0.001f, 0.001f));
            hullTriangles.Add(numVertices - 1 - i);
            //hullNormals.Add(-hullNormals[numVertices - 1 - i]);
            //UnityEngine.Debug.Log("-hullNormals[numVertices - 1 - i] = " + (-hullNormals[numVertices - 1 - i]));
        }
        */

        


        hullMesh = new Mesh();
        hullMesh.vertices = hullVertices.ToArray();
        hullMesh.triangles = hullTriangles.ToArray();
        hullMesh.normals = hullNormals.ToArray();
        //hullMesh.RecalculateNormals();

        return hullMesh;

    }

    static List<T> RemoveAllDuplicates<T>(List<T> inputList)
    {
        // Find all items that occur more than once
        var duplicates = inputList.GroupBy(item => item).Where(group => group.Count() > 1).SelectMany(group => group);

        // Remove all instances of duplicates from the list
        inputList.RemoveAll(item => duplicates.Contains(item));

        return inputList;
    }

    static List<List<Vector3>> RemoveAllDuplicateEdges(List<List<Vector3>> inputList)
    {
        /* 
         * List<HashSet<Vector3>> newList = new List<HashSet<Vector3>>();
        for (int i = 0; i < inputList.Count; i++)
        {
            HashSet<Vector3> currentItem = inputList[i];
            // Assume we haven't found a duplicate of the current item until we've found one.
            bool duplicateFound = false;
            for (int j = i + 1; j < inputList.Count && !duplicateFound; j++)
            {

            }
        }
        */
        List<List<Vector3>> inputListCopy = inputList;
        List<List<Vector3>> toReturn = new List<List<Vector3>>();
        while (inputListCopy.Count > 0)
        {
            // The idea is this: If an item is a duplicate, remove ALL the duplicates.
            // If an item is not a duplicate, add it to toReturn
            List<Vector3> currentItem = inputListCopy[0];
            List<Vector3> edge1 = inputListCopy[0].ToList();
            // Assume we haven't found a duplicate of the current item until we've found one.
            bool duplicateFound = false;
            List<int> toRemove = new List<int>();
            for (int i = 1; i < inputListCopy.Count; i++)
            {
                // Check if inputListCopy[i] is equal to inputListCopy[0].
                // In this program, we know they'll both be sets of 2 vectors.
                List<Vector3> edge2 = inputListCopy[i];
                if (((edge1[0] - edge2[0]).sqrMagnitude < 0.000001 && (edge1[1] - edge2[1]).sqrMagnitude < 0.000001) ||
                      ((edge1[0] - edge2[1]).sqrMagnitude < 0.000001 && (edge1[1] - edge2[0]).sqrMagnitude < 0.000001))
                {
                    // The edges are equal
                    duplicateFound = true;
                    toRemove.Add(i);
                }
            }
            // Remove duplicates from the list if necessary
            for (int i = toRemove.Count - 1; i >= 0; i--)
            {
                inputListCopy.RemoveAt(toRemove[i]);
            }
            // If no duplicate has been found, the edge can be added to the returned list.
            if (!duplicateFound)
            {
                toReturn.Add(currentItem);
            }
            // Either way, we remove the first element of inputListCopy
            inputListCopy.RemoveAt(0);
        }
        return toReturn;
    }

    private bool threePointsLinear(Vector3 point1, Vector3 point2, Vector3 point3)
    {
        Vector3 crossProduct = Vector3.Cross(point2 - point1, point3 - point1);
        // For our purposes, since the coordinates are integers, epsilon doesn't have to be too small
        // In this context, we only care about sqrMagnitude
        if (crossProduct.sqrMagnitude < 0.000001)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private bool fourPointsPlanar(Vector3 point1, Vector3 point2, Vector3 point3, Vector3 point4)
    {
        Vector3 crossProduct = Vector3.Cross(point2 - point1, point3 - point1);
        float dotProduct = Vector3.Dot(point4 - point1, crossProduct);
        UnityEngine.Debug.Log("Testing planarity. dotProduct = " + dotProduct);
        if (Math.Abs(dotProduct) < 0.000001)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

}