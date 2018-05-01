using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

public class Menu : MonoBehaviour {

    public string path;

    public GameObject menuButton;

	// Use this for initialization
	void Start () {

        if (Directory.Exists(path))
        {
            Debug.Log("in a valid directory");

            ProcessDirectory(path);
        }
	}

    void ProcessDirectory(string path)
    {
        string[] fileEntries = Directory.GetFiles(path);
        foreach(string file in fileEntries)
        {
            Debug.Log(file);

            //cull to only allow valid models

            //Construct a bunch of menu button prefabs with their interaction function being to resource.load the file/model.
        }
    }	

	// Update is called once per frame
	void Update () {
		
	}
}
