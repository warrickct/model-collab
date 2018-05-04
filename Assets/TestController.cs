using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using UnityEngine;

public class TestController : MonoBehaviour {

    public LargeDataTransfer largeDataTransfer;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            Debug.Log("Player pressing 1.");
            Debug.Log("Spawning superhuman.");
            SpawnResourceAsGameObject("super-human/source/temp_l.zip/temp_l");
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            Debug.Log("Player pressing 2.");
            Debug.Log("Spawning sarcophagus");
            SpawnResourceAsGameObject("sarcophagus-with-garland/source/sarcophagus/sarcophagus");

        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            Debug.Log("Player pressing 3.");
            Debug.Log("Spawning cicada");
            SpawnResourceAsGameObject("cicada-pomponia/cicada-rigsters");
        }
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            Debug.Log("Player pressing 4.");
        }
    }

    void SpawnResourceAsGameObject(string path)
    {
        GameObject go = Instantiate(Resources.Load(path) as GameObject);
        go.tag = "LocalModel";

        // If root obj is just container for children containing meshes then find the sub ones.
        if(go.GetComponent<MeshFilter>() != null)
        {
            largeDataTransfer.SendModel(go);
        }
        else
        {
            // todo: Might be a problem and make multiple root models for each child. Need to recombine the model upon receiving.
            for (int i = 0; i < go.transform.childCount; i++)
            {
                GameObject goChild = go.transform.GetChild(i).gameObject;
                if (goChild.GetComponent<MeshFilter>() != null)
                {
                    largeDataTransfer.SendModel(goChild);
                }
            }
        }
    }
}
