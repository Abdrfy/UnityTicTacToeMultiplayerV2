using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class NetworkSetup : MonoBehaviour
{
    public GameStartBroadcaster broadcaster;
    public ushort port = 7777;
    private int connectedClientsCount = 0;
    
    void Start()
    {
        Debug.Log("NetworkSetup Start");
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData("127.0.0.1", port);

        NetworkManager.Singleton.OnClientConnectedCallback += OnServerClientConnected;
        NetworkManager.Singleton.StartServer();
    }

    private void OnServerClientConnected(ulong clientId)
    {
        Debug.Log($"[Server] Client connected: {clientId}");
        connectedClientsCount++;
        broadcaster.RegisterPlayer(clientId);
        if (connectedClientsCount == 2)
        {
            Debug.Log("[Server] Both players connected. Starting game...");
            broadcaster.SendStartGameClientRpc();
        }
    }
}