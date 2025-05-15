using FishNet;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;
using Bootstrap;
using Weapons;
using FishNet.Object.Synchronizing;
namespace Player
{
    public class BazigPlayerMotor : NetworkBehaviour
    {
        public enum PlayerType
        {
            Assault,
            Support,
            Sniper
        }

        [Header("Player Type")]
        [SerializeField] private PlayerType _playerType;

        #region Serialized Fields & Config
        [Header("Input")]
        [SerializeField] private InputActionAsset _inputActionAsset;

        [Header("Movement Settings")]
        [SerializeField] private float _moveForce       = 30f;
        [SerializeField] private float _jumpForce       = 12f;
        [SerializeField] private float _abilityForce    = 12f;
        [SerializeField] private float _lookSensitivity = 0.1f;
        [SerializeField] private bool useRPCYawSync;

        [Header("Ability Settings")]
        [SerializeField] private int _abilityCooldownResetValue;
        [SerializeField] private int abilityCoolDownCounter;

        [SerializeField] private int _abilityDurationResetValue;
        [SerializeField] private int abilityDurationCounter;
        public bool _abilityActive;

        

        [Header("Ground Check")]
        [SerializeField] private Vector3 feetOffset;
        [SerializeField] private float   feetRadius;
        [SerializeField] private LayerMask groundLayers;

        [Header("References")]
        [SerializeField] private Transform           _headObject;
        [SerializeField] private GameObject          Visor;
        [SerializeField] private PredictionRigidbody _predictionRigidbody;

        public ClientMenuManager CMM;
        [SerializeField]
        private FirearmController _firearmController;
        [SerializeField]
        private PlayerHealth _playerHealth;

        [Header("Sprint Settings")]
        [SerializeField] private float _sprintMultiplier = 1.5f;
        [SerializeField] private int _sprintStaminaMax = 100;
        [SerializeField] private int _sprintStamina;
        [SerializeField] private int _sprintStaminaDrainPerTick = 2;
        [SerializeField] private int _sprintStaminaRegenPerTick = 1;
        private bool _isSprinting;
        #endregion

        // InputActions
        private InputAction _horizontalAction;
        private InputAction _verticalAction;
        private InputAction _jumpAction;
        private InputAction _abilityAction;
        private InputAction _pitchAction;
        private InputAction _yawAction;
        private InputAction _sprintAction;

        // Local look state
        private float _currentYaw     = 0f;
        private float _currentPitch   = 0f;
        private float _lastSentYaw    = 0f;
        private float _lastSentPitch  = 0f;
        private const float YawSyncThreshold = 1f;
        private const float PitchSyncThreshold = 1f;

        #region Movement Data Structs
        private struct MovementData : IReplicateData
        {
            public readonly float Horizontal;
            public readonly float Vertical;
            public readonly bool  Jump;
            public readonly bool  Ability;
            public readonly bool  Sprint;
            public readonly bool  SpeedBoost;
            public readonly int   SpeedBoostCounter;

            private uint _tick;
            public MovementData(float horizontal, float vertical, bool jump, bool ability, bool sprint, bool speedBoost, int speedBoostCounter)
            {
                Horizontal        = horizontal;
                Vertical          = vertical;
                Jump              = jump;
                Ability           = ability;
                Sprint            = sprint;
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
            public readonly int       AbilityCooldown;
            public readonly int       AbilityDuration;
            public readonly bool       AbilityActive;

            private uint _tick;
            public ReconcileData(PredictionRigidbody prb, bool speedBoost, int abilityCooldown, int abilityDuration, bool abilityActive)
            {
                PredictionRigidbody = prb;
                SpeedBoost          = speedBoost;
                AbilityCooldown     = abilityCooldown;
                AbilityDuration     = abilityDuration;
                AbilityActive       = abilityActive;
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
            _abilityAction    = map.FindAction("Ability");
            _pitchAction      = map.FindAction("Pitch");
            _yawAction        = map.FindAction("Yaw");
            _sprintAction     = map.FindAction("Sprint");

            _horizontalAction.Enable();
            _verticalAction.Enable();
            _jumpAction.Enable();
            _abilityAction.Enable();
            _pitchAction.Enable();
            _yawAction.Enable();
            _sprintAction.Enable();

            _sprintStamina = _sprintStaminaMax;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            InstanceFinder.TimeManager.OnTick     -= TimeManager_OnTick;
            InstanceFinder.TimeManager.OnPostTick -= TimeManager_OnPostTick;

            _horizontalAction.Disable();
            _verticalAction.Disable();
            _jumpAction.Disable();
            _abilityAction.Disable();
            _pitchAction.Disable();
            _yawAction.Disable();
            _sprintAction.Disable();
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
            bool ab = _abilityAction.IsPressed();
            bool sprint = _sprintAction.IsPressed();

            // speedBoost fields if you have them
            bool sb = false;
            int  sbc = 0;

            return new MovementData(h, v, groundJump, ab, sprint, sb, sbc);
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
            if (_predictionRigidbody == null) return;

            HandleAbilityReplicate(data.Ability, _predictionRigidbody);

            // Sprint logic
            float moveMultiplier = 1f;
            _isSprinting = false;
            if (data.Sprint && _sprintStamina > 0 && (Mathf.Abs(data.Horizontal) > 0.1f || Mathf.Abs(data.Vertical) > 0.1f))
            {
                moveMultiplier = _sprintMultiplier;
                _isSprinting = true;
                _sprintStamina -= _sprintStaminaDrainPerTick;
                if (_sprintStamina < 0) _sprintStamina = 0;
            }
            else
            {
                _sprintStamina += _sprintStaminaRegenPerTick;
                if (_sprintStamina > _sprintStaminaMax) _sprintStamina = _sprintStaminaMax;
            }

            if(_abilityActive)
            {
                switch (_playerType)
                {
                    case PlayerType.Assault:
                        // Implement logic for Assault player type
                        break;
                    case PlayerType.Support:
                        // Implement logic for Support player type
                        break;
                    case PlayerType.Sniper:
                        // Implement logic for Sniper player type
                        break;
                    default:
                        // Handle unexpected player type
                        break;
                }
                
            }
            Vector3 forward = transform.forward;
            Vector3 right   = transform.right;
            Vector3 force   = (forward * data.Vertical + right * data.Horizontal) * 25f * moveMultiplier;
            _predictionRigidbody.AddForce(force, ForceMode.Acceleration);

            if (data.Jump)
                _predictionRigidbody.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);

            _predictionRigidbody.AddForce(Physics.gravity, ForceMode.Acceleration);

            // Run the internal simulation step
            _predictionRigidbody.Simulate();
        }

        private void HandleAbilityReplicate(bool ability, PredictionRigidbody prb)
        {
            if (abilityCoolDownCounter < _abilityCooldownResetValue && !_abilityActive)
                abilityCoolDownCounter++;
            
            if(_abilityActive)
            {
                abilityDurationCounter--;
                if(abilityDurationCounter <= 0)
                {
                    _abilityActive = false;
                }
            }
            // 2) If they pressed the ability button AND they're off cooldown
            if (ability && abilityCoolDownCounter >= _abilityCooldownResetValue)
            {
                abilityCoolDownCounter = 0;  // reset
                ActivateAbility(_playerType);
            }
        }

        public override void CreateReconcile()
        {
            var rd = new ReconcileData(_predictionRigidbody, false, abilityCoolDownCounter, abilityDurationCounter, _abilityActive);
            ReconcileState(rd);
        }

        [Reconcile]
        private void ReconcileState(ReconcileData data, Channel channel = Channel.Unreliable)
        {
            //DO NOT REMOVE THIS ROTATION ASSIGNMENT.
            Quaternion savedBodyRot = transform.rotation;
            abilityCoolDownCounter = data.AbilityCooldown;
            abilityDurationCounter = data.AbilityDuration;
            _abilityActive = data.AbilityActive;
            _predictionRigidbody.Reconcile(
                data.PredictionRigidbody
            );
            transform.rotation = savedBodyRot;
        }

        private void ActivateAbility(PlayerType playerType)
        {
            _abilityActive = true;
            switch (playerType)
            {
                case PlayerType.Assault:
                    Debug.Log("Activating Assault ability");
                    
                   // _firearmController.ActivateAssaultAbility();
                    break;

                case PlayerType.Support:
                    // Implement the ability for Support type
                    Debug.Log("Activating Support ability");
                    // Example: Heal nearby allies
                    break;

                case PlayerType.Sniper:
                    // Implement the ability for Assasin type
                    Debug.Log("Activating Assasin ability");
                    // Example: Become invisible for a short duration
                    break;

                default:
                    Debug.LogWarning("Unknown player type");
                    break;
            }
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

            if(useRPCYawSync)
            {
                // Send to server for other clients (throttled)
                if (Mathf.Abs(_currentYaw - _lastSentYaw) > YawSyncThreshold)
                {
                    SendYawToServer(_currentYaw);
                    _lastSentYaw = _currentYaw;
                }
                if (Mathf.Abs(_currentPitch - _lastSentPitch) > PitchSyncThreshold)
                {
                    SendPitchToServer(_currentPitch);
                    _lastSentPitch = _currentPitch;
                }
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

        [ServerRpc]
        private void SendPitchToServer(float pitch)
        {
            Rpc_UpdatePitchOnObservers(pitch);
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void Rpc_UpdatePitchOnObservers(float pitch)
        {
            // Remote clients only: rotate that player's head to match
            _headObject.localEulerAngles = new Vector3(pitch, 0f, 0f);
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
