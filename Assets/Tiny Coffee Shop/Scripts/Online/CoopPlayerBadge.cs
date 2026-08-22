#if COOP_ONLINE
using Unity.Netcode;
using UnityEngine;

// Who the other player is, as opposed to where they are.
//
// One number: which animal they picked out of the wardrobe. Sent as an index
// rather than a name or a prefab reference, because both builds carry the same
// wardrobe in the same order -- so the index IS the character, and it survives
// a rename of the prefab that a string would not.
//
// A NetworkVariable rather than an RPC, and that is the whole reason this is
// not done with a message on connect. A player who joins while the other one is
// already standing there has to learn what they are wearing, and an RPC that
// fired before they arrived is an RPC they will never hear. Variables are
// handed to latecomers; events are not.
public class CoopPlayerBadge : NetworkBehaviour
{
    // Written by whoever owns this object, read by everybody. Not
    // server-written: the host does not know what animal the guest picked, and
    // asking it to would mean sending the same number twice
    private readonly NetworkVariable<int> skin = new NetworkVariable<int>(-1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Straight out of the same key the wardrobe writes when the player
            // swipes. Read rather than asked for, so this works whether or not
            // the menu happens to be open
            skin.Value = Mathf.Max(0,
                PlayerPrefs.GetInt(CharacterSkinPreview.skinPref, 0));

            return;
        }

        skin.OnValueChanged += Changed;

        // Already there, or still on its way. Both happen: a guest joining an
        // existing room gets the value with the spawn, while the host watching
        // a guest arrive usually sees the object first and the number a frame
        // later
        if (skin.Value >= 0)
            Coop.Mate(skin.Value);
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
            return;

        skin.OnValueChanged -= Changed;

        Coop.MateGone();
    }

    private void Changed(int was, int now)
    {
        if (now >= 0)
            Coop.Mate(now);
    }
}
#endif
