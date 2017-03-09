using UnityEngine;
using System.Collections;

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Diagnostics;
using System.ComponentModel;
using System.Collections.Generic;


public static class Consts {
	public const int PORTSEND = 6435;
	public const int PORTASK = 6436;
	public const int updatepythonintervalms = 2000;
	public const int lookforpythonintervalms = 2000;
	public const int MAXAGEPYTHONRESULT = 2000;

	public const int visiondisplay_x = 30; //30
	public const int visiondisplay_y = 42; //42


	public const bool debug_show_visiondisp = false;
	public const bool debug_showperpendicular = false;
	public const bool debug_showanchors = false;
}


//================================================================================


public class AiInterface : MonoBehaviour {

	public WheelCollider colliderRL;
	public WheelCollider colliderRR;
	public WheelCollider colliderFL;
	public WheelCollider colliderFR;

	public bool AITakingControl;

	public PositionTracking Tracking;
	public CarController Car;

	// for debugging
	public GameObject posMarker1;
	public GameObject posMarker2;
	public GameObject posMarker3;
	public GameObject posMarker4;

	//for sending to python
	public long lastpythonupdate =  Environment.TickCount;
	public long lastpythoncheck =  Environment.TickCount;

	//=============================================================================

	// Use this for initialization
	void Start () {
		SendToPython ("resetannvals");
	}

	// Update is called once per frame
	void Update () {

	}

	void FixedUpdate() {
		//TODO man sollte einen reset-befehl an den pythonserver schicken können (jedes mal beim neustart, und auch beim probweisen wiederherstellen hier!)
		//TODO hier sollte definitiv noch hin "IF MODE = ANN", was im Menü ausgewählt wurde!
		//TODO probeweise könnte man python zusätzlich alle keys aufnehmen können, und durch den druck einer speziellen Taste sende ich das kommando an python dass es
		//     resetten soll und alle keys nochmal genauso drücken sol

		Stopwatch stopwatch = new Stopwatch();
		stopwatch.Reset();
		stopwatch.Start();

		SendToPython(preparesend());

		AskForPython();
		if (Environment.TickCount - AsynchronousClient.response.timestamp > Consts.MAXAGEPYTHONRESULT) {
			
			string message = AsynchronousClient.response.str; //ich würde ja sagen message = Askforpython, aber asynchronität undso!
			stopwatch.Stop();
			if (message == "pleasereset") {
				AITakingControl = false;
				//TODO: eine funktion die jederzeit gecallt werden kann (bspw bei 10% strecke), die die komplette position back-upt!!

				//Vector3 newPos = new Vector3(48, 1, 150); 
				//Car.transform.position = newPos;

				UnityEngine.Debug.Log ("HEEEEREEEEE");

			} else if (message == "turning") {
				AITakingControl = true;
				//colliderRL.motorTorque = 1200.0f;
				//colliderRR.motorTorque = 1200.0f;
				UnityEngine.Debug.Log ("Turning means Speeding" + "   mS: " + stopwatch.ElapsedMilliseconds);
			} else {
				AITakingControl = false;
				//UnityEngine.Debug.Log("Ticks: " + stopwatch.ElapsedTicks + " mS: " + stopwatch.ElapsedMilliseconds + "   " + message);
			}
		}
	}


	private string preparesend() {
		
		float[] vec = GetAllInfos();
		string tosend = "";
		foreach (float elem in vec)
			tosend = tosend + " " + elem;
		tosend = ((int)(Tracking.progress * 100.0f)).ToString () + " " + tosend ;


		if (!Car.lapClean)
			tosend = "invalidround";

		return tosend;
	}

	//================================================================================
	// ################################################################
	// ################ MAIN GETTER AND SETTER FUNCTIONS ##############
	// ################################################################


	public float[] GetAllInfos() {
		List<float> all = new List<float> ();
		all.Add ((float)((int)(Tracking.progress * 100.0f)));
		all.Add(float.PositiveInfinity);
		all.AddRange (GetSpeedStear ());
		all.Add(float.PositiveInfinity);
		all.AddRange (GetCarStatusVector());
		all.Add(float.PositiveInfinity);

		//GetVisionDisplay(); und ....  //WIE krieg ich 2dimensionale vektoren da am sinnvollsten zusammen mit 1dimensionalen unter?
		//GetCenterDistVector
		//GetProgressVector, wobei wir da ja schon die Frage hatten wie ich den brauche.
		//GetLookAheadVector
		//und vom carstatusvektor fehlen noch ganz viele

		return all.ToArray();
	}


	public void ResetCar() {



		SendToPython ("reset");
	}



	public float[] GetSpeedStear() {
		float[] SpeedStearVec = new float[4] { colliderRL.motorTorque, colliderRR.motorTorque, colliderFL.steerAngle, colliderFR.steerAngle };
		return SpeedStearVec;
	}


	public void SetSpeedStear(float[] SpeedStearVec) {
		colliderRL.motorTorque = SpeedStearVec [0];
		colliderRR.motorTorque = SpeedStearVec [1];
		colliderFL.steerAngle = SpeedStearVec [2];
		colliderFR.steerAngle = SpeedStearVec [3];
	}



	//ALTE FUNKTION FURS VISIONDISPLAY

//  public float posStepSize = 1.5f;
//	// return NxM float array representing a top-view grid, indicating track surface as 1 and offtrack as 0
//	public float[,] GetVisionDisplay() {
//		float[,] visArray = new float[visiondisplay_x,visiondisplay_y];
//		Vector3 carPos = Car.transform.position;
//		float carRot = Car.transform.eulerAngles.y - 180.0f;
//		for (int i=0; i<visiondisplay_x; i++)
//		{
//			for (int j=0; j<visiondisplay_y; j++)
//			{
//				float xPosDot = (i-(float)visiondisplay_x/2.0f)*posStepSize;
//				float zPosDot = -(j+3)*posStepSize;
//				float X = carPos.x - xPosDot*Mathf.Cos(carRot*Mathf.PI/180.0f) + Mathf.Sin(carRot*Mathf.PI/180.0f)*zPosDot;
//				float Z = carPos.z + xPosDot*Mathf.Sin(carRot*Mathf.PI/180.0f) + Mathf.Cos(carRot*Mathf.PI/180.0f)*zPosDot;
//				visArray[i,j] = Car.CheckSurface(X,Z);
//				// debug
////								if (i==0 && j==0) { posMarker1.transform.position = new Vector3(X,1.5f,Z); }
////								if (i==0 && j==visiondisplay_y-1) { posMarker2.transform.position = new Vector3(X,1.5f,Z); }
////								if (i==visiondisplay_x-1 && j==visiondisplay_y-1) { posMarker3.transform.position = new Vector3(X,1.5f,Z); }
////								if (i==visiondisplay_x-1 && j==0) { posMarker4.transform.position = new Vector3(X,1.5f,Z); }
//			}
//		}
//		return visArray;
//	}





	public float[] GetCenterDistVector(int vectorLength=15, float spacing=1.0f, float sigma=1.0f) { // needs vis. display 
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



	public float[] GetLookAheadVector(int vectorLength=30, float spacing=10.0f) {
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



	public float[] GetProgressVector(int vectorLength=10, float sigma=0.1f) { // needs vis. display 
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




	public float[] GetCarStatusVector() {
		float[] carStatusVector = new float[9]; // length = sum(#) ~=18
		carStatusVector[0] = Car.velocity/200.0f; // car velocity > split up into more nodes 	# 1      //why do we need both the velocity and the speed from GetSpeedStear?
		carStatusVector[1] = Car.GetSlip(Car.colliderFL)[0]; // wheel rotation relative to car	# 1
		carStatusVector[2] = Car.GetSlip(Car.colliderFR)[0]; // wheel rotation relative to car	# 1
		carStatusVector[3] = Car.GetSlip(Car.colliderRL)[0]; // wheel rotation relative to car	# 1
		carStatusVector[4] = Car.GetSlip(Car.colliderRR)[0]; // wheel rotation relative to car  # 1
		carStatusVector[5] = Car.GetSlip(Car.colliderFL)[1]; // wheel rotation relative to car	# 1
		carStatusVector[6] = Car.GetSlip(Car.colliderFR)[1]; // wheel rotation relative to car	# 1
		carStatusVector[7] = Car.GetSlip(Car.colliderRL)[1]; // wheel rotation relative to car	# 1
		carStatusVector[8] = Car.GetSlip(Car.colliderRR)[1]; // wheel rotation relative to car  # 1
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

	public void SetCarStatusFromVector(float[] carStatusVector) {
		Car.velocity = carStatusVector [0] * 200;
		//TODO: setslip undso... falls das geht.

	}







	//================================================================================
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


//================================================================================


	// this method is called whenever something is supposed to be sent to python. This method figures out if it is even supposed to
	// send, and if so, calls AsynchronousClient's StartSenderClient
	public void SendToPython(string data) {
		int currtime = Environment.TickCount;
		if (currtime - lastpythonupdate > Consts.updatepythonintervalms) {
			AsynchronousClient.StartSenderClientWorkerAsync(data);
			lastpythonupdate = currtime;
		}
	}

	public void AskForPython() { //asynchron, daher keinen string message returnen!
		int currtime = Environment.TickCount;
		if (currtime - lastpythoncheck > Consts.lookforpythonintervalms) {
			AsynchronousClient.StartGetterClientWorkerAsync ();
			//UnityEngine.Debug.Log ("Asking Python for new Results");
			lastpythoncheck = currtime;
		}		
	}
}


//########################################################################################################################################


//stems from the Microsoft example... fun thing is only that it simply doesn't run asynchronously, haha.

public class AsynchronousClient {  //updating python's value should happen asynchronously.

	private static string preparestring(string fromwhat) {
		int len = fromwhat.Length;
		string ms = len.ToString();
		while (ms.Length < 5) {
			ms = "0" + ms;
		}
		ms = ms + fromwhat;
		return ms;
	}

	// ManualResetEvent instances signal completion.  //Notifies one or more waiting threads that an event has occurred
	private static ManualResetEvent connectDone = new ManualResetEvent(false);   
	private static ManualResetEvent sendDone =    new ManualResetEvent(false);  
	private static ManualResetEvent receiveDone = new ManualResetEvent(false);  

	//we have a sender-client, who every x seconds updates python's status
	public static void StartSenderClientWorker(string data) {  
		try {  
			connectDone = new ManualResetEvent(false);   
			sendDone = new ManualResetEvent(false);  
			IPHostEntry ipHost = Dns.GetHostEntry("");
			IPAddress ipAddress = ipHost.AddressList[0];  
			IPEndPoint ipEndPoint  = new IPEndPoint(ipAddress, Consts.PORTSEND);  
			Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);    
			client.BeginConnect(ipEndPoint, new AsyncCallback(ConnectCallback), client); 
			connectDone.WaitOne();  
			Send(client,preparestring(data));  
			sendDone.WaitOne();  
			client.Shutdown(SocketShutdown.Send);  
			client.Close();  
		} catch (Exception e) {  UnityEngine.Debug.Log(e.ToString());  }  
	}  

	public delegate void StartSenderClientWorkerDelegate(string data);

	public static void StartSenderClientWorkerAsync(string data) {
		StartSenderClientWorkerDelegate worker = new StartSenderClientWorkerDelegate (StartSenderClientWorker);
		//AsyncCallback completedCallback = new AsyncCallback (null);
		System.ComponentModel.AsyncOperation async = System.ComponentModel.AsyncOperationManager.CreateOperation (null);
		worker.BeginInvoke (data, null, async);
	}


	//================================================================================

	// The response from the remote device.  
	public class response {
		public static String str = String.Empty;  
		public static int timestamp = Environment.TickCount;
	}

	//kann sich der Getter python-seitig auf nen anderen Port anmelden, sodass Python beim anmelden an diesen Port weiß dass es da senden soll? Ja
	public static void StartGetterClientWorker() {  
		try {  
			connectDone = new ManualResetEvent(false);   
			sendDone = new ManualResetEvent(false); 
			receiveDone = new ManualResetEvent(false); 
			IPHostEntry ipHost = Dns.GetHostEntry("");
			IPAddress ipAddress = ipHost.AddressList[0];  
			IPEndPoint ipEndPoint  = new IPEndPoint(ipAddress, Consts.PORTASK);  
			Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);    
			client.BeginConnect(ipEndPoint, new AsyncCallback(ConnectCallback), client); 
			connectDone.WaitOne();  

			Receive(client);  //das ganze ist ja asynchron, das heißt Receive kann nix returnen sondern nur den value updaten.. was aber ja sogar gewünscht ist!
			receiveDone.WaitOne();  

			client.Shutdown(SocketShutdown.Send);  
			client.Close();  
		} catch (Exception e) {  UnityEngine.Debug.Log(e.ToString());  }  
	}  


	public delegate void StartGetterClientWorkerDelegate();

	public static void StartGetterClientWorkerAsync() {
		StartGetterClientWorkerDelegate worker = new StartGetterClientWorkerDelegate (StartGetterClientWorker);
		//AsyncCallback completedCallback = new AsyncCallback (null);
		System.ComponentModel.AsyncOperation async = System.ComponentModel.AsyncOperationManager.CreateOperation (null);
		worker.BeginInvoke (null, async);
	}


	private static void ConnectCallback(IAsyncResult ar) {  
		try {  
			Socket client = (Socket) ar.AsyncState;  // Retrieve the socket from the state object.  
			client.EndConnect(ar);  
			//UnityEngine.Debug.Log("Socket connected to {0}"+ client.RemoteEndPoint.ToString());  
			connectDone.Set();  
		} catch (Exception e) {  UnityEngine.Debug.Log(e.ToString());  }  
	}  
		
	private static void Send(Socket client, String data) {  
		byte[] byteData = Encoding.ASCII.GetBytes(data);  
		client.BeginSend(byteData, 0, byteData.Length, 0,  new AsyncCallback(SendCallback), client);  
	}  

	private static void SendCallback(IAsyncResult ar) {  
		try {  
			Socket client = (Socket) ar.AsyncState;  // Retrieve the socket from the state object. 
			int bytesSent = client.EndSend(ar);  
			//UnityEngine.Debug.Log("Sent {0} bytes to server.", bytesSent);  
			sendDone.Set();  
		} catch (Exception e) {  UnityEngine.Debug.Log(e.ToString());  }  
	}  
		

	private static void ReceiveCallback( IAsyncResult ar ) {  
		try {  
			StateObject state = (StateObject) ar.AsyncState;  // Retrieve the state object and the client socket 
			Socket client = state.workSocket;  
			int bytesRead = client.EndReceive(ar); // Read data from the remote device. 

			if (bytesRead > 0) {  
				state.sb.Append(Encoding.ASCII.GetString(state.buffer,0,bytesRead));   //buffer so far...
				client.BeginReceive(state.buffer,0,StateObject.BufferSize,0, new AsyncCallback(ReceiveCallback), state);  //...look for more
			} else {  
				if (state.sb.Length > 1) {  // All the data has arrived; put it in response.  
					response.str = state.sb.ToString();  
					response.timestamp = Environment.TickCount;
					//UnityEngine.Debug.Log ("Python answered: "+response);
				}  
				receiveDone.Set();  
			}  
		} catch (Exception e) {  UnityEngine.Debug.Log(e.ToString()); }  
	}  


	private static void Receive(Socket client) {  
		try {   
			StateObject state = new StateObject();  
			state.workSocket = client;  
			client.BeginReceive( state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);  
		} catch (Exception e) {  UnityEngine.Debug.Log(e.ToString()); }  
	}  


	// State object for receiving data from remote device.  
	public class StateObject {  
		// Client socket.  
		public Socket workSocket = null;  
		// Size of receive buffer.  
		public const int BufferSize = 256;  
		// Receive buffer.  
		public byte[] buffer = new byte[BufferSize];  
		// Received data string.  
		public StringBuilder sb = new StringBuilder();  
	}  
		
}  