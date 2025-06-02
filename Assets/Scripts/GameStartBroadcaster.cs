using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GameStartBroadcaster : NetworkBehaviour
{
    public class MatchState
    {
        public int[] board = new int[9];
        public ulong Player1Id;
        public ulong Player2Id;
        public int currentTurn; // 1 for Player1, 2 for Player2
    }

    private Dictionary<string, MatchState> matches = new();
    private string activeMatchId = "default"; // For now we only support one match

    void Start(){
        NetworkManager.Singleton.OnClientConnectedCallback += OnServerClientConnected;
    }

    private void OnServerClientConnected(ulong clientId)
    {
        Debug.Log($"[Server] Client connected: {clientId}");
        RegisterPlayer(clientId);
        var match = GetMatch(activeMatchId);
        var connectedClientsCount = (match.Player1Id != 0 && match.Player2Id != 0) ? 2 : 0;
        if (connectedClientsCount == 2)
        {
            Debug.Log("[Server] Both players connected. Starting game...");
            SendStartGameClientRpc();
            UpdateTurnMessageClientRpc(match.Player1Id);
        }
    }

    [ClientRpc]
    public void SendStartGameClientRpc()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartGame(); // Update UI on client
        }
    }

    [ClientRpc]
    public void ExecuteMoveClientRpc(int cellIndex, string mark)
    {
        if (GameManager.Instance != null && GameManager.Instance.cells[cellIndex] != null)
        {
            GameManager.Instance.cells[cellIndex].SetMark(mark);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestMoveServerRpc(ulong clientId, int cellIndex)
    {
        Debug.Log($"GameStartBroadcaster - [Server] Move received from Client {clientId}, Cell: {cellIndex}");
        if (!matches.ContainsKey(activeMatchId)) return;

        var match = matches[activeMatchId];
        // Check if it's the correct player's turn
        if ((match.currentTurn == 1 && clientId != match.Player1Id) ||
            (match.currentTurn == 2 && clientId != match.Player2Id))
        {
            Debug.LogWarning($"[Server] Client {clientId} attempted to move out of turn.");
            return;
        }

        string mark = (clientId == match.Player1Id) ? "X" : "O";
        match.board[cellIndex] = (mark == "X") ? 1 : 2;

        // Toggle turn
        match.currentTurn = (match.currentTurn == 1) ? 2 : 1;

        ulong newTurnClientId = (match.currentTurn == 1) ? match.Player1Id : match.Player2Id;
        UpdateTurnMessageClientRpc(newTurnClientId);

        ExecuteMoveClientRpc(cellIndex, mark);        
    }

    public void RegisterPlayer(ulong clientId)
    {
        Debug.Log("GameStartBroadcaster - RegisterPlayer - ClientId: " + clientId);
        if (!matches.ContainsKey(activeMatchId))
        {
            Debug.Log("GameStartBroadcaster - RegisterPlayer - Match not found, creating new match");
            matches[activeMatchId] = new MatchState();
            matches[activeMatchId].currentTurn = 1;
        }

        var match = matches[activeMatchId];

        if (match.Player1Id == 0)
        {
            Debug.Log("GameStartBroadcaster - RegisterPlayer - Player1Id: " + clientId);
            match.Player1Id = clientId;
        }
        else if (match.Player2Id == 0)
        {
            Debug.Log("GameStartBroadcaster - RegisterPlayer - Player2Id: " + clientId);
            match.Player2Id = clientId;
        }
    }

    public MatchState GetMatch(string matchId)
    {
        matches.TryGetValue(matchId, out var match);
        return match;
    }

    public void ResetMatch(string matchId)
    {
        if (matches.ContainsKey(matchId))
        {
            var existing = matches[matchId];
            matches[matchId] = new MatchState
            {
                Player1Id = existing.Player1Id,
                Player2Id = existing.Player2Id
            };
        }
    }

    [ClientRpc]
    private void UpdateTurnMessageClientRpc(ulong currentTurnClientId)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.alertText.text = NetworkManager.Singleton.LocalClientId == currentTurnClientId ? "Your turn" : "Opponent's turn";
        }
    }
    
}