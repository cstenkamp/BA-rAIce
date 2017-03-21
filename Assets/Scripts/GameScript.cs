using UnityEngine;
using System.Collections;

//wenn man das spiel komplett neu starten können wollte, muss man dafür ResetSessionTiming vom Timinscript callen

public class GameScript : MonoBehaviour {

	public string mode = "menu";
	public GameObject OverviewCamera;
	public GameObject CarCamera;
    public GameObject MiniMapCamera;
    public CarController Car;
	public TimingScript Timing;
	public Recorder Rec;
	public UIScript UserInterface;

	// Use this for initialization
	void Start ()
	{
        mode = "menu";
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

		if (newMode == "menu") //geht er hin wenn man escape drückt
		{
			mode = newMode;
			CarCamera.SetActive(false);
            MiniMapCamera.SetActive(false);
            OverviewCamera.SetActive(true);
			Car.ResetCar();
			Timing.ResetTiming();
			Rec.ResetLap();
		}
		else if (newMode == "driving")
		{
			mode = newMode;
			OverviewCamera.SetActive(false);
			CarCamera.SetActive(true);
            MiniMapCamera.SetActive(true);
            Car.ResetCar();
			Timing.ResetTiming();
			Rec.ResetLap();
		}
	}

}
