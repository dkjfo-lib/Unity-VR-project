using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using HTC.UnityPlugin.Vive;
using HTC.UnityPlugin.ColliderEvent;

public class Detector : BasicGrabbable
{
    public Transform probeEnd;
    public Transform gasPoint;
    public Text detectorDisplay;
    public float detectorValue = 0;
    public bool turnedOn = false;
    public ColliderButtonEventData.InputButton targetButton = ColliderButtonEventData.InputButton.Trigger;


    public float time2Switch = 3f;
    public float timeHeld = 0;

    private void Start()
    {
        detectorDisplay.gameObject.SetActive(turnedOn);
    }

    private void Update()
    {
        if (turnedOn)
        {
            Vector3 diff = probeEnd.position - gasPoint.position;
            detectorValue = diff.sqrMagnitude;
            detectorDisplay.text = $"{detectorValue:f3}";
        }
    }

    public override void OnColliderEventDragFixedUpdate(ColliderButtonEventData eventData)
    {
        if (isGrabbed && eventData.button == targetButton)
        {
            CountTime2Toggle();
        }
        base.OnColliderEventDragFixedUpdate(eventData);
    }

    public override void OnColliderEventDragUpdate(ColliderButtonEventData eventData)
    {
        if (isGrabbed && eventData.button == targetButton)
        {
            CountTime2Toggle();
        }
        base.OnColliderEventDragUpdate(eventData);
    }

    public override void OnColliderEventDragEnd(ColliderButtonEventData eventData)
    {
        if (eventData.button == targetButton)
        {
            timeHeld = 0;
        }
        base.OnColliderEventDragEnd(eventData);
    }

    void CountTime2Toggle()
    {
        timeHeld += Time.deltaTime;
        if (timeHeld > time2Switch)
        {
            ToggleDetector();
            timeHeld = 0;
        }
    }

    void ToggleDetector()
    {
        turnedOn = !turnedOn;
        detectorDisplay.gameObject.SetActive(turnedOn);
    }
}
