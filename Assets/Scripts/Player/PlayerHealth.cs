using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Steamworks;

namespace Player
{
    public class PlayerHealth : NetworkBehaviour
    {
        // --- SyncVars for health, alive state, and lives ---
        public readonly SyncVar<float> Health         = new();
        public readonly SyncVar<bool>  Alive          = new();
        public readonly SyncVar<int>   LivesRemaining = new();

        [Header("Initial Settings")]
        [SerializeField] private float StartingHealth = 100f;
        [SerializeField] private int   StartingLives  = 3;

        [Header("References")]
        public Transform spawnPoint;
        public ClientMenuManager CMM;
        [SerializeField] private BazigPlayerMotor BPM;
        [SerializeField] private GameObject bloodEffect;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (IsServer)
            {
                Health.Value         = StartingHealth;
                LivesRemaining.Value = StartingLives;
                Alive.Value          = true;
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            Health.OnChange += OnHealthChanged;
            LivesRemaining.OnChange += OnLivesChanged;
            Alive.OnChange += OnAliveChanged;
        }

        private void OnHealthChanged(float previous, float current, bool asServer)
        {
            // Always show HUD
            ShowDamageHUD();
            ShowHealth(current);

            // Show damage intensity on HUD
            if (CMM != null && StartingHealth > 0f)
            {
                float intensity = 1f - (current / StartingHealth);
                CMM.ShowDamage(intensity);
            }

            // Handle death when health falls to zero
            if (previous > 0f && current <= 0f)
            {
                // Mark dead and decrement lives
                Alive.Value = false;
                LivesRemaining.Value = Mathf.Max(0, LivesRemaining.Value - 1);

                // Notify local UI
                CMM.KilledBy = SteamFriends.GetPersonaName();
                CMM.ShowRespawnMenu();
                CMM.dead = true;

                // Optionally disable movement
                if (BPM != null)
                    BPM.enabled = false;

                // Move to spawn point after delay or on respawn RPC
            }
        }

        private void OnLivesChanged(int previous, int current, bool asServer)
        {
            Debug.Log($"Lives changed from {previous} to {current}");
        }

        private void OnAliveChanged(bool previous, bool current, bool asServer)
        {
            if (current)
            {
                // Player just respawned: reposition, restore movement
                if (spawnPoint != null && IsClient)
                    transform.root.position = spawnPoint.position;
                if (BPM != null)
                    BPM.enabled = true;
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void RespawnServerRpc()
        {
            if (LivesRemaining.Value < 1)
                return;
            // Restore state
            Health.Value = StartingHealth;
            Alive.Value = true;
            LivesRemaining.Value--;
            // Notify clients
            RespawnObserversRpc(Health.Value, LivesRemaining.Value);
        }

        [ObserversRpc]
        private void RespawnObserversRpc(float newHealth, int newLives)
        {
            Debug.LogWarning($"{SteamFriends.GetPersonaName()} has respawned with {newHealth} HP and {newLives} lives left.");
        }

        // Called by server on hit
        public void DealDamage(float damage)
        {
            if (!IsServer || !Alive.Value)
                return;
            Health.Value -= damage;
            // VFX
            TakeDamageObserversRpc(Health.Value, damage);
        }

        [ObserversRpc(RunLocally = true)]
        private void TakeDamageObserversRpc(float newHealth, float damage)
        {
            Debug.Log($"[Damage] New health: {newHealth} (took {damage})");
            Vector3 offset = new Vector3(
                Random.Range(-0.5f, 0.5f),
                Random.Range(-0.5f, 0.5f),
                Random.Range(-0.5f, 0.5f)
            );
            GameObject blood = Instantiate(bloodEffect, transform.position + offset, Quaternion.identity);
            Destroy(blood, 2f);
        }

        private void ShowDamageHUD() { /* TODO */ }

        [ObserversRpc(RunLocally = true)]
        private void ShowHealth(float health)
        {
            Debug.LogWarning($"[Health HUD] {gameObject.name}: {health} HP");
        }
    }
}
