using Unity.Cinemachine;
using FishNet.Connection;
using FishNet.Object;
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using FishNet;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using Bootstrap;
using Unity.VisualScripting.FullSerializer;


//script responsible for handling every aspect of the player shooting.
//the challenge with the script is to manage the state of the player's tank
//while having lag from the server.
//also accepts inputs from the shopui to increase damage

namespace Weapons
{

    public class FirearmController : NetworkBehaviour
    {
        public bool inHand;

        public string _weaponName;
        public enum FireState
        {
            None, Ready, Empty, Reloading, Cycling
        }
        [Header("Tank Firing Characteristics")]
        public FireState fireState;
        [SerializeField] private int fireDelayCounter, fireCounterReset, cycleDelayCounter, cycleCounterReset;
        public bool startedReloading = false;
        public int magazineCount;
        public int maxMagazineCount;
        public int reserveCount;
        private bool fullAuto;
        public float damage;
        public float projSpeed;

        public float ricochetAngle;
        
        public LayerMask targetLayerMask;
        public float explosionDecalScale;

        public GameObject explosionParticlePrefab;

        [Header("Assign In Inspector")]
       // [SerializeField] private PlayerAudioController pac;
        [SerializeField] private Rigidbody playerRB;
        [SerializeField] private Animator tank_barrel_animator;
        [SerializeField] private Light2D turretVision;
        public Transform bulletSpawn;
        // Prefabs
        [SerializeField] private GameObject projectileContainerPrefab;
        public GameObject crosshair;
        // Explosions
        public GameObject explosionTestPrefab;
        public GameObject gunExplosionTestPrefab;

        [Header("Assigned During Runtime")]

        public BootstrapNetworkManager BNM;
        [SerializeField] private CinemachineVirtualCamera mainCamera;
       // [SerializeField] private ShopManager shopManager;
        public GameObject GameManager;
    //    public ShopUI shopRef;
        public List<string> UIBlocker = new();
        [SerializeField] private Image reloadReticle;

        private bool initialized = false;

        // Start is called before the first frame update
        [Serializable]
        public struct LiveFireData
        {
            public float _damage;
            public Vector3 _direction;
            public float _explosionDecalScale;

            public Vector3 _bulletSpawnPosition;

            public Quaternion _bulletSpawnRotation;

            public float _maxDistance;

            public float _projSpeed;

            public float _ricochetAngle;

        }

        public struct FireData : IReplicateData
        {

            public bool _sentFire;

            public bool _sentReload;

            public float _tankVelocity;

            private uint _tick;
            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;

            public FireData(bool sentFire, bool sentReload, float tankVelocity)
            {
                _sentFire = sentFire;
                _sentReload = sentReload;
                _tankVelocity = tankVelocity;
                _tick = 0;
            }
        }

        public struct FireReconcileData : IReconcileData
        {

            public int _fireDelayCounter;

            public int _cycleDelayCounter;

            public FireState _fireState;

            public bool _startedReloading;

            public int _magazineCount;
            public int _reserveCount;

            private uint _tick;
            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;

            public FireReconcileData(int fireDelayCounter, int cycleDelayCounter, FireState state, bool startedReloading, int magazineCount, int reserveCount)
            {
                _fireDelayCounter = fireDelayCounter;
                _cycleDelayCounter = cycleDelayCounter;
                _fireState = state;
                _startedReloading = startedReloading;
                _magazineCount = magazineCount;
                _reserveCount = reserveCount;
                _tick = 0;
            }

        }


        private void TimeManager_OnTick()
        {
            //new and improved heh
            Fire(CreateFireReplicateData());
        }

        private void TimeManager_OnPostTick()
        {
            //EXTREMELY IMPORTANT to send anything that might affect the transform of an object!
            //this also includes any colliders attatched to the object
            //see video for full explanation

            /* Reconcile is sent during PostTick because we
                 * want to send the rb data AFTER the simulation. */
            CreateReconcile();
        }

        public override void CreateReconcile()
        {
            FireReconcileData frd = new(fireDelayCounter, cycleDelayCounter, fireState, startedReloading, magazineCount, reserveCount); // Added speedBoostCounter
            FireReconcile(frd);
        }

        private FireData CreateFireReplicateData()
        {
            if(!base.IsOwner)
            return default;

            bool sentFire = false;
            bool sentReload = false;
            if (Input.GetKey(KeyCode.Mouse0) || Input.GetKey(KeyCode.Space))
            {
                sentFire = true;
            }

            if (Input.GetKey(KeyCode.R))
            {
                sentReload = true;
            }

            float playerVelocity = playerRB.linearVelocity.magnitude;

            FireData fd = new(sentFire, sentReload, playerVelocity);
            return fd;
        }

        [Reconcile]
        private void FireReconcile(FireReconcileData frd, Channel channel = Channel.Unreliable)
        {
            fireDelayCounter = frd._fireDelayCounter;
            fireState = frd._fireState;
            startedReloading = frd._startedReloading;
            magazineCount = frd._magazineCount;
            reserveCount = frd._reserveCount;
        }

        [Replicate]
        private void Fire(FireData fd, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            Debug.Log("fire replicate");
            if (fireState == FireState.Cycling)
            {
              //  CycleReticle(magazineCount);
                if (cycleDelayCounter >= cycleCounterReset)
                {
                    fireState = FireState.Ready;
                    cycleDelayCounter = 0;
                }
                else
                {
                    cycleDelayCounter++;
                    //early return, but we cant do anything if the gun is cycling.
                    return;
                }
            }

            if (fd._sentReload)
            {
                if (fireState != FireState.Reloading && startedReloading == false && magazineCount != maxMagazineCount)
                {
                    //Debug.LogWarning("we should be reloading here");
                    fireDelayCounter = 0;
                    fireState = FireState.Reloading;
                    startedReloading = true;
                }
            }

            if (fireState == FireState.Reloading)
            {
                if (fireDelayCounter >= fireCounterReset)
                {
                    fireState = FireState.Ready;
                    fireDelayCounter = 0;
                    startedReloading = false;

                    //check for enough reserve ammo
                    if (reserveCount >= 0)
                    {
                        if (reserveCount == 0)
                        {
                            Debug.LogError("that's a smith and wesson, and you've had your 6");
                            fireState = FireState.Empty;
                            return;
                        }
                        //happens when reserve count is say, 2. set the reserve to 0 after filling up the mag
                        else if (reserveCount < maxMagazineCount)
                        {
                            magazineCount = reserveCount;
                            reserveCount = 0;
                            fireState = FireState.Ready;
                            return;
                        }

                        //subtract amount needed to fill mag from reserve
                        reserveCount -= maxMagazineCount - magazineCount;
                        //refill mag
                        magazineCount = maxMagazineCount;
                        //ready to fire
                        fireState = FireState.Ready;
                        startedReloading = false;
                    }

                }
             //  ReloadReticle(fireDelayCounter);
                fireDelayCounter++;
            }

            if (fireState != FireState.Ready)
            {
                return;
            }
            if (UIBlocker.Count != 0)
            {
                return;
            }
            if (magazineCount <= 0)
            {
                return;
            }

            if (!fd._sentFire)
            {
                return;
            }

            Transform bulletSpawn = this.bulletSpawn;

            //tell the firestate to become cycling
            fireState = FireState.Cycling;

            //if there is only one round in mag then it should start reloading automatically
            if (magazineCount == 1)
            {
                fireState = FireState.Reloading;
            }

           
            //call the observer fire function. handles stuff like making the bullet trail and playing
            //sound effects

            //on the server side
            //ObserverFire(fireCont);

            //do not call playerfire if client is host. This is because it does not need to do local stuff if
            //you are the server

            float spreadFactor = Mathf.Lerp(0.3f, 3f, Mathf.Clamp01(.2f));

            float horizontalSpread = UnityEngine.Random.Range(-spreadFactor, spreadFactor);
            float verticalSpread = UnityEngine.Random.Range(-spreadFactor, spreadFactor);

            Vector3 direction = Quaternion.Euler(verticalSpread, horizontalSpread, 0) * bulletSpawn.forward;

            // LiveFireData struct holds data related to firing projectiles, 
            // including damage, direction, explosion decal scale, bullet spawn point, and maximum distance
            // we use this to package data in an efficient manner to send to the pool manager.
            LiveFireData lfd = new()
            {
                _damage = damage,
                _direction = direction,
                _explosionDecalScale = explosionDecalScale,
                _bulletSpawnPosition = bulletSpawn.position,
                _bulletSpawnRotation = bulletSpawn.rotation,
                _maxDistance = 750f,
                _projSpeed = projSpeed,
                _ricochetAngle = ricochetAngle
            };

            // Client-side prediction: instantiate and initialize locally
           

           PredServerFire(projectileContainerPrefab, lfd, LocalConnection);

            if(state == ReplicateState.Created)
            {
            }
            
            // Now request the server to perform the actual networked spawn
            // if (!replaying)
            // {
            //     PredServerFire(projectileContainerPrefab, lfd, base.LocalConnection);
            // }

            //instansiate projectile trail prefab, and spawn on server.

            

            // RaycastHit2D hitObject = Physics2D.Raycast(bulletSpawn.position, bulletSpawn.up, maxDistance, ~ignoreLayer);


            
            //subtract one from mags
            magazineCount -= 1;

        }

        private void SetReticle()
        {
            if(UIBlocker.Count != 0)
            crosshair.SetActive(false);
            else
            crosshair.SetActive(true);
        }

        [ServerRpc(RequireOwnership = false)]
        private void PredServerFire(GameObject projectilePrefab, LiveFireData lfd, NetworkConnection conn)
        {
            GameObject spawnedProjectile = Instantiate(projectilePrefab, lfd._bulletSpawnPosition, lfd._bulletSpawnRotation);
            
            PredictedProjectileController ppc = spawnedProjectile.GetComponent<PredictedProjectileController>();

            ppc.Initialize(lfd._bulletSpawnPosition, lfd._direction, lfd._projSpeed, lfd._maxDistance, lfd._damage, targetLayerMask);
            ppc.ready = true;
            ServerManager.Spawn(spawnedProjectile, conn);
            GetComponent<AudioSource>().Play();
            GameObject firedGunDecoration = Instantiate(gunExplosionTestPrefab, bulletSpawn.position, bulletSpawn.rotation);
            ServerManager.Spawn(firedGunDecoration);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerFire(GameObject containerPrefab, LiveFireData lfd)
        {
            GameObject spawnedContainer = Instantiate(containerPrefab, lfd._bulletSpawnPosition, lfd._bulletSpawnRotation);

        //    ProjectileContainer container = spawnedContainer.GetComponent<ProjectileContainer>();
            
            // container.proj.Initialize(lfd, this);
            // container.decor.Initialize(lfd, this);

            // Debug.LogWarning("Server: spawning " + container.proj.gameObject.name);
            // container.proj.ready = true;
            // container.decor.ready = true;
            // // Spawn the objects on the server for synchronization
            // ServerManager.Spawn(spawnedContainer);
            // container.Shed(true);

            // Perform additional actions on the clients, such as playing sounds or animations
            //GetComponent<AudioSource>().Play();
            tank_barrel_animator.SetTrigger("gun");
            GameObject firedGunDecoration = Instantiate(gunExplosionTestPrefab, bulletSpawn.position, bulletSpawn.rotation);
            ServerManager.Spawn(firedGunDecoration);
        }


        


        private void OnDestroy()
        {

            if (InstanceFinder.TimeManager != null)
            {
                InstanceFinder.TimeManager.OnTick -= TimeManager_OnTick;
                InstanceFinder.TimeManager.OnPostTick -= TimeManager_OnPostTick;
            }
        }

        [ServerRpc(RunLocally = true)]
        public void UpdateTankMag(FirearmController fire)
        {
            fire.maxMagazineCount++;
        }

        [ServerRpc(RunLocally = true)]
        public void UpdateTankDamage(FirearmController fire, float amount)
        {
            fire.damage += amount;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!base.IsOwner)
            {
                this.enabled = false;
                return;
            }


            // maxMagazineCount = 3;
            // InitializeStats(this);


            //instead of FixedUpdate, use Fishnet's timemanager to update each frame
            InstanceFinder.TimeManager.OnTick += TimeManager_OnTick;
            InstanceFinder.TimeManager.OnPostTick += TimeManager_OnPostTick;
        }

        public bool InitializeComponents()
        {
            try
            {
                // GameManager = GameObject.Find("GameManager");
                // mainCamera = GameObject.Find("PrimaryCamera").GetComponent<CinemachineVirtualCamera>();
        //        shopManager = GameManager.GetComponent<ShopManager>();
                Debug.Log("Player Fire Controller initialization - OK");
         //       BNM.loadingProgress.targetFill += 0.33f;

            }
            catch (NullReferenceException ex)
            {
                Debug.LogWarning("InitializeComponents - FireController failed, execution will be halted until resolved.");
                            Debug.LogWarning(ex);
                return false;
            }
            return true;

        }


        // Update is called once per frame
        void Update()
        {

            if (!initialized)
            {
                initialized = InitializeComponents();
                return;
            }

            
            // if (shopManager.inShop)
            // {
            //     if (!UIBlocker.Contains("Shop"))
            //         UIBlocker.Add("Shop");
            // }
            // else if (!shopManager.inShop)
            // {
            //     if (UIBlocker.Contains("Shop"))
            //         UIBlocker.Remove("Shop");
            // }
         //   SetReticle();
        }

        private void ReloadReticle(int counter)
        {
            if (counter >= fireCounterReset || counter == 0)
            {
                reloadReticle.fillAmount = 1f;
            }
            else
            {
                reloadReticle.fillAmount = (float)counter / fireCounterReset;
            }
        }

        private void CycleReticle(int magazineCount)
        {
            reloadReticle.fillAmount = (float)magazineCount / maxMagazineCount;
        }

        //would be no less than a bitch to get a decoration projectile to be rendered on the client
        //before the server, ensuring that both randomly generated innacuracies match.
        //possible solution: generate random innacuracy on client, pass to server
        //dont have the skills for this at the moment, because this would require making the
        //projectiles NON-networked, which thus would require re-doing the entire impact system.
        //AHHA I DID ITTT
        public void PlayerFire()
        {

            // Debug.LogError("called playerfire");
            //must be refined to allow for a library of sound effects
            GetComponent<AudioSource>().Play();

            tank_barrel_animator.SetTrigger("gun");
            GameObject firedGunDecoration = Instantiate(gunExplosionTestPrefab, bulletSpawn.position, bulletSpawn.rotation);

        }


        // [ObserversRpc(RunLocally = false)]
        // public void ObserverFire(PlayerFireController fireCont, ProjectileRealCollision master, Vector2 direction)
        // {
        //     Debug.LogError("played sound and stuff");
        //     //play sound for other players
        //     fireCont.GetComponent<AudioSource>().Play();
        // }


    }


}
