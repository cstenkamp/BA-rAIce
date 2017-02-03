using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

public class Recorder : MonoBehaviour {

	// external scripts
	public TimingScript Timing;
	public PositionTracking Tracking;
	// public variables
	public List<PointInTime> thisLap;
	public List<PointInTime> lastLap;
	public List<PointInTime> fastestLap;
	private int nStepsFeedback = 8;

	// ##################################
	// ##### START, UPDATE & FINISH #####
	// ##################################

	public void StartList()
	{
		thisLap = new List<PointInTime>();
		thisLap.Add(new PointInTime(0.0f, 0.0f));
	}

	public void UpdateList()
	{
		thisLap.Add(new PointInTime(Timing.currentLapTime, Tracking.progress));
	}

	public void FinishList()
	{
		thisLap.Add(new PointInTime(Timing.lastLapTime, 1.0f));
		lastLap = thisLap;
		if (Timing.lastLapTime == Timing.fastestLapTime)
		{
			fastestLap = lastLap;
			SaveLap(fastestLap, "fastlap");
		}
	}

	// ##################################
	// ###### ADDITIONAL FUNCTIONS ######
	// ##################################

	public float GetDelta()
	{
		if (Timing.activeLap && Timing.fastLapSet)
		{
			int k = thisLap.Count-1;
			return thisLap[k].time - fastestLap[k].time;
		}
		return 0.0f;
	}

	public float GetFeedback()
	{
		if (Timing.activeLap && Timing.fastLapSet)
		{
			int k = thisLap.Count-1;
			int n = nStepsFeedback; if (k-n < 0) { n = k; }
			float[] nDeltas = new float[2];
			nDeltas[0] = thisLap[k-n].time - fastestLap[k-n].time;
			nDeltas[1] = thisLap[k].time - fastestLap[k].time;
			return nDeltas[0]-nDeltas[1];
		}
		return 0.0f;
	}

	public void ResetLap()
	{
		thisLap = new List<PointInTime>();
	}

	public void ResetAll()
	{
		thisLap = new List<PointInTime>();
		lastLap = new List<PointInTime>();
		fastestLap = new List<PointInTime>();
	}

	private List<PointInTime> CloneLap(List<PointInTime> originalLap)
	{
		List<PointInTime> clonedLap = new List<PointInTime>();
		for (int i = 0; i < originalLap.Count; i++)
		{
			clonedLap.Add(originalLap[i]);
		}
		return clonedLap;
	}

	// ##################################
	// #### SAVE & LOAD COMPLETE LAP ####
	// ##################################

	public void SaveLap(List<PointInTime> lap, string fileName)
	{
		BinaryFormatter bf = new BinaryFormatter();
		FileStream file = File.Create("SavedLaps/" + fileName + ".lap");
		bf.Serialize(file, lap);
		file.Close();
	}

	public bool LoadLap(string fileName)
	{
		if (File.Exists("SavedLaps/" + fileName + ".lap"))
		{
			BinaryFormatter bf = new BinaryFormatter();
			FileStream file = File.Open("SavedLaps/" + fileName + ".lap", FileMode.Open);
			fastestLap = (List<PointInTime>)bf.Deserialize(file);
			file.Close();
			return true;
		}
		return false;
	}
}

	// ##################################
	// ######### HELPER CLASSES #########
	// ##################################

[Serializable]
public class PointInTime
{
	// class variables
	public float time;
	public float progress;
	// constructors
	public PointInTime(){}
	public PointInTime(float newTime, float newProgress) { time = newTime; progress = newProgress; }
}