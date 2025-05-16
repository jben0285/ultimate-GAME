using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System;
using Steamworks;

namespace Player {
    public class PlayerHealth : NetworkBehaviour
    {

        public readonly SyncVar<float> _health = new();

        public readonly SyncVar<bool> Alive = new();

        public readonly SyncVar<int> _livesRemaning = new();

        [SerializeField]
        private float StartingHealth;


        public Transform spawnPoint;

        public ClientMenuManager CMM;
        [SerializeField]
        private BazigPlayerMotor BPM;

        [SerializeField]
        private GameObject bloodEffect;
        public override void OnStartClient()
        {
            base.OnStartClient();
            _health.OnChange += OnHealthChanged;
            _livesRemaning.OnChange += OnLivesChanged;
        }

        private void OnLivesChanged(int prev, int next, bool asServer)
        {
            Debug.Log($"Lives changed from {prev} to {next}");
        }
        [ObserversRpc]
        public void OnLivesChangedObserver(PlayerHealth ownerHealth, int lives)
        {

        }

        public void OnHealthChanged(float previous, float current, bool asServer)
        {
            ShowDamageHUD();
        }

        private void ShowDamageHUD()
        {
            //TODO: show damage hud
        }

        [ServerRpc]
        public void RespawnServer(PlayerHealth health)
        {
            if(health._livesRemaning.Value < 1)
            {
                return;
            }
            health._livesRemaning.Value--;
           
            health._health.Value = health.StartingHealth;
            health.transform.root.position = spawnPoint.position;
            health.BPM.enabled = true;

        }
        [ObserversRpc]
        public void RespawnObservers(PlayerHealth health)
        {
            Debug.LogWarning($"{SteamFriends.GetPersonaName()} has respawned!");
        }

        [ObserversRpc(RunLocally = true)]
        public void ShowHealth(float health)
        {
            Debug.LogWarning($"health of {gameObject.name} is: " + health);
        }
        // Update is called once per frame
        void Update()
        {
            
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if(base.IsServerStarted)
            {
                _health.Value = StartingHealth;
            }
        }
        public void DealDamage(PlayerHealth opponentHealth, float damage, string shotBy)
        {
            TakeDamage(opponentHealth, damage, shotBy);
        }

        public void TakeDamage(PlayerHealth ownerHealth, float damage, string shotBy)
        {
            if(!IsServerStarted)
            return;
            if(Alive.Value)
            {
                return;
            }
            _health.Value -= damage;
            if(_health.Value <= 0)
            {
            ownerHealth.CMM.KilledBy = shotBy;
            ownerHealth.CMM.ShowRespawnMenu();
            ownerHealth.CMM.dead = true;
            }
            TakeDamageObservers(ownerHealth, damage);
        }
        

        [ObserversRpc(RunLocally = true)]
        public void TakeDamageObservers(PlayerHealth ownerHealth, float damage)
        {
            Debug.Log("health: " + _health.Value);

            Vector3 randomOffset = new Vector3(UnityEngine.Random.Range(-0.5f, 0.5f), UnityEngine.Random.Range(-0.5f, 0.5f), UnityEngine.Random.Range(-0.5f, 0.5f));
            GameObject blood = GameObject.Instantiate(bloodEffect, transform.position + randomOffset, Quaternion.identity);
            Destroy(blood, 2f);
        }
    }
}