using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class TestScript : MonoBehaviour {

	// Use this for initialization
	void Start () {

		QuakeConsole.RegisterCommand ("floorCol", ChangeFloorColor, "[r g b] Change the floor color as integers from 0 - 255");
	}
	
	string ChangeFloorColor(params string[] args) {

		if (args.Length == 3) {
			int r, g, b;

			if (int.TryParse(args[0], out r) && int.TryParse(args[1], out g) && int.TryParse(args[2], out b)) {

				var newColor = new Color (r / 255, g / 255, b / 255);
				var meshRenderer = gameObject.GetComponent<MeshRenderer> ();

				if (meshRenderer != null) {

					meshRenderer.material.color = newColor;

					return "Floor colour changed";
				}
			}
		}

		return "Arguments are not correct";
	}
}
