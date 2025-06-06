using System.Collections;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplay;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public GridCell[] cells;
    public Text alertText;
    public Button startGameButton;
    public int winRate = 50; // Assign this based on actual player stats
    private Coroutine heartbeatCoroutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private async void Start()
    {
        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log($"Signed in as: {AuthenticationService.Instance.PlayerId}");
        }

        startGameButton.onClick.AddListener(() => StartMatchmaking().Forget());
    }

    private async Task StartMatchmaking()
    {
        string tier = GetTier(winRate);
        Debug.Log("Gamemanager - StartMatchmaking - With Tier: " + tier);

        try
        {
            // Step 1: Query lobbies manually with filter
            var options = new QueryLobbiesOptions
            {
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(
                        field: QueryFilter.FieldOptions.S1,
                        op: QueryFilter.OpOptions.EQ,
                        value: tier)
                }
            };

            var response = await LobbyService.Instance.QueryLobbiesAsync(options);
            Lobby lobby = null;

            if (response.Results != null && response.Results.Count > 0)
            {
                lobby = await LobbyService.Instance.JoinLobbyByIdAsync(response.Results[0].Id);
                Debug.Log("Joined filtered lobby: " + lobby.Id);
            }
            else
            {
                // Step 2: Create new lobby with the same tier
                lobby = await LobbyService.Instance.CreateLobbyAsync("TicTacToe", 2, new CreateLobbyOptions
                {
                    IsPrivate = false,
                    Data = new Dictionary<string, DataObject>
                    {
                        {
                            "tier",
                            new DataObject(
                                visibility: DataObject.VisibilityOptions.Public,
                                value: tier,
                                index: DataObject.IndexOptions.S1)
                        }
                    }
                });

                Debug.Log("Lobby created: " + lobby.Id);
                heartbeatCoroutine = StartCoroutine(HeartbeatLobby(lobby.Id));
            }

            // Step 3: Wait for second player then start game
            await WaitForOpponentAndLaunch(lobby);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError("Lobby matchmaking failed: " + e);
        }
    }

    private IEnumerator HeartbeatLobby(string lobbyId)
    {
        while (true)
        {
            LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
            yield return new WaitForSeconds(15);
        }
    }

    private async Task WaitForOpponentAndLaunch(Lobby lobby)
    {
        while (true)
        {
            var refreshed = await LobbyService.Instance.GetLobbyAsync(lobby.Id);
            if (refreshed.Players.Count >= 2)
                break;

            await Task.Delay(2000);
        }

        if (heartbeatCoroutine != null)
        {
            StopCoroutine(heartbeatCoroutine);
        }

        alertText.text = "Match Found!";

        // Connect to centralized game server
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData("127.0.0.1", 7777); // Replace with your server IP and port
        NetworkManager.Singleton.StartClient();

        // Register player with server match logic
        var broadcaster = FindFirstObjectByType<GameStartBroadcaster>();
        broadcaster.RegisterPlayerServerRpc(lobby.Id, NetworkManager.Singleton.LocalClientId);
    }

    private string GetTier(int rate)
    {
        if (rate <= 30) return "low";
        if (rate <= 60) return "mid";
        return "high";
    }

    public void StartGame(string matchId)
    {
        alertText.text = "Game Started!";
        GameState.Instance.CurrentMatchId = matchId;
    }

    public void AnnounceWinnerClientRpc()
    {
        alertText.text = "You win!";
        ToggleCellGridInput(false);
    }

    public void AnnounceLoserClientRpc()
    {
        alertText.text = "You lose!";
        ToggleCellGridInput(false);
    }

    public void AnnounceDrawClientRpc()
    {
        alertText.text = "Draw!";
        ToggleCellGridInput(false);
    }

    private void ToggleCellGridInput(bool isEnabled) {
        foreach (var cell in cells)
        {
            cell.button.interactable = isEnabled;
            if (isEnabled) {
                cell.button.GetComponentInChildren<TextMeshProUGUI>().text = "-";
            }
        }
    }
}

public class GameState
{
    private static GameState _instance;
    public static GameState Instance => _instance ??= new GameState();
    public string CurrentMatchId;
}

public static class TaskExtensions
{
    public static void Forget(this Task task)
    {
        // Intentionally ignore task result
    }
}