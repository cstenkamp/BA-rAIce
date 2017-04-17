//TODO gucken ob ich nicht andere game-modi brauche habe als die momentanen?

using UnityEngine;
using System.Collections;

using System;
using System.Threading;
using System.Diagnostics;
using System.Linq;


public static class Consts { //TODO: diese hier an python schicken!
	public const int PORTSEND = 6435;
	public const int PORTASK = 6436;
	public const int updatepythonintervalms = 100;
	public const int lookforpythonintervalms = 100;
	public const int MAXAGEPYTHONRESULT = 350;
	public const int CREATE_VECS_ALL = 50;       //TODO: these need to scale up with the game speed!

	public const int visiondisplay_x = 30; //30
	public const int visiondisplay_y = 42; //42

	public const bool debug_show_visiondisp = false;
	public const bool debug_showperpendicular = false;
	public const bool debug_showanchors = false;

	public const bool wallhit_means_reset = true;
}


//================================================================================


public class AiInterface : MonoBehaviour {

	//these are only active in the "train AI" mode
	public bool send_to_python = true;
	public bool get_from_python = true;


	public WheelCollider colliderRL;
	public WheelCollider colliderRR;
	public WheelCollider colliderFL;
	public WheelCollider colliderFR;

	public PositionTracking Tracking;
	public CarController Car;
	public MinimapScript Minmap;
	public GameScript Game;

	// for debugging
	public GameObject posMarker1;
	public GameObject posMarker2;
	public GameObject posMarker3;
	public GameObject posMarker4;

	//for sending to python
	public long lastpythonupdate =  Environment.TickCount;
	public long lastpythoncheck =  Environment.TickCount;
	public long lastgetvectortime = Environment.TickCount;
	public string lastpythonsent;

	public float nn_steer = 0;
	public float nn_brake = 0;
	public float nn_throttle = 0;
	public bool AIDriving = false;
	public bool HumanTakingControl = false;


	//=============================================================================

	// Use this for initialization
	void Start () {
		StartedAIMode ();
	}

	public void StartedAIMode() {
		if ((Game.mode.Contains ("drive_AI")) || (Game.mode.Contains ("train_AI"))) {  
			SendToPython ("resetServer", true);
		}
	}


	// Update is called once per frame
	void Update () {

		if (Input.GetKeyDown (KeyCode.S)) {  
			AsynchronousClient.SendAufJedenFall ("asdf");
		}

		if (Input.GetKeyDown (KeyCode.E)) {  
			AsynchronousClient.StopSenderClient ();
		}

		if ((Game.mode.Contains ("drive_AI")) || (Game.mode.Contains ("train_AI"))) {  
			load_infos (false, false); //da das load_infos (mostly wegen dem konvertieren des visionvektors in ein int-array) recht lange dauert, hab ich mich entschieden das LADEN des visionvektor in update zu machen, und das SENDEN in fixedupdate, damit das spiel sich nicht aufhangt.
		}
	}

	public void FlipHumanTakingControl(bool force_overwrite = false, bool overwrite_with = false) {
		if (!force_overwrite) {
			HumanTakingControl = !HumanTakingControl;
		} else {
			HumanTakingControl = overwrite_with;
		}
		AsynchronousClient.response.str = "";

	}


	void FixedUpdate() {
		if ((Game.mode.Contains("drive_AI")) && !HumanTakingControl) {
				
//			Stopwatch stopwatch = new Stopwatch();
//			stopwatch.Reset();
//			stopwatch.Start();

			SendToPython (load_infos (false, true), false);  //die sind beide nen einzelner thread, also ruhig in fixedupdate.
			AskForPython(); //Da diese funktion asynchron ist, gibts keinen returnwert, nur sooner or later geupdatete values.

//			UnityEngine.Debug.Log ((Environment.TickCount - AsynchronousClient.response.timestamp).ToString ()); //das hier sagt überhaupt gar nix.

			if (Environment.TickCount - AsynchronousClient.response.timestamp < Consts.MAXAGEPYTHONRESULT) {
				
				string message = AsynchronousClient.response.str; //ich würde ja sagen message = Askforpython, aber asynchronität undso!
//				stopwatch.Stop();
				if (message == "pleasereset") { //TODO: ich glaube das hier klappt nicht mehr
					Car.ResetCar ();
					AIDriving = false;
				} else if ((message.Length > 0) && (message [0] == '[')) {
					message = message.Substring (1, message.Length - 2);
					float[] controls = Array.ConvertAll(message.Split (','), float.Parse);
					nn_throttle = controls [0];
					nn_brake = controls [1];
					nn_steer = controls [2];
					AIDriving = true;
				} else {
					AIDriving = false;
				}
			} else {
				AIDriving = false;
			}
		}
	}


	public string load_infos(Boolean force_reload, Boolean forbid_reload) {
		int currtime = Environment.TickCount;
		if (((currtime - lastgetvectortime > Consts.CREATE_VECS_ALL) || (force_reload)) && (!forbid_reload)) {
			lastpythonsent = GetAllInfos ();
			lastgetvectortime = currtime;
		}
//		if (!Car.lapClean)
//			tosend = "invalidround";
		return lastpythonsent;
	}


	private string TwoDArrayToStr(float[,] array) {
		string alltext = "";
		string currline = "";
		for (int i = 0; i < array.GetLength (0); i++) {
			currline = "";
			for (int j = 0; j < array.GetLength (1); j++) {
				currline = currline + (array [i, j]*2).ToString();
			}
			//clinenr = ParseIntBase3 (currline);
			//alltext = alltext + clinenr.ToString("X") + ",";
			alltext = alltext + currline.ToString() + ",";
		}
		return alltext;
	}


	//================================================================================
	// ################################################################
	// ################ MAIN GETTER AND SETTER FUNCTIONS ##############
	// ################################################################


	public string GetAllInfos() {
		//Keys: P: Progress as a real number in percent
		//      S: SpeedStearVec (rounded to 4)
		//		T: CarStatusVec  (rounded to 4)
		//		C: CenterDistVec (rounded to 4)
		//		L: LookAheadVec  (rounded to 4)
		//		V: VisionVector  (converted to decimal)
		//		R: Progress as a vector (rounded to 4)

		string all = ""; 

		all += "P("+((int)(Tracking.progress * 100.0f)).ToString ()+")";

		all += "S(" + string.Join (",", GetSpeedStear ().Select (x => (Math.Round(x,4)).ToString ()).ToArray ()) + ")";

		all += "T(" + string.Join (",", GetCarStatusVector ().Select (x => (Math.Round(x,4)).ToString ()).ToArray ()) + ")";

		all += "C("+ string.Join (",", GetCenterDistVector ().Select (x => (Math.Round(x,4)).ToString ()).ToArray ()) + ")";

		all += "L("+ string.Join (",", GetLookAheadVector ().Select (x => (Math.Round(x,4)).ToString ()).ToArray ()) + ")";

		all += "V(" + TwoDArrayToStr (Minmap.GetVisionDisplay ()) + ")";

		//all += "R"+ string.Join (",", GetProgressVector ().Select (x => (Math.Round(x,4)).ToString ()).ToArray ()) + ")";

		//TODO: gucken welche vektoren ich brauche
		//TODO: klären ob ich den Progress als number oder als vector brauche
		//TODO: vom carstatusvektor fehlen noch ganz viele
		//TODO: Delta und Feedback vom recorder ebenfalls returnen
		return all;
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
		//TODO: 'setslip' undso... falls das geht.

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
	public void SendToPython(string data, Boolean force) {
		if (!send_to_python) {	return;	}
		if (data == "resetServer") {
			AsynchronousClient.StartClientSocket (Consts.PORTSEND);
		}
		data = "Time("+Environment.TickCount.ToString () + ")" + data;

		int currtime = Environment.TickCount;
		if ((currtime - lastpythonupdate > Consts.updatepythonintervalms) || (force)) {
			var t = new Thread(() => AsynchronousClient.SendAufJedenFall(data));
			t.Start();
			lastpythonupdate = currtime;
		}
	}

	public void AskForPython() { //asynchron, daher keinen string message returnen!
		if (!get_from_python) {	return;	}
		int currtime = Environment.TickCount;
		if (currtime - lastpythoncheck > Consts.lookforpythonintervalms) {
//			var t = new Thread(() => AsynchronousClient.StartGetterClient());
//			t.Start();
			lastpythoncheck = currtime;
		}		
	}


	public static void KillOtherThreads() {
		ProcessThreadCollection currentThreads = Process.GetCurrentProcess().Threads;
		foreach (Thread thread in currentThreads)    
		{
			thread.Abort();
		}	
	}

	public void Reconnect() {
		AsynchronousClient.serverdown = false;
		AsynchronousClient.ResetServerConnectTrials();
	}

	public void Disconnect() {
		AsynchronousClient.serverdown = true;
		AIDriving = false;
	}



//============================== HELPER FUNCTIONS ==================================================


	private static int ParseIntBase3(string s)
	{
		int res = 0;
		for (int i = 0; i < s.Length; i++)
		{
			res = 3 * res + s[i] - '0';
		}
		return res;
	}



}