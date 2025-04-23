using System;
using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using Unity.VisualScripting;
using UnityEngine;

public class CameraController : NetworkBehaviour
{

    [SerializeField]
    private Camera mainCamera;

    public Transform playerHeadObject;

    [SerializeField]
    private GameObject visor;

    private bool initialized = false;
    private float VerticalRotation;
    [SerializeField]
    private float _lookSensitivity;

    public override void OnStartNetwork()
    {
        enabled = base.Owner.IsLocalClient;
    }

    // Update is called once per frame
    void Update()
    {
        // if(!NetworkManager.IsClientStarted)
        // return;
        if (!initialized)
            {
                initialized = InitializeComponents();
                return;
            }
       
        float lookY = Input.GetAxis("Mouse Y");
        VerticalRotation -= lookY * _lookSensitivity; // Adjust vertical rotation based on input
        VerticalRotation = Mathf.Clamp(VerticalRotation, -80f, 80f); // Clamp vertical rotation to avoid unnatural angles
        playerHeadObject.localRotation = Quaternion.Euler(VerticalRotation, 0, 0); // Update head object rotation


    }

    private bool InitializeComponents()
    {
        try
        {
            mainCamera = Camera.main;
            visor.layer = LayerMask.NameToLayer("IgnoreFPV");
            mainCamera.transform.SetPositionAndRotation(playerHeadObject.position, playerHeadObject.rotation);
            mainCamera.transform.SetParent(playerHeadObject.transform);
           // player = GameObject.FindGameObjectWithTag("LocalPlayer").transform;
            Debug.Log("Camera Controller initialization - OK");

        }
        catch (Exception ex)
        {
            Debug.LogWarning("camera controller had missing components, retrying...");
                Debug.LogWarning(ex);
                return false;
        }
        return true;
    }
}