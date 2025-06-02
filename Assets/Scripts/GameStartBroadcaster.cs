// Now supports multiple concurrent matches using matchId string as key
using System;
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

    void Start(){
        NetworkManager.Singleton.OnClientConnectedCallback += OnServerClientConnected;
    }

    private void OnServerClientConnected(ulong clientId)
    {
        Debug.Log($"[Server] Client connected: {clientId}");
        string matchId = RegisterPlayer(clientId);
        var match = GetMatch(matchId);
        var connectedClientsCount = (match.Player1Id != 0 && match.Player2Id != 0) ? 2 : 0;
        if (connectedClientsCount == 2)
        {
            Debug.Log("[Server] Both players connected. Starting game...");
            var rpcParams = new ClientRpcParams {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { match.Player1Id, match.Player2Id } }
            };
            SendStartGameClientRpc(matchId, rpcParams);
            UpdateTurnMessageClientRpc(matchId, match.Player1Id, rpcParams);
        }
    }

    [ClientRpc]
    public void SendStartGameClientRpc(string matchId, ClientRpcParams rpcParams = default)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartGame(matchId); // Update UI on client
        }
    }

    [ClientRpc]
    public void ExecuteMoveClientRpc(string matchId, int cellIndex, string mark, ClientRpcParams rpcParams)
    {
        if (GameManager.Instance != null && GameManager.Instance.cells[cellIndex] != null)
        {
            GameManager.Instance.cells[cellIndex].SetMark(mark);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestMoveServerRpc(string matchId, ulong clientId, int cellIndex)
    {
        Debug.Log($"GameStartBroadcaster - [Server] Move received from Client {clientId}, Cell: {cellIndex}");
        if (!matches.ContainsKey(matchId)) return;

        var match = matches[matchId];
        // Check if it's the correct player's turn
        if ((match.currentTurn == 1 && clientId != match.Player1Id) ||
            (match.currentTurn == 2 && clientId != match.Player2Id))
        {
            Debug.LogWarning($"[Server] Client {clientId} attempted to move out of turn.");
            return;
        }

        string mark = (clientId == match.Player1Id) ? "X" : "O";
        match.board[cellIndex] = (mark == "X") ? 1 : 2;

        // Check win
        int[,] winConditions = new int[,]
        {
            {0,1,2}, {3,4,5}, {6,7,8},
            {0,3,6}, {1,4,7}, {2,5,8},
            {0,4,8}, {2,4,6}
        };

        bool isWin = false;
        int playerMark = (mark == "X") ? 1 : 2;

        for (int i = 0; i < winConditions.GetLength(0); i++)
        {
            if (match.board[winConditions[i, 0]] == playerMark &&
                match.board[winConditions[i, 1]] == playerMark &&
                match.board[winConditions[i, 2]] == playerMark)
            {
                isWin = true;
                break;
            }
        }

        if (isWin)
        {
            var winnerParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
            };
            var loserId = (clientId == match.Player1Id) ? match.Player2Id : match.Player1Id;
            var loserParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { loserId } }
            };

            var bothParams = new ClientRpcParams {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { match.Player1Id, match.Player2Id } }
            };

            AnnounceWinClientRpc(matchId, winnerParams);
            AnnounceLoseClientRpc(matchId, loserParams);
            ExecuteMoveClientRpc(matchId, cellIndex, mark, bothParams);
            return;
        }

        // Check draw
        bool isDraw = true;
        for (int i = 0; i < match.board.Length; i++)
        {
            if (match.board[i] == 0)
            {
                isDraw = false;
                break;
            }
        }

        if (isDraw)
        {
            var drawParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { match.Player1Id, match.Player2Id } }
            };
            AnnounceDrawClientRpc(matchId, drawParams);
            ExecuteMoveClientRpc(matchId, cellIndex, mark, drawParams);
            return;
        }

        var rpcParams = new ClientRpcParams {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { match.Player1Id, match.Player2Id } }
        };

        // Toggle turn
        match.currentTurn = (match.currentTurn == 1) ? 2 : 1;
        ulong newTurnClientId = (match.currentTurn == 1) ? match.Player1Id : match.Player2Id;
        
        UpdateTurnMessageClientRpc(matchId, newTurnClientId, rpcParams);
        ExecuteMoveClientRpc(matchId, cellIndex, mark, rpcParams);
    }

    public string RegisterPlayer(ulong clientId)
    {
        Debug.Log("GameStartBroadcaster - RegisterPlayer - ClientId: " + clientId);

        foreach (var kvp in matches)
        {
            var match = kvp.Value;
            if (match.Player1Id != 0 && match.Player2Id == 0)
            {
                match.Player2Id = clientId;
                Debug.Log("GameStartBroadcaster - RegisterPlayer - Added to existing match: " + kvp.Key);
                return kvp.Key;
            }
        }

        string newMatchId = System.Guid.NewGuid().ToString();
        var newMatch = new MatchState
        {
            Player1Id = clientId,
            currentTurn = 1
        };
        matches[newMatchId] = newMatch;

        Debug.Log("GameStartBroadcaster - RegisterPlayer - Created new match: " + newMatchId);
        return newMatchId;
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
    private void UpdateTurnMessageClientRpc(string matchId, ulong currentTurnClientId, ClientRpcParams rpcParams)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.alertText.text = NetworkManager.Singleton.LocalClientId == currentTurnClientId ? "Your turn" : "Opponent's turn";
        }
    }

    [ClientRpc]
    private void AnnounceWinClientRpc(string matchId, ClientRpcParams rpcParams)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AnnounceWinnerClientRpc();
        }
    }

    [ClientRpc]
    private void AnnounceLoseClientRpc(string matchId, ClientRpcParams rpcParams)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AnnounceLoserClientRpc();
        }
    }

    [ClientRpc]
    private void AnnounceDrawClientRpc(string matchId, ClientRpcParams rpcParams)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AnnounceDrawClientRpc();
        }
    }
}