using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Player;
namespace Weapons
{
public class WallBuy : PurchasePoint
{
    

    [SerializeField]
    private GameObject weaponAvailibleToPurchase;

    public bool purchased;

    
    [SerializeField]
    private GameObject visualObject;

    void Start()
    {
        purchasePointType = PurchasePointType.WallBuy;
    }

    public override GameObject BuyWeapon()
    {
        if(!purchased)
        {
        purchased = true;
        buySound.Play();
        return weaponAvailibleToPurchase;
        }
        return null;
    }

    protected override void HandlePlayerWeaponsUpdated()
    {
    }
}
}