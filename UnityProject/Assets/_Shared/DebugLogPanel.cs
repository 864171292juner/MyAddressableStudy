using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DebugLogPanel : MonoBehaviour
{
    private TextMeshProUGUI _logText;
    private ScrollRect _scrollRect;

    private const int MaxLines = 20;
    private readonly List<string> _lines = new();

    private void Awake()
    {
        _scrollRect = GetComponentInChildren<ScrollRect>();
        _logText = _scrollRect.content.GetComponent<TextMeshProUGUI>();
    }

    public void Log(string message)
    {
        var time = System.DateTime.Now.ToString("HH:mm:ss");
        _lines.Add($"[{time}] {message}");
        if (_lines.Count > MaxLines)
            _lines.RemoveAt(0);
        _logText.text = string.Join("\n", _lines);
        Canvas.ForceUpdateCanvases();
        _scrollRect.verticalNormalizedPosition = 0f;
    }

    public void Clear()
    {
        _lines.Clear();
        _logText.text = string.Empty;
    }
}
