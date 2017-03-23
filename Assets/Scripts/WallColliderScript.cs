using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WallColliderScript : MonoBehaviour {

	public CarController Car;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

	void OnCollisionEnter(Collision collision) {
		if (Consts.wallhit_means_reset) {
			//TODO: reset to last checkpoint with time-punishment
			Car.ResetCar ();
		}
	}
}