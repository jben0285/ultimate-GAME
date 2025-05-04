using System;
using FishNet;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bootstrap
{
    public class MainMenuManager : MonoBehaviour
    {
        private static MainMenuManager instance;

        //public bool IsLobbyOwner = false;

        [SerializeField] private GameObject menuScreen, lobbyScreen, joinScreen;
        [SerializeField] private TMP_InputField lobbyInput;

        [SerializeField] private TextMeshProUGUI lobbyTitle, lobbyIDText, lobbyPlayerCount, lobbyChat;
        [SerializeField] private Button startGameButton;
        [SerializeField] private TextMeshProUGUI[] playerRankList = new TextMeshProUGUI[4];
        [SerializeField] private TextMeshProUGUI[] playerTextList = new TextMeshProUGUI[4];

        [SerializeField] private PlayerSelector _playerSelector;

        //[SerializeField] private TextMeshProUGUI LobbyViewTestTitle;
        [SerializeField] private Button joinButton_Prefab;
        //[SerializeField] private TextMeshProUGUI playerCount;
        [SerializeField] private GameObject blockPrefab;
        [SerializeField] private GameObject blockParent;

        [SerializeField] private bool developmentMode;
        private void Awake() => instance = this;

        private void Start()
        {
            OpenMainMenu();
        }

        public void CreateLobby()
        {
            Debug.Log("clicked");
            BootstrapManager.CreateLobby();
            //instance.IsLobbyOwner = true;
        }

        public static void OpenMainMenu()
        {
            CloseAllScreens();
            instance.menuScreen.SetActive(true);
            Cursor.visible = true;
        }

        public void OpenLobby()
        {
            CloseAllScreens();
            instance.lobbyScreen.SetActive(true);
        }


        public void OpenJoin()
        {
            CloseAllScreens();
            instance.joinScreen.SetActive(true);
            BootstrapManager.FindLobbies();
            
        }

        public static string GetPersona()
        {
            return SteamFriends.GetPersonaName();
        }

        public static void ShowLobbies(string title, string count, CSteamID id)
        {
            GameObject newBlock = Instantiate(instance.blockPrefab, instance.blockParent.transform);
            Button newButton = Instantiate(instance.joinButton_Prefab, newBlock.transform);
            // LobbyElement setup = newBlock.GetComponent<LobbyElement>();
            // setup.AssignButton(newButton);
            // setup.JoinButton.transform.GetChild(1).name = id.ToString();
            // setup.TitleText.text = title;
            // setup.CountText.text = count;
            newButton.onClick.AddListener(() => JoinLobby(newButton));

        }

        public static void LobbyEntered(string lobbyName, bool isHost)
        {
            instance.lobbyTitle.text = lobbyName;
            instance.startGameButton.gameObject.SetActive(isHost);
            instance.lobbyIDText.text = BootstrapManager.CurrentLobbyID.ToString();
            instance.OpenLobby();
        }

        static void CloseAllScreens()
        {
            instance.menuScreen.SetActive(false);
            instance.lobbyScreen.SetActive(false);
            instance.joinScreen.SetActive(false);
        }

        public static void JoinLobby(Button button)
        {
            Debug.Log("button clicked: " + button.name);
            CSteamID steamID = new(Convert.ToUInt64(button.transform.GetChild(1).name));
            BootstrapManager.JoinByID(steamID);
        }

        public void LeaveLobby()
        {
            BootstrapManager.LeaveLobby();
            OpenMainMenu();
        }

        public void StartGame()
        {
            string[] scenesToClose = new string[] { "Main Menu" };
            BootstrapNetworkManager.ChangeNetworkScene("SampleScene", scenesToClose);
        }

        public static void UpdateLobbyPlayerCount(int count)
        {
            instance.lobbyPlayerCount.text = count.ToString() + "/4";
            //Debug.Log("big city cleaner");
        }

        public static void UpdateLobbyPlayerText(int index, string rank, string persona)
        {
            //        Debug.Log("updating text for index: " + index);
            instance.playerRankList[index].text = rank;
            instance.playerTextList[index].text = persona;
        }

        public static void UpdateLobbyChat(string newText)
        {
            instance.lobbyChat.text += newText;
        }

        public static int GetSelectedPlayer()
        {
            return instance._playerSelector.currentPlayerType;
        }

        public void QuitGame()
        {
            Debug.Log("QUIT");
            BootstrapManager.QuitGame();
        }

    }
}