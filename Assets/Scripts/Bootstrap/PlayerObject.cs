using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerObject
{
    private GameObject _clientObject;
    private GameObject _tankObject;
    private GameObject _cameraObject;

    public PlayerObject()
    {
        //do nothing, should never be called
    }

    public PlayerObject(GameObject client, GameObject tank, GameObject cam)
    {
        _clientObject = client;
        _tankObject = tank;
        _cameraObject = cam;
    }


    public GameObject GetClient()
    {
        return _clientObject;
    }

    public GameObject GetPlayer()
    {
        return _tankObject;
    }

    public GameObject GetCam()
    {
        return _cameraObject;
    }

}
