using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GridCell : NetworkBehaviour
{
    public Button button;
    private TextMeshProUGUI buttonText;

    void Start()
    {
        Debug.Log("GridCell - Start");
        buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
        button.onClick.AddListener(OnClick);
    }

    void OnClick()
    {
        Debug.Log("GridCell - OnClick");
        if (buttonText.text != "-") {  
            Debug.Log("GridCell - OnClick - Button already clicked");
            return;
        }

        var broadcaster = FindFirstObjectByType<GameStartBroadcaster>();
        if (broadcaster == null)
        {
            Debug.LogWarning("GridCell - GameStartBroadcaster not found");
            return;
        }

        // if (!broadcaster.isMyTurn)
        // {
        //     Debug.Log("GridCell - Not your turn");
        //     return;
        // }

        Debug.Log("GridCell - OnClick - Request move");
        broadcaster.RequestMoveServerRpc(NetworkManager.Singleton.LocalClientId, GetCellIndex());
    }

    public void SetMark(string mark)
    {
        if (buttonText != null)
        {
            buttonText.text = mark;
        }
    }

    int GetCellIndex() { return int.Parse(gameObject.name.Replace("Cell", "")) - 1; }
}