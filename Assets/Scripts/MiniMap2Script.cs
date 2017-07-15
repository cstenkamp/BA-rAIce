using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;


public class MiniMap2Script : MonoBehaviour {

	public GameScript Game;

	public string GetVisionDisplay() {
		if (!(Game.mode.Contains ("drive_AI")) && !(Game.mode.Contains ("train_AI"))) {
			return "";
		}
		
		Camera cam = gameObject.GetComponent<Camera> ();
		if (cam.enabled) {

			cam.aspect = (Consts.visiondisp2_x + 0.0f) / Consts.visiondisp2_y; //0.5f;

			cam.rect = new Rect (0, 0, 1, 1);
			RenderTexture myRT = new RenderTexture (Consts.visiondisp2_x, Consts.visiondisp2_y, 24);  //,RenderTextureFormat.ARGB32
			myRT.Create ();
			cam.targetTexture = myRT;
			RenderTexture.active = myRT;
			try {
				cam.Render (); 
				Texture2D myImg = new Texture2D (Consts.visiondisp2_x, Consts.visiondisp2_y, TextureFormat.RGB24, false); //false = no mipmaps
				myImg.ReadPixels (new Rect (0, 0, Consts.visiondisp2_x, Consts.visiondisp2_y), 0, 0); //"the center section"
				myImg.Apply (false);

				//debug
				//		byte[] bytes;
				//		bytes = myImg.EncodeToPNG();
				//		System.IO.File.WriteAllBytes("./picpicpic.png", bytes );


				float displaywidth = 0.1f;
				cam.targetTexture = null;
				cam.rect = new Rect (0.77f, 0.6f, displaywidth, displaywidth / (0.5f * cam.aspect));

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
