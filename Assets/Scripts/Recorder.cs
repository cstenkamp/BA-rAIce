using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Threading;

//TODO probeweise könnte man python zusätzlich alle keys aufnehmen können, und durch den druck einer speziellen Taste sende ich das kommando an python dass es
//     resetten soll und alle keys nochmal genauso drücken soll

public class Recorder : MonoBehaviour {

	//this is only true in the "train AI supervisedly"-mode
	public static bool sv_save_round = true;


	// external scripts & stuff
	public TimingScript Timing;
	public PositionTracking Tracking;
	public CarController Car;
	public AiInterface AiInt;

	// für's timen der besten runde
	public List<PointInTime> thisLap; //eigene klasse, siehe unten
	public List<PointInTime> lastLap;
	public List<PointInTime> fastestLap;
	private int nStepsFeedback = 8;

	// für's komplette tracken fürs supervised-learning
	public List<TrackingPoint> SVLearnLap;
	private const int trackAllXMS = 100; //bei alle 10ms sinds 4 MB...
	private int lasttrack = Environment.TickCount;



	void FixedUpdate() {
		if (sv_save_round) {
			int currtime = Environment.TickCount;
			if (currtime - lasttrack > trackAllXMS) {
				SVLearnUpdateList ();
				lasttrack = currtime;
			}
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

		if (sv_save_round) {
			SVLearnLap = new List<TrackingPoint> ();
			SVLearnLap.Add (new TrackingPoint (0.0f, Car.throttlePedalValue, Car.brakePedalValue, Car.steeringValue, 0.0f, AiInt.load_infos(false,true))); 
		}
	}


	public void UpdateList() //wird in der positiontracking.triggerrec gecallt (aka jedes mal wenn er nen trigger durchfährt)
	{
		thisLap.Add(new PointInTime(Timing.currentLapTime, Tracking.progress));
	}


	//function SVLearnUpdateList, die jedes Frame (oder x mal die sekunde) gecallt wird (globaler param oben)
	public void SVLearnUpdateList(){
		if (sv_save_round) {
			SVLearnLap.Add (new TrackingPoint (Timing.currentLapTime, Car.throttlePedalValue, Car.brakePedalValue, Car.steeringValue, Tracking.progress, AiInt.load_infos(false,true)));
		}
	}

	//FinishList wird im TimingScript ausgeführt, bei Triggerkollision mit dem Start/Ziel-Trigger, und zwar nur wenn activeLap && ccPassed && Car.lapClean
	public void FinishList()
	{
		thisLap.Add(new PointInTime(Timing.currentLapTime, 1.0f));
		lastLap = thisLap;
		if (Timing.lastLapTime == Timing.fastestLapTime)
		{
			fastestLap = lastLap;
			SaveLap(fastestLap, "fastlap");
		}

		if (sv_save_round) {
			SVLearnLap.Add (new TrackingPoint (Timing.lastLapTime, Car.throttlePedalValue, Car.brakePedalValue, Car.steeringValue, 1.0f, AiInt.load_infos (false, true)));
			SaveSVLearnLapStart (SVLearnLap, "complete_" + DateTime.Now.ToString ("yy_MM_dd__hh_mm_ss")); 
		}
	}

	// ##################################
	// ###### ADDITIONAL FUNCTIONS ######
	// ##################################
	//TODO: diese werden gebraucht um anhand derer supervisedly zu lernen! ...und möglicherweise noch mehr?
	//TODO: in jedem Fall diese hier mitsenden... :/

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


	//TODO: nen flag, dass, wenn gesetzt, dafur sorgt dass er aufhort zu recorden

//	public void ResetAll()
//	{
//		ResetLap ();
//		lastLap = new List<PointInTime>();
//		fastestLap = new List<PointInTime>();
// 		//TODO: so wie das hier ist machts keinen Sinn. Er sollte auch die files loschen, und dafur sorgen dass das nicht direkt neu geladen wird
//		//TODO: in die optionen ne funktion zum loschen aller laps, die diese function callt
//	}


	// ##################################
	// #### SAVE & LOAD COMPLETE LAP ####
	// ##################################

	public void SaveSVLearnLapStart(List<TrackingPoint> SVLearnLap, string fileName) 
	{
		if (sv_save_round) {
			var t = new Thread (() => SaveSVLearnLap (SVLearnLap, fileName)); 
			t.Start ();
		}
	}

	public void SaveSVLearnLap(List<TrackingPoint> SVLearnLap, string fileName) 
	{
		if (sv_save_round) {
			if (!Directory.Exists ("SavedLaps/")) {
				Debug.Log ("You have to create a folder 'SavedLaps' to save laps!");
			} else {
				System.Xml.Serialization.XmlSerializer xs = new System.Xml.Serialization.XmlSerializer (SVLearnLap.GetType ());
				FileStream file = File.Create ("SavedLaps/" + fileName + ".svlap");
				xs.Serialize (file, SVLearnLap);
				file.Close ();
			}	
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
	


//diese Klasse ist für's supervisedly learning: alle x Millisekunden speichert sie alle nötigen informaitonen für's supervised-learning (als NN-Input für python)
[Serializable]
public class TrackingPoint
{
	// class variables
	public float time;
	public float throttlePedalValue;
	public float brakePedalValue;
	public float steeringValue;
	public float progress;
	public string vectors;

	// constructors
	public TrackingPoint(){}
	public TrackingPoint(float newtime, float newthrottlePedalValue, float newbrakePedalValue, float newsteeringValue, float newprogress, string newvectors) { 
		time = newtime;
		throttlePedalValue = newthrottlePedalValue; 
		brakePedalValue = newbrakePedalValue; 
		steeringValue = newsteeringValue; 
		progress = newprogress;
		vectors = newvectors;
	}
}