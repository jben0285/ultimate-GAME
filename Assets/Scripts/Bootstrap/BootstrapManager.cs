using System;
using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Broadcast;
using FishNet.Managing;
using FishNet.Transporting;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bootstrap
{
    public class BootstrapManager : MonoBehaviour
    {

        public struct LobbyCloseBroadcast : IBroadcast
        {
            public bool inGame;
        };



        private static BootstrapManager instance;
        private void Awake() => instance = this;



        [SerializeField] private string mainMenuName;
        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private FishySteamworks.FishySteamworks _fishySteamworks;
        [SerializeField] private bool isLobbyOwner;

        [SerializeField] private bool inLobby;

        public static bool GetInLobby()
        {
            return instance != null ? instance.inLobby : false;
        }

        public static void SetInLobby(bool value)
        {
            if (instance != null)
            {
                instance.inLobby = value;
            }
        }
        [SerializeField] private List<CSteamID> lobbyIDs = new();

        //declaration of callbacks
        protected Callback<LobbyCreated_t> LobbyCreated;
        protected Callback<GameLobbyJoinRequested_t> JoinRequest;
        protected Callback<LobbyEnter_t> LobbyEntered;
        protected Callback<LobbyChatUpdate_t> LobbyUpdated;
        protected Callback<LobbyChatMsg_t> LobbyChat;
        protected Callback<LobbyMatchList_t> LobbyMatchList;
        protected Callback<LobbyDataUpdate_t> LobbyDataUpdate;

        //unsigned long storing lobby id
        public static ulong CurrentLobbyID;

        private void Start()
        {
            //steamapi being initalized is REQUIRED for using callbacks and other important functionality
            if (SteamAPI.Init())
            {
                Debug.Log("Steam API was succesfully initialized");

                LobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
                JoinRequest = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequest);
                LobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
                LobbyUpdated = Callback<LobbyChatUpdate_t>.Create(OnLobbyUpdate);
                LobbyChat = Callback<LobbyChatMsg_t>.Create(OnLobbyChat);
                LobbyMatchList = Callback<LobbyMatchList_t>.Create(OnLobbyList);
                LobbyMatchList= Callback<LobbyMatchList_t>.Create(OnLobbyList);
                LobbyDataUpdate = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
                RunCallbacks();

            }
            InstanceFinder.ClientManager.RegisterBroadcast<LobbyCloseBroadcast>(OnLobbyClose);

            //_fishySteamworks.Initialize(_networkManager, 0);

        }

        //broadcast reciever 
        private void OnLobbyClose(LobbyCloseBroadcast broadcast, Channel channel)
        {

            Debug.Log("recieved close broadcast");
            CSteamID lobbyID = new(CurrentLobbyID);
            SteamMatchmaking.LeaveLobby(lobbyID);
            CurrentLobbyID = 0;
            string[] scenesToClose = { "SampleScene", "Main Menu" };



            Debug.LogWarning("is server?: " + instance._networkManager.IsServerStarted + "\n is owner?: " + instance.isLobbyOwner);

            if (instance._networkManager.IsServerStarted || instance.isLobbyOwner)

            {
                instance._fishySteamworks.StopConnection(true);
                Debug.LogWarning("stopping server");
            }
            else
            {
                instance._fishySteamworks.StopConnection(false);

            }

            isLobbyOwner = false;

            BootstrapNetworkManager.ChangeNetworkScene("Main Menu", scenesToClose, broadcast.inGame);
            MainMenuManager.OpenMainMenu();
        }

        private void RunCallbacks()
        {
            //Debug.Log("is owner? " + instance.isLobbyOwner);
            SteamAPI.RunCallbacks();
            StartCoroutine(CallbackDelay());

            //        Debug.LogWarning("State? " + _fishySteamworks.GetConnectionState(false));
        }

        private IEnumerator CallbackDelay()
        {
            yield return new WaitForSeconds(1f);
            RunCallbacks();
        }

        public void GoToMenu()
        {
            SceneManager.LoadScene(mainMenuName, LoadSceneMode.Additive);
        }

        public static void CreateLobby()
        {
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 4);
            instance.isLobbyOwner = true;
            //Debug.Log("is owner CREATE? " + instance.isLobbyOwner);
        }


        public static void FindLobbies()
        {
            CSteamID steamIDLobby = CSteamID.Nil;
            SteamMatchmaking.AddRequestLobbyListNearValueFilter("distance", 0);
           // SteamMatchmaking.AddRequestLobbyListStringFilter("name", "IndustrialMan's lobby", ELobbyComparison.k_ELobbyComparisonEqual);
            SteamAPICall_t handle = SteamMatchmaking.RequestLobbyList();
        }

        private void OnLobbyList(LobbyMatchList_t result)
        {
            Debug.Log("Received lobby match list callback. Num lobbies: " + result.m_nLobbiesMatching);
            Debug.Log("Number of lobbies found: " + result.m_nLobbiesMatching);

            for (int i = 0; i < result.m_nLobbiesMatching; i++)
            {
                CSteamID lobbyID = SteamMatchmaking.GetLobbyByIndex(i);
                SteamMatchmaking.RequestLobbyData(lobbyID);
            }

        }

        void OnLobbyDataUpdate(LobbyDataUpdate_t result)
    {
        if (result.m_bSuccess != 1)
        {
            Debug.LogError("Failed to update lobby data.");
            return;
        }

        CSteamID lobbyID = new CSteamID(result.m_ulSteamIDLobby);
        string lobbyName = SteamMatchmaking.GetLobbyData(lobbyID, "name");

        // Display lobby information
        Debug.Log("Lobby Name: " + lobbyName + "\n");
    }



        /// <summary>
        /// from the current lobbyID, iterates the lobby and updates a list of all current players
        /// not very efficent rn
        /// </summary>
        private void UpdateCurrentLobbyList(CSteamID currentID)
        {
            for (int i = 0; i < 4; i++)
            {
            //clear the text
                MainMenuManager.UpdateLobbyPlayerText(i, "", "");
            }

            lobbyIDs.Clear();         //easiest way to maintain
            int tempCount = SteamMatchmaking.GetNumLobbyMembers(currentID);
            try
            {
                for (int i = 0; i < tempCount; i++)
                {
                    CSteamID temp = SteamMatchmaking.GetLobbyMemberByIndex(currentID, i);
                    if (!lobbyIDs.Contains(temp))
                    {
                        lobbyIDs.Add(temp);
                        MainMenuManager.UpdateLobbyPlayerText(i, (i+1).ToString(), SteamFriends.GetFriendPersonaName(temp));
                    }
                }
            }
            catch (IndexOutOfRangeException)
            {
                Debug.LogError("player left while iterating?");
            }
        }

        private void OnLobbyCreated(LobbyCreated_t callback)
        {

            Debug.Log("Starting lobby creation: " + callback.m_eResult.ToString());
            if (callback.m_eResult != EResult.k_EResultOK)
                return;


            CurrentLobbyID = callback.m_ulSteamIDLobby;
            CSteamID id = new CSteamID(CurrentLobbyID);
            SteamMatchmaking.SetLobbyData(id, "HostAddress", SteamUser.GetSteamID().ToString());
            SteamMatchmaking.SetLobbyData(id, "name", SteamFriends.GetPersonaName().ToString() + "'s lobby");
            _fishySteamworks.SetClientAddress(SteamUser.GetSteamID().ToString());
            _fishySteamworks.StartConnection(true);
            instance.inLobby = true;
            //instance.isLobbyOwner = true;
       //     Debug.Log("Lobby creation was successful");
        }

        private void OnJoinRequest(GameLobbyJoinRequested_t callback)
        {
            SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
        }

        private void OnLobbyEntered(LobbyEnter_t callback)
        {
            CurrentLobbyID = callback.m_ulSteamIDLobby;
            CSteamID lobbyID = new(CurrentLobbyID);
            //Debug.Log("id? " + lobbyID);

            MainMenuManager.LobbyEntered(SteamMatchmaking.GetLobbyData(lobbyID, "name"), _networkManager.IsServerStarted);
            _fishySteamworks.SetClientAddress(SteamMatchmaking.GetLobbyData(lobbyID, "HostAddress"));
            //Debug.Log("addr: " + SteamMatchmaking.GetLobbyData(lobbyID, "HostAddress"));
            _fishySteamworks.StartConnection(false);

            MainMenuManager.UpdateLobbyPlayerCount(SteamMatchmaking.GetNumLobbyMembers(lobbyID));

            //on entering a lobby get list of ids
            UpdateCurrentLobbyList(lobbyID);

            for (int i = 0; i < lobbyIDs.Count; i++)
            {
                MainMenuManager.UpdateLobbyPlayerText(i, i.ToString(), SteamFriends.GetFriendPersonaName(lobbyIDs[i]));
            }
            PrintLobbyMetadata(lobbyID);

        }

        void PrintLobbyMetadata(CSteamID lobbyID)
    {
        int dataCount = SteamMatchmaking.GetLobbyDataCount(lobbyID);
        for (int i = 0; i < dataCount; i++)
        {
            bool success = SteamMatchmaking.GetLobbyDataByIndex(lobbyID, i, out string key, 256, out string value, 256);
            if (success)
            {
                Debug.Log($"Lobby Metadata - Key: {key}, Value: {value}");
            }
        }
    }

        private void OnLobbyUpdate(LobbyChatUpdate_t callback)
        {
            Debug.Log("callback CITY");
            MainMenuManager.UpdateLobbyPlayerCount(SteamMatchmaking.GetNumLobbyMembers(new CSteamID(CurrentLobbyID)));

            UpdateCurrentLobbyList(new CSteamID(callback.m_ulSteamIDLobby));

            for (int i = 0; i < lobbyIDs.Count; i++)
            {
                MainMenuManager.UpdateLobbyPlayerText(i, (i + 1).ToString(), SteamFriends.GetFriendPersonaName(lobbyIDs[i]));
            }

        }


        private void OnLobbyChat(LobbyChatMsg_t callback)
        {
            //thanks chatgpt
            CSteamID lobbyID = new(CurrentLobbyID);
            //getting real now
            byte[] data = new byte[4096];

            if (SteamMatchmaking.GetLobbyChatEntry(lobbyID, (int)callback.m_iChatID, out CSteamID sender, data, data.Length, out EChatEntryType chatEntryType) > 0)
            {
                // Process the chat message (you might have a more sophisticated message handling system)
                string message = System.Text.Encoding.UTF8.GetString(data);
                Debug.Log($"Received lobby chat message: {message}");

                MainMenuManager.UpdateLobbyChat(message);
            }
        }

        public static void JoinByID(CSteamID steamID)
        {
            Debug.Log("Attempting to join lobby with ID: " + steamID.m_SteamID);
            if (SteamMatchmaking.RequestLobbyData(steamID))
                SteamMatchmaking.JoinLobby(steamID);
            else
                Debug.Log("Failed to join lobby with ID: " + steamID.m_SteamID);
        }

        public static void LeaveLobby(bool inGame = false)
        {
            CSteamID lobbyID = new(CurrentLobbyID);
            if (instance.isLobbyOwner)
            {
                //should always be true
                if (InstanceFinder.IsServerStarted)
                    InstanceFinder.ServerManager.Broadcast(new LobbyCloseBroadcast(){inGame = inGame});

                Debug.Log("sending leave message");
                string leaveMessage = SteamFriends.GetPersonaName() + " has left the lobby.\n";
                SteamMatchmaking.SendLobbyChatMsg(
                    lobbyID, System.Text.Encoding.UTF8.GetBytes(leaveMessage), leaveMessage.Length + 1);

            }
            else
            {
                instance._fishySteamworks.StopConnection(false);
                SteamMatchmaking.LeaveLobby(lobbyID);
                CurrentLobbyID = 0;
                if (inGame)
                {
                    SceneManager.UnloadSceneAsync(SceneManager.GetActiveScene().name);
                if (!SceneManager.GetSceneByName("Main Men").isLoaded)
                { 
                    SceneManager.LoadScene("Main Men", LoadSceneMode.Additive);
                }
                }
                
                MainMenuManager.OpenMainMenu();
            }
            instance.inLobby = false;

        }

        public static void QuitGame()
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }


        private void OnApplicationQuit()
        {
            SteamAPI.Shutdown();
        }

        public bool InLobby
        {
            get { return inLobby; }
        }
    }
}