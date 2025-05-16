using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Steamworks;

namespace Player
{
    public class PlayerHealth : NetworkBehaviour
    {
        // --- SyncVars for health, alive state, and lives ---
        public readonly SyncVar<float> Health           = new();
        public readonly SyncVar<bool>  Alive            = new();
        public readonly SyncVar<int>   LivesRemaining   = new();

        [Header("Initial Settings")]
        [SerializeField] private float StartingHealth = 100f;
        [SerializeField] private int   StartingLives  = 3;

        [Header("References")]
        public Transform spawnPoint;
        public ClientMenuManager CMM;
        [SerializeField] private BazigPlayerMotor BPM;
        [SerializeField] private GameObject bloodEffect;

        // --- Setup initial server state ---
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

        // --- Subscribe to changes on clients ---
        public override void OnStartClient()
        {
            base.OnStartClient();
            Health.OnChange         += OnHealthChanged;
            LivesRemaining.OnChange += OnLivesChanged;
            Alive.OnChange          += OnAliveChanged;
        }

        private void OnHealthChanged(float previous, float current, bool asServer)
        {
            ShowDamageHUD();
            ShowHealth(current);
        }

        private void OnLivesChanged(int previous, int current, bool asServer)
        {
            Debug.Log($"Lives changed from {previous} to {current}");
        }

        private void OnAliveChanged(bool previous, bool current, bool asServer)
        {
            if (!current)
            {
                // Player just died
                // e.g. disable model, play death VFX, etc.
            }
            else
            {
                // Player just respawned
            }
        }

        // --- Public method to request a respawn ---
        [ServerRpc(RequireOwnership = false)]
        public void RespawnServerRpc()
        {
            if (LivesRemaining.Value < 1)
                return;

            // Decrement lives, restore health & alive state
            LivesRemaining.Value--;
            Health.Value = StartingHealth;
            Alive.Value  = true;

            // Move to spawn point
            if (spawnPoint != null)
                transform.root.position = spawnPoint.position;

            // Re-enable movement
            if (BPM != null)
                BPM.enabled = true;

            // Notify clients
            RespawnObserversRpc(Health.Value, LivesRemaining.Value);
        }

        [ObserversRpc]
        private void RespawnObserversRpc(float newHealth, int newLives)
        {
            Debug.LogWarning($"{SteamFriends.GetPersonaName()} has respawned with {newHealth} HP and {newLives} lives left.");
            // e.g. hide death UI
        }

        // --- Called by server-side projectile on hit ---
        public void DealDamage(float damage, string shotBy)
        {
            if (!IsServerStarted) return;
            TakeDamage(damage, shotBy);
        }

        private void TakeDamage(float damage, string shotBy)
        {
            if (!Alive.Value)
                return; // can't damage a dead player

            Health.Value -= damage;

            // Show blood/effects on all clients
            TakeDamageObserversRpc(Health.Value, damage);

            if (Health.Value <= 0f)
            {
                // Mark dead and decrement a life
                Alive.Value          = false;
                LivesRemaining.Value = Mathf.Max(0, LivesRemaining.Value - 1);

                // Show respawn UI locally
                CMM.KilledBy      = shotBy;
                CMM.ShowRespawnMenu();
                CMM.dead          = true;
            }
        }

        [ObserversRpc(RunLocally = true)]
        private void TakeDamageObserversRpc(float newHealth, float damage)
        {
            Debug.Log($"[Damage] New health: {newHealth} (took {damage})");
            // Spawn a blood splatter VFX
            Vector3 offset = new Vector3(
                Random.Range(-0.5f, 0.5f),
                Random.Range(-0.5f, 0.5f),
                Random.Range(-0.5f, 0.5f)
            );
            GameObject blood = Instantiate(bloodEffect, transform.position + offset, Quaternion.identity);
            Destroy(blood, 2f);
        }

        private void ShowDamageHUD()
        {
            // TODO: flash screen or update UI
        }

        [ObserversRpc(RunLocally = true)]
        private void ShowHealth(float health)
        {
            Debug.LogWarning($"[Health HUD] {gameObject.name}: {health} HP");
        }
    }
}
