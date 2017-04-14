using UnityEngine;
using System.Collections;
using System.Linq;

//wenn man das spiel komplett neu starten können wollte, muss man dafür ResetSessionTiming vom Timinscript callen

public class GameScript : MonoBehaviour {

	public string[] mode = new string[1]{"menu"};
	public GameObject OverviewCamera;
	public GameObject CarCamera;
    public GameObject MiniMapCamera;
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

		AsynchronousClient.ResetServerConnectTrials ();
		AiInterface.KillOtherThreads ();

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

		AiInt.sent_to_python = false;
		AiInt.get_from_python = false;
		AsynchronousClient.serverdown = true;
		Recorder.sv_save_round = false;
		CarCamera.SetActive (false);
		MiniMapCamera.SetActive (false);
		OverviewCamera.SetActive (false);
		Car.ResetCar ();
		Timing.ResetTiming ();
		UserInterface.UpdateGameModeDisp ();
		//Rec.ResetLap() ist überflüssig


		if (mode.Contains("menu")) { //geht er hin wenn man escape drückt
			OverviewCamera.SetActive (true);
		} 

		if (mode.Contains("driving")) {
			CarCamera.SetActive (true);
			MiniMapCamera.SetActive (true);
		}

		if (mode.Contains("train_AI")) {
			Recorder.sv_save_round = true;
		} 

		if (mode.Contains("drive_AI")) {
			AiInt.sent_to_python = true;
			AiInt.get_from_python = true;
			AsynchronousClient.serverdown = false; 
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
