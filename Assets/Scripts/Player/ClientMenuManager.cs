using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Bootstrap;
using TMPro;

namespace Player
{
    public class ClientMenuManager : MonoBehaviour
    {
        private static ClientMenuManager instance;

        [SerializeField] private GameObject _scopeImage;
        //assigned by bootstrap network manager
        public PlayerHealth _health;
        public bool InMenu;

        public bool dead;


        
        //public PlayerFireController fireCont;
       // PlayerFireController.FireState prevState;
        [SerializeField] Button ResumeButton, LeaveButton, RespawnButton;
        [SerializeField]
        private TextMeshProUGUI RespawnText;
        [SerializeField] GameObject ClientMenu;

        [SerializeField] GameObject RespawnMenu;

        public string KilledBy;

        private void Awake() => instance = this;

        // Start is called before the first frame update
        void Start()
        {
            _scopeImage.SetActive(false);
        }

        // Update is called once per frame
        void Update()
        {
            if (!InMenu && Input.GetKeyDown(KeyCode.Escape))
            {
                ShowMenu();
            }
            else if (InMenu && Input.GetKeyDown(KeyCode.Escape))
            {
                HideMenu_Click();
            }
            if(_health != null)
            {
                _health.CMM = this;
            }
        }

        private void ShowMenu()
        {
           // Debug.LogWarning("SUCK ME SILLY" + fireCont.gameObject.name);
            ClientMenu.SetActive(true);
            InMenu = true;
            // if (!fireCont.UIBlocker.Contains("Menu"))
            //     fireCont.UIBlocker.Add("Menu");
            Cursor.visible = true;
        }

        public void HideMenu_Click()
        {
            if (!InMenu)
                return;
            ClientMenu.SetActive(false);
            InMenu = false;
            // if (fireCont.UIBlocker.Contains("Menu"))
            //     fireCont.UIBlocker.Remove("Menu");

            Cursor.visible = false;

        }

        public void LeaveGame_Click()
        {
            //string[] scenesToClose = new string[] { "Scne" };
            //BootstrapNetworkManager.ChangeNetworkScene("Main men", scenesToClose, true);
            //true as in in game
            BootstrapManager.LeaveLobby(true);
            Cursor.visible = true;
        }

        public void ShowRespawnMenu()
        {
            RespawnText.text = "You were killed by: " + KilledBy;
            RespawnMenu.SetActive(true);
            dead = false;
        }

        public void Respawn_Click()
        {
            RespawnMenu.SetActive(false);
            dead = false;
            //looks confusing
            _health.RespawnServer(_health);
        }

        public void ToggleScope(bool toggle)
        {
            _scopeImage.SetActive(toggle);
        }
    }
}