using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode.Transports.UTP;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public Button startMatchBtn;
    public GridCell[] cells;
    public TMP_Text alertText;
    public GameObject gridParent;
    public Button restartBtn;

    void Awake() { Instance = this; }

    void Start()
    {
        // Hide grid initially
        if (gridParent != null)
            gridParent.SetActive(false);

        startMatchBtn.onClick.AddListener(() =>
        {
            startMatchBtn.interactable = false;
            alertText.text = "Connecting to server...";
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", 7777);
            NetworkManager.Singleton.StartClient();
        });

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    void OnClientConnected(ulong clientId)
    {
        Debug.Log("GameManager - OnClientConnected");
        startMatchBtn.gameObject.SetActive(false);
        alertText.text = "Waiting for opponent...";
    }

    public void StartGame()
    {
        Debug.Log("GameManager - StartGame");

        // Show grid when the game starts
        if (gridParent != null)
            gridParent.SetActive(true);

        foreach (var cell in cells)
        {
            cell.button.interactable = true;
            cell.gameObject.SetActive(true);
        }

        restartBtn.gameObject.SetActive(false);
        alertText.text = "Game Start!";
    }

    [ClientRpc]
    public void StartGameClientRpc()
    {
        StartGame();
    }

    [ClientRpc]
    private void EndMatchClientRpc(string result)
    {
        alertText.text = result;
        foreach (var cell in cells)
        {
            if (cell?.button != null)
                cell.button.interactable = false;
        }
        restartBtn.gameObject.SetActive(true);
    }

    // public void OnRestartButton()
    // {
    //     var match = GameStartBroadcaster.Instance.GetMatch("default");
    //     if (match == null) return;

    //     for (int i = 0; i < match.board.Length; i++)
    //     {
    //         match.board[i] = 0;
    //         cells[i].button.GetComponentInChildren<Text>().text = "";
    //         cells[i].button.interactable = true;
    //     }

    //     restartBtn.gameObject.SetActive(false);
    //     alertText.text = "";
    //     StartGame();
    // }

    [ClientRpc]
    public void AnnounceWinnerClientRpc()
    {
        alertText.text = "You won!";
        DisableCellGridInput();
        // restartBtn.gameObject.SetActive(true);
    }

    [ClientRpc]
    public void AnnounceLoserClientRpc()
    {
        alertText.text = "You lost!";
        DisableCellGridInput();
    //     restartBtn.gameObject.SetActive(true);
    }

    [ClientRpc]
    public void AnnounceDrawClientRpc()
    {
        alertText.text = "It's a draw!";
        DisableCellGridInput();
        // restartBtn.gameObject.SetActive(true);
    }

    private void DisableCellGridInput() {
        foreach (var cell in cells)
        {
            cell.button.interactable = false;
        }
    }
}