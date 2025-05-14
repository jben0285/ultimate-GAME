using FishNet;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;
using Bootstrap;
using FishNet.Object.Synchronizing;
namespace Player
{
    public class BazigPlayerMotor : NetworkBehaviour
    {


        #region Serialized Fields & Config
        [Header("Input")]
        [SerializeField] private InputActionAsset _inputActionAsset;

        [Header("Movement Settings")]
        [SerializeField] private float _moveForce       = 30f;
        [SerializeField] private float _jumpForce       = 12f;
        [SerializeField] private float _grappleForce    = 12f;
        [SerializeField] private float _lookSensitivity = 0.1f;

        [Header("Ground Check")]
        [SerializeField] private Vector3 feetOffset;
        [SerializeField] private float   feetRadius;
        [SerializeField] private LayerMask groundLayers;

        [Header("References")]
        [SerializeField] private Transform           _headObject;
        [SerializeField] private GameObject          Visor;
        [SerializeField] private PredictionRigidbody _predictionRigidbody;

        public ClientMenuManager CMM;
        #endregion

        // InputActions
        private InputAction _horizontalAction;
        private InputAction _verticalAction;
        private InputAction _jumpAction;
        private InputAction _grappleAction;
        private InputAction _pitchAction;
        private InputAction _yawAction;

        // Local look state
        private float _currentYaw     = 0f;
        private float _currentPitch   = 0f;
        private float _lastSentYaw    = 0f;
        private const float YawSyncThreshold = 0.1f;

        #region Movement Data Structs
        private struct MovementData : IReplicateData
        {
            public readonly float Horizontal;
            public readonly float Vertical;
            public readonly bool  Jump;
            public readonly bool  Grapple;

            public readonly bool  SpeedBoost;
            public readonly int   SpeedBoostCounter;

            private uint _tick;
            public MovementData(float horizontal, float vertical, bool jump, bool grapple, bool speedBoost, int speedBoostCounter)
            {
                Horizontal        = horizontal;
                Vertical          = vertical;
                Jump              = jump;
                Grapple           = grapple;
                SpeedBoost        = speedBoost;
                SpeedBoostCounter = speedBoostCounter;
                _tick             = 0u;
            }
            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        public struct ReconcileData : IReconcileData
        {
            public PredictionRigidbody PredictionRigidbody;
            public readonly bool       SpeedBoost;

            private uint _tick;
            public ReconcileData(PredictionRigidbody prb, bool speedBoost)
            {
                PredictionRigidbody = prb;
                SpeedBoost          = speedBoost;
                _tick               = 0u;
            }
            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }
        #endregion

        private void Awake()
        {
            // Grab a PredictionRigidbody from the cache
            _predictionRigidbody = ObjectCaches<PredictionRigidbody>.Retrieve();
            _predictionRigidbody.Initialize(GetComponent<Rigidbody>());
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            // Bind to FishNet's tick events
            InstanceFinder.TimeManager.OnTick     += TimeManager_OnTick;
            InstanceFinder.TimeManager.OnPostTick += TimeManager_OnPostTick;

            // Set up controls
            var map = _inputActionAsset.FindActionMap("Player");
            _horizontalAction = map.FindAction("Horizontal");
            _verticalAction   = map.FindAction("Vertical");
            _jumpAction       = map.FindAction("Jump");
            _grappleAction    = map.FindAction("Grapple");
            _pitchAction      = map.FindAction("Pitch");
            _yawAction        = map.FindAction("Yaw");

            _horizontalAction.Enable();
            _verticalAction.Enable();
            _jumpAction.Enable();
            _grappleAction.Enable();
            _pitchAction.Enable();
            _yawAction.Enable();
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            InstanceFinder.TimeManager.OnTick     -= TimeManager_OnTick;
            InstanceFinder.TimeManager.OnPostTick -= TimeManager_OnPostTick;

            _horizontalAction.Disable();
            _verticalAction.Disable();
            _jumpAction.Disable();
            _grappleAction.Disable();
            _pitchAction.Disable();
            _yawAction.Disable();
        }

        private void OnDestroy()
        {
            ObjectCaches<PredictionRigidbody>.StoreAndDefault(ref _predictionRigidbody);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner)
            {
                // Hide your own visor, parent the camera to your head, init look angles
                Visor.layer = LayerMask.NameToLayer("TransparentFX");
                Camera.main.transform.SetParent(_headObject, false);
                Camera.main.transform.localPosition = new Vector3(0, 0, 0);
                _currentYaw   = transform.eulerAngles.y;
                _currentPitch = _headObject.localEulerAngles.x;
            }
        }

        private void TimeManager_OnTick()
        {
            // Gather and send input data each tick
            MovementData md = CreateReplicateData();
            Move(md);
        }

        private void TimeManager_OnPostTick()
        {
            if (!IsServerStarted) return;
            // Send authoritative position & velocity—but NOT rotation
            CreateReconcile();
        }

        #region Input → Movement
        private MovementData CreateReplicateData()
        {
            if(!base.IsOwner)
            {
                return default;
            }
            float h = _horizontalAction.ReadValue<float>();
            float v = _verticalAction.ReadValue<float>();
            bool groundJump = _jumpAction.IsPressed() && IsGrounded(out _);
            bool grap = _grappleAction.IsPressed();

            // speedBoost fields if you have them
            bool sb = false;
            int  sbc = 0;

            return new MovementData(h, v, groundJump, grap, sb, sbc);
        }

        private bool IsGrounded(out RaycastHit hit)
        {
            Vector3 origin    = transform.position + feetOffset;
            Vector3 direction = Vector3.down;
            float   length    = feetRadius + 0.1f;
            bool    g         = Physics.Raycast(origin, direction, out hit, length, groundLayers);
            Debug.DrawRay(origin, direction * length, g ? Color.green : Color.red);
            return g;
        }
        #endregion

        [Replicate]
        private void Move(MovementData data, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            if(!base.IsOwner)
            {
                Debug.LogError("CLIENT ON REPLICATE");
            }
            Debug.Log($"[Replicate] on {(IsOwner? "Owner" : IsServer? "Server" : "Spectator")} tick {base.TimeManager.LocalTick} state={state}");

            // if (_predictionRigidbody == null) return;

            Vector3 forward = transform.forward;
            Vector3 right   = transform.right;
            Vector3 force   = (forward * data.Vertical + right * data.Horizontal) * 25f;
            _predictionRigidbody.AddForce(force, ForceMode.Acceleration);

            if (data.Jump)
                _predictionRigidbody.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);

            if (data.Grapple)
                _predictionRigidbody.AddForce(_headObject.forward * _grappleForce, ForceMode.Impulse);

            _predictionRigidbody.AddForce(Physics.gravity, ForceMode.Acceleration);

            // Run the internal simulation step
            _predictionRigidbody.Simulate();
        }

        public override void CreateReconcile()
        {
            var rd = new ReconcileData(_predictionRigidbody, false);
            ReconcileState(rd);
        }

        [Reconcile]
        private void ReconcileState(ReconcileData data, Channel channel = Channel.Unreliable)
        {
            Debug.Log($"[Reconcile] on {(IsOwner? "Owner" : IsServer? "Server" : "Spectator")} tick {base.TimeManager.LocalTick}");
            // Reconcile position & velocity, but leave rotation alone
            _predictionRigidbody.Reconcile(data.PredictionRigidbody);
        }

        #region Camera Look (Client-Only)
        private void Update()
        {

            if (!IsOwner) return;
            HandleMouseLook();
        }

        private void HandleMouseLook()
        {
            float mouseX = _yawAction.ReadValue<float>()   * _lookSensitivity;
            float mouseY = _pitchAction.ReadValue<float>() * _lookSensitivity;

            // Update yaw & pitch
            _currentYaw   += mouseX;
            _currentPitch  = Mathf.Clamp(_currentPitch - mouseY, -67.5f,  67.5f);

            // Apply locally
            transform.rotation = Quaternion.Euler(0f, _currentYaw, 0f);
            _headObject.localEulerAngles = new Vector3(_currentPitch, 0f, 0f);

            // Send to server for other clients (throttled)
            if (Mathf.Abs(_currentYaw - _lastSentYaw) > YawSyncThreshold)
            {
                SendYawToServer(_currentYaw);
                _lastSentYaw = _currentYaw;
            }
        }

        [ServerRpc]
        private void SendYawToServer(float yaw)
        {
            // Immediately broadcast out to all other observers
            Rpc_UpdateYawOnObservers(yaw);
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void Rpc_UpdateYawOnObservers(float yaw)
        {
            // Remote clients only: rotate that player's body to match
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
        #endregion

        void OnEnable()
        {
            BootstrapNetworkManager.OnGameStart += HandleGameStart;
        }

        void OnDisable()
        {
            BootstrapNetworkManager.OnGameStart -= HandleGameStart;
        }

        private void HandleGameStart()
        {
        }

    }
}
