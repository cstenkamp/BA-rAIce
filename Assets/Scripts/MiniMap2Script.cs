﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;


public class MiniMap2Script : MonoBehaviour {

	public GameScript Game;
	Rect standard; 

	void Start() {
		Camera cam = gameObject.GetComponent<Camera> ();
		cam.aspect = (Consts.visiondisp2_x + 0.0f) / Consts.visiondisp2_y; //0.5f;
		standard = new Rect(0.77f, 0.63f, 0.1f, 0.25f);
		cam.rect = standard;
	}


	public string GetVisionDisplay() {
		if (!Game.AiInt.AIMode && !Game.Rec.SV_SaveMode) {
			return "";
		}
		
		Camera cam = gameObject.GetComponent<Camera> ();
		if (cam.enabled) {
				

			cam.rect = new Rect (0, 0, 1, 1);
			RenderTexture myRT = new RenderTexture (Consts.visiondisp2_x, Consts.visiondisp2_y, 24);  //,RenderTextureFormat.ARGB32
			//myRT.Create ();
			cam.targetTexture = myRT;
			RenderTexture.active = myRT;
			try {
				cam.Render (); 
				Texture2D myImg = new Texture2D (Consts.visiondisp2_x, Consts.visiondisp2_y, TextureFormat.RGB24, false); //false = no mipmaps
				RenderTexture.active = myRT;
				myImg.ReadPixels (new Rect (0, 0, Consts.visiondisp2_x, Consts.visiondisp2_y), 0, 0); //"the center section"
				myImg.Apply (false);

				//debug
				//		byte[] bytes;
				//		bytes = myImg.EncodeToPNG();
				//		System.IO.File.WriteAllBytes("./picpicpic.png", bytes );

				cam.targetTexture = null;
				RenderTexture.active = null;
				Destroy(myRT);
				cam.rect = standard;

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
