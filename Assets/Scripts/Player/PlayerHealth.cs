using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Steamworks;

namespace Player
{
    public class PlayerHealth : NetworkBehaviour
    {
        // SyncVars
        public readonly SyncVar<float> Health         = new();
        public readonly SyncVar<bool>  Alive          = new();
        public readonly SyncVar<int>   LivesRemaining = new();

        [Header("Settings")]
        [SerializeField] float StartingHealth = 100f;
        [SerializeField] int   StartingLives  = 3;

        [Header("Refs")]
        public Transform spawnPoint;
        public ClientMenuManager CMM;
        [SerializeField] BazigPlayerMotor BPM;
        [SerializeField] GameObject bloodEffect;

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
            Health.OnChange         += OnHealthChanged;
            LivesRemaining.OnChange += OnLivesChanged;
            Alive.OnChange          += OnAliveChanged;
        }

        private void OnHealthChanged(float prev, float curr, bool asServer)
        {
            // Only *this* player shows the damage HUD
            if (IsOwner)
            {
                // Numeric & overlay
                ShowHealth(curr);
                float intensity = 1f - (curr / StartingHealth);
                CMM?.ShowDamage(intensity);

                // If we just died:
                if (prev > 0f && curr <= 0f)
                {
                    Alive.Value = false;
                    LivesRemaining.Value = Mathf.Max(0, LivesRemaining.Value - 1);

                    CMM.KilledBy = SteamFriends.GetPersonaName();
                    CMM.ShowRespawnMenu();
                    CMM.dead = true;
                    BPM.enabled = false;
                }
            }
        }

        private void OnLivesChanged(int prev, int curr, bool asServer)
        {
            Debug.Log($"Lives: {prev}→{curr}");
        }

        private void OnAliveChanged(bool prev, bool curr, bool asServer)
        {
            // When coming back to life, respawn position & movement
            if (curr && IsOwner)
            {
                if (spawnPoint != null)
                    transform.root.position = spawnPoint.position;
                BPM.enabled = true;
            }
        }

        // Called on server when a projectile hits
        public void DealDamage(float damage)
        {
            if (!IsServer || !Alive.Value) return;
            Health.Value -= damage;
            // Blood effect on owner only
            TakeDamageObserversRpc(Health.Value, damage);
        }

        [ObserversRpc(RunLocally = true)]
        private void TakeDamageObserversRpc(float newHealth, float damage)
        {
            if (!IsOwner) return;
            Debug.Log($"[Damage] HP: {newHealth} (−{damage})");
            Vector3 off = Random.insideUnitSphere * 0.5f;
            var b = Instantiate(bloodEffect, transform.position + off, Quaternion.identity);
            Destroy(b, 2f);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RespawnServerRpc()
        {
            if (LivesRemaining.Value < 1) return;
            Health.Value = StartingHealth;
            Alive.Value  = true;
            RespawnObserversRpc(StartingHealth, LivesRemaining.Value);
        }

        [ObserversRpc]
        private void RespawnObserversRpc(float hp, int lives)
        {
            if (!IsOwner) return;
            CMM.ShowDamage(0f);
            Debug.Log($"Respawned with {hp} HP, {lives} lives.");
        }

        private void ShowHealth(float hp)
        {
            if (!IsOwner) return;
            Debug.Log($"[HUD] {gameObject.name} HP = {hp}");
        }
    }
}
