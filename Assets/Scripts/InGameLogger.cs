using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LogViewer : MonoBehaviour
{
    public TextMeshProUGUI logText;
    public ScrollRect scrollRect;

    private void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    void HandleLog(string logString, string stackTrace, LogType type)
    {
        logText.text += logString + "\n";
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f; // Auto-scroll to bottom
    }
}