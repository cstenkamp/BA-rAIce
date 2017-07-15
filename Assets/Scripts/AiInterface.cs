/// <summary>
/// MAIN TODO HERE:
/// 1) Gucken was ich mache wenn MAXAGEPYTHONRESULT überschritten ist
/// </summary>
using UnityEngine;
using System.Collections;
using System;
using System.Threading;
using System.Diagnostics;
using System.Linq;
using System.Text;


public static class Consts { //TODO: diese hier an python schicken!
	public const int PORTSEND = 6435;
	public const int PORTASK = 6436;
	public const int updatepythonintervalms = 200;  //multiples of 25
	public const int MAXAGEPYTHONRESULT = 150;     //this uses realtime, in constrast to all other time-dependent stuff
	public const int CREATE_VECS_ALL = 25;         
	public const int trackAllXMS = 25;             //hier gehts ums sv-tracken (im recorder) 
	public const bool fixedresultusagetime = true;

	public const int visiondisplay_x = 30; //30
	public const int visiondisplay_y = 45; //45
	public const int visiondisp2_x = 30; //30
	public const int visiondisp2_y = 45; //45

	public const bool debug_showperpendicular = false;
	public const bool debug_showanchors = false;
	public const bool debug_makevalidafterwallhit = false;

	public const bool sei_verzeihender = true;
	public const bool wallhit_means_reset = true;

	public const bool secondcamera = true; //the sizes of the cameras are set in the Start() of GameScript
	public const bool SeeCurbAsOff = false;
}

//================================================================================

public class AiInterface : MonoBehaviour {

	public WheelCollider colliderRL;
	public WheelCollider colliderRR;
	public WheelCollider colliderFL;
	public WheelCollider colliderFR;

	public PositionTracking Tracking;
	public CarController Car;
	public MinimapScript Minmap;
	public MiniMap2Script Minmap2;
	public GameScript Game;
	public Recorder Rec;

	// for debugging
	public GameObject posMarker1;
	public GameObject posMarker2;
	public GameObject posMarker3;
	public GameObject posMarker4;

	//for sending to python
	public long lastpythonupdate;
	public long lastpythonresult;
	public long lastgetvectortime;
	public string lastpythonsent;
	public Vector3 lastCarPos;
	public Quaternion lastCarRot;

	public float nn_steer = 0;
	public float nn_brake = 0;
	public float nn_throttle = 0;
	public bool AIDriving = false;
	public bool HumanTakingControl = false;
	public bool just_hit_wall = false;

	public AsynchronousClient SenderClient   = new AsynchronousClient(true);
	public AsynchronousClient ReceiverClient = new AsynchronousClient(false);

	//=============================================================================

	public static long MSTime() {
		//https://stackoverflow.com/questions/243351/environment-tickcount-vs-datetime-now 
		return ((long)(DateTime.UtcNow.Ticks/10000)) % 1000000; //there are 10000ticks in a ms
	}

	public static long UnityTime() {
		return (long)(Time.time * 1000);
	}


	// Use this for initialization
	void Start () {
		//Time.fixedDeltaTime = (10.0f)/ 1000; //TODO war mal CREATE_VECS_ALL
		lastpythonupdate =  UnityTime();
		lastpythonresult =  UnityTime();
		lastgetvectortime = UnityTime();	
		StartedAIMode ();
	}


	public void StartedAIMode() {
		if ((Game.mode.Contains ("drive_AI")) || (Game.mode.Contains ("train_AI"))) {  
			UnityEngine.Debug.Log ("Started AI Mode");
			SendToPython ("resetServer", true);
			ConnectAsReceiver ();
		}
	}


	// Update is called once per frame, and, in contrast to FixedUpdate, also runs when the game is frozen, hence the UnQuickPause here
	void Update () {
		if (ReceiverClient.response.othercommand && ReceiverClient.response.command == "pleaseUnFreeze") //this must be in Update, because if the game is frozen, FixedUpdate won't run.
			if ((Game.mode.Contains ("drive_AI")) && !HumanTakingControl)
				Car.UnQuickPause ();
	}


	public void FlipHumanTakingControl(bool force_overwrite = false, bool overwrite_with = false) {
		if (!force_overwrite) {
			HumanTakingControl = !HumanTakingControl;
		} else {
			HumanTakingControl = overwrite_with;
		}
		ReceiverClient.response.reset ();
	}
		

	public void resetCarAI() {
		if (Game.mode.Contains ("drive_AI")) {
			ReceiverClient.response.reset ();
			nn_brake = 0;
			nn_steer = 0;
			nn_throttle = 0;
		}
	}


	public void punish_wallhit() {
		if (Game.mode.Contains ("drive_AI")) {
			SendToPython ("wallhit", true); //ist das doppelt gemoppelt?
			just_hit_wall = true;
		}
	}


	//fixedupdate is run every physics-timestep, which is independent of framerate.
	void FixedUpdate() {
		
		if ((Game.mode.Contains ("drive_AI")) || (Game.mode.Contains ("train_AI"))) {  
			load_infos (false, false); //da das load_infos (mostly wegen dem konvertieren des visionvektors in ein int-array) recht lange dauert, hab ich mich entschieden das LADEN des visionvektor in update zu machen, und das SENDEN in fixedupdate, damit das spiel sich nicht aufhangt.
		}




		//SENDING the already prepared (every CREATE_VECS_ALL/25 ms, in Update) data to python (all updatepythonintervalms/200 ms)
		if (Game.mode.Contains("drive_AI")) {
			SendToPython (load_infos (false, true), false);  //die ist ein einzelner thread, also ruhig in fixedupdate.   (-> wenn not ONLY_UPDATE_IF_NEW, ODER wenn eh neu, DANN Sendet er!!
		}

		//RECEIVING the result from python
		if ((Game.mode.Contains("drive_AI")) && !HumanTakingControl) {
			string message;
			if (MSTime() - ReceiverClient.response.timestampStarted < Consts.MAXAGEPYTHONRESULT) 
			{
				message = ReceiverClient.response.pedals;
			} else { 
				//TODO: was SOLL er tun wenn das python-result zu alt ist??
				message = ReceiverClient.response.pedals; //[0, 0, 0]; //nicht leer, da der dann nicht die anderen sahcne überschreit!
				AIDriving = false;
			}
			if (ReceiverClient.response.othercommand && ReceiverClient.response.command == "pleasereset") { 
				Car.ResetCar (false); //false weil, wenn python dir gesagt hast dass du dich resetten sollst, du nicht python das noch sagen sollst
				ReceiverClient.response.othercommand = false;
				AIDriving = false;
			} else if (ReceiverClient.response.othercommand && ReceiverClient.response.command == "pleaseFreeze") { 
				Car.QuickPause ();
			} else if (ReceiverClient.response.othercommand && ReceiverClient.response.command == "pleaseUnFreeze") { 
				Car.UnQuickPause (); //is useless here, because FixedUpdate is not run during Freeze
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

		}
	}


	public string load_infos(Boolean force_reload, Boolean forbid_reload) {
		long currtime = UnityTime();

//		Vector3 pos = Car.Car.position;
//		Quaternion rot = Car.Car.rotation;
//
//		if (pos != lastCarPos || rot != lastCarRot) {
		if (((currtime - lastgetvectortime >= Consts.CREATE_VECS_ALL) || (force_reload)) && (!forbid_reload)) {
			lastgetvectortime = lastgetvectortime + Consts.CREATE_VECS_ALL;
			lastpythonsent = GetAllInfos ();
		} 
		return lastpythonsent;
	}




	//================================================================================
	// ################################################################
	// ################ MAIN GETTER AND SETTER FUNCTIONS ##############
	// ################################################################


	public string GetAllInfos() {
		//Keys: P: Progress as a real number in percent, Laptime rounded to 2, lapcount, validlap
		//      S: SpeedSteerVec (rounded to 4)
		//		T: CarStatusVec  (rounded to 4)
		//		C: CenterDistVec (rounded to 4)
		//		L: LookAheadVec  (rounded to 4)
		//		D: Delta & Feedback
		//	   V1: VisionVector1 (converted to decimal)
		//	   V2: VisionVector2 (converted to decimal) (if needed)
		//		R: Progress as a vector (rounded to 4) 
		//  CTime: CreationTime of Vector (Not send-time) (this one is in Unity-Time only! The STime will be in real time)

		StringBuilder all = new StringBuilder(1900);

		all.Append ("CTime("); all.Append (UnityTime().ToString()); all.Append (")");

		all.Append ("P("); all.Append (Math.Round(Tracking.progress * 100.0f ,3).ToString ()); all.Append(","); all.Append (Math.Round (Car.Timing.currentLapTime, 2).ToString ()); all.Append (","); all.Append (Car.Timing.lapCount.ToString ()); all.Append (","); all.Append (Car.lapClean.ToString () [0]); all.Append (")");

		all.Append ("S("); all.Append (string.Join (",", GetSpeedSteer ().Select (x => (Math.Round (x, 4)).ToString ()).ToArray ())); all.Append (")");

		all.Append ("T("); all.Append (string.Join (",", GetCarStatusVector ().Select (x => (Math.Round (x, 4)).ToString ()).ToArray ())); all.Append (")");

		float tmp = Tracking.GetCenterDist ();
		if (just_hit_wall) 
			tmp = 11; //10 ist die distanz der mauer, aber da er ja direkt resettet weiß er es anderenfalls nicht mehr
		all.Append ("C("); all.Append (Math.Round(tmp,3).ToString()); all.Append (","); all.Append (string.Join (",", GetCenterDistVector ().Select (x => (Math.Round(x,4)).ToString ()).ToArray ())); all.Append (")");

		all.Append ("L("); all.Append (string.Join (",", GetLookAheadVector ().Select (x => (Math.Round (x, 4)).ToString ()).ToArray ())); all.Append (")");

		all.Append ("D("); all.Append (Math.Round (Rec.GetDelta (), 2).ToString () + "," + Math.Round (Rec.GetFeedback (), 2).ToString ());  all.Append (")");

		all.Append ("V1("); all.Append (Minmap.GetVisionDisplay ()); all.Append (")");

		if (Consts.secondcamera)
			all.Append ("V2("); all.Append (Minmap2.GetVisionDisplay ()); all.Append (")");

		//all += "R"+ string.Join (",", GetProgressVector ().Select (x => (Math.Round(x,4)).ToString ()).ToArray ()) + ")";

		//TODO: gucken welche vektoren ich brauche
		//TODO: klären ob ich den Progress als number oder als vector brauche
		//TODO: vom carstatusvektor fehlen noch ganz viele

		return all.ToString ();
	}



	public float[] GetSpeedSteer() {
		float velo = Car.velocity;
		if (HumanTakingControl) { //um geschwindigkeiten zu faken damit man sich die entsprechenden q-werte anschauen kann
			if (Input.GetKey (KeyCode.Alpha0)) 
				velo = 0;
			if (Input.GetKey (KeyCode.Alpha1))
				velo = 20;
			if (Input.GetKey (KeyCode.Alpha2))
				velo = 40;
			if (Input.GetKey (KeyCode.Alpha3))
				velo = 60;
			if (Input.GetKey (KeyCode.Alpha4))
				velo = 90;
			if (Input.GetKey (KeyCode.Alpha5))
				velo = 110;
			if (Input.GetKey (KeyCode.Alpha6))
				velo = 140;
			if (Input.GetKey (KeyCode.Alpha7))
				velo = 180;
			if (Input.GetKey (KeyCode.Alpha8))
				velo = 210;
			if (Input.GetKey (KeyCode.Alpha9))
				velo = 250;			
		}
		float[] SpeedSteerVec = new float[6] { colliderRL.motorTorque, colliderRR.motorTorque, colliderFL.steerAngle, colliderFR.steerAngle, velo, Convert.ToInt32(Tracking.rightDirection) };
		return SpeedSteerVec;
	}


	//diese funktion wäre nötig für eine perfekt-reset-funktion
//	public void SetSpeedSteer(float[] SpeedSteerVec) {
//		colliderRL.motorTorque = SpeedSteerVec [0];
//		colliderRR.motorTorque = SpeedSteerVec [1];
//		colliderFL.steerAngle = SpeedSteerVec [2];
//		colliderFR.steerAngle = SpeedSteerVec [3];
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
		carStatusVector[0] = Car.velocity/200.0f; // car velocity > split up into more nodes 	# 1      //why do we need both the velocity and the speed from GetSpeedSteer?
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
		if (argClosest == int.MaxValue) {
			argClosest = 0;
		}
		return argClosest;
	}

	float GaussianDist(float mu, float sigma, float x)
	{
		return 1.0f/Mathf.Sqrt(2.0f*Mathf.PI*sigma)*Mathf.Exp(-Mathf.Pow((x-mu),2.0f)/(2.0f*Mathf.Pow(sigma,2.0f)));
	}


//================================================================================


	// this method is called whenever something is supposed to be sent to python. This method figures out if it is even supposed to
	// send, and if so, calls SenderClient's StartSenderClient
	public void SendToPython(string data, Boolean force) {
		if (!(Game.mode.Contains ("drive_AI"))) {return;}

		if (data == "resetServer") {
			SenderClient.StartClientSocket ();
			data += Consts.updatepythonintervalms; //hier weist er python auf die fps hin
		} else {
			data = "STime(" + MSTime() + ")" + data;
		}

		long currtime = UnityTime();
		if ((currtime - lastpythonupdate >= Consts.updatepythonintervalms) || (force)) {
			lastpythonupdate = lastpythonupdate + Consts.updatepythonintervalms;
			if (data != "resetServer")
				just_hit_wall = false;
			UnityEngine.Debug.Log ("SENDING TIME: " + MSTime ());
			var t = new Thread(() => SenderClient.SendAufJedenFall(data));
			t.Start();
		}
	}

	public void ConnectAsReceiver() {
		ReceiverClient.serverdown = false;
		ReceiverClient.StartClientSocket ();

		var t = new Thread(() => ReceiverClient.StartReceiveLoop());
		t.Start();
	}


	public static void KillOtherThreads() {
		ProcessThreadCollection currentThreads = Process.GetCurrentProcess().Threads;
		foreach (Thread thread in currentThreads)    
		{
			thread.Abort();
		}	
	}

	public void Reconnect() {
		Disconnect ();
		UnityEngine.Debug.Log ("Connecting...");
		SenderClient.serverdown = false;
		SenderClient.ResetServerConnectTrials();
		ReceiverClient.serverdown = false;
		ReceiverClient.ResetServerConnectTrials();
		ConnectAsReceiver ();
		SendToPython ("resetServer", true);
	}

	public void Disconnect() {
		if (!SenderClient.serverdown) {
			UnityEngine.Debug.Log ("Disconnecting...");
			SenderClient.StopClient ();
			SenderClient.serverdown = true;
			ReceiverClient.StopClient ();
			ReceiverClient.serverdown = true;
			AIDriving = false;
		}
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