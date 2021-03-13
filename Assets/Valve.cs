using HTC.UnityPlugin.ColliderEvent;
using HTC.UnityPlugin.Utility;
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using GrabberPool = HTC.UnityPlugin.Utility.ObjectPool<HTC.UnityPlugin.Vive.BasicGrabbable.Grabber>;
using HTC.UnityPlugin.Vive;

public class Valve : GrabbableBase<BasicGrabbable.Grabber>
    , IColliderEventDragStartHandler
    , IColliderEventDragFixedUpdateHandler
    , IColliderEventDragUpdateHandler
    , IColliderEventDragEndHandler
{
    public float valveMaxRotation = 360;
    public float valveRotation = 0;
    public float canRotatePerSet = 90;
    public float currentlyRotated = 0;
    public float initialDeltaDegrees;
    public float valveOpenness => valveRotation / valveMaxRotation;


    private IndexedTable<ColliderButtonEventData, BasicGrabbable.Grabber> m_eventGrabberSet;

    [Range(MIN_FOLLOWING_DURATION, MAX_FOLLOWING_DURATION)]
    [FormerlySerializedAs("followingDuration")]
    [SerializeField]
    private float m_followingDuration = DEFAULT_FOLLOWING_DURATION;
    [FormerlySerializedAs("overrideMaxAngularVelocity")]
    [SerializeField]
    private bool m_overrideMaxAngularVelocity = true;
    [FormerlySerializedAs("unblockableGrab")]
    [SerializeField]
    private bool m_unblockableGrab = true;
    [SerializeField]
    [FlagsFromEnum(typeof(ControllerButton))]
    private ulong m_primaryGrabButton = 0ul;
    [SerializeField]
    [FlagsFromEnum(typeof(ColliderButtonEventData.InputButton))]
    private uint m_secondaryGrabButton = 1u << (int)ColliderButtonEventData.InputButton.Trigger;
    [SerializeField]
    [HideInInspector]
    private ColliderButtonEventData.InputButton m_grabButton = ColliderButtonEventData.InputButton.Trigger;
    [SerializeField]
    private bool m_allowMultipleGrabbers = true;

    public override float followingDuration { get { return m_followingDuration; } set { m_followingDuration = Mathf.Clamp(value, MIN_FOLLOWING_DURATION, MAX_FOLLOWING_DURATION); } }

    public override bool overrideMaxAngularVelocity { get { return m_overrideMaxAngularVelocity; } set { m_overrideMaxAngularVelocity = value; } }

    public bool unblockableGrab { get { return m_unblockableGrab; } set { m_unblockableGrab = value; } }

    public ColliderButtonEventData grabbedEvent { get { return isGrabbed ? currentGrabber.eventData : null; } }

    public ulong primaryGrabButton { get { return m_primaryGrabButton; } set { m_primaryGrabButton = value; } }

    public uint secondaryGrabButton { get { return m_secondaryGrabButton; } set { m_secondaryGrabButton = value; } }

    private bool moveByVelocity { get { return !unblockableGrab && grabRigidbody != null && !grabRigidbody.isKinematic; } }

    public bool IsPrimeryGrabButtonOn(ControllerButton btn) { return EnumUtils.GetFlag(m_primaryGrabButton, (int)btn); }

    public void SetPrimeryGrabButton(ControllerButton btn, bool isOn = true) { EnumUtils.SetFlag(ref m_primaryGrabButton, (int)btn, isOn); }

    public void ClearPrimeryGrabButton() { m_primaryGrabButton = 0ul; }

    public bool IsSecondaryGrabButtonOn(ColliderButtonEventData.InputButton btn) { return EnumUtils.GetFlag(m_secondaryGrabButton, (int)btn); }

    public void SetSecondaryGrabButton(ColliderButtonEventData.InputButton btn, bool isOn = true) { EnumUtils.SetFlag(ref m_secondaryGrabButton, (int)btn, isOn); }

    public void ClearSecondaryGrabButton() { m_secondaryGrabButton = 0u; }

#if UNITY_EDITOR
    protected virtual void OnValidate() { RestoreObsoleteGrabButton(); }
#endif
    private void RestoreObsoleteGrabButton()
    {
        if (m_grabButton == ColliderButtonEventData.InputButton.Trigger) { return; }
        ClearSecondaryGrabButton();
        SetSecondaryGrabButton(m_grabButton, true);
        m_grabButton = ColliderButtonEventData.InputButton.Trigger;
    }

    protected override void Awake()
    {
        base.Awake();

        RestoreObsoleteGrabButton();
    }

    protected virtual void OnDisable()
    {
        ClearGrabbers(true);
        ClearEventGrabberSet();
    }

    private void ClearEventGrabberSet()
    {
        if (m_eventGrabberSet == null) { return; }

        for (int i = m_eventGrabberSet.Count - 1; i >= 0; --i)
        {
            BasicGrabbable.Grabber.Release(m_eventGrabberSet.GetValueByIndex(i));
        }

        m_eventGrabberSet.Clear();
    }

    protected bool IsValidGrabButton(ColliderButtonEventData eventData)
    {
        if (m_primaryGrabButton > 0ul)
        {
            ViveColliderButtonEventData viveEventData;
            if (eventData.TryGetViveButtonEventData(out viveEventData) && IsPrimeryGrabButtonOn(viveEventData.viveButton)) { return true; }
        }

        return m_secondaryGrabButton > 0u && IsSecondaryGrabButtonOn(eventData.button);
    }

    public virtual void OnColliderEventDragStart(ColliderButtonEventData eventData)
    {
        if (!IsValidGrabButton(eventData)) { return; }

        if (!m_allowMultipleGrabbers)
        {
            ClearGrabbers(false);
            ClearEventGrabberSet();
        }

        var grabber = BasicGrabbable.Grabber.Get(eventData);
        var offset = RigidPose.FromToPose(grabber.grabberOrigin, new RigidPose(transform));
        grabber.grabOffset = offset;

        if (m_eventGrabberSet == null) { m_eventGrabberSet = new IndexedTable<ColliderButtonEventData, BasicGrabbable.Grabber>(); }
        m_eventGrabberSet.Add(eventData, grabber);

        AddGrabber(grabber);
        Vector3 deltaVec = grabber.grabberOrigin.pos - transform.position;
        Vector3 deltaVecProjected = Vector3.ProjectOnPlane(deltaVec, transform.up).normalized;
        initialDeltaDegrees = Vector3.SignedAngle(transform.forward, deltaVecProjected, transform.up);
    }

    public virtual void OnColliderEventDragFixedUpdate(ColliderButtonEventData eventData) { }

    public virtual void OnColliderEventDragUpdate(ColliderButtonEventData eventData)
    {
        if (isGrabbed && currentGrabber.eventData == eventData)
        {
            RecordLatestPosesForDrop(Time.time, 0.05f);
            RotateTowardsLikeWheel(transform, currentGrabber.grabberOrigin.pos);
        }
    }

    public void RotateTowardsLikeWheel(Transform transform, Vector3 rotateTowards)
    {
        Vector3 deltaVec = rotateTowards - transform.position;
        Vector3 deltaVecProjected = Vector3.ProjectOnPlane(deltaVec, transform.up).normalized;
        float deltaDegrees = Vector3.SignedAngle(transform.forward, deltaVecProjected, transform.up);
        deltaDegrees -= initialDeltaDegrees;

        if (valveMaxRotation < valveRotation + deltaDegrees)
        {
            deltaDegrees = valveMaxRotation - valveRotation;
        }
        if (0 > valveRotation + deltaDegrees)
        {
            deltaDegrees = -valveRotation;
        }

        currentlyRotated += deltaDegrees;
        if (currentlyRotated * currentlyRotated > canRotatePerSet * canRotatePerSet)
        {
            currentlyRotated = Mathf.Clamp(currentlyRotated, -canRotatePerSet, canRotatePerSet);
            deltaDegrees = 0;
        }

        valveRotation += deltaDegrees;
        transform.Rotate(0, deltaDegrees, 0);
    }

    public virtual void OnColliderEventDragEnd(ColliderButtonEventData eventData)
    {
        if (m_eventGrabberSet == null) { return; }

        BasicGrabbable.Grabber grabber;
        if (!m_eventGrabberSet.TryGetValue(eventData, out grabber)) { return; }

        RemoveGrabber(grabber);
        m_eventGrabberSet.Remove(eventData);
        BasicGrabbable.Grabber.Release(grabber);

        currentlyRotated = 0;
    }

    private void OnDrawGizmos()
    {
        Vector3 rotateTowards = currentGrabber != null ? currentGrabber.grabberOrigin.pos : transform.position;
        Vector3 deltaVec = rotateTowards - transform.position;
        Vector3 deltaVecProjected = Vector3.ProjectOnPlane(deltaVec, transform.up);

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + deltaVec);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + deltaVecProjected);
    }
}