using FishNet;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace Weapons {
    /// <summary>
    /// A projectile that uses client-side prediction with reconciliation across server and clients.
    /// </summary>
    public class PredictedProjectileController : NetworkBehaviour
    {
        public struct LiveFireData
        {
            public Vector3 position;
            public Vector3 direction;
            public float speed;
            public float lifeTime;
            public float damage;
        }

        // Reconcile data carries authoritative position
        public struct ProjectileReconcileData : IReconcileData
        {
            public Vector3 Position;
            private uint _tick;
            public ProjectileReconcileData(Vector3 position)
            {
                Position = position;
                _tick = 0u;
            }
            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint v) => _tick = v;
        }

        // Empty input data: no client-driven input for the projectile
        private struct EmptyInput : IReplicateData
        {
            private uint _tick;
            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint v) => _tick = v;
        }

        public LiveFireData lfd;
        public LayerMask collisionMask;

        private bool alreadyHit = false;

        /// <summary>
        /// One-time initialization of projectile state.
        /// Call this immediately after spawning on the server.
        /// </summary>
        public void Initialize(Vector3 position, Vector3 direction, float speed, float lifeTime, float damage, LayerMask collisionMask)
        {
            lfd = new LiveFireData { position = position, direction = direction.normalized, speed = speed, lifeTime = lifeTime, damage = damage };
            this.collisionMask = collisionMask;
            transform.position = position;
            Destroy(gameObject, lifeTime);
        }

        private void Awake()
        {
            // nothing here; initialization happens in Initialize()
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            // Subscribe to tick events for prediction
            InstanceFinder.TimeManager.OnTick     += TimeManager_OnTick;
            InstanceFinder.TimeManager.OnPostTick += TimeManager_OnPostTick;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            InstanceFinder.TimeManager.OnTick     -= TimeManager_OnTick;
            InstanceFinder.TimeManager.OnPostTick -= TimeManager_OnPostTick;
        }

        private void TimeManager_OnTick()
        {
            // Simulate movement each tick via replicate
            Move(default);
        }

        private void TimeManager_OnPostTick()
        {
            if (!IsServerStarted) return;
            // Send authoritative position back after simulation
            CreateReconcile();
        }

        [Replicate]
        private void Move(EmptyInput data, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            // Advance along direction by speed * tick delta
            float delta = (float)InstanceFinder.TimeManager.TickDelta;
            Vector3 nextPos = transform.position + lfd.direction * lfd.speed * delta;

            // Check collision via raycast
            if (Physics.Raycast(transform.position, lfd.direction, out RaycastHit hit, lfd.speed * delta, collisionMask))
            {
                transform.position = hit.point;

                var health = hit.collider.GetComponentInParent<Player.PlayerHealth>();
                if (health != null)
                {
                    if(!alreadyHit)
                    {
                        alreadyHit = true;
                        health.DealDamage(lfd.damage, Steamworks.SteamFriends.GetPersonaName());
                    }
                }

                // Destroy across network
                if (IsServerStarted)
                    NetworkObject.Despawn();
            }
            else
            {
                transform.position = nextPos;
            }
        }

        public override void CreateReconcile()
        {
            var rd = new ProjectileReconcileData(transform.position);
            ReconcileState(rd);
        }

        [Reconcile]
        private void ReconcileState(ProjectileReconcileData data, Channel channel = Channel.Unreliable)
        {
            // Overwrite position to server's authoritative
            transform.position = data.Position;
        }
    }
}
