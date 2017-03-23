﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WallColliderScript : MonoBehaviour {

	public CarController Car;
	public PositionTracking Pos;
	public TimingScript Timing;

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
			Vector3 Position = Pos.anchorVector [Pos.getClosestAnchorBehind (Car.transform.position) - POSITIONPUNISH]; //TODO: das führt bestimmt noch bei einigen positions zu nem indexerror.
			Position.y = Car.startPosition.y;
			Quaternion Angle = Quaternion.AngleAxis(180+Pos.absoluteAnchorAngles[Pos.getClosestAnchorBehind(Car.transform.position)-POSITIONPUNISH], Vector3.up); 
			Timing.PunishTime (TIMEPUNISH);
			Car.ResetToPosition (Position, Angle, true);
		}
	}
}