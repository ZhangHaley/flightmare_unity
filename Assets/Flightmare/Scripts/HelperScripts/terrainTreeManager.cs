using System.Collections;
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;
using System.Threading;

// ZMQ/LCM
using NetMQ;
using NetMQ.Sockets;

// Include JSON
using Newtonsoft.Json;
using MessageSpec;

namespace RPGFlightmare
{
    // Idea from: https://www.tutorialfor.com/questions-7293.htm
    public class terrainTreeManager : MonoBehaviour
    {
        // ==============================================================================
        // Default Parameters 
        // ==============================================================================
        [HideInInspector]
        public const int tree_client_default_port = 10255;
        [HideInInspector]
        public const int confirm_client_default_port = 10256;
        [HideInInspector]
        public const string client_ip_default = "127.0.0.1";
        [HideInInspector]
        public const string client_ip_pref_key = "client_ip";
        [HideInInspector]
        public const int connection_timeout_seconds_default = 5;
        [HideInInspector]
        public string rpg_dsim_version = "";

        // ==============================================================================
        // Public Parameters
        // ==============================================================================

        // Inspector default parameters
        public string client_ip = client_ip_default;
        public int tree_client_port = tree_client_default_port;
        public int confirm_client_port = confirm_client_default_port;
        public const int connection_timeout_seconds = connection_timeout_seconds_default;

        //================================================================================
        // NetWork
        //===============================================================================
        private SubscriberSocket pull_socket;
        private PublisherSocket push_socket; 
        private bool socket_initialized = false;      
        private TreeMessage_t tree_message;
     

        //===============================================================================
        // Private Parameters
        //===============================================================================

        private object socket_lock;
        private static readonly Type terrainDataType = typeof(TerrainData);
#pragma warning disable IDE0052 // 删除未读的私有成员
        private static Terrain terrain ;
#pragma warning restore IDE0052 // 删除未读的私有成员

        Thread client_thread_; 
        private UnityEngine.Object thisLock_ = new UnityEngine.Object();
        bool stop_thread_ = false;

        void Awake()
        {
            terrain = Terrain.activeTerrain;
            Clear(terrain);
        }
        public void Start()
        {
            Debug.Log("Start a request thread.");
            client_thread_ = new Thread(debugStart);
            client_thread_.Start();
        }



        void debugStart()
        {
            AsyncIO.ForceDotNet.Force();
            InstantiateSocket();
            //NetMQConfig.Cleanup();

            //InstantiateSocket();
            //client_ip = PlayerPrefs.GetString(client_ip_pref_key, client_ip_default);
        }
        void Update()
        {
            if ( !socket_initialized)
            {
                pull_socket.Close();
                push_socket.Close();
                Debug.Log("Terminated ZMQ sockets.");
                NetMQConfig.Cleanup();
                socket_initialized = true;
            }
        }

        // Start is called before the first frame update
        void nullStart()
        {
            //DontDestroyOnLoad(this.gameObject);
            AsyncIO.ForceDotNet.Force();
            socket_lock = new object();
            InstantiateSocket();
            client_ip = PlayerPrefs.GetString(client_ip_pref_key, client_ip_default);

            //如果不是在 Editror中运行的话
            if (!Application.isEditor)
            {
                // Check if FlightGoggles should change its connection ports (for parallel operation)
                // Check if the program should use CLI arguments for IP.
                tree_client_port = Int32.Parse(GetArg("-input-port", "10255"));
                confirm_client_port = Int32.Parse(GetArg("-output-port", "10256"));
                // Check if the program should use CLI arguments for IP.
                string client_ip_from_cli = GetArg("-client-ip", "");
                if (client_ip_from_cli.Length > 0)
                {
                ConnectToClient(client_ip_from_cli);
                }
                else
                {
                ConnectToClient(client_ip);
                }
                // Check if the program should use CLI arguments for IP.
                // obstacle_perturbation_file = GetArg("-obstacle-perturbation-file", "");
                // Disable fullscreen.
                // Screen.fullScreen = false;
                // Screen.SetResolution(1024, 768, false);
            }
            //如果在 Editor 中运行
            else
            {
                // Try to connect to the default ip
                ConnectToClient(client_ip);
            }
            // MassTreePlacement mlp = new MassTreePlacement();
            // PlaceRandomTrees(terrain, mlp);

            StartCoroutine(WaitForRender());
            Debug.Log("terrainTreeManager End Start");
            pull_socket.Close();
            push_socket.Close();
            Debug.Log("Terminated ZMQ sockets.");
            NetMQConfig.Cleanup();
        }

        public IEnumerator WaitForRender()
        {
        // Wait until end of frame to transmit images
            while (true)
            {
                // Wait until all rendering + UI is done.
                // Blocks until the frame is rendered.
                // Debug.Log("Wait for end of frame: " + Time.deltaTime);
                yield return new WaitForEndOfFrame();
                // Check if this frame should be rendered.
                Debug.Log("Ready to Render.");
                // Read the frame from the GPU backbuffer and send it via ZMQ.
                
            }
        }

        // Update is called once per frame
        void null_Update()
        {
            //如果收到了一条 Message
            if(pull_socket.HasIn || socket_initialized)
            {
                var msg = new NetMQMessage();
                var new_msg = new NetMQMessage();
                bool received_new_packet = pull_socket.TryReceiveMultipartMessage(new TimeSpan(0, 0, connection_timeout_seconds), ref new_msg);
                while (pull_socket.TryReceiveMultipartMessage(ref new_msg)) ;
                if ("PLACETREE"== new_msg[0].ConvertToString())
                {
                    // Check that we got the whole message
                    if (new_msg.FrameCount >= msg.FrameCount) { msg = new_msg; }
                    if (msg.FrameCount != 2) { return; }

                    if (!socket_initialized)
                    {
                        tree_message = JsonConvert.DeserializeObject<TreeMessage_t>(msg[1].ConvertToString());
                        MassTreePlacement mlp = new MassTreePlacement();
                        PlaceRandomTrees(terrain, mlp);
                        socket_initialized = true;
                        sendReady();
                        return;
                    }

                }

            }
        }
        void sendReady()
        {
            TreeReadyMessage_t metadata = new TreeReadyMessage_t(socket_initialized);
            var msg = new NetMQMessage();
            msg.Append(JsonConvert.SerializeObject(metadata));
            if (push_socket.HasOut)
            {
                push_socket.TrySendMultipartMessage(msg);
            }
        }

        public static void Clear(Terrain terrain)
        {
            terrain.terrainData.treeInstances = new TreeInstance[0];
            //RecalculateTreePositions(terrain.terrainData);
            terrain.Flush();
            //刷新更改
        }

        public static void PlaceRandomTrees(Terrain terrain, MassTreePlacement mtp)
        {
            var data = terrain.terrainData;

            var num = data.treePrototypes.Length;
            if (num == 0)
            {
                Debug.LogWarning("Can't place trees because no prototypes are defined. Process aborted.");
                return;
            }

            //Undo.RegisterCompleteObjectUndo(data, "Mass Place Trees");

            var start = DateTime.Now;

            var array = new TreeInstance[mtp.Count];
            var i = 0;
            var terrainScaleY = data.size.y;
            var terrainScaleX = data.size.x;
            var terrainScaleZ = data.size.z;
            Debug.Log("terrainScaleY");
            Debug.Log(terrainScaleY);
            while (i < array.Length)
            {
                // stop if process have run for over X seconds
                var delta = DateTime.Now - start;
                if (delta.TotalSeconds >= mtp.MaxTime)
                {
                    Debug.LogWarning("Process was taking too much time to run");
                    return;
                }

                var position = new Vector3(Random.value, 0.0f, Random.value);

                // don't allow placement of trees below minWorldY and above maxWorldY
                var worldY = data.GetHeight(Convert.ToInt16(position.x*terrainScaleX),
                                                               Convert.ToInt16(position.z*terrainScaleZ));
                var y = ( worldY-3)/terrainScaleY;
                position.y = y;

                if (worldY < mtp.MinWorldY || worldY > mtp.MaxWorldY)
                {
                    continue;
                }

                // don't allow placement of trees on surfaces flatter than minSlope and steeper than maxSlope
                var steepness = data.GetSteepness(position.x, position.z);
                if (steepness < mtp.MinSlope || steepness > mtp.MaxSlope)
                {
                    continue;
                }

                var color = Color.Lerp(Color.white, Color.gray * 0.7f, Random.value);
                color.a = 1f;

                var treeInstance = default(TreeInstance);
                treeInstance.position = position;
                treeInstance.color = color;
                treeInstance.lightmapColor = Color.white;
                treeInstance.prototypeIndex = Random.Range(0, num);
                treeInstance.widthScale = Random.Range(mtp.MinWidthScale, mtp.MaxWidthScale);
                treeInstance.heightScale = Random.Range(mtp.MinHeightScale, mtp.MaxHeightScale);
                array[i] = treeInstance;
                i++;
            }
            data.treeInstances = array;
            //RecalculateTreePositions(data);
            terrain.Flush();
        }
        private static void RecalculateTreePositions(TerrainData data)
        {
            terrainDataType.InvokeMember(
                "RecalculateTreePositions",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.InvokeMethod,
                null,
                data,
                null
                );
        }
        private static string GetArg(string name, string default_return)
        {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == name && args.Length > i + 1)
            {
            return args[i + 1];
            }
        }
        return default_return;
            }
        private void ConnectToClient(string inputIPString)
        {
            Debug.Log("Trying to connect to: " + inputIPString);
            string tree_host_address = "tcp://" + inputIPString + ":" + tree_client_port.ToString();
            string confirm_host_address = "tcp://" + inputIPString + ":" + confirm_client_port.ToString();
            // Close ZMQ sockets
            pull_socket.Close();
            push_socket.Close();
            Debug.Log("Terminated ZMQ sockets.");
            NetMQConfig.Cleanup();

            // Reinstantiate sockets
            InstantiateSocket();

            // Try to connect sockets
            try
            {
                Debug.Log(tree_host_address);
                pull_socket.Connect(tree_host_address);
                push_socket.Connect(confirm_host_address);
                Debug.Log("Sockets bound.");
                // Save ip address for use on next boot.
                // PlayerPrefs.SetString(client_ip_pref_key, inputIPString);
                // PlayerPrefs.Save();
            }
            catch (Exception)
            {
                Debug.LogError("Input address from textbox is invalid. Note that hostnames are not supported!");
                throw;
            }

        }
/*        private void OnApplicationQuit()
        {
            // Init simple splash screen
            //splash_screen.GetComponentInChildren<Text>(true).text = "Welcome to RPG Flightmare!";
            // Close ZMQ sockets
            pull_socket.Close();
            push_socket.Close();
            Debug.Log("Terminated ZMQ sockets.");
            NetMQConfig.Cleanup();
        }*/
        private void InstantiateSocket()
        {
            // Configure sockets
            Debug.Log("Configuring sockets.");
            pull_socket = new SubscriberSocket();
            pull_socket.Options.ReceiveHighWatermark = 6;
            // Setup subscriptions.
            pull_socket.Subscribe("PLACETREE");
            pull_socket.Subscribe("PLACEOBJECT");
            push_socket = new PublisherSocket();
            push_socket.Options.Linger = TimeSpan.Zero; // Do not keep unsent messages on hangup.
            push_socket.Options.SendHighWatermark = 6; // Do not queue many images.
        }


    }
}
