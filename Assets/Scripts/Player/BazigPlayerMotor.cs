using System;
using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;


namespace Player
{
public class BazigPlayerMotor : NetworkBehaviour
{
    // Input fields
    [SerializeField]
    private InputActionAsset _inputActionAsset;

    private InputAction _horizontalAction;
    private InputAction _verticalAction;
    private InputAction _jumpAction;

    private InputAction _grappleAction;
    private InputAction _pitchAction;
    private InputAction _yawAction;

    // Serialized Fields for fine tuning movement
    private float _moveForce = 2f;
    private float _jumpForce = 2f;

    private float _grappleForce = 10f;
    private float _lookSensitivity = 0.1f;


    [SerializeField]
    private Vector3 feetOffset;

    [SerializeField]
    private float feetRadius;

    [SerializeField]
    private LayerMask groundLayers;

    [SerializeField]
    private Transform _headObject;

    [SerializeField]
    private GameObject Visor;

    //input values client side
    private float horizontal;
    private float vertical;
    private bool jump;

    private bool grapple;
    private float pitch;
    private float yaw;

    [SerializeField]
    private PredictionRigidbody _predictionRigidbody;

    // [SerializeField]
    // private Rigidbody _rigidBody;
    public ClientMenuManager CMM;

    [SerializeField]
    private PlayerHealth _health;

    /// <summary>
    /// move data structure. this holds all the data relevant to the user input
    /// </summary>
    private struct MovementData : IReplicateData
    {
        public readonly float Horizontal;
        public readonly float Vertical;

        public readonly float Yaw;

        public readonly float Pitch;
        public readonly float YawRotation; 

        public readonly int SpeedBoostCounter;

        public readonly bool Jump;
        

        public readonly bool Grapple;

        public readonly bool SpeedBoost;

        public MovementData(float horizontal, float vertical, float yaw, float pitch, float yawRotation, bool speedBoost, int speedBoostCounter, bool jump, bool grapple) // Updated to float
        {
            Horizontal = horizontal;
            Vertical = vertical;
            Yaw = yaw;
            Pitch = pitch;
            YawRotation = yawRotation;
            SpeedBoost = speedBoost;
            SpeedBoostCounter = speedBoostCounter;
            Jump = jump;
            Grapple = grapple;
            _tick = 0u;
        }


        //required by fishnet
        private uint _tick;
        //do not remove, used "under the hood"
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }

/// <summary>
///contains all the move data to reconcile (fix) with the server
/// </summary>
    public struct ReconcileData : IReconcileData
    {   
        public PredictionRigidbody PredictionRigidbody;
        // public Vector3 Position;
        // public Quaternion Rotation;

        // public Vector3 Velocity;
        // public Vector3 AngularVelocity;
        public bool SpeedBoost;

        public ReconcileData(PredictionRigidbody predictionRigidbody, /* Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity,*/
        bool speedBoost) // Updated to float
        {
            PredictionRigidbody = predictionRigidbody;
            // Position = position;
            // Rotation = rotation;
            // Velocity = velocity;
            // AngularVelocity = angularVelocity;
            // TurretAngle = turretAngle;
            // TurretRotation = turretRotation;
            SpeedBoost = speedBoost;
            _tick = 0;
        }

        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }
    // [SerializeField]
    // PlayerAudioController pac;

    //can be changed in inspector
    
    [SerializeField]
    private float _reverseMultiplier;


    [SerializeField]
    private bool _jump;


    public float engine;
    public bool repairing = false;

    [SerializeField]
    private Transform PlayerHeadObject;


    [SerializeField]
    private bool speedBoost;

    [SerializeField]
    private int speedBoostCounter = 0;

    [SerializeField]
    private Transform bulletSpawn;

    private const float MovementThreshold = 0.05f;


    [SerializeField]
    private bool isCursorLocked;

    private float _currentPitch = 0f;
    private float _currentYaw = 0f;
    private float _lastSentYaw = 0f;
    private const float YawSyncThreshold = 0.1f; // Only sync if yaw changes enough

    private void Awake()
    {
        //Debug.LogError("Awake");
        //instead of FixedUpdate, use Fishnet's timemanager to update each frame
        

        _predictionRigidbody = ObjectCaches<PredictionRigidbody>.Retrieve();
        _predictionRigidbody.Initialize(GetComponent<Rigidbody>());

    }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            InstanceFinder.TimeManager.OnTick += TimeManager_OnTick;
            InstanceFinder.TimeManager.OnPostTick += TimeManager_OnPostTick;
            
            _horizontalAction = _inputActionAsset.FindActionMap("Player").FindAction("Horizontal");
            _verticalAction = _inputActionAsset.FindActionMap("Player").FindAction("Vertical");
            _jumpAction = _inputActionAsset.FindActionMap("Player").FindAction("Jump");
            _grappleAction = _inputActionAsset.FindActionMap("Player").FindAction("Grapple");
            _pitchAction = _inputActionAsset.FindActionMap("Player").FindAction("Pitch");
            _yawAction = _inputActionAsset.FindActionMap("Player").FindAction("Yaw");
            
            _horizontalAction.Enable();
            _verticalAction.Enable();
            _jumpAction.Enable();
            _grappleAction.Enable();
            _pitchAction.Enable();
            _yawAction.Enable();

            _predictionRigidbody = ObjectCaches<PredictionRigidbody>.Retrieve();
            _predictionRigidbody.Initialize(GetComponent<Rigidbody>());

            _moveForce = 2f;
             _jumpForce = 2f;
             _grappleForce = 10f;
            _lookSensitivity = 0.1f;

        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (InstanceFinder.TimeManager != null)
            {
                InstanceFinder.TimeManager.OnTick -= TimeManager_OnTick;
                InstanceFinder.TimeManager.OnPostTick -= TimeManager_OnPostTick;
            }
            _horizontalAction.Disable();
            _verticalAction.Disable();
            _jumpAction.Disable();
            _grappleAction.Disable();
            _pitchAction.Disable();
            _yawAction.Disable();
        }
    private void LockCursor()
    {
        // Cursor.lockState = CursorLockMode.Locked; // Lock the cursor to the center
        // Cursor.visible = false; // Hide the cursor
        // isCursorLocked = true; // Update state
    }

    private void UnlockCursor()
    {
        // Cursor.lockState = CursorLockMode.None; // Free the cursor
        // Cursor.visible = true; // Show the cursor
        // isCursorLocked = false; // Update state
    }


    private void OnDestroy()
    {
        ObjectCaches<PredictionRigidbody>.StoreAndDefault(ref _predictionRigidbody);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        base.PredictionManager.OnPreReplicateReplay += PredictionManager_OnPreReplicateReplay;
        // LockCursor();
        if(base.IsOwner)
        {
            Visor.layer = LayerMask.NameToLayer("TransparentFX");
            Camera.main.transform.parent = _headObject;
            Camera.main.transform.SetPosition(true, Vector3.zero);
        }
    }


    public override void OnStartServer()
    {
        base.OnStartServer();
        base.PredictionManager.OnPreReplicateReplay -= PredictionManager_OnPreReplicateReplay;


    }

    /// <summary>
    /// Called every time any predicted object is replaying. Replays only occur for owner.
    /// Currently owners may only predict one object at a time.
    /// </summary>
    private void PredictionManager_OnPreReplicateReplay(uint clientTick, uint serverTick)
    {
        /* Server does not replay so it does
         * not need to add gravity. */
        if (!base.IsServerStarted) { }
    }


    

    

    //subscription to fishnet's built in update function. it overrides the update but only for this script
    private void TimeManager_OnTick()
    {
        MovementData moveData;


        //gather user input and put it into movedata "structure"
        moveData = CreateReplicateData();
        //move player using the data stored in movedata
        Move(moveData);
        // Refactored method call

        // Toggle cursor lock state with Escape key
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isCursorLocked)
                UnlockCursor();
            else
                LockCursor();
        }

    }

    
    //object's transform will be changed after the physics simulation,
    //so data after the simulation, which is POST tick
    private void TimeManager_OnPostTick()
    {
        if(!IsServerStarted)
        return;

        

        //EXTREMELY IMPORTANT to send anything that might affect the transform of an object!
        //this also includes any colliders attatched to the object
        //see video for full explanation

        /* Reconcile is sent during PostTick because we
             * want to send the rb data AFTER the simulation. */
       CreateReconcile();
        //in general, the "runningAsServer" parameter
        //should be set to true when: SERVER has (DATA) to send to => CLIENT
        //should be set to false when: CLIENT has (DATA) to send to => SERVER

    }

    

        public override void CreateReconcile()
        {
            ReconcileData rd = new ReconcileData(_predictionRigidbody, /*_rigidbody.linearVelocity, _rigidBody.position, _rigidBody.rotation, */  speedBoost); // Added speedBoostCounter
            ReconcileState(rd);
        }


    private MovementData CreateReplicateData()
    {
        if(!base.IsOwner)
        return default;
        horizontal = _horizontalAction.ReadValue<float>();
        vertical = _verticalAction.ReadValue<float>();
        jump = _jumpAction.IsPressed() && IsGrounded(out RaycastHit hitInfo);
        grapple = _grappleAction.IsPressed();
        pitch = _pitchAction.ReadValue<float>();
        yaw = _yawAction.ReadValue<float>();


        // Debug.Log($"Float Values: Horizontal Input: {horizontal},\n" + 
        //           $"Vertical Input: {vertical},\n" + 
        //           $"Look X: {yaw},\n" + 
        //           $"Look Y: {pitch}");
        // Return the data as new MoveData struct
        MovementData moveData = new MovementData(horizontal, vertical, yaw, pitch, -1f, this.speedBoost, this.speedBoostCounter, jump, grapple);
        return moveData;
    }

    private bool IsGrounded(out RaycastHit hitInfo)
    {
        // Define the ray starting point slightly above the player's feet
        Vector3 rayOrigin = transform.position + feetOffset;

        // Use the world down vector to ensure the ray points downwards
        Vector3 rayDirection = Vector3.down;
        
        // Define the ray length (adjust as needed)
        float rayLength = feetRadius + 0.1f; // Slightly longer than feetRadius to ensure contact

        // Perform the raycast and store the result in hitInfo
        bool isGrounded = Physics.Raycast(rayOrigin, rayDirection, out hitInfo, rayLength, groundLayers);
        var t = hitInfo;
        // Optionally, visualize the ray in the editor for debugging
        Debug.DrawRay(rayOrigin, rayDirection * rayLength, isGrounded ? Color.green : Color.red);
        Debug.LogWarning(isGrounded);
        return isGrounded;
    }

    
    [Replicate]
    private void Move(MovementData data, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
    {
        float sensitivity = .1f;
        if (_predictionRigidbody != null) // Ensure the Rigidbody reference is not null
        {
            // Calculate movement directions using player's local axes
            Vector3 forwardDirection =  transform.forward; // Forward for W/S
            Vector3 rightDirection =  transform.right; // Right for A/D

            // Combine input to calculate total movement force
            Vector3 movementForce = (forwardDirection * data.Vertical + rightDirection * data.Horizontal) * 30f;
            // Debug.Log(movementForce);
            // Apply movement force to the Rigidbody
            _predictionRigidbody.AddForce(movementForce, ForceMode.Acceleration);

            // Check if the player is grounded before allowing jump
            if (data.Jump)
            {
                // Apply upward force for jumping
                Vector3 jumpForce = Vector3.up * 12f; 
                _predictionRigidbody.AddForce(jumpForce, ForceMode.Impulse);
            }

            if (data.Grapple)
            {
                Vector3 grappleForce = _headObject.transform.forward * 12f;
                _predictionRigidbody.AddForce(grappleForce, ForceMode.Impulse);
            }

            // Apply gravity
            _predictionRigidbody.AddForce(Physics.gravity, ForceMode.Acceleration);

            // Camera/head rotation is now handled client-side only in Update()

            _predictionRigidbody.Simulate();
        }
    }

    [ServerRpc]
    private void BroadcastYawRotationHost(float yawRotation)
    {
        BroadcastYawRotationHostObserver(yawRotation);
    }

    [ObserversRpc(ExcludeServer = true)]
    private void BroadcastYawRotationHostObserver(float yawRotation)
    {
        if (!IsOwner)
            transform.localRotation = Quaternion.Euler(0, yawRotation, 0);
    }

    [ObserversRpc(RunLocally = true)]
    private void BroadcastYawRotation(float yawRotation)
    {
        if (!IsOwner)
            transform.localRotation = Quaternion.Euler(0, yawRotation, 0);
    }





[Reconcile]
private void ReconcileState(ReconcileData data, Channel channel = Channel.Unreliable)
{
    _predictionRigidbody.Reconcile(data.PredictionRigidbody);
}

    private void Update()
    {
        if (!IsOwner) return;
        HandleMouseLook();
    }

    private void HandleMouseLook()
    {
        float mouseX = _yawAction.ReadValue<float>() * _lookSensitivity;
        float mouseY = _pitchAction.ReadValue<float>() * _lookSensitivity;

        // Yaw: rotate the player body (Y axis)
        _currentYaw += mouseX;
        transform.localRotation = Quaternion.Euler(0f, _currentYaw, 0f);

        // Pitch: rotate the head/camera (X axis)
        _currentPitch -= mouseY;
        _currentPitch = Mathf.Clamp(_currentPitch, -67.5f, 67.5f);
        Vector3 headEuler = _headObject.localEulerAngles;
        headEuler.x = _currentPitch;
        _headObject.localEulerAngles = headEuler;

        // Sync yaw to server if changed enough
        if (Mathf.Abs(_currentYaw - _lastSentYaw) > YawSyncThreshold)
        {
            BroadcastYawRotationHost(_currentYaw);
            _lastSentYaw = _currentYaw;
        }
    }

}
}