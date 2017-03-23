using UnityEngine;
using System.Collections;

public class TimingScript : MonoBehaviour {

	// variables for lap time measurement
	public float currentLapTime;
	public float lastLapTime;
	public float fastestLapTime;
	public int fastestLapCount;
	float currentLapStart;
	public int lapCount = 0;
	public bool timeSet = false;
	public bool fastLapSet = false;
	public bool activeLap;
	public bool ccPassed = false;
	public UIScript UserInterface;
	public CarController Car;

	// variables for lapSaving
	public Recorder Rec;

	private float time_punishs;

	// Use this for initialization
	void Start () 
	{
		time_punishs = 0;	
	}
	
	// Update is called once per frame
	void Update ()
	{
		if (activeLap)
		{
			currentLapTime = Time.time - currentLapStart + time_punishs;
		}
	}

	// Start/ finish collider updating the laptimes
	void OnTriggerEnter (Collider other)            //"this" here is the timingsystem, a collider in root, and the only "other" there is that can move is only the car
	{
		// last lap time & fastest lap time update 
		if (activeLap && ccPassed && Car.lapClean)  //ccpassed heißt dass er schon durch den zweiten collider ist, lapclean heißt 1 reifen auf straße... activelap ist true sobald man den trigger entered (was dank CarControllerSkript nur im game-modus geht)
		{                                           //also, im grunde kommt man hier schon rein wenn man eine valide, ungecheatete, komplette runde gefahren ist.
			lastLapTime = Time.time - currentLapStart;
			timeSet = true;
			if (!fastLapSet || (timeSet && lastLapTime < fastestLapTime)) //wenn diese die erste oder letzte runde ist
			{
				fastestLapTime = lastLapTime;
				fastestLapCount = lapCount;
				fastLapSet = true;
			}
			Rec.FinishList();
		}
		activeLap = true; //activelap ists erst nach dem zweiten validen ungecheateten colliderdurchlauf
		Rec.StartList();
		lapCount += 1;
		currentLapStart = Time.time;
		Car.LapCleanTrue();
		ccPassed = false; //wird erst wieder true wenn man durch den zweiten collider ist, und wieder falls sobald man cheatenderweise nochmal anschließend zurückfährt (deswegen da ontriggerexit!)
		time_punishs = 0;
	}

	public void PunishTime(float howmuch) 
	{
		if (activeLap)
			time_punishs += howmuch;
	}

	//TODO: Ich glaube es wird bspw beim menu offnen timing.reset aufgerufen obwohl es timing.stop sein sollte

	// Reset Timing Script
	public void ResetTiming()
	{
		currentLapTime = 0.0f;
		activeLap = false;
	}

	// Reset Session Script
	public void ResetSessionTiming()
	{
		lapCount = 0;
		currentLapTime = 0.0f;
		timeSet = false;
		activeLap = false;
	}

	public void FlipCcPassed()
	{
		if (ccPassed) { ccPassed = false; }
		else if (!ccPassed) { ccPassed = true; }
	}

}