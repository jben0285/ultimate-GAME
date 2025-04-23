using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Bootstrap;

namespace Player
{
    public class ClientMenuManager : MonoBehaviour
    {
        private static ClientMenuManager instance;

        public bool InMenu;

        //public PlayerFireController fireCont;
       // PlayerFireController.FireState prevState;
        [SerializeField] Button ResumeButton, LeaveButton;
        [SerializeField] GameObject ClientMenu;

        private void Awake() => instance = this;

        // Start is called before the first frame update
        void Start()
        {
            //instance.LeaveButton.onClick
            // .AddListener(() => BootstrapNetworkManager.ChangeNetworkScene("Main Men", list));

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
            BootstrapManager.LeaveLobby(true);
            Cursor.visible = true;
        }
    }
}