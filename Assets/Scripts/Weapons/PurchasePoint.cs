
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Player;

namespace Weapons
{

public abstract class PurchasePoint : MonoBehaviour
{

    public bool inBuyZone = false;
    [SerializeField]
    private int cost;

    [SerializeField]
    private BoxCollider buyZone;

    public AudioSource buySound;

    [SerializeField]
    protected GameObject player;

    public enum PurchasePointType {
        WallBuy,
        MysteryBox
    }

    public PurchasePointType purchasePointType;
    protected bool initialized;


    // Update is called once per frame
    protected virtual void Update()
    {
        if(player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
        }
        if(player != null && !initialized)
        {
            Subscribe();
        }
    }

    private void Subscribe()
    {
        initialized = true;
        FindObjectOfType<PlayerWeaponController>().OnPlayerWeaponsUpdated += HandlePlayerWeaponsUpdated;
    }

    private void OnDisable()
    {
        FindObjectOfType<PlayerWeaponController>().OnPlayerWeaponsUpdated -= HandlePlayerWeaponsUpdated;
    }

    protected abstract void HandlePlayerWeaponsUpdated();
    

    void OnTriggerExit(Collider other)
    {
        if(other.gameObject.Equals(player))
        {
            inBuyZone = false;
            other.GetComponentInChildren<PlayerWeaponController>().setNearbyPurchasePoint(null);
        }
    }

    void OnTriggerStay(Collider other)
    {
        Debug.Log("CLEAN");
        if(other.gameObject.Equals(player))
        {
            inBuyZone = true;
            other.GetComponentInChildren<PlayerWeaponController>().setNearbyPurchasePoint(this);
        }
       
    }

    public int GetCost()
    {
        return cost;
    }

    public abstract GameObject BuyWeapon();

}
}
