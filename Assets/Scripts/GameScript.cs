﻿using UnityEngine;
using System.Collections;
using System.Linq;

//wenn man das spiel komplett neu starten können wollte, muss man dafür ResetSessionTiming vom Timinscript callen

public class GameScript : MonoBehaviour {

	public string[] mode = new string[1]{"menu"};
	public GameObject OverviewCamera;
	public GameObject CarCamera;
	public GameObject MiniMapCamera;
	public GameObject MiniMapCam2;
    public CarController Car;
	public TimingScript Timing;
	public Recorder Rec;
	public UIScript UserInterface;
	public AiInterface AiInt;


	// Use this for initialization
	void Start ()
	{
		mode = new string[1]{"menu"};
		CarCamera.SetActive(false);
		MiniMapCamera.SetActive(false);
		MiniMapCam2.SetActive (false); 
		if (!Consts.secondcamera) { 
			((Camera)MiniMapCam2.GetComponent<Camera>()).enabled = false;	
			((Camera)MiniMapCamera.GetComponent<Camera>()).orthographicSize = 57;
			MiniMapCamera.transform.localPosition= new Vector3 (MiniMapCamera.transform.localPosition.x, MiniMapCamera.transform.localPosition.y, 50);
		} else {
			((Camera)MiniMapCamera.GetComponent<Camera>()).orthographicSize = 75;
			MiniMapCam2.transform.localPosition= new Vector3 (MiniMapCam2.transform.localPosition.x, MiniMapCam2.transform.localPosition.y, 28);
			MiniMapCamera.transform.localPosition= new Vector3 (MiniMapCamera.transform.localPosition.x, MiniMapCamera.transform.localPosition.y, 63);
			((Camera)MiniMapCam2.GetComponent<Camera>()).orthographicSize = 35;
		}
        OverviewCamera.SetActive(true);
		if (Rec.LoadLap("fastlap"))
		{
			Timing.fastLapSet = true;
			Timing.fastestLapTime = Rec.fastestLap[Rec.fastestLap.Count-1].time;
		}
	}
	
	// Update is called once per frame
	void Update ()
	{
	}

	// handle changes to the game mode
	public void SwitchMode (string newMode)
	{
		//TODO! wenn man escape drückt soll der ein ANDERES menü öffnen "gehe zu letztem checkpoint, resette auto, zurück" (da drin wäre dann auch nicht timing.reset sondern timing.stop)
		//TODO: dann beim resetten auf die resets in carcontroller, recorder, timingscript, ... achten!

		Car.UnQuickPause ("All");
		if (AiInt.AIMode) {
			AiInt.SenderClient.ResetServerConnectTrials ();
			AiInterface.KillOtherThreads ();
			AiInt.SenderClient.serverdown = true;
		}

		//TODO: diese 3 Zeilen müssen früher oder später weg.
		if (newMode == "driving") {
			mode = new string[3]{ "driving", "train_AI", "keyboarddriving" };  
		} else

		if (newMode == "train_AI") {
			mode = new string[3]{ "driving", "train_AI", "keyboarddriving" };
		} else if (newMode == "drive_AI") {
			mode = new string[2]{ "driving", "drive_AI" };
		} else 	{
			mode = new string[1]{newMode};
		}
		
		AiInt.AIMode = false;
		Recorder.sv_save_round = false;
		CarCamera.SetActive (false);
		MiniMapCamera.SetActive (false);
		if (Consts.secondcamera) { MiniMapCam2.SetActive (false); }
		OverviewCamera.SetActive (false);
		Car.ResetCar (true);
		Timing.ResetTiming ();
		UserInterface.UpdateGameModeDisp ();
		//Rec.ResetLap() ist überflüssig


		if (mode.Contains("menu")) { //geht er hin wenn man escape drückt
			OverviewCamera.SetActive (true);
		} 

		if (mode.Contains("driving")) {
			CarCamera.SetActive (true);
			MiniMapCamera.SetActive (true);
			if (Consts.secondcamera) { MiniMapCam2.SetActive (true); }
		}

		if (mode.Contains("train_AI")) {
			Recorder.sv_save_round = true;
		} 

		if (mode.Contains("drive_AI")) {
			AiInt.StartedAIMode ();
			AiInt.SenderClient.serverdown = false; 
		} 
			

	}

	public string UpdateGameModeDisplay() {
		string modetxt = "";
		foreach (string curr in mode) {
			modetxt += curr + " ";
		}	
		if (AiInt.HumanTakingControl) {
			modetxt += "(Human Intervention)";
		}
		return modetxt;
	}


}
