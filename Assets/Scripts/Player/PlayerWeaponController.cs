using System.Collections;
using System.Collections.Generic;
using FishNet.CodeGenerating;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using Weapons;

namespace Player
{
public class PlayerWeaponController : NetworkBehaviour
{

    public delegate void PlayerWeaponsUpdatedHandler();
    public event PlayerWeaponsUpdatedHandler OnPlayerWeaponsUpdated;

    private void NotifyWeaponChange()
    {
        OnPlayerWeaponsUpdated?.Invoke();
    }

    public int currentAmmoCount;

    public int totalBulletCount;





    [SerializeField]
    private Transform weaponSpawnPoint;

    [SerializeField]
    private PurchasePoint nearbyPurchasePoint;

    public int money;


    public override void OnStartServer()
    {
        base.OnStartServer();
    }


    // Update is called once per frame
    void Update()
    {
        
    }
   

    [ObserversRpc]
    void weaponChangeObserver(){

    }

   

    public void setNearbyPurchasePoint(PurchasePoint point)
    {
        nearbyPurchasePoint = point;
    }

    private void BuyWeapon()
    {
        if(nearbyPurchasePoint == null)
            return; 

        if(nearbyPurchasePoint.GetCost() > money)
        {
            Debug.LogWarning("lacking money.");
            return;
        }

        if(nearbyPurchasePoint.purchasePointType == PurchasePoint.PurchasePointType.WallBuy)
        {
           if(((WallBuy)nearbyPurchasePoint).purchased)
           {
               return;
           }
        }


        money -= nearbyPurchasePoint.GetCost();
        
        GameObject newWeapon = nearbyPurchasePoint.BuyWeapon();
        
        if(newWeapon == null)
        return;
        GameObject spawnedWeapon = Instantiate(newWeapon, transform);
        spawnedWeapon.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        spawnedWeapon.transform.SetPositionAndRotation(weaponSpawnPoint.position, weaponSpawnPoint.rotation);

    //     if(nearbyPurchasePoint.purchasePointType == PurchasePoint.PurchasePointType.MysteryBox)
    //     {
    //         MysteryBox box = (MysteryBox)nearbyPurchasePoint;
    //     box.HandlePlayerWeaponsUpdated();
    //    }

        
    }

}
}