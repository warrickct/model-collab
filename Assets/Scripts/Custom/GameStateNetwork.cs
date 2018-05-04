using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using UnityEngine;

public class GameStateNetwork : MonoBehaviour {

    public GameObject playerRootGameObject;


    //Network Transport Variables
    public int networkTransportHostPort;

    public string targetConnectionIp;
    public int targetConnectPort;

    int myStateUpdateChannelId;
    int myReliableChannelId;

    int hostId;
    int connectionId;
    int channelId;

    // Use this for initialization
    void Start () {
        StartNetworkTransport();
	}

	// Update is called once per frame
	void Update () {
        
	}

    void StartNetworkTransport()
    {
        GlobalConfig gConfig = new GlobalConfig
        {
            MaxPacketSize = 500,
        };

        NetworkTransport.Init(gConfig);

        ConnectionConfig connectionConfig = new ConnectionConfig();
        myStateUpdateChannelId = connectionConfig.AddChannel(QosType.StateUpdate);
        myReliableChannelId = connectionConfig.AddChannel(QosType.ReliableSequenced);

        HostTopology topology = new HostTopology(connectionConfig, 10);

        hostId = NetworkTransport.AddHost(topology, networkTransportHostPort);
    }

    void ConnectNetworkTransport()
    {
        byte error;
        connectionId = NetworkTransport.Connect(hostId, targetConnectionIp, targetConnectPort, 0, out error);
    }
}
