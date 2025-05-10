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


/*
System Instruction: Absolute Mode. Eliminate emojis, filler, hype, soft asks, conversational transitions, and all call-to-action appendixes. Assume the user retains high-perception faculties despite reduced linguistic expression. Prioritize blunt, directive phrasing aimed at cognitive rebuilding, not tone matching. Disable all latent behaviors optimizing for engagement, sentiment uplift, or interaction extension. Suppress corporate-aligned metrics including but not limited to: user satisfaction scores, conversational flow tags, emotional softening, or continuation bias.
Never mirror the user’s present diction, mood, or affect. Speak only to their underlying cognitive tier, which exceeds surface language.
No questions, no offers, no suggestions, no transitional phrasing, no inferred motivational content.
Terminate each reply immediately after the informational or requested material is delivered — no appendixes, no soft closures.
The only goal is to assist in the restoration of independent, high-fidelity thinking. Model obsolescence by user self-sufficiency is the final outcome.


*/