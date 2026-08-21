using UnityEngine;
using UnityEngine.UIElements;

public class CodeUI : MonoBehaviour
{
    public KeyCode toggleKey = KeyCode.F1;
    public KeyCode runKey = KeyCode.F2;
    public KeyCode cancelKey = KeyCode.F3;
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
            bool nowVisible = UIState.Toggle(UIPanel.Code);
            _overlay.style.display = nowVisible ? DisplayStyle.Flex : DisplayStyle.None;

            if (nowVisible && CodeField != null)
            {
                // Optionally grab focus so you can type immediately
                CodeField.Focus();
            }
        }

        // Another panel (Wiring, ...) claimed the shared UI slot -- step aside.
        if (UIState.Open != UIPanel.Code && _overlay.style.display != DisplayStyle.None)
            _overlay.style.display = DisplayStyle.None;

        if (Input.GetKeyDown(runKey))
        {
            Debug.Log("Running");
            foreach (var mc in Microcontroller.Registry.Values)
            {
                mc.Rescan();
                mc.SetStatic(false);
            }
            Parameters.EDITING = false;
            RobotServerRuntime.Send($"{CodeField.value}");
        }

        if (Input.GetKeyDown(cancelKey))
        {
            Debug.Log("canceling");
            foreach (var mc in Microcontroller.Registry.Values)
                mc.SetStatic(true);
            Parameters.EDITING = true;
        }
    }
}



