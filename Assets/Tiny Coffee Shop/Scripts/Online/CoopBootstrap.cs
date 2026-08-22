#if COOP_ONLINE
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

// The NetworkManager, built in code instead of placed in a scene.
//
// Every scene needs one and there is no scene where it belongs: the kitchen is
// where the game is, the menu is drawn on top of the kitchen, and a second
// bootstrap scene would mean the single player game loads two scenes to reach
// the same kitchen it loads one for today.
//
// So it is made on demand, the first time somebody presses a co-op button, and
// single player never pays for it at all.
public static class CoopBootstrap
{
    // Loaded from Resources rather than assigned. Same reason as above -- there
    // is no scene to assign it in. Built by:
    //
    // Cooked Fast > Online > 3 - Test Sahnesini Kur
    public const string playerPath = "Online/Coop Player";

    public static NetworkManager Ensure()
    {
        if (NetworkManager.Singleton != null)
            return NetworkManager.Singleton;

        GameObject holder = new GameObject("NetworkManager");

        Object.DontDestroyOnLoad(holder);

        NetworkManager net = holder.AddComponent<NetworkManager>();

        // A component added at runtime gets whatever the field initialisers
        // gave it, and that is not guaranteed to be a config object. Every line
        // below would be a null reference if it were not
        if (net.NetworkConfig == null)
            net.NetworkConfig = new NetworkConfig();

        UnityTransport transport = holder.AddComponent<UnityTransport>();

        net.NetworkConfig.NetworkTransport = transport;

        // Off, and it stays off.
        //
        // Netcode can load the scene on the guest for you, and in a game with a
        // menu scene and a game scene that is what you want. This game has one
        // scene: both players are already standing in the kitchen before either
        // presses a button. Switching it on would make the host order a load of
        // the scene the guest is already in, which costs a black screen and
        // wakes every manager in the room a second time
        net.NetworkConfig.EnableSceneManagement = false;

        // Nothing to approve yet. The room is private, it holds two, and the
        // service already checked the code before the packets got this far.
        // Version checking belongs here later -- an old build joining a new one
        // desyncs quietly, which is far worse than being turned away
        net.NetworkConfig.ConnectionApproval = false;

        GameObject player = Resources.Load<GameObject>(playerPath);

        if (player != null)
            net.NetworkConfig.PlayerPrefab = player;
        else
            Debug.LogWarning("Coop: Resources/" + playerPath + " yok. " +
                             "Baglanti kurulur ama kimse gorunmez.\n" +
                             "Cooked Fast > Online > 3 - Test Sahnesini Kur");

        return net;
    }
}
#endif
