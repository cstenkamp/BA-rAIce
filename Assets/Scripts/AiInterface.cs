using UnityEngine;
using System.Collections;
using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Diagnostics;


public class AiInterface : MonoBehaviour {

    public WheelCollider colliderRL;
    public WheelCollider colliderRR;
    public WheelCollider colliderFL;
    public WheelCollider colliderFR;

    public bool AITakingControl;

    public PositionTracking Tracking;
	public CarController Car;

	// for vis display
	public int arraySizeX = 10;
	public int arraySizeY = 30;
	public float posStepSize = 1.5f;

	// for debugging
	public GameObject posMarker1;
	public GameObject posMarker2;
	public GameObject posMarker3;
	public GameObject posMarker4;

	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update ()
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Reset();
        stopwatch.Start();
        float[] vec = GetSpeedStear();
        string tosend = "";
        foreach (float elem in vec)
            tosend = tosend + " " + elem;
        // UnityEngine.Debug.Log(tosend);
        string message = SynchronousSocketClient.StartClient(tosend);
        stopwatch.Stop();
        if (message == "turning")
        {
            AITakingControl = true;
            colliderRL.motorTorque = 1200.0f;
            colliderRR.motorTorque = 1200.0f;
            UnityEngine.Debug.Log("Turning means Speeding" + "   mS: " + stopwatch.ElapsedMilliseconds);

        } else
        {
            AITakingControl = false;
            UnityEngine.Debug.Log("Ticks: " + stopwatch.ElapsedTicks + " mS: " + stopwatch.ElapsedMilliseconds + "   " + message);
        }

    }

    void FixedUpdate()
    {
    }

    // #############################################
    // ########### MAIN GETTER FUNCTIONS ###########
    // #############################################

    // return NxM float array representing a top-view grid, indicating track surface as 1 and offtrack as 0

    public float[] GetSpeedStear()
    {
        float[] SpeedStearVec = new float[4] { colliderRL.motorTorque, colliderRR.motorTorque, colliderFL.steerAngle, colliderFR.steerAngle };
        return SpeedStearVec;
    }

    public float[,] GetVisionDisplay()
	{
		float[,] visArray = new float[arraySizeX,arraySizeY];
		Vector3 carPos = Car.transform.position;
		float carRot = Car.transform.eulerAngles.y - 180.0f;
		for (int i=0; i<arraySizeX; i++)
		{
			for (int j=0; j<arraySizeY; j++)
			{
				float xPosDot = (i-(float)arraySizeX/2.0f)*posStepSize;
				float zPosDot = -(j+3)*posStepSize;
				float X = carPos.x - xPosDot*Mathf.Cos(carRot*Mathf.PI/180.0f) + Mathf.Sin(carRot*Mathf.PI/180.0f)*zPosDot;
				float Z = carPos.z + xPosDot*Mathf.Sin(carRot*Mathf.PI/180.0f) + Mathf.Cos(carRot*Mathf.PI/180.0f)*zPosDot;
				visArray[i,j] = Car.CheckSurface(X,Z);
				// debug
//				if (i==0 && j==0) { posMarker1.transform.position = new Vector3(X,1.5f,Z); }
//				if (i==0 && j==arraySizeY-1) { posMarker2.transform.position = new Vector3(X,1.5f,Z); }
//				if (i==arraySizeX-1 && j==arraySizeY-1) { posMarker3.transform.position = new Vector3(X,1.5f,Z); }
//				if (i==arraySizeX-1 && j==0) { posMarker4.transform.position = new Vector3(X,1.5f,Z); }
			}
		}
		return visArray;
	}

	public float[] GetCenterDistVector(int vectorLength=15, float spacing=1.0f, float sigma=1.0f) // needs vis. display
	{
		// car-centered: where is the center line of the race track relative to my car?
		float centerLineDist = Tracking.GetCenterDist(); // positive if the center line is to the left, negative if it is to the right

		// RBF centers: spread out like [-3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0]
		float[] centerPositions = new float[vectorLength];
		for (int i=0; i<centerPositions.Length; i++) { centerPositions[i] = (i-((vectorLength-1)/2.0f))*spacing; }

		// centerDistVector: Gaussian distribution with centerPosition as mu, sigma as sigma, and centerLineDist as x
		float[] centerDistVector = new float[vectorLength];
		for (int j=0; j<centerDistVector.Length; j++)
		{
			float centerLineDistAdjusted = centerLineDist;
			if (j==0 && centerLineDistAdjusted < centerPositions[0]) { centerLineDistAdjusted = centerPositions[0]; }
			if (j==(centerPositions.Length-1) && centerLineDistAdjusted > centerPositions[centerPositions.Length-1]) { centerLineDistAdjusted = centerPositions[centerPositions.Length-1]; }
			centerDistVector[j] = GaussianDist(centerPositions[j], sigma, centerLineDistAdjusted);
		}
		return centerDistVector;
	}

	public float[] GetLookAheadVector(int vectorLength=30, float spacing=10.0f)
	{
		// track-centered: does not take into accout current rotation of the car
		float progressMeters = Tracking.progress*Tracking.trackLength;
		float[] lookAhead = new float[vectorLength];
		for (int i=0; i<vectorLength; i++)
		{
			float lookAheadPointOne = progressMeters+i*spacing;
			float lookAheadPointTwo = progressMeters+(i+1)*spacing;
			if (lookAheadPointOne > Tracking.trackLength) { lookAheadPointOne -= Tracking.trackLength; }
			if (lookAheadPointTwo > Tracking.trackLength) { lookAheadPointTwo -= Tracking.trackLength; }
			float absoluteAnglePoint1 = InterpolatePointAngle(Tracking.absoluteAnchorAngles, Tracking.absoluteAnchorDistances, lookAheadPointOne);
			float absoluteAnglePoint2 = InterpolatePointAngle(Tracking.absoluteAnchorAngles, Tracking.absoluteAnchorDistances, lookAheadPointTwo);
			float tmp = (absoluteAnglePoint2 - absoluteAnglePoint1);
			if (tmp > 180.0f) { tmp -= 360.0f; }
			if (tmp < -180.0f) { tmp += 360.0f; }
			lookAhead[i] = tmp;
		}
		return lookAhead;
	}

	public float[] GetProgressVector(int vectorLength=10, float sigma=0.1f) // needs vis. display
	{
		// parameters
		float spacing = 1.0f / (float)vectorLength;

		// RBF center positions
		float[] centerPositions = new float[vectorLength];
		centerPositions[0] = 0.0f;
		for (int i=1; i<vectorLength; i++) { centerPositions[i] = centerPositions[i-1]+spacing; }

		// distances from RBF centers
		float[] progressVector = new float[vectorLength];
		for (int j=0; j<vectorLength; j++)
		{
			float distFromCenter = Mathf.Abs(centerPositions[j] - Tracking.progress);
			if (distFromCenter > 0.5f) { distFromCenter -= 1.0f; distFromCenter = Mathf.Abs(distFromCenter); }
			progressVector[j] = GaussianDist(0.0f, sigma, distFromCenter); // dist from center is put in directly, so new center is 0
		}

		return progressVector;
	}

	public float[] GetCarStatusVector()
	{
		float[] carStatusVector = new float[9]; // length = sum(#) ~=18
		carStatusVector[0] = Car.velocity/200.0f; // car velocity > split up into more nodes 	# 1
		carStatusVector[1] = Car.GetSlip(Car.colliderFL)[0]; // wheel rotation relative to car	# 1
		carStatusVector[2] = Car.GetSlip(Car.colliderFR)[0]; // wheel rotation relative to car	# 1
		carStatusVector[3] = Car.GetSlip(Car.colliderRL)[0]; // wheel rotation relative to car	# 1
		carStatusVector[4] = Car.GetSlip(Car.colliderRR)[0]; // wheel rotation relative to car  # 1
//		carStatusVector[5] = Car.GetSlip(Car.colliderFL)[1]; // wheel rotation relative to car	# 1
//		carStatusVector[6] = Car.GetSlip(Car.colliderFR)[1]; // wheel rotation relative to car	# 1
//		carStatusVector[7] = Car.GetSlip(Car.colliderRL)[1]; // wheel rotation relative to car	# 1
//		carStatusVector[8] = Car.GetSlip(Car.colliderRR)[1]; // wheel rotation relative to car  # 1
		// front wheel rotation relative to centerLine rotation									# 2
		// car rotation relative to centerLine rotation											# 2
		// car rotation relative to velocity vector												# 1
		// longitudinal slip FL																	# 1
		// longitudinal slip FR																	# 1
		// longitudinal slip RL																	# 1
		// longitudinal slip RR																	# 1
		// slip angle FL																		# 1
		// slip angle FR																		# 1
		// slip angle RL																		# 1
		// slip angle RR																		# 1
		return carStatusVector;
	}

	// #############################################
	// ############## HELPER FUNCTIONS #############
	// #############################################

	float InterpolatePointAngle(float[] absoluteAnchorAngles, float[] absoluteAnchorDistances, float d)
	{
		int pos = ClosestSmallerThan(absoluteAnchorDistances, d);
		int posPlus1 = pos+1; if (posPlus1 >= absoluteAnchorDistances.Length) { posPlus1 -= absoluteAnchorDistances.Length; }
		float A = absoluteAnchorDistances[pos];				// d is either A, B, or between A and B
		float B = absoluteAnchorDistances[posPlus1];
		float interpolationRatio = (d-A)/(B-A);

		float angleA = absoluteAnchorAngles[pos];
		float angleB = absoluteAnchorAngles[posPlus1];
		if ((angleA-angleB)>180.0f) { angleA -= 360.0f; } // a=360, b=0 
		if ((angleA-angleB)<-180.0f) { angleB -= 360.0f; } // a=0, b=360 
		return (1-interpolationRatio)*angleA + interpolationRatio*angleB; // return linear interpolation between angle in point A and B
	}


	static int ClosestSmallerThan(float[] collection, float target)
	{
		float minDifference = float.MaxValue;
		int argClosest = int.MaxValue;
		for (int i=0; i<collection.Length; i++)
		{
			if (target > collection[i])
			{
				float difference = Mathf.Abs(collection[i] - target);
				if (minDifference > difference)
				{
					argClosest = i;
					minDifference = difference;
				}
			} 
	    }
		return argClosest;
	}

	float GaussianDist(float mu, float sigma, float x)
	{
		return 1.0f/Mathf.Sqrt(2.0f*Mathf.PI*sigma)*Mathf.Exp(-Mathf.Pow((x-mu),2.0f)/(2.0f*Mathf.Pow(sigma,2.0f)));
	}

}








public class SynchronousSocketClient
{

    public static string StartClient(string data)
    {
        // Data buffer for incoming data.  
        byte[] bytes = new byte[1024];

        try
        {
            // Resolves a host name to an IPHostEntry instance           
            IPHostEntry ipHost = Dns.GetHostEntry("");

            // Gets first IP address associated with a localhost
            IPAddress ipAddr = ipHost.AddressList[0];

            // Creates a network endpoint
            IPEndPoint ipEndPoint = new IPEndPoint(ipAddr, 5005);

            // Create one Socket object to setup Tcp connection
            Socket sender = new Socket(
                ipAddr.AddressFamily,// Specifies the addressing scheme
                SocketType.Stream,   // The type of socket 
                ProtocolType.Tcp     // Specifies the protocols 
                );

            sender.NoDelay = false;   // Using the Nagle algorithm

            // Establishes a connection to a remote host
            sender.Connect(ipEndPoint);
            UnityEngine.Debug.Log("Socket connected to {0}" + sender.RemoteEndPoint.ToString());

            // Sending message
            //<Client Quit> is the sign for end of data
            string theMessage = data;
            int len = theMessage.Length;

            string ms = len.ToString();
            while (ms.Length < 5)
            {
                ms = "0" + ms;
            }
            theMessage = ms + theMessage;

            // byte[] msg = Encoding.Unicode.GetBytes(theMessage + "<Client Quit>");
            byte[] msg = Encoding.ASCII.GetBytes(theMessage);
            UnityEngine.Debug.Log(msg);

            // Sends data to a connected Socket.
            int bytesSend = sender.Send(msg);

            // Receives data from a bound Socket.
            int bytesRec = sender.Receive(bytes);

            // Converts byte array to string
            theMessage = Encoding.ASCII.GetString(bytes, 0, bytesRec);
            theMessage = theMessage.Substring(5);

            // Continues to read the data till data isn't available
            while (sender.Available > 0)
            {
                bytesRec = sender.Receive(bytes);
                theMessage += Encoding.Unicode.GetString(bytes, 0, bytesRec);
            }
            UnityEngine.Debug.Log("The server reply: {0}" + theMessage);

            // Disables sends and receives on a Socket.
            sender.Shutdown(SocketShutdown.Both);

            //Closes the Socket connection and releases all resources
            sender.Close();
            return (theMessage);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.Log("Exception: {0}" + ex.ToString());
        }
        return ("");


    }

    public static int Main(String[] args)
    {
        StartClient("hello World!");
        return 0;
    }

}