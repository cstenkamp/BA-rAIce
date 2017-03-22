using UnityEngine;
using System.Collections;
using System;
using System.Linq;

//hierein die funktion zum reset bei Wandkontakt. Kann dafür im start() nen trigger an alle wände setzen, und OnTriggerEnter (Collider other) das auto (falls flag true) resetten


public class CarController : MonoBehaviour {

	public GameScript Game;
    public AiInterface AiInt;

	public float throttlePedalValue;
	public float brakePedalValue;
	public float steeringValue;

	public WheelCollider colliderFL;
	public WheelCollider colliderFR;
	public WheelCollider colliderRL;
	public WheelCollider colliderRR;

	public Transform transformFL;
	public Transform transformFR;
	public Transform transformRL;
	public Transform transformRR;

	public bool lapClean;
	string surfaceFL;
	string surfaceFR;
	string surfaceRL;
	string surfaceRR;
	string pedalType = "digital"; // "digital" for direct A / Y input

	public Rigidbody Car;

	float maxMotorTorque = 1200.0f;
	float maxBrakeTorque = 3300.0f;
	float maxSteer = 20.0f;

	public float gear = 1.0f;
	float BrakeBias = 0.65f; // fraction of brake torque applied to front wheels: 0.67

	Vector3 startPosition = new Vector3(48.0f,1.1f,150.0f);
	Quaternion startRotation = new Quaternion(0.0f,180.0f,0.0f,0.0f);
	Vector3 lastPosition;
	float lastTime;
	float deltaPosition;
	float deltaTime;
	public float velocity;

	// Use this for initialization
	void Start ()
	{
		// center of mass
		Car.transform.position = startPosition;
		Car.centerOfMass = new Vector3(0.0f,0.0f,0.0f);
	}


	// Update is called once per frame
	void FixedUpdate ()
	{
		if (Game.mode.Contains("driving"))
		{
			// check surfaces
			AdjustFriction(colliderFL, "FL");
			AdjustFriction(colliderFR, "FR");
			AdjustFriction(colliderRL, "RL");
			AdjustFriction(colliderRR, "RR");

			// check if car is on track
			surfaceFL = CheckSurface(colliderFL);
			surfaceFR = CheckSurface(colliderFR);
			surfaceRL = CheckSurface(colliderRL);
			surfaceRR = CheckSurface(colliderRR);
			int chksm = 0;
			if (surfaceFL == "off") { chksm += 1; }
			if (surfaceFR == "off") { chksm += 1; }
			if (surfaceRL == "off") { chksm += 1; }
			if (surfaceRR == "off") { chksm += 1; }
			if (chksm >= 3) { lapClean = false; }

			// calculate velocity
			deltaTime = Time.time - lastTime;
			deltaPosition = Vector3.Distance(lastPosition,transform.position);
			velocity = deltaPosition/deltaTime*3.6f;
			lastPosition = transform.position;
			lastTime = Time.time;
	
			// car control
			steeringValue = Input.GetAxis("Horizontal");
			if (pedalType == "digital")
			{
				throttlePedalValue = System.Convert.ToSingle(Input.GetKey(KeyCode.A));
				brakePedalValue = System.Convert.ToSingle(Input.GetKey(KeyCode.Y));
			}
			else
			{
				throttlePedalValue = System.Convert.ToSingle(Input.GetAxis("Vertical"));
				if (throttlePedalValue < 0) { throttlePedalValue = 0.0f; }
				brakePedalValue = -System.Convert.ToSingle(Input.GetAxis("Vertical"));
				if (brakePedalValue < 0) { brakePedalValue = 0.0f; }
			}



            // accelerate
            if (AiInt.AITakingControl == false) { 
                colliderRL.motorTorque = maxMotorTorque * throttlePedalValue * gear;
                colliderRR.motorTorque = maxMotorTorque * throttlePedalValue * gear;
            }

            // brake
            colliderFL.brakeTorque = maxBrakeTorque * brakePedalValue * BrakeBias;
			colliderFR.brakeTorque = maxBrakeTorque * brakePedalValue * BrakeBias;
			colliderRL.brakeTorque = maxBrakeTorque * brakePedalValue * (1.0f-BrakeBias);
			colliderRR.brakeTorque = maxBrakeTorque * brakePedalValue * (1.0f-BrakeBias);
            // steer
            colliderFL.steerAngle = maxSteer * steeringValue;
			colliderFR.steerAngle = maxSteer * steeringValue;
		}
	}

	void Update()
	{

		if (Game.mode.Contains("menu"))
		{
			// reverse gear
			if (Input.GetKeyDown(KeyCode.R)) { gear *= -1.0f; }
	
			// wheels rotation
			transformFL.Rotate(colliderFL.rpm/60*360*Time.deltaTime,0,0);
			transformFR.Rotate(colliderFR.rpm/60*360*Time.deltaTime,0,0);
			transformRL.Rotate(colliderRL.rpm/60*360*Time.deltaTime,0,0);
			transformRR.Rotate(colliderRR.rpm/60*360*Time.deltaTime,0,0);
	
			// steering lock
			Vector3 tmpRotFL = transformFL.localEulerAngles;
			tmpRotFL.y = colliderFL.steerAngle-transformFL.localEulerAngles.z;
			transformFL.localEulerAngles = tmpRotFL;
			Vector3 tmpRotFR = transformFR.localEulerAngles;
			tmpRotFR.y = colliderFR.steerAngle-transformFR.localEulerAngles.z;
			transformFR.localEulerAngles = tmpRotFR;

			// wheel height FL
			Vector3 FLpos;
			Quaternion FLrot;
			colliderFL.GetWorldPose(out FLpos, out FLrot);
			Vector3 tmpPosFL = transformFL.transform.position;
			tmpPosFL.y = FLpos.y;
			transformFL.position = tmpPosFL;

			// wheel height FR
			Vector3 FRpos;
			Quaternion FRrot;
			colliderFR.GetWorldPose(out FRpos, out FRrot);
			Vector3 tmpPosFR = transformFR.transform.position;
			tmpPosFR.y = FRpos.y;
			transformFR.position = tmpPosFR;

			// wheel height RL
			Vector3 RLpos;
			Quaternion RLrot;
			colliderRL.GetWorldPose(out RLpos, out RLrot);
			Vector3 tmpPosRL = transformRL.transform.position;
			tmpPosRL.y = RLpos.y;
			transformRL.position = tmpPosRL;

			// wheel height RR
			Vector3 RRpos;
			Quaternion RRrot;
			colliderRR.GetWorldPose(out RRpos, out RRrot);
			Vector3 tmpPosRR = transformRR.transform.position;
			tmpPosRR.y = RRpos.y;
			transformRR.position = tmpPosRR;

		}

		// pause & exit to menu
		if (Input.GetKeyDown(KeyCode.Escape)) { Game.SwitchMode("menu"); }
	}

	public void ResetCar() //TODO: das hier nicht nur für den start, sondern für alle möglichen reset-punkte
	{
		// reset the car & kill innertia
		Car.transform.position = startPosition;
		Car.transform.rotation = startRotation;
		Car.velocity = Vector3.zero;
	    Car.angularVelocity = Vector3.zero;
		
		// reset wheel torques and steering angles instantly
		colliderRL.motorTorque = 0.0f;
		colliderRR.motorTorque = 0.0f;
		colliderFL.brakeTorque = 0.0f;
		colliderFR.brakeTorque = 0.0f;
		colliderRL.brakeTorque = 0.0f;
		colliderRR.brakeTorque = 0.0f;
		colliderFL.steerAngle = 0.0f;
		colliderFR.steerAngle = 0.0f;
	}



	void AdjustFriction(WheelCollider wheel, string whichWheel)
	{
		float axle_multi = 1.0f;
		float surface_multi = 1.0f;

		if (whichWheel == "FL") { axle_multi=1.0f; }
		if (whichWheel == "FR") { axle_multi=1.0f; }
		if (whichWheel == "RL") { axle_multi=1.4f; }
		if (whichWheel == "RR") { axle_multi=1.4f; }

		string currentSurface = CheckSurface(wheel);
		if (currentSurface == "track") { surface_multi = 1.0f; }
		if (currentSurface == "curb") { surface_multi = 0.9f; }
		if (currentSurface == "off") { surface_multi = 0.5f; }

		// slip handling
		float slipAlpha = 0.5f; 		// upper bound of grip loss through slip
		float posSlip_multi = 1.0f;
		float negSlip_multi = 1.0f;
		float negSlip_factor = 1.0f;
		float posSlip_factor = 5.0f;
		Vector2 slipVector = GetSlip(wheel);
		float forwardSlip = slipVector[0];
		if (slipVector[0] > 0) // if there is positive slip
		{
			posSlip_multi = (1.0f-Math.Abs(forwardSlip)*posSlip_factor);
			if (posSlip_multi < 0) { posSlip_multi = 0; }
		}
		else if (slipVector[0] < 0) // if there is negative slip
		{
			negSlip_multi = (1.0f-Math.Abs(forwardSlip)*negSlip_factor);
			if (negSlip_multi < 0) { negSlip_multi = 0; }
		}

		float feS = 0.15f; // 0.15f
		float feV = 1.0f;  // 1.0f
		float faS = 2.0f;  // 2.0f
		float faV = 0.4f;  // 0.8f
		float fsT = 2.0f * surface_multi * axle_multi;

		float seS = 0.15f; // 0.15f
		float seV = 1.0f;  // 1.0f
		float saS = 2.0f;  // 2.0f
		float saV = 0.4f;  // 0.8f
		float ssT = 2.0f * surface_multi *  axle_multi;
		ssT = ssT*(1.0f-slipAlpha) + (ssT*slipAlpha) *posSlip_multi *negSlip_multi; // slip handling in ssT

		// forward friction
		WheelFrictionCurve forward = wheel.forwardFriction;
		forward.extremumSlip = feS;
		forward.extremumValue = feV;
		forward.asymptoteSlip = faS;
		forward.asymptoteValue = faV;
		forward.stiffness = fsT;
		wheel.forwardFriction = forward;

		// sideways friction
		WheelFrictionCurve sideways = wheel.sidewaysFriction;
		sideways.extremumSlip = seS;
		sideways.extremumValue = seV;
		sideways.asymptoteSlip = saS;
		sideways.asymptoteValue = saV;
		sideways.stiffness = ssT;
		wheel.sidewaysFriction = sideways;


		// note which wheel has which surface
		if (whichWheel == "FL") { surfaceFL = currentSurface; }
		if (whichWheel == "FR") { surfaceFR = currentSurface; }
		if (whichWheel == "RL") { surfaceRL = currentSurface; }
		if (whichWheel == "RR") { surfaceRR = currentSurface; }
	}

	public Vector2 GetSlip(WheelCollider colliderW) {
		WheelHit hit;
		Vector2 slipVector = new Vector2();     // wenn man fast steht, springt der slipvalue zwischen 0.13 und -0.13 herum
		if (colliderW.GetGroundHit (out hit)) { // TODO: calculate forward slip by deviding wheel rotation by car velocity in forward direction (von reifenumdrehungen,reifengrosse die reifenoberflachenvelocity errechnen und teilen durch tatsachliches autovelocity (aber nur in direction der reifenausrichtung))
			slipVector[0] = hit.forwardSlip;    // alternativer workaround ware einfach zu sagen wenn die carvelocity zu klein ist, 
			slipVector[1] = hit.sidewaysSlip;
		}
		else {
			slipVector[0] =		 -5.0f;
			slipVector[1] = -5.0f;
		}
		return slipVector;
	}

	string CheckSurface(WheelCollider wheel)
	{
		RaycastHit hit;
		if (Physics.Raycast(wheel.transform.position, -Vector3.up, out hit, maxDistance: 1.2f))
		{
			if(hit.collider.tag=="surfaceTrack") { return "track"; }
			if(hit.collider.tag=="surfaceCurb") { return "curb"; }
			if(hit.collider.tag=="surfaceOff") { return "off"; }
		}
		return "unknown";
	}

	public float CheckSurface(float X, float Z)
	{
		RaycastHit hit;
		Vector3 pos = new Vector3(X,1.4f,Z);
		if (Physics.Raycast(pos, -Vector3.up, out hit, maxDistance: 0.5f))
		{
			if(hit.collider.tag=="surfaceTrack") { return 1.0f; }
			if(hit.collider.tag=="surfaceCurb") { return 0.0f; }
			if(hit.collider.tag=="surfaceOff") { return 0.0f; }
		}
		return 0.0f;
	}

	public void LapCleanTrue()
	{
		lapClean = true;
	}

}
