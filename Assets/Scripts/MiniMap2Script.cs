using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;


public class MiniMap2Script : MonoBehaviour {

	public GameScript Game;
	Rect showRect; Rect sendRect; Rect readRect;
	RenderTexture myRT;
	Texture2D myImg;

	void Start() {
		Camera cam = gameObject.GetComponent<Camera> ();
		cam.aspect = (Consts.visiondisp2_x + 0.0f) / Consts.visiondisp2_y; //0.5f;
		showRect = new Rect(0.77f, 0.63f, 0.1f, 0.25f);
		cam.rect = showRect;
		PrepareVision ();
	}

	public void PrepareVision() {
		if (Game.AiInt.AIMode || Game.Rec.SV_SaveMode) {
			sendRect = new Rect (0, 0, 1, 1);
			readRect = new Rect (0, 0, Consts.visiondisp2_x, Consts.visiondisp2_y);
			myRT = new RenderTexture (Consts.visiondisp2_x, Consts.visiondisp2_y, 24);
		}
	}


	public string GetVisionDisplay() {
		if (!Game.AiInt.AIMode && !Game.Rec.SV_SaveMode) {
			return "";
		}
		
		Camera cam = gameObject.GetComponent<Camera> ();
		if (cam.enabled) {
			
			cam.rect = sendRect;
			myRT.Create ();
			cam.targetTexture = myRT;
			RenderTexture.active = myRT;
			try {
				cam.Render (); 
				myImg = new Texture2D (Consts.visiondisp2_x, Consts.visiondisp2_y, TextureFormat.RGB24, false); //false = no mipmaps
				RenderTexture.active = myRT;
				myImg.ReadPixels (readRect, 0, 0); //"the center section"
				myImg.Apply (false);

				//debug
				//		byte[] bytes;
				//		bytes = myImg.EncodeToPNG();
				//		System.IO.File.WriteAllBytes("./picpicpic.png", bytes );

				cam.targetTexture = null;
				RenderTexture.active = null;
				cam.rect = showRect;

				// return imgToArray(myImg); dann müsste man danach noch TwoDImageToStr aufrufen, aber das ist sinnlos
				return MinimapScript.imgToStr (myImg);

			} catch (Exception e) {
				UnityEngine.Debug.Log ("Flare renderer to update not found - in UnityEngine.Camera:Render()");
				return "";
			} 
		} else {
			return "";
		}
	}
}
