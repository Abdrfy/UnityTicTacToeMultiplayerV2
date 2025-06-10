using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using static GameState;

public class GridCell : MonoBehaviour
{
    public Button button;
    public TextMeshProUGUI buttonText;

    void Start()
    {
        buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
        button.onClick.AddListener(OnClick);
    }

    void OnClick()
    {
        if (buttonText.text != "-") 
        {  
            return;
        }

        var broadcaster = FindFirstObjectByType<MatchService>();
        if (broadcaster == null)
        {
            Debug.LogError("Could not find MatchService"); // Or Debug.LogWarning("Could not find MatchService");
            return;
        }

        if (string.IsNullOrEmpty(GameState.Instance.CurrentMatchId))
        {
            return;
        }

        broadcaster.RequestMoveServerRpc(GameState.Instance.CurrentMatchId, NetworkManager.Singleton.LocalClientId, GetCellIndex());
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