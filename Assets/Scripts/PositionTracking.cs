using UnityEngine;
using System.Collections;
using System.Linq;

public class PositionTracking : MonoBehaviour {

	public GameObject trackOutline;
	public Vector3[] anchorVector;
	public float progress;
	public GameObject timedCar;
	public Recorder Rec;
	public float[] segmentAngles;
	public float[] segmentAnglesNorm;
	public GameObject Marker;
	public float trackLength;
	public float[] segmentLengths;
	public float[] absoluteAnchorDistances;
	public float[] absoluteAnchorAngles;

	Vector3 carPosition;
	int triggerCount = 0;
	float lastProgress;
	Vector3 lastPerpendicular;
	float[] anchorProgress;

	// for debugging
	public GameObject anchorPrototype;
	public GameObject countText;
	
	// Use this for initialization
	void Start ()
	{
		anchorVector = MeshToVectorArray(trackOutline, 4, 22);
		segmentLengths = GetSegmentLengths(anchorVector);
		absoluteAnchorDistances = GetAbsoluteAnchorDistances(segmentLengths);
		segmentAngles = GetSegmentAngles(anchorVector,false);
		segmentAnglesNorm = GetSegmentAngles(anchorVector,true);
		absoluteAnchorAngles = GetAbsoluteAnchorAngles(segmentAngles);
		anchorProgress = GetAbsoluteAnchorProgress(anchorVector);
		trackLength = segmentLengths.Sum();

		if (Consts.debug_showanchors)
			ShowAnchors(anchorVector, anchorPrototype, countText, "absoluteAngles"); // for debugging
	}
	
	// Update is called once per frame
	void Update()
	{
		if (Consts.debug_showperpendicular)
			ShowPerpendicular(); // for debugging
	}

	void FixedUpdate ()
	{
		progress = GetProgress(anchorVector, segmentLengths, timedCar.transform.position);
		TriggerRec(progress, 0.5f);
	}

	// #####################################################################
	// ########################## FUNCTIONS START ##########################
	// #####################################################################

	// 	##################### GET TRACK OUTLINE VECTOR #####################

	//wir haben in der mitte der strasse nen mesh an ganz vielen vertikal stehenden planes. Wir wollen nen vektorarrays mit punkten auf der mitte der strecke.
	//dafur nehmen wir die jeweils die oberen 2 koordindaten dieser planes/meshes, packen sie in eine liste \
	//und packen solange die vorderen davon ans ende der liste bis der erste dieser meshes genau auf der startziellinie ist.
	//(es gibt ubrigens mehr von diesen planes je scharfer die kurve ist)
	Vector3[] MeshToVectorArray (GameObject meshObject, int spacing = 4, int scroll = 22, bool flip = false)
	{
		Mesh mesh = meshObject.GetComponent<MeshFilter>().mesh;
		Vector3[] meshVertices = mesh.vertices;
		Vector3[] vectorArray = new Vector3[meshVertices.Length/spacing];
		int i = 0;
        while (i < meshVertices.Length)
		{
			if ((i-1) % spacing == 0) { vectorArray[i/spacing] = meshVertices[i]; }
            i++;
        }
		int j = 0;
		while (j < vectorArray.Length)
		{
			vectorArray[j][2] = -vectorArray[j][1];
			vectorArray[j][1] = 0.0F;
			j++;
		}
		if (flip) { anchorVector = FlipVectorArray(vectorArray); }
		anchorVector = ScrollVectorArray(vectorArray, scroll);
		return anchorVector;
	}

	//bei dieser strecke gehen die meshes entgegen der fahrtrichtung, da mussen wir sie noch drehen
	Vector3[] FlipVectorArray (Vector3[] varray)
	{
		Vector3[] flipped = new Vector3[varray.Length];
		int i = 0;
		while (i < varray.Length)
		{
			flipped[i] = varray[varray.Length-1-i];
			i++;
		}
		return flipped;
	}

	//solange die vorderen koordinaten ans ende der liste packen, bis der in der nahe der startziellinie ganz vorne steht
	Vector3[] ScrollVectorArray (Vector3[] varray, int n)
	{
		Vector3[] scrolled = new Vector3[varray.Length];
		int i = 0;
		while (i < varray.Length)
		{
			if (i+n < varray.Length) { scrolled[i] = varray[i+n]; }
			else { scrolled[i] = varray[i+n-varray.Length]; }
			i++;
		}
		return scrolled;
	}

	//wir wollen spater den prozentualen fortschritt der anchors wissen. Dafur brauchen wir zuerst die lange zwischen 2 stuck (die lange der unterliegenden planes)
	float[] GetSegmentLengths(Vector3[] anchorVector) 
	{
		float[] segmentLengths = new float[anchorVector.Length];
		for (int i = 0; i < segmentLengths.Length; i++) 
		{
			int anchorStart = i;
			int anchorStop = i+1;
			if (anchorStop == anchorVector.Length) { anchorStop = 0; }
			segmentLengths[i] = Vector3.Distance(anchorVector[anchorStart], anchorVector[anchorStop]);
		}
		return segmentLengths;
	}

	//und anschliessend rechnen wir die kumulierte distanz ab dem startpunkt aus.
	float[] GetAbsoluteAnchorDistances(float[] segmentLengths)
	{
		float[] absoluteAnchorDistances = new float[segmentLengths.Length];
		absoluteAnchorDistances[0] = 0.0f;
		for (int i=1; i<absoluteAnchorDistances.Length; i++)
		{
			absoluteAnchorDistances[i] = absoluteAnchorDistances[i-1]+segmentLengths[i-1];
		}
		return absoluteAnchorDistances;
	}

	//anhand der koordinaten von den vektoren konnen wir sagen wie viel gedreht der nachste punkt in relation zur gerade der 2 vorherigen ist...
	float[] GetSegmentAngles(Vector3[] anchorVector, bool normalized=false)
	{
		float[] segmentAngles = new float[anchorVector.Length];
		for (int i=0; i < anchorVector.Length-1; i++)
		{
				int iPlus1 = i+1;
				int iPlus2 = i+2;
				if (iPlus1>=anchorVector.Length) { iPlus1 -= anchorVector.Length; }
				if (iPlus2>=anchorVector.Length) { iPlus2 -= anchorVector.Length; }

				Vector2 a = new Vector2(anchorVector[iPlus1].x-anchorVector[i].x,anchorVector[iPlus1].z-anchorVector[i].z);
				Vector2 b = new Vector2(anchorVector[iPlus2].x-anchorVector[iPlus1].x,anchorVector[iPlus2].z-anchorVector[iPlus1].z);
				double angle = (Mathf.Atan2(a.y, a.x) - Mathf.Atan2(b.y, b.x))*Mathf.Rad2Deg; //is this correct?
				if (angle > 180.0f) { angle -= 360.0f; }
				if (angle < -180.0f) { angle += 360.0f; }
				segmentAngles[i] = (float)angle;
			}
		if (normalized)
		{
			float maxAngle = Mathf.Max(Mathf.Max(segmentAngles), -Mathf.Min(segmentAngles));
			for (int j=0; j<segmentAngles.Length;j++) { segmentAngles[j] /= maxAngle; }
		
		}
		return segmentAngles;
	}

	//...und summieren wir das alles auf haben wir den absoluten winkel von jedem einzelnem anchor. 
	float[] GetAbsoluteAnchorAngles(float[] segmentAngles)
	{
		float[] absoluteAnchorAngles = new float[segmentAngles.Length];
		absoluteAnchorAngles[0] = 0.0f;
		for (int i=1; i<absoluteAnchorAngles.Length; i++)
		{
			absoluteAnchorAngles[i] = absoluteAnchorAngles[i-1]+segmentAngles[i-1];
		}
		return absoluteAnchorAngles;
	}

	//progress eines anchors in prozent von der gesamtlange der strecke 
	float[] GetAbsoluteAnchorProgress(Vector3[] anchorVector)
	{
		float[] segmentLengths = GetSegmentLengths(anchorVector);
		float trackLength = segmentLengths.Sum();

		float[] anchorProgress = new float[anchorVector.Length];
		anchorProgress[0] = 0.0f;
		for (int i=1; i<segmentLengths.Length; i++)
		{
			anchorProgress[i] = anchorProgress[i-1]+(segmentLengths[i]/trackLength);
		}
		return anchorProgress;
	}

	//zeigt die vektoren in unity
	void ShowAnchors(Vector3[] anchorVector, GameObject anchorPrototype, GameObject countText, string hoverText="count") 
	{
		for (int i = 0; i < anchorVector.Length; i++) 
		{
			Vector3 position_sphere = anchorVector[i];
			Vector3 position_count = anchorVector[i];
			position_sphere.y += 1.0f;
			position_count.y += 1.6f;
			Quaternion rotation = Quaternion.identity;
			rotation.eulerAngles = new Vector3(0,180,0);
			Instantiate(anchorPrototype,position_sphere,rotation);
			GameObject current_count = (GameObject) Instantiate(countText,position_count,rotation);
			if (hoverText == "count") { (current_count.GetComponent(typeof(TextMesh)) as TextMesh).text = i.ToString(); }
			if (hoverText == "angles") { (current_count.GetComponent(typeof(TextMesh)) as TextMesh).text = segmentAngles[i].ToString("F2"); }
			if (hoverText == "absoluteAngles") { (current_count.GetComponent(typeof(TextMesh)) as TextMesh).text = absoluteAnchorAngles[i].ToString("F2"); }
		}
	}

	// so. wir haben jetzt folgende vektor-arrays:
	//  #  anchor-vektor   segment-length   abs.segm.length   segm.winkel   abs.segm.winkel   progress
	//  1   pos(anchor1)    ||A1-A0||        ...                                               0.564%
	// ...

	// #####################################################################
	// ###################### FUNCTIONS FOR PROGRESS #######################
	// #####################################################################

	//gets the progress of the car. How? by getting the euclidianly nearest anchor, and also the relative position from the car to this vector (if its +0.4 in front of it for example)
	//for that, after finding the nearest anchor it takes this one (B), the one before (A) and the one after (C) it, and finds a point (D) on AB as well as BC, which has a vector to the coordinates of the car (P)
	//which is perpendicular to AB bzw. BC. (the length of this line is btw the distance from car to center). Then it looks which of those two lines is shorter, and it thus knows if its rather before point B 
	//or after it. Then it interpolates between, say, A and B and sees where D lies percentually in closeness to B. that percentage with either negativ or positive sign is then the more precise position.
	//this absolute value ("the car is at anchor 15,45") is then converted into a percentage, by taking the lengths of the first 15 + 0.45 the distance between 15 and 16, divided by the gesamtlength.
	//the "length of this line" is precondition for two of the shown vektor on the screen!
	float GetProgress(Vector3[] anchorVector, float[] segmentLengths, Vector3 carPosition) 
	{
		// get car closest anchor
		int closestAnchor = GetClosestAnchor(carPosition);

		// calculate progress on a scale from 0 to anchorVector.Length, i.e. track progress in "number of anchors" 
		float progressInAnchors = closestAnchor*1.0f;
		progressInAnchors += ProgressFromClosestAnchor(carPosition, anchorVector, closestAnchor);
		if (progressInAnchors < 0.0f) { progressInAnchors += segmentLengths.Length; }

		// convert track progress from anchor-based to meters to percent
		float progress = ProgressConvert(progressInAnchors,segmentLengths); // progress from 0 to 1
		return progress;
	}

	//finds the vector with the smallest euclidian distance to the car.
	int GetClosestAnchor(Vector3 position)
	{
		float[] distanceVector = new float[anchorVector.Length];
		for (int i = 0; i < anchorVector.Length; i++) 
		{
			distanceVector[i] = Vector3.Distance(anchorVector[i],position);
		}
		int closestAnchor = distanceVector.ToList().IndexOf(distanceVector.Min());
		return closestAnchor;
	}

	//converts the absolute position in anchors to a percentage of the whole track
	float ProgressConvert(float progressRelative, float[] segmentLengths)
	{
		// get lower bound based on passed anchors
		float trackLength = segmentLengths.Sum();
		int anchorPassed = (int)Mathf.Floor(progressRelative);
		float[] segmentLengthsPassed = segmentLengths.Take(anchorPassed).ToArray();
		float progressDistance = segmentLengthsPassed.Sum(); // lower bound for progress

		// add the distance of relative progress within the current segment 
		float currentSegmentProgress = progressRelative-Mathf.Floor(progressRelative);
		progressDistance += currentSegmentProgress*segmentLengths[anchorPassed];
		
		// return progress relative to track length
		return progressDistance/trackLength;
	}

	//finds the value after the comma for the carposition relative to the vector (see above). This is all the LinA-stuff where we find a perpendicular etc.
	float ProgressFromClosestAnchor(Vector3 P, Vector3[] anchorVector, int closestAnchor) // P = carPosition
	{
		// define which anchors are of intrest
		int anchorA = closestAnchor-1;
		int anchorB = closestAnchor;
		int anchorC = closestAnchor+1;
		if (anchorB == 0) { anchorA = anchorVector.Length-1; }
		if (anchorB == anchorVector.Length-1 ) { anchorC = 0; }
		Vector3 A = anchorVector[anchorA];
		Vector3 B = anchorVector[anchorB];
		Vector3 C = anchorVector[anchorC];

		// get perpendicular point of carPosition on |AB|
		Vector3 vectorAP = P-A;
		Vector3 vectorAB = B-A;
		Vector3 vectorBP = P-B;
		Vector3 vectorBC = C-B;
		Vector3 projectionAB = Vector3.Project(vectorAP, vectorAB); // projectionAB is a vector with origin in A, pointing to the perpendicular of P on AB
		Vector3 projectionBC = Vector3.Project(vectorBP, vectorBC); // projectionBC is a vector with origin in B, pointing to the perpendicular of P on BC
		Vector3 perpendicularPAB = vectorAP-projectionAB;
		Vector3 perpendicularPBC = vectorBP-projectionBC;

		float progressAB = projectionAB.x / vectorAB.x;
		float progressBC = projectionBC.x / vectorBC.x;
		if (progressAB > 0.0f && progressAB < 1.0f) // if the perpendicular falls between A&B...
		{
			if (perpendicularPAB.magnitude < perpendicularPBC.magnitude || progressBC <= 0.0f || progressBC > 1.0f) // ...and if either perpPAB is shorter than perpPBC, or perpPBC doesn't fall between B&C
			{
				return -(1.0f-progressAB);
			}
		}
		if (progressBC > 0.0f && progressBC < 1.0f) // if the perpendicular falls between B&C (all other cases have already been caught in the lines above)
		{
			return progressBC;
		}

		// if the perpendicular point is neither on |AB| nor on |BC|, point B must be the closest - no correction needed.
		return 0.0f;
	}

	//this is unneeded?
//	Vector3 GetLocalPerpendicular(Vector3 A, Vector3 B, Vector3 P)
//	{
//		Vector3 vectorAP = P-A;
//		Vector3 vectorAB = B-A;
//		Vector3 projectionAB = Vector3.Project(vectorAP, vectorAB); // projectionAB is a vector with origin in A, pointing to the perpendicular of P on AB
//		return vectorAP-projectionAB; // perpendicular from P on AB
//	}

	static int ClosestSmallerThan(float[] collection, float target)
	{
		float minDifference = float.MaxValue;
		int argClosest = int.MaxValue;
		for (int i=0; i<collection.Length; i++)
		{
			if (target > collection[i])
			{
				float difference = Mathf.Abs(collection[i] - target);
				if (minDifference > difference)
				{
					argClosest = i;
					minDifference = difference;
				}
			} 
	    }
		return argClosest;
	}

	// #####################################################################
	// ######################### TRIGGER HANDLING ##########################
	// #####################################################################

	void TriggerRec(float progress, float inc)
	{
		if (progress > triggerCount*inc/100.0f)
		{
			triggerCount += 1;
			Rec.UpdateList();
		}
		if ((lastProgress - progress) > 0.5f)
		{
			triggerCount = 0;
		}
		lastProgress = progress;
	}

	// #####################################################################
	// ########################## OTHER FUNCTIONS ##########################
	// #####################################################################

	public float GetCenterDist()
	{
		// get coordinates
		Vector3 carPosition = timedCar.transform.position;
		Vector3 perpendicularPoint = GetPerpendicular(carPosition);

		// get sign for centerDist
		int anchor = ClosestSmallerThan(anchorProgress, progress);
		int anchorPlus1 = anchor+1;
		if (anchorPlus1 >= anchorVector.Length) { anchorPlus1 -= anchorVector.Length; }
		Vector3 AB = anchorVector[anchorPlus1]-anchorVector[anchor];
		Vector3 PC = carPosition-perpendicularPoint;
		Vector3 cross = Vector3.Cross(AB,PC);

		// output
		float centerDist = PC.magnitude;
		if (cross.y<0.0f) { centerDist = -centerDist; }
		return centerDist;
	}

	Vector3 GetPerpendicular(Vector3 carPosition)
	{
		// get closest anchor
		int closestAnchor = GetClosestAnchor(carPosition);
		int caPlus1 = closestAnchor+1;
		int caMinus1 = closestAnchor-1;
		if (caPlus1 >= anchorVector.Length) { caPlus1 -= anchorVector.Length; }
		if (caMinus1 < 0) { caMinus1 += anchorVector.Length; }

		float offset = ProgressFromClosestAnchor(carPosition, anchorVector, closestAnchor);
		Vector3 fromCAtoP = new Vector3();
		if (offset >= 0.0f) { fromCAtoP = (anchorVector[caPlus1]-anchorVector[closestAnchor])*offset; }
		else { fromCAtoP = (anchorVector[caMinus1]-anchorVector[closestAnchor])*Mathf.Abs(offset); }
		Vector3 perpendicular = anchorVector[closestAnchor]+fromCAtoP;
		perpendicular.y = carPosition.y;
		return perpendicular;
	}

	public void ShowPerpendicular()
	{
		Marker.transform.position =  GetPerpendicular(timedCar.transform.position);
	}

}
