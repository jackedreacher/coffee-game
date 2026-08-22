#if COOP_ONLINE
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// The throwaway player for the connection test.
//
// Deliberately not the squirrel. The question this scene answers is "do two
// machines on two different internet connections end up looking at the same
// thing", and answering it with the real character would mean the first bug
// could be Relay, ownership, the NavMesh, the animator, the plateau or the tap
// router -- six suspects for one symptom.
//
// It is still built the way the real player will be built, though: the tap is
// read on the machine that owns the capsule, sent to the host as a REQUEST, and
// the host is the only one that moves anything. Nothing here would have to be
// unlearned later.
[RequireComponent(typeof(NetworkObject))]
public class CoopCapsule : NetworkBehaviour
{
    [SerializeField] private float speed = 6f;
    [SerializeField] private float rayLength = 200f;

    [Tooltip("Kendi kapsulun. Yesil olan sensin")]
    [SerializeField] private Color mine = new Color(.24f, .80f, .38f);

    [Tooltip("Diger oyuncunun kapsulu")]
    [SerializeField] private Color theirs = new Color(.95f, .55f, .15f);

    // Server only. The guest's capsule has one of these too, sitting at zero
    // and never read, because the guest never moves anything
    private Vector3 target;
    private bool going;

    public override void OnNetworkSpawn()
    {
        // Scaffolding stays in the room that was built for it.
        //
        // There is one player prefab for the whole game and right now it is a
        // grey capsule -- so connecting from the main menu would drop two of
        // them into the middle of the kitchen. Switched off rather than not
        // spawned: the object still has to exist, because the badge riding on
        // it is what tells the menu which animal the other player picked
        if (!CoopTestRoom.Here)
        {
            Renderer[] skins = GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < skins.Length; i++)
                skins[i].enabled = false;

            enabled = false;

            return;
        }

        // Spread out by the host, before anybody has seen either of them.
        //
        // Both capsules are the same prefab and Netcode spawns them where the
        // prefab sits, so left alone the second player arrives standing exactly
        // inside the first -- which looks like the guest never spawned at all,
        // and that is the one bug this whole scene exists to rule out
        if (IsServer)
        {
            Vector3 spot = transform.position;

            spot.x = OwnerClientId % 2 == 0 ? -2.5f : 2.5f;
            spot.z = 0f;

            transform.position = spot;
        }

        target = transform.position;

        // Coloured by whose screen this is, not by client id. "The green one is
        // you" is a sentence that works on both phones at once; "player 0 is
        // green" needs both players to know which one they are
        Renderer skin = GetComponentInChildren<Renderer>();

        if (skin != null)
            skin.material.color = IsOwner ? mine : theirs;
    }

    private void Update()
    {
        if (IsOwner)
            Ask();

        if (IsServer)
            Walk();
    }

    // ---- the owner's half: a tap is a request, not a move -------------------

    private void Ask()
    {
        // Pointer covers mouse in the editor and touch on device, no split
        // needed -- same as the kitchen controller
        if (Pointer.current == null || !Pointer.current.press.wasPressedThisFrame)
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        Camera eye = Camera.main;

        if (eye == null)
            return;

        Ray ray = eye.ScreenPointToRay(Pointer.current.position.ReadValue());

        if (!Physics.Raycast(ray, out RaycastHit hit, rayLength))
            return;

        // Nothing moves here. Not even a little, not even locally. The whole
        // point of the exercise is that the capsule does not budge until the
        // host has heard about it and sent the movement back -- if it moved
        // first, a broken connection would look exactly like a working one
        MoveRpc(hit.point);
    }

    [Rpc(SendTo.Server)]
    private void MoveRpc(Vector3 point)
    {
        // Everything a host is allowed to refuse would be refused here: too far
        // to have tapped, standing in a wall, holding something that forbids
        // it. Nothing to refuse yet on an empty floor, so this is only the
        // shape of the check, not the check
        target = point;
        target.y = transform.position.y;

        going = true;
    }

    // ---- the host's half: the only place anything actually moves ------------

    private void Walk()
    {
        if (!going)
            return;

        Vector3 here = transform.position;
        Vector3 step = Vector3.MoveTowards(here, target, speed * Time.deltaTime);

        transform.position = step;

        Vector3 facing = target - here;

        facing.y = 0f;

        if (facing.sqrMagnitude > .0001f)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(facing), 12f * Time.deltaTime);

        if ((step - target).sqrMagnitude < .0025f)
            going = false;
    }
}
#endif
