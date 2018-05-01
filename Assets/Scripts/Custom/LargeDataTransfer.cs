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
using System.Text;

public class LargeDataTransfer : MonoBehaviour {

    #region Variables

    //Sending variables
    public GameObject modelToSend;

    byte[] dataToSend;


    //Receiving variables
    WireData receivedWireData;
    MeshData[] subMeshDatasArray;

    // Thread control booleans
    bool serializing;
    bool sending;

    bool generatingMesh;
    bool generatingGameObject;

    // Send/Receive Endpoint Config
    [SerializeField] string receiveIp;
    [SerializeField] int receivePort;

    [SerializeField] string sendIp;
    [SerializeField] int sendPort;

    #endregion

    #region Unity Functions

    private void Start()
    {
        Thread listenerThread = new Thread(() => Listener());
        listenerThread.IsBackground = true;
        listenerThread.Start();
    }

    private void Update()
    {
        // todo: Add functionality for a queue to send models so it can process multiple at a time.

        //Sending
        if (modelToSend != null && !serializing)
        {
            Mesh mesh = modelToSend.GetComponent<MeshFilter>().mesh;
            Vector4[] tangents = mesh.tangents;
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector2[] uvs = mesh.uv;
            int[] triangles = mesh.triangles;

            Debug.Log("Beginning serialization of model size: " + mesh.vertices.Length / 1000 + "k");

            Thread serializerThread = new Thread(() => SerializeModel(tangents, vertices, normals, uvs, triangles))
            {
                IsBackground = true
            };
            serializerThread.Start();

            serializing = true;
        }
        if (dataToSend != null && !sending)
        {
            Thread senderThread = new Thread(() => Send(dataToSend))
            {
                IsBackground = true
            };
            senderThread.Start();

            sending = true;
        }


        //Receiving
        if (receivedWireData != null && !generatingMesh)
        {
            Thread meshGenerationThread = new Thread(() => GenerateMesh(receivedWireData))
            {
                IsBackground = true
            };
            meshGenerationThread.Start();

            generatingMesh = true;
        }
        if (subMeshDatasArray != null && !generatingGameObject)
        {
            Debug.Log(subMeshDatasArray.Length);

            GenerateModels(subMeshDatasArray);
        }
    }

    #endregion

    #region Network Receiving

    void Listener()
    {
        IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse(receiveIp), receivePort);
        TcpListener tcpListener = new TcpListener(ipEndPoint);

        tcpListener.Start();

        byte[] bytes = new byte[1024];
        String data = null;

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
                data = Encoding.ASCII.GetString(bytes, 0, i);
                data.ToUpper();
                Debug.Log("receiving");
            }
            byte[] fullDataBytes = fullData.ToArray();
            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream ms = new MemoryStream(fullDataBytes);
            receivedWireData = bf.Deserialize(ms) as WireData;
        }
    }

    void GenerateMesh(WireData wd)
    {
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

        Vector4[] vecTangentArray = vecTangents.ToArray();
        Vector3[] vecVertArray = vectorVertices.ToArray();
        Vector3[] vecNormalArray = vecNormals.ToArray();
        Vector2[] vecUvsArray = vectorUvs.ToArray();
        int[] intTrianglesArray = wd.triangles;

        //cast to meshdata object for easier return object.
        MeshData meshData = new MeshData(intTrianglesArray, vecUvsArray, vecVertArray, vecNormalArray, vecTangentArray);

        //test: make submesh if mesh too big then generate that instead

        // Unity's vertices limit is around 65k vertices. 
        const int VerticesLimit = 60000;
        List<MeshData> subMeshDatas = new List<MeshData>();


        // Iterative submesh generation start.
        List<Vector3> verticesList = new List<Vector3>();
        List<Vector3> normalsList = new List<Vector3>();
        List<int> trianglesList = new List<int>();

        int triValue = 0;

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
        Debug.Log("Final submodel vert size: " + final.vertices.Length);
        subMeshDatas.Add(final);
        subMeshDatasArray = subMeshDatas.ToArray();

        //Setting to prevent generating same meshes again.
        receivedWireData = null;
        generatingMesh = false;
    }

    void GenerateModels(MeshData[] meshDatas)
    {
        GameObject parentModel = new GameObject
        {
            name = "generatedModel",
            tag = "RemoteModel",
        };

        foreach (MeshData meshData in meshDatas)
        {
            GameObject genModel = new GameObject
            {
                name = "generatedSubModel",
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

            genModel.transform.parent = parentModel.transform;
        }

        //Setting to prevent making previously made models
        subMeshDatasArray = null;
        generatingGameObject = false;
    }

    #endregion

    #region Network Sending

    /// <summary>
    /// Sets a model to be sent.
    /// </summary>
    /// <param name="model"></param>
    public void SendModel(GameObject model)
    {
        this.modelToSend = model;
    }

    void SerializeModel(Vector4[] tangents, Vector3[] vertices, Vector3[] normals, Vector2[] uvs, int[] triangles)
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
        foreach (Vector3 vert in vertices)
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

        WireData wd = new WireData(tangentFloatArray, vertFloatArray, normalFloatArray, uvFloatArray, triangles);

        MemoryStream ms = new MemoryStream();
        BinaryFormatter bf = new BinaryFormatter();
        bf.Serialize(ms, wd);
        byte[] data = ms.ToArray();

        dataToSend = data;

        // Setting so it wont redo the same model.
        modelToSend = null;
        serializing = false;
    }

    void Send(byte[] data)
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

        // Settings to it wont resend old serialized models
        dataToSend = null;
        sending = false;
    }

    #endregion

    #region Structs

    struct MeshData
    {
        //todo: Change name once properties start to include non-mesh properties. Maybe "ModelData".

        public Vector4[] tangents;
        public Vector3[] vertices;
        public Vector3[] normals;
        public Vector2[] uv;
        public int[] triangles;

        public MeshData(int[] triangles, Vector2[] uv, Vector3[] vertices, Vector3[] normals, Vector4[] tangents)
        {
            this.tangents = tangents;
            this.triangles = triangles;
            this.uv = uv;
            this.vertices = vertices;
            this.normals = normals;
        }
    }

    #endregion

}

[Serializable]
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