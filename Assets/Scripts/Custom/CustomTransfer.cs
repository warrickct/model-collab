using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System;
using System.Linq;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Net.Sockets;
using System.Net;

public class CustomTransfer : MonoBehaviour {

    // Receiving address
    public string receiveIp;
    public int receivePort;

    // Sending address
    public string sendIp;
    public int sendPort;

    //SENDING
    // Switch to start sending data
    byte[] sendData;
    // Switch to prevent sending called while already sending.
    bool isSending = false;

    //RECEIVING
    //For detecting if there's received meshdatas
    MeshData[] subMeshDatasArray;
    // To prevent generating models that are being generated.
    bool isGeneratingModels = false;

    // todo: Test model for sending via editor
    public GameObject testModel;

    private void Start()
    {
        //Start receiving on script start.
        Thread listenerThread = new Thread(Listener);
        listenerThread.IsBackground = true;
        listenerThread.Start();

        // todo: Testing the script by hardcoding a send.
        SendModel(testModel);
    }

    //sender
    void SendModel(GameObject model)
    {
        // Extract verts here so only threadsafe unity api (vectors) is used in thread.
        Mesh mesh = model.GetComponent<MeshFilter>().mesh;
        Vector4[] tangents = mesh.tangents;
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Vector2[] uv = mesh.uv;
        int[] triangles = mesh.triangles;

        Debug.Log("number of verts for local model: " + mesh.vertices.Length / 1000 + "k");

        Thread newThread = new Thread(() => SerializeModel(tangents, vertices, normals, uv, triangles));
        newThread.IsBackground = true;
        newThread.Start();
    }

    void SerializeModel(Vector4[] tangents, Vector3[] verts, Vector3[] normals, Vector2[] uvs, int[] triangles)
    {
        List<float> tangentFloats = new List<float>();
        foreach (Vector4 tangent in tangents)
        {
            tangentFloats.Add(tangent.w);
            tangentFloats.Add(tangent.x);
            tangentFloats.Add(tangent.y);
            tangentFloats.Add(tangent.z);
        }

        List<float> vertFloats = new List<float>();
        foreach (Vector3 vert in verts)
        {
            vertFloats.Add(vert.x);
            vertFloats.Add(vert.y);
            vertFloats.Add(vert.z);
        }

        List<float> normalFloats = new List<float>();
        foreach (Vector3 normal in normals)
        {
            normalFloats.Add(normal.x);
            normalFloats.Add(normal.y);
            normalFloats.Add(normal.z);
        }

        List<float> uvFloats = new List<float>();
        foreach (Vector2 uv in uvs)
        {
            uvFloats.Add(uv.x);
            uvFloats.Add(uv.y);
        }

        float[] tangentFloatArray = tangentFloats.ToArray();
        float[] vertFloatArray = vertFloats.ToArray();
        float[] normalFloatArray = normalFloats.ToArray();
        float[] uvFloatArray = uvFloats.ToArray();

        Debug.Log("Completed array conversion: " + vertFloatArray.Length + " " + uvFloatArray.Length);

        WireData2 wd2 = new WireData2(tangentFloatArray, vertFloatArray, normalFloatArray, uvFloatArray, triangles);

        MemoryStream ms = new MemoryStream();
        BinaryFormatter bf = new BinaryFormatter();
        bf.Serialize(ms, wd2);
        byte[] data = ms.ToArray();

        sendData = data;
        Debug.Log("Serialization finished. Added serialized to sendData queue");
    }

    //Sender 
    void SenderThread(byte[] data)
    {

        IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse(sendIp), sendPort);

        Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Debug.Log("client set up");

        client.Connect(ipEndPoint);
        Debug.Log("connected");

        int dataLength = data.Length;
        byte[] sizeData = BitConverter.GetBytes(dataLength);

        //client.Send(sizeData);
        client.Send(data);
        client.Close();

        //clear send buffer, notify that sending thread has stopped.
        sendData = null;
        isSending = false;
    }

    //receiver
    //Receive the stream
    void Listener()
    {
        IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse(receiveIp), receivePort);
        TcpListener tcpListener = new TcpListener(ipEndPoint);

        tcpListener.Start();

        byte[] bytes = new byte[1024];
        String data = null;

        //Continuously listens for tcp.
        while (true)
        {
            TcpClient client = tcpListener.AcceptTcpClient();

            data = null;
            List<byte> fullData = new List<byte>();

            NetworkStream netStream = client.GetStream();

            int i;

            while ((i = netStream.Read(bytes, 0, bytes.Length)) != 0)
            {
                fullData.AddRange(bytes);
                Debug.Log("receiving");
            }
            byte[] fullDataBytes = fullData.ToArray();
            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream ms = new MemoryStream(fullDataBytes);
            WireData wd = bf.Deserialize(ms) as WireData;

            Debug.Log("finished receiving and converted to wiredata");

            //todo: Call reconstruct mesh arrays thread with wiredata as param.
            Thread reconstructThread = new Thread(() => ReconstructMeshArrays(wd));
            reconstructThread.IsBackground = true;
            reconstructThread.Start();
        }
    }

    //receiveHandler
    void ReconstructMeshArrays(WireData wd)
    {
        Debug.Log("Reconstruction thread started");
        Debug.Log("received tangets length" + wd.tangents.Length);

        //tangents
        float[] tangents = wd.tangents;
        List<Vector4> vecTangents = new List<Vector4>();
        for (int i = 0; i < tangents.Length; i += 4)
        {
            Vector4 tangent = new Vector4(tangents[i + 1], tangents[i + 2], tangents[i + 3], tangents[i]);
            vecTangents.Add(tangent);
        }

        //normals
        float[] normals = wd.normals;
        List<Vector3> vecNormals = new List<Vector3>();
        for (int i = 0; i < normals.Length; i += 3)
        {
            Vector3 normal = new Vector3(normals[i], normals[i + 1], normals[i + 2]);
            vecNormals.Add(normal);
        }

        //vertices
        float[] verts = wd.verts;
        List<Vector3> vectorVertices = new List<Vector3>();
        for (int i = 0; i < verts.Length; i += 3)
        {
            Vector3 vertex = new Vector3(verts[i], verts[i + 1], verts[i + 2]);
            vectorVertices.Add(vertex);
        }

        //uv
        float[] uvs = wd.uvs;
        List<Vector2> vectorUvs = new List<Vector2>();
        for (int i = 0; i < uvs.Length; i += 2)
        {
            Vector2 uv = new Vector2(uvs[i], uvs[i + 1]);
            vectorUvs.Add(uv);
        }


        //dont need to do anything for triangles.

        Debug.Log("Reconstruction check 1");


        Vector4[] vecTangentArray = vecTangents.ToArray();
        Vector3[] vecVertArray = vectorVertices.ToArray();
        Vector3[] vecNormalArray = vecNormals.ToArray();
        Vector2[] vecUvsArray = vectorUvs.ToArray();
        int[] intTrianglesArray = wd.triangles;

        //cast to meshdata object for easier return object.
        MeshData meshData = new MeshData(intTrianglesArray, vecUvsArray, vecVertArray, vecNormalArray, vecTangentArray);

        Debug.Log("Reconstruction check 2");


        // Unity's vertices limit is around 65k vertices. 
        const int VerticesLimit = 60000;

        List<Vector3> verticesList = new List<Vector3>();
        List<Vector3> normalsList = new List<Vector3>();
        List<int> trianglesList = new List<int>();

        int triValue = 0;

        //Create dynamic list that when completed fills the subMesh array in an atomic manner.
        List<MeshData> subMeshDatas = new List<MeshData>();

        // Start splitting mesh every vert limit
        for (int j = 0; j < meshData.triangles.Length; j++)
        {
            verticesList.Add(meshData.vertices[meshData.triangles[j]]);
            normalsList.Add(meshData.normals[meshData.triangles[j]]);
            trianglesList.Add(triValue);
            triValue++;

            if (verticesList.Count == VerticesLimit)
            {
                triValue = 0;
                //make mesh from list
                MeshData subMd = new MeshData
                {
                    vertices = verticesList.ToArray(),
                    normals = normalsList.ToArray(),
                    triangles = trianglesList.ToArray(),
                };

                // Add newly created child mesh to list of childmeshes.
                subMeshDatas.Add(subMd);

                // Check the segmentations for each child mesh are correct and under the limit.
                Debug.Log("made submesh with vert length: " + subMd.vertices.Length);

                //Clear the list for the next child mesh.
                verticesList.Clear();
                normalsList.Clear();
                trianglesList.Clear();
            }
        }
        // Final case for creating mesh less than unity's vertices limit.
        MeshData final = new MeshData
        {
            vertices = verticesList.ToArray(),
            normals = normalsList.ToArray(),
            triangles = trianglesList.ToArray(),
        };
        Debug.Log(final.vertices.Length);
        subMeshDatas.Add(final);
        subMeshDatasArray = subMeshDatas.ToArray();
    }

    //Convert the received into a game object.
    void GenerateModels(MeshData[] meshDatas)
    {
        foreach (MeshData meshData in meshDatas)
        {
            GameObject genModel = new GameObject
            {
                name = "GeneratedModel" + meshData
            };

            //todo: Add additional mesh properties to the meshData class. 
            Mesh genMesh = new Mesh
            {
                vertices = meshData.vertices,
                //uv = meshData.uv,
                triangles = meshData.triangles,
                //tangents = meshData.tangents,
                normals = meshData.normals,
            };

            genMesh.RecalculateBounds();

            genModel.AddComponent<MeshFilter>();
            genModel.GetComponent<MeshFilter>().mesh = genMesh;

            MeshRenderer generatedRenderer = genModel.AddComponent<MeshRenderer>();

            Material genMaterial = generatedRenderer.material = new Material(Shader.Find("Standard"));
            genMaterial.name = "GeneratedMaterial";
        }

        // Clear models to generate queue.
        // Turn bool to accept generation calls again.
        subMeshDatasArray = null;
        isGeneratingModels = false;
    }


    private void Update()
    {
        //if data ready to be sent and sending thread not already running.
        if (sendData != null && isSending == false)
        {
            Debug.Log("send data contains data. Starting sending thread.");
            // Send data if there's data in the send queue.
            Thread senderThread = new Thread(() => SenderThread(sendData));
            senderThread.IsBackground = true;
            senderThread.Start();
            
            //Prevent from being called again. Is set back to false at end of thread.
            isSending = true;
        }
        if (subMeshDatasArray != null && isGeneratingModels == false)
        {
            GenerateModels(subMeshDatasArray);
        }
    }
}

public class WireData
{
    //todo: Rename this class and make it the default wiredata class.
    //todo: Add more sendable information to this class.

    [SerializeField]
    public float[] tangents, verts, normals, uvs;

    [SerializeField]
    public int[] triangles;

    public WireData(float[] tangents, float[] verts, float[] normals, float[] uvs, int[] triangles)
    {
        this.tangents = tangents;
        this.verts = verts;
        this.normals = normals;
        this.uvs = uvs;
        this.triangles = triangles;
    }
}
