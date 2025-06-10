// Now supports multiple concurrent matches using matchId string as key
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class MatchService : NetworkBehaviour
{
    public class MatchState
    {
        public int[] board = new int[9];
        public ulong Player1Id;
        public ulong Player2Id;
        public int currentTurn; // 1 for Player1, 2 for Player2
    }

    private Dictionary<string, MatchState> matches = new();

    private void Awake()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[Server] Client {clientId} disconnected from server");
    }

    [Rpc(SendTo.Server)]
    public void RegisterPlayerServerRpc(string matchId, ulong clientId)
    {
        RegisterPlayer(matchId, clientId);
        var match = GetMatch(matchId);

        if (match.Player1Id != 0 && match.Player2Id != 0)
        {
            Debug.Log($"[Server] Match {matchId} ready. Sending start game.");
            SendStartGameClientRpc(matchId, RpcTarget.Group(new[] { match.Player1Id, match.Player2Id }, RpcTargetUse.Temp));
            var rpcParams = new RpcParams {
                Send = RpcTarget.Group(new[] { match.Player1Id, match.Player2Id }, RpcTargetUse.Temp)
            };
            UpdateTurnMessageClientRpc(matchId, match.Player1Id, rpcParams);
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void SendStartGameClientRpc(string matchId, RpcParams rpcParams)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartGame(matchId); // Update UI on client
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void ExecuteMoveClientRpc(string matchId, int cellIndex, string mark, RpcParams rpcParams)
    {
        if (GameManager.Instance != null && GameManager.Instance.cells[cellIndex] != null)
        {
            GameManager.Instance.cells[cellIndex].SetMark(mark);
        }
    }

    [Rpc(SendTo.Server)]
    public void RequestMoveServerRpc(string matchId, ulong clientId, int cellIndex)
    {
        Debug.Log($"MatchService - [Server] Move received from Client {clientId}, Cell: {cellIndex}");
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
            AnnounceWinRpc(matchId, RpcTarget.Single(clientId, RpcTargetUse.Temp));

            var loserId = (clientId == match.Player1Id) ? match.Player2Id : match.Player1Id;
            AnnounceLoseRpc(matchId, RpcTarget.Single(loserId, RpcTargetUse.Temp));
            
            var bothParams = new RpcParams {
                Send = RpcTarget.Group(new[] { match.Player1Id, match.Player2Id }, RpcTargetUse.Temp)
            };
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
            AnnounceDrawRpc(matchId, RpcTarget.Group(new[] { match.Player1Id, match.Player2Id }, RpcTargetUse.Temp));
            var drawParams = new RpcParams
            {
                Send = RpcTarget.Group(new[] { match.Player1Id, match.Player2Id }, RpcTargetUse.Temp)
            };
            ExecuteMoveClientRpc(matchId, cellIndex, mark, drawParams);
            return;
        }

        var rpcParams = new RpcParams {
            Send = RpcTarget.Group(new[] { match.Player1Id, match.Player2Id }, RpcTargetUse.Temp)
        };

        // Toggle turn
        match.currentTurn = (match.currentTurn == 1) ? 2 : 1;
        ulong newTurnClientId = (match.currentTurn == 1) ? match.Player1Id : match.Player2Id;
        
        UpdateTurnMessageClientRpc(matchId, newTurnClientId, rpcParams);
        ExecuteMoveClientRpc(matchId, cellIndex, mark, rpcParams);
    }

    public string RegisterPlayer(string matchId, ulong clientId)
    {
        if (matches.TryGetValue(matchId, out var match))
        {
            match.Player2Id = clientId;
            return matchId;
        }

        var newMatch = new MatchState
        {
            Player1Id = clientId,
            currentTurn = 1
        };

        matches[matchId] = newMatch;
        return matchId;
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

    [Rpc(SendTo.SpecifiedInParams)]
    private void UpdateTurnMessageClientRpc(string matchId, ulong currentTurnClientId, RpcParams rpcParams)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.alertText.text = NetworkManager.Singleton.LocalClientId == currentTurnClientId ? "Your turn" : "Opponent's turn";
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void AnnounceWinRpc(string matchId, RpcParams rpcParams)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AnnounceWinnerClientRpc();
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void AnnounceLoseRpc(string matchId, RpcParams rpcParams)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AnnounceLoserClientRpc();
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void AnnounceDrawRpc(string matchId, RpcParams rpcParams)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AnnounceDrawClientRpc();
        }
    }
}