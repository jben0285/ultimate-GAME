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
    [AllowMutableSyncType]  
    public SyncVar<List<GameObject>> weapons ;

    [SerializeField]
    [AllowMutableSyncType]
    private SyncVar<int> selectedWeapon;

    [SerializeField]
    private Transform weaponSpawnPoint;

    [SerializeField]
    private PurchasePoint nearbyPurchasePoint;

    public int money;


    public override void OnStartServer()
    {
        base.OnStartServer();
        weapons.Value.Capacity = 4;
    }


    // Update is called once per frame
    void Update()
    {
        if(base.IsOwner)
        {
        for(int i = (int)KeyCode.Alpha1; i < (int)KeyCode.Alpha4; i++)
        if(Input.GetKey((KeyCode)i))
        {
           weaponChange(i - (int)KeyCode.Alpha1);  
        }
        if(Input.GetKey(KeyCode.E))
        {
            BuyWeapon();
        }
        }
        
    }
    [ServerRpc]
    void weaponChange(int index, bool newWeapon = false)
    {
        if(index > weapons.Value.Count - 1)
        {
            Debug.LogWarning("player tried to access a weapon slot where there was no weapon");
            return;
        }
        if(selectedWeapon.Value == index && !newWeapon)
        {
            return;
        }
        
        weapons.Value[selectedWeapon.Value].SetActive(false);
        weapons.Value[index].SetActive(true);
        weapons.Value[selectedWeapon.Value].GetComponent<FirearmController>().inHand = false;
        weapons.Value[index].GetComponent<FirearmController>().inHand = true;
        
        selectedWeapon.Value = index;
    }

    [ObserversRpc]
    void weaponChangeObserver(){

    }

    void WeaponReplace(GameObject newWeapon)
    {
        Debug.Log("weapons were full, going to replace");
        GameObject temp = weapons.Value[selectedWeapon.Value];
        weapons.Value.RemoveAt(selectedWeapon.Value);
        Destroy(temp);
        weapons.Value.Insert(selectedWeapon.Value, newWeapon);
        
        weaponChange(selectedWeapon.Value, true);
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

        if(weapons.Value.Count + 1 == weapons.Value.Capacity)
        {
            WeaponReplace(spawnedWeapon);
        }
        else
        {
        
        weapons.Value.Add(spawnedWeapon);
        weaponChange(weapons.Value.Count - 1);
        }
        NotifyWeaponChange();
    //     if(nearbyPurchasePoint.purchasePointType == PurchasePoint.PurchasePointType.MysteryBox)
    //     {
    //         MysteryBox box = (MysteryBox)nearbyPurchasePoint;
    //     box.HandlePlayerWeaponsUpdated();
    //    }

        
    }

}
}