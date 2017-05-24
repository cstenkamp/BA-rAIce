using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class WallColliderScript : MonoBehaviour {

	public CarController Car;
	public PositionTracking Pos;
	public TimingScript Timing;
	public AiInterface AiInt;

	public const int TIMEPUNISH = 5;
	public const int POSITIONPUNISH = 2;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

	void OnCollisionEnter(Collision collision) {
		if (Consts.wallhit_means_reset) {
			Vector3 Position;
			try {
				Position = Pos.anchorVector [Pos.getClosestAnchorBehind (Car.transform.position) - POSITIONPUNISH]; //TODO: das führt bestimmt noch bei einigen positions zu nem indexerror.
			} catch (IndexOutOfRangeException) {
				Position = Pos.anchorVector [0];
			}
			Position.y = Car.startPosition.y;

			Quaternion Angle;
			try {
				Angle = Quaternion.AngleAxis(180+Pos.absoluteAnchorAngles[Pos.getClosestAnchorBehind(Car.transform.position)-POSITIONPUNISH], Vector3.up); 
			} catch (IndexOutOfRangeException) {
				Angle = Quaternion.AngleAxis(180+Pos.absoluteAnchorAngles[0], Vector3.up); 
			}
			AiInt.punish_wallhit ();
			Timing.PunishTime (TIMEPUNISH);
			Car.ResetToPosition (Position, Angle, true, false); //letzte false, weil python bei nem Wallhit eben nicht resetten soll sondern wissen was den hit verursacht hat
		}
	}
}