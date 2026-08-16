using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Tabsil.Sijil;
using System;

[RequireComponent(typeof(PlayerAnimator))]
public abstract class PlayerController : MonoBehaviour, IWantToBeSaved
{
    [Header(" Elements ")]
    protected PlayerAnimator playerAnimator;

    [Header(" Settings ")]
    [SerializeField] protected float moveSpeed;
    private Vector3 lastPosition;

    [Header(" Data ")]
    private const string lastPositionKey = "LAST_POSITION";


    // Start is called before the first frame update
    protected virtual void Start()
    {
        playerAnimator = GetComponent<PlayerAnimator>();
    }

    // Update is called once per frame
    void Update()
    {
        UpdateMovement();
        TrySaveLastPosition();
    }

    // Not "agent": ClickToMovePlayerController already has a field by that name,
    // and Unity refuses to serialize the same name twice in one hierarchy
    private UnityEngine.AI.NavMeshAgent pinAgent;

    private void LateUpdate()
    {
        // The joystick pushes a CharacterController, which drifts on Y and has
        // to be pinned back down every frame. A NavMeshAgent is not being
        // pushed -- it places the transform itself, at navmesh height plus its
        // own base offset -- and writing over that position every LateUpdate
        // fights the agent for the transform it is trying to drive
        if (pinAgent == null)
            pinAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();

        if (pinAgent != null && pinAgent.enabled && pinAgent.isOnNavMesh)
            return;

        transform.position = transform.position.With(y: 0);
    }

    protected abstract void UpdateMovement();

    private void TrySaveLastPosition()
    {
        if(Vector3.Distance(transform.position, lastPosition) > 4)
        {
            lastPosition = transform.position;
            Save();
        }
    }

    public abstract bool IsMoving();

    public virtual void SetMoveSpeed(float speed)
    {
        moveSpeed = speed;
    }

    public void Load()
    {
        if (Sijil.TryLoad(this, lastPositionKey, out object _lastPosition))
            transform.position = (Vector3)_lastPosition;
    }

    public void Save()
    {
        Sijil.Save(this, lastPositionKey, transform.position);
    }
}
