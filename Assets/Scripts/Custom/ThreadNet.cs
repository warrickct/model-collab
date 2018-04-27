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

public class ThreadNet : MonoBehaviour {

    public GameObject model;

    //Debugging 
    bool connected = false;

    WireData2 wd2 = null;

    bool isConstructing = false;
    bool meshArrayConstructionDone = false;

    Vector3[] receivedVerts;
    Vector2[] receivedUvs;
    int[] receivedTriangles;

    //testing for incorrect data send.
    Vector4[] localTangents;
    Vector3[] localVerts;
    Vector3[] localNormals;
    Vector2[] localUv;
    int[] localTriangles;

    //Made to handle when to instantiate game obj (as it can only be done from update/start.
    bool hasMesh = false;
    MeshData meshData;

    //made to prevent the repeat calls to generate the model.
    bool isGenerated = false;

    bool generatedSubModels = false;

    //Testing across 2 clients.
    public bool sending;
    public bool receiving;

    public string receiveIp;
    public int receivePort;

    public string sendIp;
    public int sendPort;

    // Use this for initialization
    void Start () {

        Mesh mesh = model.GetComponent<MeshFilter>().mesh;
        Vector4[] tangents = mesh.tangents;
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Vector2[] uv = mesh.uv;
        int[] triangles = mesh.triangles;

        Debug.Log("number of verts for local model: " + mesh.vertices.Length / 1000 + "k");

        // Pre-send check of vertices to checking corruption from sending.
        /*
        Debug.Log("tangents length in as floats before sending" + tangents.Length * 4);
        Debug.Log("model firsts - lasts:");
        Debug.Log("tangent:" + tangents[0] + tangents[tangents.Length-1]);
        Debug.Log("vertex:" + vertices[0] + vertices[vertices.Length-1]);
        Debug.Log("normal:" + normals[0] + normals[normals.Length -1]);
        Debug.Log("uv:" + uv[0] + uv[uv.Length -1]);
        Debug.Log("triangle:" + triangles[triangles.Length -1]);
        */

        //get tangets normals and bounds as well.

        byte[] data;
        Thread newThread = new Thread(() => SerializeModel(tangents, vertices, normals, uv, triangles));
        newThread.IsBackground = true;
        newThread.Start();

        Thread listenerThread = new Thread(Listener);
        listenerThread.IsBackground = true;
        listenerThread.Start();
    }


    /// <summary>
    /// Extracts model vert, uv, triangle. Converts them into float arrays, constructs into serializable class, serializes into byte array.
    /// </summary>
    /// <param name="verts"></param>
    /// <param name="uvs"></param>
    /// <param name="triangles"></param>
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
        foreach( Vector3 vert in verts)
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

        Thread senderThread = new Thread(() => SenderThreaded(data));
        senderThread.IsBackground = true;
        senderThread.Start();

        Debug.Log("sender thread started");
    }

    /// <summary>
    /// Sends byte array using sockets
    /// </summary>
    /// <param name="data"></param>
    void SenderThreaded(byte[] data)
    {
        //for debugging when testing one client as a sender and the other as receiver.
        if (!sending)
        {
            return;
        }

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
    }

    /// <summary>
    /// Receives data stream from specified endpoint. Sends the complete stream data to handling function.
    /// </summary>
    void Listener()
    {
        //for debugging.
        if (!receiving)
            return;

        IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse(receiveIp), receivePort);
        TcpListener tcpListener = new TcpListener(ipEndPoint);

        tcpListener.Start();

        byte[] bytes = new byte[1024];
        String data = null;

        while (true)
        {
            TcpClient client = tcpListener.AcceptTcpClient();

            //Debugging 
            connected = true;

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
            wd2 = bf.Deserialize(ms) as WireData2;

            HandleWireData(wd2);
        }
    }


    //Public for receiving and sending
    //? Not sure if necessary for them to be outside HandleWireData.
    List<MeshData> subMeshDatas = new List<MeshData>();
    MeshData[] subMeshDatasArray;

    /// <summary>
    /// Parent function for running wiredata handling and conversion into model.
    /// </summary>
    /// <param name="wd"></param>
    void HandleWireData(WireData2 wd)
    {
        meshData = ReconstructMeshArrays(wd);
    }

    
    /// <summary>
    /// Deserializes wiredata and converts 
    /// into appropriate types to create a MeshData
    /// </summary>
    /// <param name="wd2"></param>
    /// <returns></returns>
    MeshData ReconstructMeshArrays(WireData2 wd2)
    {
        // For threading control. To prevent constructing of a mesh when one is already being constructed.
        isConstructing = true;

        Debug.Log("received tangets length" + wd2.tangents.Length);

        //tangents
        float[] tangents = wd2.tangents;
        List<Vector4> vecTangents = new List<Vector4>();
        for (int i = 0; i < tangents.Length; i += 4)
        {
            Vector4 tangent = new Vector4(tangents[i + 1], tangents[i + 2], tangents[i + 3], tangents[i]);
            vecTangents.Add(tangent);
        }

        //normals
        float[] normals = wd2.normals;
        List<Vector3> vecNormals = new List<Vector3>();
        for (int i = 0; i < normals.Length; i += 3)
        {
            Vector3 normal = new Vector3(normals[i], normals[i + 1], normals[i + 2]);
            vecNormals.Add(normal);
        }

        //vertices
        float[] verts = wd2.verts;
        List<Vector3> vectorVertices = new List<Vector3>();
        for (int i=0; i < verts.Length; i+=3)
        {
            Vector3 vertex = new Vector3( verts[i], verts[i+1], verts[i+2] );
            vectorVertices.Add(vertex);
        }

        //uv
        float[] uvs = wd2.uvs;
        List<Vector2> vectorUvs = new List<Vector2>();
        for (int i=0; i < uvs.Length; i+=2 )
        {
            Vector2 uv = new Vector2(uvs[i], uvs[i + 1]);
            vectorUvs.Add(uv);
        }

        //dont need to do anything for triangles.

        Vector4[] vecTangentArray = vecTangents.ToArray();
        Vector3[] vecVertArray = vectorVertices.ToArray();
        Vector3[] vecNormalArray = vecNormals.ToArray();
        Vector2[] vecUvsArray = vectorUvs.ToArray();
        int[] intTrianglesArray = wd2.triangles;


        // Simple test to check received data is not uncorrupted.
        /*
        Debug.Log("Generated meshdata firsts + lasts");
        Debug.Log("tangent" + vecTangentArray[vecTangentArray.Length -1]);
        Debug.Log("verts" + vecVertArray[vecVertArray.Length -1]);
        Debug.Log("normals" + vecNormalArray[vecNormalArray.Length -1]);
        Debug.Log("uvs" + vecUvsArray[vecUvsArray.Length -1]);
        Debug.Log("triangles " + intTrianglesArray[intTrianglesArray.Length-1]);
        */

        //cast to meshdata object for easier return object.
        MeshData meshData = new MeshData(intTrianglesArray, vecUvsArray, vecVertArray, vecNormalArray, vecTangentArray);

        //test: make submesh if mesh too big then generate that instead

        // Unity's vertices limit is around 65k vertices. 
        const int VerticesLimit = 60000;
        
        // Create segment mesh into child meshes for child models when a model mesh is over unity's limit.
        if (meshData.vertices.Length > VerticesLimit) {

            List<Vector3> verticesList = new List<Vector3>();
            List<Vector3> normalsList = new List<Vector3>();
            List<int> trianglesList = new List<int>();

            int triValue = 0;

            for (int j =0; j < meshData.triangles.Length; j++ )
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

            return subMeshDatas[1];
        }

        return meshData;
    }

    /// <summary>
    /// Creates a game object then creates a mesh from a MeshData and adds it to the game object.
    /// </summary>
    /// <param name="meshData"></param>
    /// <returns></returns>
    GameObject GenerateModel(MeshData meshData)
    {
        GameObject genModel = new GameObject
        {
            name = "GeneratedModel"
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

        // Works as a switch to stop the same model being generated repeatedly.
        isGenerated = true;

        return genModel;
    }

    /// <summary>
    /// Currently used as the flow control for the threads
    /// and calling non-threadsafe Unity api.
    /// </summary>
    private void Update()
    {
        if (meshData.triangles != null && isGenerated == false)
        {
            // todo: prevent from running both singular and segmented generation loops. 

            //! Commented out singular model generation temporarily but possibly not necessary anymore as multiple generation handles all cases.
            //GenerateModel(meshData);
        }
        Debug.Log("UPDATE: submesh data count: " + subMeshDatasArray.Length);

        if (generatedSubModels == false && subMeshDatas != null)
        {
            GameObject parentModel = new GameObject
            {
                name = "rootModel",
            };

            foreach (MeshData subMesh in subMeshDatasArray)
            {
                GameObject subModel = GenerateModel(subMesh);
                subModel.transform.parent = parentModel.transform;
            }
            generatedSubModels = true;
        }
    }
}

/// <summary>
/// Convenience class for returning multiple mesh data properties in one return.
/// </summary>
public struct MeshData
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

[Serializable]
public class WireData2
{
    //todo: Rename this class and make it the default wiredata class.
    //todo: Add more sendable information to this class.

    [SerializeField]
    public float[] tangents, verts, normals, uvs;

    [SerializeField]
    public int[] triangles;

    public WireData2(float[] tangents, float[] verts, float[] normals, float[] uvs, int[] triangles)
    {
        this.tangents = tangents;
        this.verts = verts;
        this.normals = normals;
        this.uvs = uvs;
        this.triangles = triangles;
    }
}
