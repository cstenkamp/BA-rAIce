using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

public class Recorder : MonoBehaviour {

	// external scripts & stuff
	public TimingScript Timing;
	public PositionTracking Tracking;
	public CarController Car;

	// für's timen der besten runde
	public List<PointInTime> thisLap; //eigene klasse, siehe unten
	public List<PointInTime> lastLap;
	public List<PointInTime> fastestLap;
	private int nStepsFeedback = 8;

	// für's komplette tracken fürs supervised-learning
	public List<TrackingPoint> SVLearnLap;
	private const int trackAllXMS = 100;
	private int lasttrack = Environment.TickCount;



	void FixedUpdate() {
		int currtime = Environment.TickCount;
		if (currtime - lasttrack > trackAllXMS) {

			SVLearnUpdateList ();
			lasttrack = currtime;
		}
	}





	// ##################################
	// ##### START, UPDATE & FINISH #####
	// ##################################

	// zwischen StartList und FinishList müssen für supervised learning sämtliche inputs (und ein reinformentlearning-target) getrackt werden.

	// dieses reinforcement-learning-target kann ab der zweiten validen runde die differenz zur ersten runde sein...? Dann würde man am Ende die erste runde mit überall positive infinity definitiv
	//   ...rausnehmen müssen da das das learning kaputt macht... Der Nachteil der Methode ist dass nen Target von 0 immer noch sehr gut sein kann... und RL das nicht checkt.
	// Alternative dazu wäre dass man immer die differenz zu ner baseline-runde nimmt. Dann würde der vielleicht individuell wissen welche er beschleunigen kann und welche nicht...
	// Dritte Alternative (die momentane Standard-Q-Learn-Procedure) ist dass man halt immer nur die finale Runde zählt, aber da das millions of frames apart ist wäre der Gradient definitiv = 0.

	public void StartList()
	{
		thisLap = new List<PointInTime>();
		thisLap.Add(new PointInTime(0.0f, 0.0f));

		if (Consts.record_all_inputs) {
			SVLearnLap = new List<TrackingPoint> ();
			SVLearnLap.Add (new TrackingPoint (0.0f, Car.throttlePedalValue, Car.brakePedalValue, Car.steeringValue, 0.0f)); //TODO: sollte er nicht auch beim SV-Learning den Visionvector etc durchgehend mit-recorden???
		}
	}


	public void UpdateList() //wird in der positiontracking.triggerrec gecallt (aka jedes mal wenn er nen trigger durchfährt)
	{
		thisLap.Add(new PointInTime(Timing.currentLapTime, Tracking.progress));
	}


	//function SVLearnUpdateList, die jedes Frame (oder x mal die sekunde) gecallt wird (globaler param oben)
	public void SVLearnUpdateList(){
		if (Consts.record_all_inputs) {
			SVLearnLap.Add (new TrackingPoint (Timing.currentLapTime, Car.throttlePedalValue, Car.brakePedalValue, Car.steeringValue, Tracking.progress));
		}
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

		//TODO woher weiß ich eigentlich, dass er nr valid runden zählt??
		SVLearnLap.Add (new TrackingPoint (Timing.lastLapTime, Car.throttlePedalValue, Car.brakePedalValue, Car.steeringValue, 1.0f));
		SaveSVLearnLap (SVLearnLap, "complete_" + DateTime.Now.ToString ("MM_dd_yy_h_mm_ss")); 

	}

	// ##################################
	// ###### ADDITIONAL FUNCTIONS ######
	// ##################################
	//TODO: diese werden gebraucht um anhand derer supervisedly zu lernen! ...und möglicherweise noch mehr?

	public float GetDelta() //für das Sekunden-Delta im UI
	{
		if (Timing.activeLap && Timing.fastLapSet)
		{
			int k = thisLap.Count-1;
			return thisLap[k].time - fastestLap[k].time;
		}
		return 0.0f; //fürs Learnen sollte das doch eher positive infinity sein, oder? Erste basis-runde ist überall top
	}



	public float GetFeedback() //für die Feedbackbar vom UI
	{
		if (Timing.activeLap && Timing.fastLapSet)
		{
			int k = thisLap.Count-1;
			int n = nStepsFeedback; if (k-n < 0) { n = k; } //die nStepsFeedback kann fürs netwerk sehr relevant sein, kann sein dass der mit zu vielen gar nichts macht
															//TODO eine zusäztliche nStepsFeedback fürs lernen... da es helfen könnte sehrsehrviel öfter feedback fürs netz zu kriegen
			float[] nDeltas = new float[2];
			nDeltas[0] = thisLap[k-n].time - fastestLap[k-n].time; //Feedback ist im gegensatz zu delta NUR der Unterschied innerhalb des letzten checkpointsteps, whereas Delta ist der Unterschied since start...
			nDeltas[1] = thisLap[k].time - fastestLap[k].time;     //Ist fürs Netzwerk nicht beides Relevant? Innerhalb kurven (kurven-abschnitte), die sehr viel länger als ein solches checkpointstep sind...
			return nDeltas[0]-nDeltas[1];						   //Kann Deep-Q-Learning irgendwie mit 3 verschiedenen Targets im großem, mittel & kleinem maß, umgehen??
		}
		return 0.0f;
	}



	//TODO ResetLap in der noch-kommenden Neustart-funktion nutzen!
	//TODO ne resetlap für das svlearnlapdingsi!
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

	//braucht er scheinbar nicht?
//	private List<PointInTime> CloneLap(List<PointInTime> originalLap)
//	{
//		List<PointInTime> clonedLap = new List<PointInTime>();
//		for (int i = 0; i < originalLap.Count; i++)
//		{
//			clonedLap.Add(originalLap[i]);
//		}
//		return clonedLap;
//	}

	// ##################################
	// #### SAVE & LOAD COMPLETE LAP ####
	// ##################################

	public void SaveSVLearnLap(List<TrackingPoint> SVLearnLap, string fileName) 
	{
		if (!Directory.Exists("SavedLaps/")) {
			Debug.Log ("You have to create a folder 'SavedLaps' to save laps!");
		} else {
			BinaryFormatter bf = new BinaryFormatter();
			FileStream file = File.Create("SavedLaps/" + fileName + ".svlap");
			bf.Serialize(file, SVLearnLap);
			file.Close();
		}	
	}


	public void SaveLap(List<PointInTime> lap, string fileName)
	{
		if (!Directory.Exists("SavedLaps/")) {
			Debug.Log ("You have to create a folder 'SavedLaps' to save laps!");
		} else {
			BinaryFormatter bf = new BinaryFormatter();
			FileStream file = File.Create("SavedLaps/" + fileName + ".lap");
			bf.Serialize(file, lap);
			file.Close();
		}
	}

	public bool LoadLap(string fileName) //wird in "start" von Gamescript gecallt, ganz zu Anfang des Spiels
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
	
// fürs alles-tracken ne ähnliche klasse machen, die zu jedem frame (oder x mal die sekunde), nicht nur jedem xten positiontracker, alles inkl. inputs trackt und speichert als NN-inputs 
// (als file, das der fürs supervised-learning auspackt)

[Serializable]
public class TrackingPoint
{
	// class variables
	public float time;
	public float throttlePedalValue;
	public float brakePedalValue;
	public float steeringValue;
	public float progress;

	// constructors
	public TrackingPoint(){}
	public TrackingPoint(float newtime, float newthrottlePedalValue, float newbrakePedalValue, float newsteeringValue, float newprogress) { 
		time = newtime;
		throttlePedalValue = newthrottlePedalValue; 
		brakePedalValue = newbrakePedalValue; 
		steeringValue = newsteeringValue; 
		progress = newprogress;
	}
}