using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine.SceneManagement;
using Steamworks;
using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using TMPro;
using Player;
using FishNet;

namespace Bootstrap
{
    public class BootstrapNetworkManager : NetworkBehaviour
    {

        private static BootstrapNetworkManager instance;
        private void Awake() => instance = this;
        [Header("Assign In Inspector")]
        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private BootstrapManager bootstrap;
        [Tooltip("Client Prefab to spawn when game starts")]
        [SerializeField] private GameObject ClientPrefab;
      //  [SerializeField] private GameObject CameraControllerPrefab;

        [Tooltip("Test Spawn")]
        [SerializeField] private Transform TestSpawn;
       // [SerializeField] private GameObject LoadingCanvas;
        //public LoadingProgress loadingProgress;
        int spawnIndex = 0;

        [Header("Assigned At Runtime")]
        [Tooltip("Local Client stores array of objects that were spawned")]
        public List<GameObject> spawnedObjects = new();
        [Tooltip("Enables enemies to access a list of players")]
        public List<GameObject> _playerObjectList = new();
      //  public SyncVar<List<PlayerObject>> players = new();
        private PlayerObject localPlayer;

        //Game start event handler
        public delegate void GameStartEvent();
        public static event GameStartEvent OnGameStart;

        public delegate void GameEndEvent();
        public static event GameEndEvent OnGameEnd;



        /// <summary>
        /// Closes scenes and gathers scene load data, and loads the scene for each client.
        /// </summary>
        /// <param name="sceneName">Name of scene to change to</param>
        /// <param name="scenesToClose">Array of strings to close</param>
        /// <param name="leavingGame">True if only handling single connection, for now this is for disconnecting only</param>
        public static void ChangeNetworkScene(string sceneName, string[] scenesToClose, bool leavingGame = false)
        {
            SceneLoadData sld = new(sceneName);
            if (leavingGame)
            {
                Debug.Log("LEAVING GAME HERE");
                NetworkConnection conn;
                conn = instance.LocalConnection;
                instance.UnloadObjects(conn);
                UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
                //instance.SceneManager.LoadConnectionScenes(conn, sld);
                foreach (string scene in scenesToClose)
                {
                    try
                    {
                    UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
                    }
                    catch(ArgumentException)
                    {
                        Debug.LogError("Unity is complaining about unloading an invalid scene: " + scene);
                    }
                }
             //   BootstrapManager.SetInLobby(true);
            }
            else
            {
               // BootstrapManager.SetInLobby(false);
                instance.CloseScenes(scenesToClose);

                NetworkConnection[] conns = instance.ServerManager.Clients.Values.ToArray();
                instance.SceneManager.LoadConnectionScenes(conns, sld);
                foreach (NetworkConnection conn in conns)
                {
                    instance.SpawnPlayer(conn, instance);
                }
            }

        }

        /// <summary>
        /// Observer Method to trigger game start event
        /// </summary>
        ///
        private IEnumerator StartDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            TriggerGameStartEvent();
        }

        [ObserversRpc(RunLocally = true)]
        private void TriggerGameStartEvent()
        {
            //LoadingCanvas.SetActive(false);
           // Debug.LogWarning("invoked");
        
            OnGameStart?.Invoke();
        }


        [ServerRpc(RequireOwnership = false)]
        void CloseScenes(string[] scenesToClose)
        {
            CloseScenesObserver(scenesToClose);
        }


        /// <summary>
        /// setter to add a spawned object to the list, these are non-networked objects ONLY
        /// </summary>
        /// <param name="go">gameobject</param>
        public static void AddSpawnedObject(GameObject go)
        {
            if (instance.spawnedObjects.Contains(go))
                return;
            instance.spawnedObjects.Add(go);
        }


        /// <summary>
        /// Close all scenes for each of the clients using observer rpc
        /// </summary>
        /// <param name="scenesToClose">Array of strings to close, passed from ChangeNetworkScene</param>
        [ObserversRpc(RunLocally = true)]
        void CloseScenesObserver(string[] scenesToClose)
        {
        //    instance.LoadingCanvas.SetActive(true);
           // instance.loadingProgress.StartLoading(2f);
            foreach (var sceneName in scenesToClose)
            {
            //    Debug.Log("closing scene: " + sceneName);
                UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(sceneName);
            }
        }


        /// <summary>
        /// Unload objects, since
        /// they are spawned inside bootstrap they must be unloaded if the client returns to menu
        /// </summary>
        /// <param name="conn">Connection</param>
        //[TargetRpc]
        void UnloadObjects(NetworkConnection conn)
        {
           // Debug.LogWarning("unloading objects");
            UnloadObjectsServer(conn, this);

        }

        // [ServerRpc(RunLocally = true, RequireOwnership = false)]
        void UnloadObjectsServer(NetworkConnection conn, BootstrapNetworkManager BNM)
        {
            foreach (NetworkObject nob in conn.Objects)
            {
                instance.ServerManager.Despawn(nob);
            }

            foreach (GameObject go in BNM.spawnedObjects)
            {
                Destroy(go);
            }
            BNM.spawnedObjects.Clear();
            //i was on the phone with tiff when i figured this was the reason why the camera was jank after starting a new lobby
         //   BNM.players.Remove(BNM.localPlayer);
            BNM.localPlayer = null;

            OnGameEnd?.Invoke();
           // loadingProgress.targetFill = 0f;
        }






        /// <summary>
        /// Called as a private TargetRPC method, spawns in the tank and camera.
        /// TargetRPC is called on each client, then each client then calls the spawn on the server
        /// after serializing arguments
        /// </summary>
        [TargetRpc]
        private void SpawnPlayer(NetworkConnection conn, BootstrapNetworkManager BNM)
        {
            StartCoroutine(BNM.StartDelay(1f));

          //  Debug.LogWarning("Calling SpawnPlayer TARGET RPC");

            //instansiate then spawn using servermanager

            //use static method from MMM to get tank, since this happens on local client, it will always be correct
            SpawnComponents(conn, BNM, _playerObjectList[MainMenuManager.GetSelectedPlayer()], MainMenuManager.GetPersona());
        }

        /// <summary>
        /// Called on server, use servermanager to spawn in the tank and client after instansiating the prefab
        /// Stores an int as a field and adds 10 to the x position so players dont spawn on top of eachother
        /// </summary>
        /// <param name="conn">Connection</param>
        /// <param name="BNM">Reference to this</param>
        /// <param name="player">Tank Prefab</param>
        /// <param name="parent">Deprecated</param>
        [ServerRpc(RequireOwnership = false)]
        private void SpawnComponents(NetworkConnection conn, BootstrapNetworkManager BNM, GameObject player, string name)
        {
          //  Debug.LogWarning("Calling SpawnComponents SERVER RPC");
            //clone prefabs locally
            GameObject activePlayer = Instantiate(player, TestSpawn.position + new Vector3(BNM.spawnIndex, 0, 0), TestSpawn.rotation);
         //   GameObject activeCam = Instantiate(CameraControllerPrefab);
       //     Debug.LogWarning(activeTank.GetComponent<PlayerFireController>().bulletSpawn);
         //   Transform temp = activeTank.GetComponent<PlayerFireController>().bulletSpawn;
            //spawn over network
            ServerManager.Spawn(activePlayer, conn);
         //   ServerManager.Spawn(activeCam, conn);

            //set as parent over the network
            InitializeObserver(BNM, activePlayer, name);
            //using playerobject class, call constructor with each of the created objects.
            //conn.firstobject will always be the client thanks to fishnet player spawner.
            PlayerObject newPlayer = new(conn.FirstObject.gameObject, activePlayer, null);
            BNM.localPlayer = newPlayer;
            //Important, add the new player object to the list of players, for now this is used so the
            //enemies can access the list of active tanks
       //     BNM.players.Add(newPlayer);

            //now return control to local client with references to instantiated client, tank, and cam
            //this is a much cleaner way to initalize components
            InitializeComponents(conn, BNM, newPlayer.GetClient(), newPlayer.GetPlayer(), null);

            //naive approach to spawning in players, just move the spawn location to the right 10 units each time
            BNM.spawnIndex += 10;


            //Transform activePlayer = Instantiate(parent);
            //conn.FirstObject.transform.parent = activePlayer;
            //activeTank.transform.parent = activePlayer;
            //activeCam.transform.parent = activePlayer;
            //ServerManager.Spawn(activePlayer.gameObject, conn);
        }

        [ObserversRpc(RunLocally = true, ExcludeServer = false)]
        private void InitializeObserver(BootstrapNetworkManager BNM, GameObject player, string name)
        {
      //      tank.GetComponent<PlayerDurability>().nameTag.GetComponent<TextMeshProUGUI>().text = name;
        }

        //standard order of components is client, tank, camera
        /// <summary>
        /// TargetRPC method called from server, handles assigning/linking all of the neccesary components in all aspects of the player
        /// Has error handling to report if this doesnt run succesfully
        /// Critical to ensure gameplay functionality
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="BNM"></param>
        /// <param name="activeClient">Reference to client</param>
        /// <param name="activePlayer">Reference to player</param>
        /// <param name="activeCam">Reference to camera</param>
        [TargetRpc]
        private void InitializeComponents(NetworkConnection conn, BootstrapNetworkManager BNM, GameObject activeClient, GameObject activePlayer, GameObject activeCam)
        {
            Debug.LogWarning("Calling InitializeComponents TARGET RPC");
            int counter = 0;
            while (true)
            {

                //try
                //{
                if (counter > 200)
                {
                    Debug.LogError("ITERATION ERROR: there was a MAJOR problem initializing some components in the bootstrap network manager!");
                    break;
                }
                activeClient.tag = "LocalClient";
                activePlayer.tag = "LocalPlayer";
                activePlayer.layer = LayerMask.NameToLayer("LocalPlayerLayer");
                ClientMenuManager CMM = activeClient.GetComponent<ClientMenuManager>();
                CMM.BPM = activePlayer.GetComponent<BazigPlayerMotor>();
                activePlayer.GetComponent<BazigPlayerMotor>().CMM = CMM;
                CMM._health = activePlayer.GetComponent<PlayerHealth>();
                if(!conn.IsHost)
                {
                    InstanceFinder.NetworkManager.TimeManager.TickRate = 90;
                }
                break;

                //main stuff here

                //references to Client Scripts
                // ClientTankManager CTM = activeClient.GetComponent<ClientTankManager>();
                // ClientMenuManager CMM = activeClient.GetComponent<ClientMenuManager>();
                // activeClient.tag = "LocalClient";
                // //Client tank manager
                // CTM.activeTank = activeTank;
                // CTM.pfc = activeTank.GetComponent<PlayerFireController>();
                // CTM.tankIsActive = true;
                // CTM.clientTankNum = MainMenuManager.GetSelectedTank();
                // CTM.bootstrap = BNM.bootstrap;
                // CTM.shop.playerTank = activeTank;
                // CTM.shop.playerFireCont = activeTank.GetComponent<PlayerFireController>();
                // CTM.shop.playerDurability = activeTank.GetComponent<PlayerDurability>();
                // CTM.shop.cameraController = activeTank.GetComponent<CameraController>();
                // CTM.shop.enabled = true;

                // //Client menu manager
                // CMM._bootstrap = BNM.bootstrap;
                // CMM.fireCont = activeTank.GetComponent<PlayerFireController>();

                // //tank handling
                // activeTank.GetComponent<PlayerFireController>().BNM = BNM;
                // activeTank.GetComponent<PlayerDurability>().CTM = CTM;
                // activeTank.GetComponent<PlayerFireController>().shopRef = CTM.shop;
                // //clever approach to ensure that only the local player has this tag
                // //saw it in bobsi video, set the tag to "othertank" by default then update it here
                // activeTank.tag = "LocalPlayerTank";

                // //this is for assigning the host's fire controller with a reference to every players fire controller.
                // //needed because the synctimer on that script must be updated on the server, therefore the host runs it every update frame

                // //camera
                // activeCam.GetComponent<CameraController>().tank = activeTank;
                // activeCam.GetComponent<CameraController>().BNM = BNM;


                // Debug.Log("Bootstrap Network Manager initialization - OK");
                // loadingProgress.targetFill += 0.33f;
                // //CTM.spawnedCamera = activeCam;
                // //CTM.activeHUD.SetActive(true);
                // break;
                //}
                //catch (Exception e)``
                //{
                //    counter++;
                //    Debug.LogError("CRITICAL ERROR: there was a problem initializing some components in the bootstrap network manager.");
                //    Debug.LogError(e);
                //}
            }
        }

    }
}