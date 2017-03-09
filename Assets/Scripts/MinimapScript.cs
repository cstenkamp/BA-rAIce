using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.UI;

//http://answers.unity3d.com/questions/27968/getpixels-of-rendertexture.html

public class MinimapScript : MonoBehaviour {

	public Image pixel;
	public Image pixel_clone;
	public GameObject pixelParent;


	void Start() {
		if (!Consts.debug_show_visiondisp) {
			pixel.enabled = false;
			Destroy (pixelParent.gameObject);
		}
	}


	public float[,] GetVisionDisplay() {
		Camera cam = gameObject.GetComponent<Camera> ();
		cam.aspect = 1.0f;

		cam.rect = new Rect (0, 0, 1, 1);
		RenderTexture myRT = new RenderTexture(Consts.visiondisplay_x, Consts.visiondisplay_y, 24);  //,RenderTextureFormat.ARGB32
		myRT.Create();
		cam.targetTexture = myRT;
		RenderTexture.active = myRT;
		cam.Render ();
		Texture2D myImg = new Texture2D (Consts.visiondisplay_x, Consts.visiondisplay_y, TextureFormat.RGB24, false); //false = no mipmaps
		myImg.ReadPixels (new Rect (0, 0, Consts.visiondisplay_x, Consts.visiondisplay_y), 0, 0); //"the center section"
		myImg.Apply(false);

		//debug
//		byte[] bytes;
//		bytes = myImg.EncodeToPNG();
//		System.IO.File.WriteAllBytes("./picpicpic.png", bytes );


		cam.targetTexture = null;
		cam.rect = new Rect(0.77f, 0.63f, 0.1f, 0.25f);

		float[,] visiondisplay = new float[myImg.width, myImg.height];

		for (int i = 0; i < myImg.width; i++) {
			for (int j = 0; j < myImg.height; j++) {
				if ((float)myImg.GetPixel (i, j).grayscale > 0.8)
					visiondisplay [i, j] = 1;
				else if ((float)myImg.GetPixel (i, j).grayscale > 0.4)
					visiondisplay [i, j] = 0.5f;
				else
					visiondisplay [i, j] = 0;
			}
		}
		return visiondisplay;
	}


	public void CreatePixelImage(Image pixel, GameObject pixelParent, int xlen, int ylen)
	{
		for (int i = 0; i<xlen*ylen; i++)
		{
			int x = i%xlen;
			int y = i/xlen;
			pixel_clone = (Image)Instantiate(pixel, new Vector3(0,0,0), Quaternion.identity, pixelParent.transform);
			pixel_clone.rectTransform.localScale = new Vector3(1f,1f,1f);
			pixel_clone.rectTransform.localPosition = new Vector3(-28.5f+x*2.0f,-40.5f+y*2.0f,0);
			pixel_clone.name = "visPixel_"+x.ToString()+"_"+y.ToString();
		}
	}

	public void ShowVisionDisplay(float[,] a)
	{
		for (int x = 0; x<a.GetLength(0); x++)
		{
			for (int y = 0; y<a.GetLength(1); y++)
			{
				float c = a[x,y];
				Image currentPixel = GameObject.Find("visPixel_"+x.ToString()+"_"+y.ToString()).GetComponent<Image>();
				currentPixel.color = new Color (c,c,c);
			}
		}
	}





}

