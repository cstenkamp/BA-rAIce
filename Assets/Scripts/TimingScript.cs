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

	// Use this for initialization
	void Start () {}
	
	// Update is called once per frame
	void Update ()
	{
		if (activeLap)
		{
			currentLapTime = Time.time - currentLapStart;
		}
	}

	// Start/ finish collider updating the laptimes
	void OnTriggerEnter (Collider other)
	{
		// last lap time & fastest lap time update
		if (activeLap && ccPassed && Car.lapClean)
		{
			lastLapTime = Time.time - currentLapStart;
			if (!timeSet)
			{
				timeSet = true;
			}
			if (!fastLapSet)
			{
				fastestLapTime = lastLapTime;
				fastLapSet = true;
			}
			else if (timeSet && lastLapTime < fastestLapTime)
			{
				fastestLapTime = lastLapTime;
				fastestLapCount = lapCount;
			}
			Rec.FinishList();
		}
		if (!activeLap)
		{
			activeLap = true;
		}
		Rec.StartList();
		lapCount += 1;
		currentLapStart = Time.time;
		Car.LapCleanTrue();
		ccPassed = false;
	}

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