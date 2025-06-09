using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class NetworkSetup : MonoBehaviour
{
    private ushort port = 7777;
    
    void Start()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData("127.0.0.1", port);

        NetworkManager.Singleton.StartServer();
    }
}