using FishNet;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using FishNet.Object.Prediction;
using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// Add this component alongside <see cref="BazigPlayerMotor"/> to give the player a simple grappling‑hook ability.
    /// Owners press RMB (or configurable key) to shoot a ray; if it hits something within range the hook latches and
    /// the player is pulled toward the grapple point until they release (RMB again) or arrive within the close‑enough
    /// radius. All movement forces are applied to the same <see cref="PredictionRigidbody"/> already used by the motor,
    /// so the feature remains fully predicted.
    /// </summary>
    [RequireComponent(typeof(BazigPlayerMotor))]
    public class GrappleHook : NetworkBehaviour
    {
        #region Inspector
        [Header("Grapple Settings")]
        [SerializeField] private Transform grappleOrigin;   // usually the camera / head transform
        [SerializeField] private float    maxDistance      = 60f;
        [SerializeField] private float    pullAcceleration = 30f;
        [SerializeField] private float    stopDistance     = 2f;
        [SerializeField] private KeyCode  fireKey          = KeyCode.Mouse1;
        #endregion

        #region Runtime Fields
        private PredictionRigidbody prb;

        // authoritative grapple state (predicted)
        private bool    isGrappling;
        private Vector3 grapplePoint;

        // simple line display (client‑only)
        private LineRenderer lr;
        #endregion

        #region Fish‑Net lifecycle
        private void Awake()
        {
            prb = GetComponent<BazigPlayerMotor>().GetComponent<PredictionRigidbody>();
            if (prb == null)
            {
                // motor stores PRB in its own field; we can fetch through that instead
                var motor = GetComponent<BazigPlayerMotor>();
                var field = typeof(BazigPlayerMotor).GetField("PredictionRigidbody", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                prb = field?.GetValue(motor) as PredictionRigidbody;
            }
        }

        public override void OnStartNetwork()
        {
            InstanceFinder.TimeManager.OnTick     += OnTick;
            InstanceFinder.TimeManager.OnPostTick += OnPostTick;
        }
        public override void OnStopNetwork()
        {
            if (InstanceFinder.TimeManager != null)
            {
                InstanceFinder.TimeManager.OnTick     -= OnTick;
                InstanceFinder.TimeManager.OnPostTick -= OnPostTick;
            }
        }

        public override void OnStartClient()
        {
            if (IsOwner)
            {
                // set up a basic line renderer to show the rope (owner only)
                lr = gameObject.AddComponent<LineRenderer>();
                lr.enabled           = false;
                lr.useWorldSpace     = true;
                lr.positionCount     = 2;
                lr.widthMultiplier   = 0.02f;
                lr.material          = new Material(Shader.Find("Sprites/Default"));
            }
        }
        #endregion

        #region Tick cycle
        private void OnTick()
        {
            if (!IsOwner) return;

            // 1) INPUT (client‑side only, predicted) -----------------------------------
            if (Input.GetKeyDown(fireKey))
            {
                if (!isGrappling) TryStartGrapple();
                else              RequestRelease();
            }

            if (!isGrappling) return;

            // 2) PULL PHYSICS -----------------------------------------------------------
            Vector3 dir = (grapplePoint - transform.position);
            float   dist = dir.magnitude;
            if (dist < stopDistance) { RequestRelease(); return; }

            dir.Normalize();
            prb.AddForce(dir * pullAcceleration, ForceMode.Acceleration);
        }

        // We only need PostTick to update the line after physics simulation
        private void OnPostTick()
        {
            if (IsOwner && lr != null)
            {
                lr.enabled = isGrappling;
                if (isGrappling)
                {
                    lr.SetPosition(0, grappleOrigin.position);
                    lr.SetPosition(1, grapplePoint);
                }
            }
        }
        #endregion

        #region Grapple control (predicted)
        private void TryStartGrapple()
        {
            if (grappleOrigin == null) grappleOrigin = transform;

            RaycastHit hit;
            if (!Physics.Raycast(grappleOrigin.position, grappleOrigin.forward, out hit, maxDistance)) return;

            grapplePoint = hit.point;
            isGrappling  = true;
            // let server/observers know
            ServerSetGrappleState(true, grapplePoint);
        }

        private void RequestRelease()
        {
            if (!isGrappling) return;
            isGrappling = false;
            ServerSetGrappleState(false, Vector3.zero);
        }
        #endregion

        #region Networking
        [ServerRpc]
        private void ServerSetGrappleState(bool grappling, Vector3 point)
        {
            isGrappling = grappling;
            grapplePoint = point;
            BroadcastGrappleState(grappling, point);
        }

        [ObserversRpc(BufferLast = true, ExcludeOwner = false)]
        private void BroadcastGrappleState(bool grappling, Vector3 point)
        {
            isGrappling  = grappling;
            grapplePoint = point;
        }
        #endregion
    }
}
