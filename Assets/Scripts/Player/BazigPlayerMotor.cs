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
using Microsoft.Unity.VisualStudio.Editor;
using TMPro;
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
        [SerializeField] private GameObject          _graphicalWeapon;

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
        
        // Double jump fields
        [Header("Double Jump Settings")]
        [SerializeField] private int _maxJumps = 2;
        private int _jumpsPerformed = 0;
        private int _doubleJumpTickCounter = 0;
        //seems to be a good amount of time to wait for the double jump
        private const int DoubleJumpTickDelay = 7;

        [Header("Weapon Spread Settings")]
        [SerializeField] private float _defaultWeaponSpread = 1.0f;
        [SerializeField] private float _sniperWeaponSpread = 0.0f;
        #endregion

        // InputActions
        private InputAction _horizontalAction;
        private InputAction _verticalAction;
        private InputAction _jumpAction;
        private InputAction _abilityAction;
        private InputAction _pitchAction;
        private InputAction _yawAction;
        private InputAction _sprintAction;
        private InputAction _aimAction;

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
            public readonly int   JumpsPerformed;
            public readonly int   DoubleJumpTickCounter;
            public readonly bool  Aim;

            private uint _tick;
            public MovementData(float horizontal, float vertical, bool jump, bool ability, bool sprint, bool speedBoost, int speedBoostCounter, int jumpsPerformed, int doubleJumpTickCounter, bool aim)
            {
                Horizontal        = horizontal;
                Vertical          = vertical;
                Jump              = jump;
                Ability           = ability;
                Sprint            = sprint;
                SpeedBoost        = speedBoost;
                SpeedBoostCounter = speedBoostCounter;
                JumpsPerformed    = jumpsPerformed;
                DoubleJumpTickCounter = doubleJumpTickCounter;
                Aim               = aim;
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
            public readonly bool      AbilityActive;
            public readonly int       JumpsPerformed;
            public readonly int       DoubleJumpTickCounter;

            private uint _tick;
            public ReconcileData(PredictionRigidbody prb, bool speedBoost, int abilityCooldown, int abilityDuration, bool abilityActive, int jumpsPerformed, int doubleJumpTickCounter)
            {
                PredictionRigidbody = prb;
                SpeedBoost          = speedBoost;
                AbilityCooldown     = abilityCooldown;
                AbilityDuration     = abilityDuration;
                AbilityActive       = abilityActive;
                JumpsPerformed      = jumpsPerformed;
                DoubleJumpTickCounter = doubleJumpTickCounter;
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
            _aimAction        = map.FindAction("Aim");

            _horizontalAction.Enable();
            _verticalAction.Enable();
            _jumpAction.Enable();
            _abilityAction.Enable();
            _pitchAction.Enable();
            _yawAction.Enable();
            _sprintAction.Enable();
            _aimAction.Enable();

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
            _aimAction.Disable();
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
            bool isGrounded = IsGrounded(out _);
            if (isGrounded)
            {
                _jumpsPerformed = 0;
                _doubleJumpTickCounter = 0;
            }
            // Only allow jump if jump key was pressed this frame and we have jumps left
            bool jumpPressed = _jumpAction.WasPressedThisFrame();
            bool canJump = false;
            if (jumpPressed && _jumpsPerformed < _maxJumps)
            {
                if (_jumpsPerformed == 0)
                {
                    canJump = true;
                }
                else if (_doubleJumpTickCounter >= DoubleJumpTickDelay)
                {
                    canJump = true;
                }
            }
            bool ab = _abilityAction.IsPressed();
            bool sprint = _sprintAction.IsPressed();
            bool aim = _aimAction.IsPressed();

            // speedBoost fields if you have them
            bool sb = false;
            int  sbc = 0;

            return new MovementData(h, v, canJump, ab, sprint, sb, sbc, _jumpsPerformed, _doubleJumpTickCounter, aim);
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

            bool isGrounded = IsGrounded(out _);
            if (isGrounded)
            {
                _jumpsPerformed = 0;
                _doubleJumpTickCounter = 0;
            }
            else if (_jumpsPerformed > 0)
            {
                _doubleJumpTickCounter++;
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
            Vector3 force   = (forward * data.Vertical + right * data.Horizontal) * _moveForce * moveMultiplier;
            _predictionRigidbody.AddForce(force, ForceMode.Acceleration);

            // Multi-jump logic
            if (data.Jump && data.JumpsPerformed < _maxJumps)
            {
                _predictionRigidbody.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
                _jumpsPerformed++;
                if (_jumpsPerformed > 1)
                    _doubleJumpTickCounter = 0;
            }

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
                    DeactivateAbility();
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
            var rd = new ReconcileData(_predictionRigidbody, false, abilityCoolDownCounter, abilityDurationCounter, _abilityActive, _jumpsPerformed, _doubleJumpTickCounter);
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
            _jumpsPerformed = data.JumpsPerformed;
            _doubleJumpTickCounter = data.DoubleJumpTickCounter;
            _predictionRigidbody.Reconcile(
                data.PredictionRigidbody
            );
            transform.rotation = savedBodyRot;
        }
        private void DeactivateAbility()
        {
            _abilityActive = false;
            abilityDurationCounter = _abilityDurationResetValue;
        switch (_playerType)
        {
            case PlayerType.Assault:
                Debug.Log("Deactivating Assault ability");
                // Add logic to deactivate Assault ability if needed
                RpcUpdateAssaultWeaponStats(6, 250);
                break;

            case PlayerType.Support:
                Debug.Log("Deactivating Support ability");
                // Add logic to deactivate Support ability if needed
                break;

            case PlayerType.Sniper:
                Debug.Log("Deactivating Sniper ability");
                _maxJumps = 2;
                RpcUpdateSniperWeaponStats(_defaultWeaponSpread);
                break;

            default:
                Debug.LogWarning("Unknown player type during deactivation");
                break;
        }
        
        }
        private void ActivateAbility(PlayerType playerType)
        {
            //do not activate ability if it is already active
            if(_abilityActive)
            {
                return;
            }
            _abilityActive = true;
            switch (playerType)
            {
                case PlayerType.Assault:
                    Debug.Log("Activating Assault ability");
                    RpcUpdateAssaultWeaponStats(4, 100);
                    // _firearmController.ActivateAssaultAbility();
                    break;

                case PlayerType.Support:
                    // Implement the ability for Support type
                    Debug.Log("Activating Support ability");
                    // Example: Heal nearby allies
                    break;

                case PlayerType.Sniper:
                    // Implement the ability for Assasin type
                    Debug.Log("Activating Sniper ability");
                    _maxJumps = 4;
                    RpcUpdateSniperWeaponStats(_sniperWeaponSpread);
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
            if(_playerType == PlayerType.Sniper)
            {
                HandleAimToggle();
                if (_firearmController != null)
                    _firearmController.spread = _sniperWeaponSpread;
            }
            else
            {
                if (_firearmController != null)
                    _firearmController.spread = _defaultWeaponSpread;
            }
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

        private bool _isAiming = false;
        private void HandleAimToggle()
        {
            // Toggle on button press (not hold)
            if (_aimAction.WasPressedThisFrame())
            {
                _isAiming = !_isAiming;
                if (_graphicalWeapon != null)
                    _graphicalWeapon.SetActive(!_isAiming);
                if (CMM != null)
                    CMM.ToggleScope(_isAiming);
                if (Camera.main != null)
                    Camera.main.fieldOfView = _isAiming ? 22f : 60f;
            }
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

        [ObserversRpc]
        private void RpcUpdateAssaultWeaponStats(int cycleReset, int fireReset)
        {
            if (_firearmController != null)
            {
                _firearmController.cycleCounterReset = cycleReset;
                _firearmController.fireCounterReset = fireReset;
            }
        }

        [ObserversRpc]
        private void RpcUpdateSniperWeaponStats(float spread)
        {
            if (_firearmController != null)
            {
                _firearmController.spread = spread;
            }
        }
    }
}
