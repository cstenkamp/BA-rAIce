using UnityEngine;
using System.Collections;

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
		//such that the miniviewcamera can be shown too
        if (Display.displays.Length > 1)
            Display.displays[1].Activate();
		
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
		if (newMode == "menu")
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
