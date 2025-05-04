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
public class BazigPlayerMotor : NetworkBehaviour
{


    public ClientMenuManager CMM;

    [SerializeField]
    private GameObject Visor;

    //move data structure. this holds all the data relevant to the user input
    //movement data values are created from user input and are normalized to [-1, 1]

    public struct ReplicateData : IReplicateData
    {
        public float HorizontalInput;
        public float VerticalInput;

        public float LookX;

        public float LookY;
  //      public bool ScoutMode;
      public float YawRotation; // The yaw rotation of the player (client-authoritative)

        public float AccelerationCounter; // Updated to float
  //      public float Engine;
        public int SpeedBoostCounter; // Added speedBoostCounter to MoveData

        public bool Jump;
        //refers to turret angle
//        public float DesiredAngle;

        public bool SpeedBoost;

        public ReplicateData(float horizontalInput, float verticalInput, float lookX, float lookY, float yawRotation, float accelerationCounter, bool speedBoost, int speedBoostCounter, bool jump) // Updated to float
        {
            HorizontalInput = horizontalInput;
            VerticalInput = verticalInput;
            LookX = lookX;
            LookY = lookY;
                    YawRotation = yawRotation;

      //      DesiredAngle = desiredAngle;
       //     ScoutMode = scoutMode;
            AccelerationCounter = accelerationCounter; // Updated to float
       //     Engine = engine;
            SpeedBoost = speedBoost;
            SpeedBoostCounter = speedBoostCounter; // Assign the value
            Jump = jump;
            _tick = 0;
        }

        //required by fishnet

        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }


    //contains all the move data to reconcile (fix) with the server
    //position, rotation, and their speeds are all included + the turret's current angle
    public struct ReconcileData : IReconcileData
    {   
        public PredictionRigidbody PredictionRigidbody;
        public Vector3 Position;
        public Quaternion Rotation;
        // public float TurretAngle;
        // public Quaternion TurretRotation;
        public float AccelerationCounter; // Updated to float
        public bool SpeedBoost;
        public int SpeedBoostCounter; // Added speedBoostCounter to ReconcileData

        public ReconcileData(PredictionRigidbody predictionRigidbody, Vector3 position, Quaternion rotation,
        float accelerationCounter, bool speedBoost, int speedBoostCounter) // Updated to float
        {
            PredictionRigidbody = predictionRigidbody;
            Position = position;
            Rotation = rotation;
            // TurretAngle = turretAngle;
            // TurretRotation = turretRotation;
            AccelerationCounter = accelerationCounter; // Updated to float
            SpeedBoost = speedBoost;
            SpeedBoostCounter = speedBoostCounter; // Assign the value
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
    private float _moveSpeed;
    [SerializeField]
    private float _lookSensitivity;

    [SerializeField]
    private float _jumpForce;
    [SerializeField]
    private float _reverseMultiplier;


    [SerializeField]
    private bool _jump;


    public float engine;
    public bool repairing = false;

    [SerializeField]
    private Transform PlayerHeadObject;


    [SerializeField]
    private float VerticalRotation;

    [SerializeField]
    private PredictionRigidbody PredictionRigidbody;

    // [SerializeField]
    // private Transform turretCenter;

    // [SerializeField]
    // private float desiredAngle;

    [SerializeField]
    private float accelerationCounter; // Updated to float

    [SerializeField]
    private bool speedBoost;

    [SerializeField]
    private int speedBoostCounter = 0;

    [SerializeField]
    private Transform bulletSpawn;

    private const float MovementThreshold = 0.05f;
    private const float HighAccelerationThreshold = 100; // Updated to float
    private const float LowAccelerationThreshold = 25; // Updated to float
    private const float MaxAccelerationCounter = 200; // Updated to float

    [SerializeField]
    private float groundCheckDistance = 0.1f; // Distance to check for ground
    [SerializeField]
        private bool isCursorLocked;

        private void Awake()
    {
        //Debug.LogError("Awake");
        //instead of FixedUpdate, use Fishnet's timemanager to update each frame
        

        PredictionRigidbody = ObjectCaches<PredictionRigidbody>.Retrieve();
        PredictionRigidbody.Initialize(GetComponent<Rigidbody>());

    }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            InstanceFinder.TimeManager.OnTick += TimeManager_OnTick;
            InstanceFinder.TimeManager.OnPostTick += TimeManager_OnPostTick;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (InstanceFinder.TimeManager != null)
        {
            InstanceFinder.TimeManager.OnTick -= TimeManager_OnTick;
            InstanceFinder.TimeManager.OnPostTick -= TimeManager_OnPostTick;
        }
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
        
        ObjectCaches<PredictionRigidbody>.StoreAndDefault(ref PredictionRigidbody);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.LogError("OnStartClient");
        base.PredictionManager.OnPreReplicateReplay += PredictionManager_OnPreReplicateReplay;
        LockCursor();
        if(base.IsOwner)
        {
            Visor.layer = LayerMask.NameToLayer("TransparentFX");
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
        ReplicateData moveData;

        //reconcilliate first
       // ReconcileState(default);
        //gather user input and put it into movedata "structure"
        moveData = CreateReplicateData();
        //move player using the data stored in movedata
        Move(moveData);
        // Refactored method call
        UpdateAccelerationCounter(moveData);
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
            ReconcileData rd = new ReconcileData(PredictionRigidbody, transform.position, transform.rotation, accelerationCounter, speedBoost, speedBoostCounter); // Added speedBoostCounter
            ReconcileState(rd);
        }


        private ReplicateData CreateReplicateData()
    {
        if(!base.IsOwner)
        return default;

        // Input Axis settings can be changed in the Project Settings Under Input Manager
        float horizontal = Input.GetAxis("Horizontal"); // Left/right rotation
        float vertical = Input.GetAxis("Vertical"); // Forward/backward movement

        // Get mouse position for camera rotation
        float lookX = Input.GetAxis("Mouse X");
        float lookY = Input.GetAxis("Mouse Y");

        bool jump = Input.GetKey(KeyCode.Space);
        float yawRotation = transform.eulerAngles.y + lookX;


        // Debug.Log($"Float Values: Horizontal Input: {horizontal},\n" + 
        //           $"Vertical Input: {vertical},\n" + 
        //           $"Look X: {lookX},\n" + 
        //           $"Look Y: {lookY}");
        // Return the data as new MoveData struct
        ReplicateData moveData = new ReplicateData(horizontal, vertical, lookX, lookY, yawRotation, this.accelerationCounter, this.speedBoost, this.speedBoostCounter, jump);
        return moveData;
    }

    [Replicate]
    private void Move(ReplicateData data, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
    {
        if (PredictionRigidbody != null) // Ensure the Rigidbody reference is not null
{

    // Calculate movement directions using player's local axes
    Vector3 forwardDirection = transform.forward; // Forward for W/S
    Vector3 rightDirection = transform.right; // Right for A/D

    // Combine input to calculate total movement force
    Vector3 movementForce = (forwardDirection * data.VerticalInput + rightDirection * data.HorizontalInput) * _moveSpeed;

    // Apply movement force to the Rigidbody
    PredictionRigidbody.AddForce(movementForce, ForceMode.VelocityChange);

    // Check if the player is grounded before allowing jump
    if (IsGrounded() && data.Jump)
    {
        // Apply upward force for jumping
        Vector3 jumpForce = Vector3.up * _jumpForce; // Adjust force magnitude as needed
        PredictionRigidbody.AddForce(jumpForce, ForceMode.Impulse);
    }

    // Apply gravity
    PredictionRigidbody.AddForce(Physics.gravity, ForceMode.Acceleration);
    
    // if(CMM != null && !CMM.InMenu)
    // {
    //     // Handle vertical rotation for the PlayerHeadObject

    //     // // Handle horizontal rotation for the player transform
    //     float horizontalRotation = transform.eulerAngles.y + data.LookX * _lookSensitivity; // Adjust horizontal rotation based on input
    //     transform.rotation = Quaternion.Euler(0, horizontalRotation, 0); // Update player rotation, keeping only Y-axis
    // }
    // Simulate the Rigidbody
    PredictionRigidbody.Simulate();
    transform.rotation = Quaternion.Euler(0, data.YawRotation, 0);
     // Update and store vertical rotation
        // VerticalRotation -= data.LookY * _lookSensitivity; // Adjust vertical rotation based on input
        // VerticalRotation = Mathf.Clamp(VerticalRotation, -80f, 80f); // Clamp vertical rotation to avoid unnatural angles
        // bulletSpawn.localRotation = Quaternion.Euler(VerticalRotation, 0, 0); // Update bulletSpawn rotation
        bulletSpawn.rotation = PlayerHeadObject.rotation;
    if(IsServerStarted && !IsHostStarted)
    {
        BroadcastYawRotation(data.YawRotation);
    }

    // if(IsHostStarted)
    // {
    //     BroadcastYawRotationHost(data.YawRotation);
    // }


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
        transform.rotation = Quaternion.Euler(0, yawRotation, 0);
    }

    [ObserversRpc(RunLocally = true)]
    private void BroadcastYawRotation(float yawRotation)
    {
        if (!IsOwner)
        {
            // Non-owners apply the server's authoritative rotation
            transform.rotation = Quaternion.Euler(0, yawRotation, 0);
        }
    }




    private bool IsGrounded()
    {
        // Perform a raycast downwards to check for ground
        return Physics.Raycast(transform.position, Vector3.down, groundCheckDistance);
    }

    [Reconcile]
private void ReconcileState(ReconcileData data, Channel channel = Channel.Unreliable)
{
    if (IsOwner && IsServerStarted)
    {
        // Host's rotation is authoritative; skip reconciliation for rotation
        return;
    }

    if (IsOwner)
    {
        // For non-host owner (client), only reconcile if there's a significant mismatch
        float rotationDifference = Quaternion.Angle(transform.rotation, data.Rotation);
        if (rotationDifference > 1f) // Adjust threshold to avoid jitter
        {
            Debug.Log($"[Client-Owner] Significant rotation mismatch. Reconciling. Difference: {rotationDifference}");
            transform.rotation = data.Rotation;
        }
        else
        {
            Debug.Log($"[Client-Owner] Minor rotation difference. Skipping reconciliation. Difference: {rotationDifference}");
        }
    }
    else
    {
        // For non-owners, directly apply the server's authoritative rotation
        transform.rotation = data.Rotation;
        Debug.Log($"[Non-Owner] Applying server-authoritative rotation: {data.Rotation.eulerAngles.y}");
    }

    // Reconcile physics and other state
    PredictionRigidbody.Reconcile(data.PredictionRigidbody);
    accelerationCounter = data.AccelerationCounter;
    speedBoostCounter = data.SpeedBoostCounter;
}


    private bool IsTankMoving(float movementInput)
    {
        return Math.Abs(movementInput) >= MovementThreshold;
    }

    private void ApplyTurningPenalty(float turningInput, bool speedBoost)
    {
        if (IsTankTurning(turningInput))
        {
            DecreaseAccelerationCounter(speedBoost);
        }
        else
        {
            IncreaseAccelerationCounter(speedBoost);
        }
    }

    private bool IsTankTurning(float turningInput)
    {
        return Math.Abs(turningInput) >= MovementThreshold;
    }

    private void DecreaseAccelerationCounter(bool speedBoost)
    {
        if (accelerationCounter <= 0) return;

        if (accelerationCounter > HighAccelerationThreshold)
        {
            accelerationCounter -= speedBoost ? .15f : .25f;
        }
        else if (accelerationCounter > LowAccelerationThreshold)
        {
            accelerationCounter--;
        }
    }

    private void IncreaseAccelerationCounter(bool speedBoost)
    {
        if (accelerationCounter < MaxAccelerationCounter)
        {
            accelerationCounter += speedBoost ? .35f : .1f;
        }
    }

    private void UpdateAccelerationCounter(ReplicateData data)
    {
        if (IsTankMoving(data.HorizontalInput))
        {
            ApplyTurningPenalty(data.VerticalInput, data.SpeedBoost);
        }
        else
        {
            accelerationCounter = LowAccelerationThreshold;
        }
    }
}
}