using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System;
namespace Player {
    public class PlayerHealth : NetworkBehaviour
    {

        public readonly SyncVar<float> _health = new();


        public override void OnStartClient()
        {
            base.OnStartClient();
            _health.OnChange += OnHealthChanged;
        }

        public void OnHealthChanged(float previous, float current, bool asServer)
        {
            Debug.Log($"onhealthchanged from {previous} to {current}");
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
                _health.Value = 25f;
            }
        }
        public void DealDamage(PlayerHealth opponentHealth, float damage)
        {
            if(!IsServerStarted)
            return;
            Debug.LogWarning("dealt damage: " + damage);
            opponentHealth._health.Value -= damage;
        }

        [ObserversRpc]
        public void TakeDamageObservers(float damage)
        {

        }
    }
}