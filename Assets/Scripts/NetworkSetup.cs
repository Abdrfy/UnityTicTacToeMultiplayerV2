using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class NetworkSetup : MonoBehaviour
{
    public ushort port = 7777;
    
    void Start()
    {
        Debug.Log("NetworkSetup Start");
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData("127.0.0.1", port);

        NetworkManager.Singleton.StartServer();
    }
}