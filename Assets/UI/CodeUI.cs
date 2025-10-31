using UnityEngine;
using UnityEngine.UIElements;

public class CodeUI : MonoBehaviour
{
    public KeyCode toggleKey = KeyCode.F1;
    public KeyCode runKey = KeyCode.F2;
    public VisualTreeAsset overlayUxml;

    UIDocument _doc;
    VisualElement _overlay;
    TextField CodeField;
    void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        if (overlayUxml != null)
        {
            _overlay = overlayUxml.CloneTree();
            _overlay.name = "BigTextOverlay"; // ensures predictable name
            _doc.rootVisualElement.Add(_overlay);

            // Start hidden
            _overlay.style.display = DisplayStyle.None;

            // Get references
            CodeField = _overlay.Q<TextField>("CodeUI");
            if (CodeField != null)
            {
                CodeField.multiline = true;
                CodeField.isDelayed = false;
                CodeField.RegisterValueChangedCallback(evt =>
                {
                    // handle text change
                    Debug.Log(evt.newValue);
                });
            }
        }
        else
        {
            Debug.LogError("OverlayToggleUI: overlayUxml is not assigned.");
        }
        // VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        // var field = root.Q<TextField>("CodeUI");
        // field.multiline = true;
        // field.isDelayed = false; // get change events as you type
        // field.RegisterValueChangedCallback(evt =>
        // {
        //     // handle text change
        //     Debug.Log(evt.newValue);
        // });
    }

    void Update()
    {
        if (_overlay == null) return;

        // Simple keybind; change to your preferred key
        if (Input.GetKeyDown(toggleKey))
        {
            bool nowVisible = _overlay.style.display == DisplayStyle.None;
            _overlay.style.display = nowVisible ? DisplayStyle.Flex : DisplayStyle.None;

            if (nowVisible && CodeField != null)
            {
                // Optionally grab focus so you can type immediately
                CodeField.Focus();
            }
        }

        if (Input.GetKeyDown(runKey))
        {
            Debug.Log("Running");
            RobotServerRuntime.Send($"{CodeField.value}");
        }
    }
}
