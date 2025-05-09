using System;
using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// Network‑authoritative player motor that supports ground movement, jump and a client‑toggleable flight mode.
    /// Press <b>F</b> to enter/leave flight.
    /// </summary>
    public class BazigFlightPlayer : NetworkBehaviour
    {
        #region Inspector
        public ClientMenuManager CMM;
        [SerializeField] private GameObject Visor;

        [Header("Ground Movement")]
        [SerializeField] private float _moveSpeed       = 4f;
        [SerializeField] private float _lookSensitivity = 2f;
        [SerializeField] private float _jumpForce       = 5f;

        [Header("Flight")]
        [SerializeField] private float _flightSpeed        = 8f;
        [SerializeField] private float _maxFlightVelocity  = 8f;

        [Header("Misc Physics")]
        [SerializeField] private float groundCheckDistance = 0.1f;
        #endregion

        #region Runtime Fields
        // menu / UI
        private bool isCursorLocked;

        // movement state
        private bool  isFlying = false;
        private float accelerationCounter;
        private bool  speedBoost;
        private int   speedBoostCounter;

        // references
        [SerializeField] private PredictionRigidbody PredictionRigidbody;
        [SerializeField] private Transform PlayerHeadObject;
        [SerializeField] private Transform bulletSpawn;

        // constants
        private const float MovementThreshold        = 0.05f;
        private const float HighAccelerationThreshold = 100;
        private const float LowAccelerationThreshold  = 25;
        private const float MaxAccelerationCounter    = 200;
        #endregion

        #region Network Data Structs
        public struct ReplicateData : IReplicateData
        {
            public float HorizontalInput, VerticalInput;
            public float LookX, LookY;
            public float YawRotation;
            public float AccelerationCounter;
            public int   SpeedBoostCounter;
            public bool  SpeedBoost;
            public bool  Jump;
            public bool  IsFlying;

            public ReplicateData(float h, float v, float lx, float ly, float yaw, float acc, bool sb, int sbc, bool jump, bool fly)
            {
                HorizontalInput     = h;
                VerticalInput       = v;
                LookX               = lx;
                LookY               = ly;
                YawRotation         = yaw;
                AccelerationCounter = acc;
                SpeedBoost          = sb;
                SpeedBoostCounter   = sbc;
                Jump                = jump;
                IsFlying            = fly;
                _tick = 0;
            }

            private uint _tick;
            public void Dispose() {}
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        public struct ReconcileData : IReconcileData
        {
            public PredictionRigidbody PredictionRigidbody;
            public Vector3   Position;
            public Quaternion Rotation;
            public float     AccelerationCounter;
            public bool      SpeedBoost;
            public int       SpeedBoostCounter;
            public bool      IsFlying;

            public ReconcileData(PredictionRigidbody pr, Vector3 pos, Quaternion rot, float acc, bool sb, int sbc, bool fly)
            {
                PredictionRigidbody = pr;
                Position            = pos;
                Rotation            = rot;
                AccelerationCounter = acc;
                SpeedBoost          = sb;
                SpeedBoostCounter   = sbc;
                IsFlying            = fly;
                _tick = 0;
            }

            private uint _tick;
            public void Dispose() {}
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }
        #endregion

        #region Unity / Fish‑Net hooks
        private void Awake()
        {
            PredictionRigidbody = ObjectCaches<PredictionRigidbody>.Retrieve();
            PredictionRigidbody.Initialize(GetComponent<Rigidbody>());
        }

        public override void OnStartNetwork()
        {
            InstanceFinder.TimeManager.OnTick     += TimeManager_OnTick;
            InstanceFinder.TimeManager.OnPostTick += TimeManager_OnPostTick;
        }

        public override void OnStopNetwork()
        {
            if (InstanceFinder.TimeManager != null)
            {
                InstanceFinder.TimeManager.OnTick     -= TimeManager_OnTick;
                InstanceFinder.TimeManager.OnPostTick -= TimeManager_OnPostTick;
            }
        }

        private void OnDestroy() => ObjectCaches<PredictionRigidbody>.StoreAndDefault(ref PredictionRigidbody);

        public override void OnStartClient()
        {
            base.PredictionManager.OnPreReplicateReplay += PredictionManager_OnPreReplicateReplay;
            LockCursor();
            if (IsOwner) Visor.layer = LayerMask.NameToLayer("TransparentFX");
        }

        public override void OnStartServer()
        {
            base.PredictionManager.OnPreReplicateReplay -= PredictionManager_OnPreReplicateReplay;
        }
        #endregion

        #region Tick Cycle
        private void TimeManager_OnTick()
        {
            ReplicateData md = CreateReplicateData();
            Move(md);
            UpdateAccelerationCounter(md);

            if (Input.GetKeyDown(KeyCode.Escape))
                if (isCursorLocked) UnlockCursor(); else LockCursor();
        }

        private void TimeManager_OnPostTick() => CreateReconcile();
        #endregion

        #region Replication helpers
        public override void CreateReconcile()
        {
            ReconcileData rd = new ReconcileData(PredictionRigidbody, transform.position, transform.rotation,
                                                  accelerationCounter, speedBoost, speedBoostCounter, isFlying);
            ReconcileState(rd);
        }

        private ReplicateData CreateReplicateData()
        {
            if (!IsOwner) return default;

            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            float lookX = Input.GetAxis("Mouse X");
            float lookY = Input.GetAxis("Mouse Y");
            bool  jump  = Input.GetKey(KeyCode.Space);

            if (Input.GetKeyDown(KeyCode.F)) isFlying = !isFlying;

            float yaw = transform.eulerAngles.y + lookX;

            return new ReplicateData(h, v, lookX, lookY, yaw,
                                     accelerationCounter, speedBoost, speedBoostCounter,
                                     jump, isFlying);
        }
        #endregion

        #region Movement (Replicate)
        [Replicate]
        private void Move(ReplicateData d, ReplicateState s = ReplicateState.Invalid, Channel ch = Channel.Unreliable)
        {
            if (PredictionRigidbody == null) return;

            // yaw
            transform.rotation = Quaternion.Euler(0, d.YawRotation, 0);

            if (d.IsFlying)
            {
                // ─── FLY MODE ──────────────────────────────────────
                PredictionRigidbody.Rigidbody.useGravity = false;

                Vector3 thrust = transform.forward * d.VerticalInput +
                                 transform.right   * d.HorizontalInput +
                                 Vector3.up        * (d.Jump ? 1f : (Input.GetKey(KeyCode.LeftControl) ? -1f : 0f));
                if (thrust.sqrMagnitude > 1f) thrust.Normalize();
                PredictionRigidbody.AddForce(thrust * _flightSpeed, ForceMode.VelocityChange);

                // clamp velocity
                Vector3 vel = PredictionRigidbody.Rigidbody.linearVelocity;
                if (vel.magnitude > _maxFlightVelocity)
                    PredictionRigidbody.Rigidbody.linearVelocity = vel.normalized * _maxFlightVelocity;
            }
            else
            {
                // ─── GROUND MODE ───────────────────────────────────
                PredictionRigidbody.Rigidbody.useGravity = true;

                Vector3 force = (transform.forward * d.VerticalInput + transform.right * d.HorizontalInput) * _moveSpeed;
                PredictionRigidbody.AddForce(force, ForceMode.VelocityChange);

                if (IsGrounded() && d.Jump)
                    PredictionRigidbody.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);

                PredictionRigidbody.AddForce(Physics.gravity, ForceMode.Acceleration);
            }

            // aim object matches camera pitch
            bulletSpawn.rotation = PlayerHeadObject.rotation;

            if (IsServerStarted && !IsHostStarted)
                BroadcastYawRotation(d.YawRotation);

            PredictionRigidbody.Simulate();
        }
        #endregion

        #region Rotation Broadcast
        [ServerRpc] private void BroadcastYawRotationHost(float y) => BroadcastYawRotationHostObserver(y);
        [ObserversRpc(ExcludeServer = true)] private void BroadcastYawRotationHostObserver(float y) => transform.rotation = Quaternion.Euler(0, y, 0);
        [ObserversRpc(RunLocally = true)] private void BroadcastYawRotation(float y) { if (!IsOwner) transform.rotation = Quaternion.Euler(0, y, 0); }
        #endregion

        #region Reconcile
        [Reconcile]
        private void ReconcileState(ReconcileData d, Channel ch = Channel.Unreliable)
        {
            if (IsOwner && IsServerStarted) return; // host authority

            if (IsOwner)
            {
                float diff = Quaternion.Angle(transform.rotation, d.Rotation);
                if (diff > 1f) transform.rotation = d.Rotation;
            }
            else transform.rotation = d.Rotation;

            PredictionRigidbody.Reconcile(d.PredictionRigidbody);
            accelerationCounter = d.AccelerationCounter;
            speedBoostCounter   = d.SpeedBoostCounter;
            isFlying            = d.IsFlying;
        }
        #endregion

        #region Helpers • Ground vs Fly
        private bool IsGrounded() => Physics.Raycast(transform.position, Vector3.down, groundCheckDistance);

        // acceleration logic retained from original script ----------------
        private bool IsTankMoving(float m)  => Math.Abs(m) >= MovementThreshold;
        private bool IsTankTurning(float t) => Math.Abs(t) >= MovementThreshold;

        private void ApplyTurningPenalty(float tInput, bool sb) { if (IsTankTurning(tInput)) DecreaseAccelerationCounter(sb); else IncreaseAccelerationCounter(sb); }
        private void DecreaseAccelerationCounter(bool sb)
        {
            if (accelerationCounter <= 0) return;
            if (accelerationCounter > HighAccelerationThreshold)      accelerationCounter -= sb ? .15f : .25f;
            else if (accelerationCounter > LowAccelerationThreshold)  accelerationCounter--;
        }
        private void IncreaseAccelerationCounter(bool sb)
        {
            if (accelerationCounter < MaxAccelerationCounter)
                accelerationCounter += sb ? .35f : .1f;
        }
        private void UpdateAccelerationCounter(ReplicateData d)
        {
            if (IsTankMoving(d.HorizontalInput)) ApplyTurningPenalty(d.VerticalInput, d.SpeedBoost);
            else accelerationCounter = LowAccelerationThreshold;
        }
        #endregion

        #region Cursor
        private void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            isCursorLocked = true;
        }
        private void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            isCursorLocked = false;
        }
        #endregion

        #region Prediction Replay Stub
        private void PredictionManager_OnPreReplicateReplay(uint clientTick, uint serverTick) { /* intentionally empty for gravity bypass example */ }
        #endregion
    }
}
