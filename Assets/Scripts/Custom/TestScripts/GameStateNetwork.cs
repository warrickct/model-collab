using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using UnityEngine;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

public class GameStateNetwork : MonoBehaviour {

    public GameObject playerRootGameObject;

    //Network Transport Variables
    public int networkTransportHostPort;

    public string targetConnectionIp;
    public int targetConnectPort;

    int myStateUpdateChannelId;
    int myReliableChannelId;

    int hostId;
    List<int> connectionIds;
    int channelId;

    // Use this for initialization
    void Start () {
        StartNetworkTransport();
        ConnectNetworkTransport();
	}

	// Update is called once per frame
	void Update () {
        
        //if connected, send player transform
        if (connectionIds != null)
        {
            SendPlayerState();
        }

    }

    void StartNetworkTransport()
    {
        GlobalConfig gConfig = new GlobalConfig
        {
            //MaxPacketSize = 500,
        };

        NetworkTransport.Init(gConfig);

        ConnectionConfig connectionConfig = new ConnectionConfig();
        myStateUpdateChannelId = connectionConfig.AddChannel(QosType.StateUpdate);
        myReliableChannelId = connectionConfig.AddChannel(QosType.ReliableSequenced);

        HostTopology topology = new HostTopology(connectionConfig, 10);

        hostId = NetworkTransport.AddHost(topology, 12345);
    }

    void ConnectNetworkTransport()
    {
        byte error;
        connectionIds.Add(NetworkTransport.Connect(hostId, targetConnectionIp, targetConnectPort, 0, out error));
        if ((NetworkError)error == NetworkError.Ok)
        {
            Debug.Log("Network transport connection success");
        }
    }

    void SendPlayerState()
    {
        //foreach connection present. Send the player state to all.
        foreach (int connection in connectionIds)
        {
            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream ms = new MemoryStream();
            PlayerWireData pwd = new PlayerWireData(playerRootGameObject);

            bf.Serialize(ms, pwd);
            byte[] buffer = ms.ToArray();

            byte error;
            NetworkTransport.Send(hostId, connection, myStateUpdateChannelId, buffer, buffer.Length, out error);
        }
    }
}

[Serializable]
public class PlayerWireData
{
    [SerializeField]
    public float posX, posY, posZ, rotX, rotY, rotZ, scaleX, scaleY, scaleZ;

    public PlayerWireData(GameObject player)
    {
        var position = player.transform.position;
        var rotation = player.transform.rotation;
        var scale = player.transform.localScale;

        this.posX = position.x;
        this.posX = position.x;
        this.posX = position.x;

        this.rotX = rotation.x;
        this.rotY = rotation.y;
        this.rotZ = rotation.z;

        this.scaleX = scale.x;
        this.scaleY = scale.y;
        this.scaleZ = scale.z;
    }
}
