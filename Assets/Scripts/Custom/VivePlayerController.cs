using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VivePlayerController : MonoBehaviour {

    public LargeDataTransfer largeSender;

	// Use this for initialization
	void Start () {
		
	}

    void Update()
    {

        var leftI = SteamVR_Controller.GetDeviceIndex(SteamVR_Controller.DeviceRelation.Leftmost);
        var rightI = SteamVR_Controller.GetDeviceIndex(SteamVR_Controller.DeviceRelation.Rightmost);
        if (leftI == rightI)
        {
            // Single Controller
            rightI = -1;
        }

        if (leftI != -1)
        {
            ViveControl(leftI);
        }

        if (rightI != -1)
        {
            ViveControl(rightI);
        }

    }

    void ViveControl(int controllerId)
    {
        var controller = SteamVR_Controller.Input(controllerId);
        if (controller.GetPress(SteamVR_Controller.ButtonMask.Trigger))
        {
            var v = controller.velocity;
            v.Scale(transform.localScale);
            transform.position += v;
            transform.Rotate(controller.angularVelocity, Space.World);
        }
        //if (controller.GetPress(SteamVR_Controller.ButtonMask.Touchpad))
        if (controller.GetHairTriggerDown())
        {
            Debug.Log("Pressing down controller");

            GameObject newCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            newCube.transform.position = transform.position;
            largeSender.SendModel(newCube);
        }
    }
}
