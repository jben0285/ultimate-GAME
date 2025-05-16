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


        [SerializeField] private Image damageOverlay;
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
            ShowDamage(0);
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
            //true as in in game
            BootstrapManager.LeaveLobby(true);
            Cursor.visible = true;
        }

        public void ShowRespawnMenu()
        {
            Debug.LogError("SHOW RESPAWN MENU");
            RespawnText.text = "You were killed by: " + KilledBy;
            RespawnMenu.SetActive(true);
            dead = false;
        }

        public void Respawn_Click()
        {
            RespawnMenu.SetActive(false);
            dead = false;
            ShowDamage(0);
            //looks confusing
            _health.RespawnServerRpc();
        }

        public void ToggleScope(bool toggle)
        {
            _scopeImage.SetActive(toggle);
        }

        // Call this when _health is assigned or changes
        public void OnHealthChanged(PlayerHealth newHealth)
        {
            _health = newHealth;
            if (_health != null)
            {
                _health.CMM = this;
            }
        }

        // Call this to show damage on the HUD with a given intensity
        public void ShowDamage(float intensity)
        {
            // TODO: Implement your HUD damage feedback logic here
            Debug.Log($"ShowDamage called with intensity: {intensity}");
            // Example: flash a red overlay, shake camera, etc.
        // Assuming you have a damage overlay GameObject or UI element
            // Adjust the alpha or intensity of the damage overlay based on the intensity parameter
            Color overlayColor = damageOverlay.color;
            overlayColor.a = Mathf.Clamp01(intensity); // Ensure alpha is between 0 and 1
            damageOverlay.color = overlayColor;
        }
    }
}