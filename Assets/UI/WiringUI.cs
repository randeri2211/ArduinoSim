using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// Grid-based schematic wiring: pins render as small colored circles (color = capability,
// name/type shown only on hover) positioned in an unbounded Vector2Int coordinate space
// centered on the microcontroller at (0,0). A wire is routed by clicking a start pin,
// clicking free grid points to bend it through, then clicking a compatible end pin to
// finish -- Escape cancels the in-progress wire and discards its bends. Components spawn
// at a random position near the origin (see BuildUI/WiringGrid) and can be dragged by
// their label afterward to organize the layout; wires follow live since rendering always
// reads each peripheral's current WiringGridPosition.
public class WiringUI : MonoBehaviour
{
    public KeyCode toggleKey = KeyCode.F4;
    public VisualTreeAsset wiringUiUxml;

    enum PlaceState { Idle, Placing, Dragging }

    // Identifies one pin, either on the microcontroller or on a component.
    struct PinRef
    {
        public bool IsMcu;
        public string McuPin;
        public RobotPeripheral Peripheral;
        public string PeripheralPin;

        public static PinRef Mcu(string pin) => new PinRef { IsMcu = true, McuPin = pin };
        public static PinRef Component(RobotPeripheral p, string pin) => new PinRef { IsMcu = false, Peripheral = p, PeripheralPin = pin };
    }

    static bool PinRefEquals(PinRef a, PinRef b) =>
        a.IsMcu == b.IsMcu && a.McuPin == b.McuPin && a.Peripheral == b.Peripheral && a.PeripheralPin == b.PeripheralPin;

    class PinVisual
    {
        public PinRef Ref;
        public Vector2Int LocalOffset; // for MCU pins this IS the absolute grid pos (anchor is (0,0))
        public List<string> Capabilities;
        public VisualElement Circle;

        public Vector2Int AbsoluteGridPos => (Ref.IsMcu ? Vector2Int.zero : Ref.Peripheral.WiringGridPosition) + LocalOffset;
    }

    static readonly Color PwmColor = new(1f, 0.6f, 0.15f);
    static readonly Color DigitalColor = new(0.3f, 0.55f, 1f);
    static readonly Color AnalogColor = new(0.3f, 0.85f, 0.4f);

    static Color CapabilityColor(List<string> caps)
    {
        if (caps.Contains("PWM")) return PwmColor;
        if (caps.Contains("Digital")) return DigitalColor;
        return AnalogColor;
    }

    UIDocument _doc;
    VisualElement _panel; // the actual "WiringUIRoot" element -- CloneTree() wraps it in a
                          // TemplateContainer, so this is a Q() lookup, not the CloneTree() result itself
    Label _robotLabel;
    VisualElement _gridCanvas;
    Label _statusLabel;
    Label _tooltip;

    Microcontroller _mc;
    PlaceState _state = PlaceState.Idle;
    PinRef _startPin;
    RobotPeripheral _draggingPeripheral;
    readonly List<Vector2Int> _pendingBends = new();
    Vector2 _cursorPixel;
    Vector2 _panPixels;

    const float PanSpeed = 300f; // pixels/sec, mirrors CameraMovement's fast/slow modifiers

    readonly List<PinVisual> _pinVisuals = new();
    Label _mcuLabel;
    int _mcuBoxWidthCells = 3;
    readonly List<(Label label, RobotPeripheral peripheral, int widthCells)> _componentLabels = new();

    const int ComponentBoxHeightCells = 3;
    const int McuBoxHeightCells = 2; // spans exactly from the digital row (y=-1) to the analog row (y=+1)
    const float PinCircleRadius = 7f; // half of the 14px pin circle

    void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        if (wiringUiUxml == null)
        {
            Debug.LogError("WiringUI: wiringUiUxml is not assigned.");
            return;
        }

        var root = wiringUiUxml.CloneTree();
        // CloneTree() wraps the UXML content in an extra container with no explicit
        // size (defaults to auto height). WiringUIRoot below is position:absolute with
        // top/bottom:0 to stretch-fill its parent, but an absolutely positioned child
        // doesn't contribute back to an auto-height parent's size -- so without this,
        // the wrapper (and therefore WiringUIRoot) resolves to height 0, and ScrollView
        // (unlike a plain VisualElement) actually clips its content to that zero height.
        root.style.position = Position.Absolute;
        root.style.left = 0;
        root.style.right = 0;
        root.style.top = 0;
        root.style.bottom = 0;
        // The wrapper itself is just a sizing shim -- it must never intercept clicks
        // (even while WiringUIRoot inside it is display:none), or it silently blocks
        // input to everything else (e.g. BuildUI's button) across the whole screen.
        // Its children (pin circles etc.) still hit-test normally regardless of this.
        root.pickingMode = PickingMode.Ignore;
        _doc.rootVisualElement.Add(root);

        _panel = root.Q<VisualElement>("WiringUIRoot");
        _robotLabel = root.Q<Label>("RobotLabel");
        _gridCanvas = root.Q<VisualElement>("GridCanvas");
        _statusLabel = root.Q<Label>("StatusLabel");
        _tooltip = root.Q<Label>("TooltipLabel");
        _panel.style.display = DisplayStyle.None; // explicit, don't rely on the UXML attribute alone

        _gridCanvas.generateVisualContent += DrawGrid;
        _gridCanvas.RegisterCallback<ClickEvent>(OnCanvasClicked);
        _gridCanvas.RegisterCallback<PointerMoveEvent>(OnCanvasPointerMove);
        // resolvedStyle.width/height (what ViewCenter() reads) isn't valid until layout
        // has actually run for this element -- that's not guaranteed the instant it goes
        // from display:none to visible, so positions computed right away can be off
        // until the real size is known. GeometryChangedEvent fires exactly when that
        // happens (and again on any later resize), so reposition there instead of
        // guessing at a delay.
        _gridCanvas.RegisterCallback<GeometryChangedEvent>(_ =>
        {
            RepositionAll();
            _gridCanvas.MarkDirtyRepaint();
        });
    }

    void Update()
    {
        if (_panel == null) return;

        if (Input.GetKeyDown(toggleKey))
        {
            bool nowVisible = UIState.Toggle(UIPanel.Wiring);
            if (nowVisible)
            {
                _mc = FindActiveMicrocontroller();
                if (_mc == null)
                {
                    Debug.Log("WiringUI: no robot to wire yet.");
                    UIState.Close(UIPanel.Wiring);
                    nowVisible = false;
                }
                else
                {
                    Rebuild();
                }
            }
            _panel.style.display = nowVisible ? DisplayStyle.Flex : DisplayStyle.None;
            ResetInteraction();
        }

        // Another panel (Code, ...) claimed the shared UI slot -- step aside.
        if (UIState.Open != UIPanel.Wiring && _panel.style.display != DisplayStyle.None)
        {
            _panel.style.display = DisplayStyle.None;
            ResetInteraction();
        }

        if (!Parameters.EDITING && _panel.style.display != DisplayStyle.None)
        {
            _panel.style.display = DisplayStyle.None;
            UIState.Close(UIPanel.Wiring);
            ResetInteraction();
        }

        if (_state == PlaceState.Placing && Input.GetKeyDown(KeyCode.Escape))
            ResetInteraction();

        if (_panel.style.display != DisplayStyle.None)
            HandlePan();
    }

    // WASD pans the schematic view -- reuses CameraMovement's keybinds (and fast/slow
    // modifiers) since the player camera is disabled while this panel is open.
    void HandlePan()
    {
        Vector2 pan = Vector2.zero;
        if (Input.GetKey(KeyCode.W)) pan.y -= 1;
        if (Input.GetKey(KeyCode.S)) pan.y += 1;
        if (Input.GetKey(KeyCode.A)) pan.x -= 1;
        if (Input.GetKey(KeyCode.D)) pan.x += 1;
        if (pan == Vector2.zero) return;

        float speed = PanSpeed;
        if (Input.GetKey(KeyCode.LeftShift)) speed *= 2f;
        if (Input.GetKey(KeyCode.LeftControl)) speed *= 0.5f;

        _panPixels += pan.normalized * speed * Time.deltaTime;
        RepositionAll();
        _gridCanvas.MarkDirtyRepaint();
    }

    static Microcontroller FindActiveMicrocontroller()
    {
        // Single-robot for now, matches the rest of the build system (BuildUI.CreateRobot
        // hardcodes RobotId "Robot1" and gates on Microcontroller.Registry.Count > 0).
        foreach (var mc in Microcontroller.Registry.Values)
            return mc;
        return null;
    }

    static int ExtractPinNumber(string pin)
    {
        int i = 0;
        while (i < pin.Length && !char.IsDigit(pin[i])) i++;
        return int.TryParse(pin.Substring(i), out var n) ? n : 0;
    }

    void Rebuild()
    {
        _robotLabel.text = $"Robot: {_mc.RobotId}";

        _gridCanvas.Clear();
        _pinVisuals.Clear();
        _componentLabels.Clear();

        // MCU pins: two short local rows straddling the (0,0) anchor -- digital pins in
        // one row, analog in another, ordered by numeric suffix (not alphabetically,
        // which would misorder D2/D3/.../D13).
        var digital = new List<string>();
        var analog = new List<string>();
        foreach (var pin in _mc.Pins.Keys)
            (pin.StartsWith("A") ? analog : digital).Add(pin);
        digital.Sort((a, b) => ExtractPinNumber(a).CompareTo(ExtractPinNumber(b)));
        analog.Sort((a, b) => ExtractPinNumber(a).CompareTo(ExtractPinNumber(b)));

        // Just a text label now -- the box itself is drawn directly (see DrawBox in
        // DrawGrid), not built out of a styled VisualElement.
        _mcuBoxWidthCells = Mathf.Max(digital.Count, analog.Count) + 2;
        _mcuLabel = new Label($"{_mc.RobotId} Microcontroller");
        StyleAsLabel(_mcuLabel, _mcuBoxWidthCells);
        _gridCanvas.Add(_mcuLabel);

        for (int i = 0; i < digital.Count; i++)
            AddPinCircle(PinRef.Mcu(digital[i]), new Vector2Int(i - digital.Count / 2, -1), _mc.Pins[digital[i]]);
        for (int i = 0; i < analog.Count; i++)
            AddPinCircle(PinRef.Mcu(analog[i]), new Vector2Int(i - analog.Count / 2, 1), _mc.Pins[analog[i]]);

        // Component pins, clustered around each component's own (draggable) position.
        // Each gets a bounds box (min 3x3 cells, +1 wide per pin) labeled with its
        // LocalName -- the label doubles as the drag handle.
        foreach (var peripheral in _mc.GetComponentsInChildren<RobotPeripheral>())
        {
            int pinCount = peripheral.Pins.Count;
            int boxWidth = Mathf.Max(3, pinCount + 2);
            // Even width so `topLeft.x = pos.x - width/2` divides exactly -- an odd
            // width would truncate asymmetrically and leave the box off-center.
            if (boxWidth % 2 != 0) boxWidth++;

            var label = new Label(peripheral.LocalName);
            StyleAsLabel(label, boxWidth);
            var capturedPeripheral = peripheral;
            label.RegisterCallback<PointerDownEvent>(evt => OnLabelPointerDown(evt, capturedPeripheral, label));
            label.RegisterCallback<PointerMoveEvent>(evt => OnLabelPointerMove(evt, capturedPeripheral));
            label.RegisterCallback<PointerUpEvent>(evt => OnLabelPointerUp(evt, capturedPeripheral, label));
            label.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            _gridCanvas.Add(label);
            _componentLabels.Add((label, peripheral, boxWidth));

            int i = 0;
            foreach (var kv in peripheral.Pins)
            {
                var localOffset = new Vector2Int(i - pinCount / 2, 1);
                AddPinCircle(PinRef.Component(peripheral, kv.Key), localOffset, kv.Value);
                i++;
            }
        }

        RefreshPinVisualStates();
        RepositionAll(); // best-effort now; GeometryChangedEvent corrects it once real layout resolves
        _gridCanvas.MarkDirtyRepaint();
    }

    static void StyleAsLabel(Label label, int widthCells)
    {
        label.style.position = Position.Absolute;
        label.style.color = Color.white;
        label.style.width = widthCells * WiringGrid.CellSize;
        label.style.unityTextAlign = TextAnchor.UpperCenter;
        label.style.whiteSpace = WhiteSpace.Normal;
    }

    void AddPinCircle(PinRef pinRef, Vector2Int localOffset, List<string> capabilities)
    {
        var circle = new VisualElement();
        circle.style.position = Position.Absolute;
        circle.style.width = 14;
        circle.style.height = 14;
        circle.style.borderTopLeftRadius = 7;
        circle.style.borderTopRightRadius = 7;
        circle.style.borderBottomLeftRadius = 7;
        circle.style.borderBottomRightRadius = 7;
        circle.style.backgroundColor = CapabilityColor(capabilities);
        circle.style.borderLeftWidth = 2;
        circle.style.borderRightWidth = 2;
        circle.style.borderTopWidth = 2;
        circle.style.borderBottomWidth = 2;

        var visual = new PinVisual { Ref = pinRef, LocalOffset = localOffset, Capabilities = capabilities, Circle = circle };
        circle.RegisterCallback<PointerEnterEvent>(_ => ShowTooltip(visual));
        circle.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());
        circle.RegisterCallback<ClickEvent>(evt =>
        {
            OnPinClicked(visual);
            evt.StopPropagation();
        });

        _gridCanvas.Add(circle);
        _pinVisuals.Add(visual);
    }

    Vector2 ViewCenter() => new Vector2(_gridCanvas.resolvedStyle.width / 2f, _gridCanvas.resolvedStyle.height / 2f) - _panPixels;

    void PositionElement(VisualElement el, Vector2Int gridPos, Vector2 center)
    {
        Vector2 px = WiringGrid.ToPixels(gridPos, center);
        el.style.left = px.x - PinCircleRadius;
        el.style.top = px.y - PinCircleRadius;
    }

    // Top-left grid-dot coordinate of each box -- shared by RepositionAll (for the text
    // label) and DrawGrid (for the actual drawn box), so both always agree exactly.
    Vector2Int McuBoxTopLeft() => new Vector2Int(-_mcuBoxWidthCells / 2, -1);

    Vector2Int ComponentBoxTopLeft(RobotPeripheral peripheral, int widthCells) =>
        new Vector2Int(peripheral.WiringGridPosition.x - widthCells / 2, peripheral.WiringGridPosition.y + 1 - ComponentBoxHeightCells);

    void PositionLabel(VisualElement label, Vector2Int boxTopLeftGrid, Vector2 center)
    {
        Vector2 px = WiringGrid.ToPixels(boxTopLeftGrid, center);
        label.style.left = px.x;
        label.style.top = px.y + 2;
    }

    void RepositionAll()
    {
        Vector2 center = ViewCenter();
        if (_mcuLabel != null)
            PositionLabel(_mcuLabel, McuBoxTopLeft(), center);
        foreach (var (label, peripheral, widthCells) in _componentLabels)
            PositionLabel(label, ComponentBoxTopLeft(peripheral, widthCells), center);
        foreach (var pv in _pinVisuals)
            PositionElement(pv.Circle, pv.AbsoluteGridPos, center);
    }

    void RepositionPeripheral(RobotPeripheral peripheral)
    {
        Vector2 center = ViewCenter();
        foreach (var (label, p, widthCells) in _componentLabels)
            if (p == peripheral)
                PositionLabel(label, ComponentBoxTopLeft(p, widthCells), center);
        foreach (var pv in _pinVisuals)
            if (!pv.Ref.IsMcu && pv.Ref.Peripheral == peripheral)
                PositionElement(pv.Circle, pv.AbsoluteGridPos, center);
    }

    PinVisual FindPinVisual(PinRef pinRef) => _pinVisuals.Find(pv => PinRefEquals(pv.Ref, pinRef));

    void ShowTooltip(PinVisual pv)
    {
        string name = pv.Ref.IsMcu ? pv.Ref.McuPin : pv.Ref.PeripheralPin;
        _tooltip.text = $"{name} ({string.Join(", ", pv.Capabilities)})";
        _tooltip.style.display = DisplayStyle.Flex;
        Vector2 local = _panel.WorldToLocal(pv.Circle.worldBound.position);
        _tooltip.style.left = local.x + 16;
        _tooltip.style.top = local.y - 4;
    }

    void HideTooltip() => _tooltip.style.display = DisplayStyle.None;

    void OnCanvasPointerMove(PointerMoveEvent evt)
    {
        _cursorPixel = evt.localPosition;
        if (_state == PlaceState.Placing)
            _gridCanvas.MarkDirtyRepaint();
    }

    void OnCanvasClicked(ClickEvent evt)
    {
        if (_state != PlaceState.Placing) return;
        Vector2Int gridPos = WiringGrid.ToGrid(evt.localPosition, ViewCenter());
        _pendingBends.Add(gridPos);
        _gridCanvas.MarkDirtyRepaint();
    }

    void OnPinClicked(PinVisual pv)
    {
        _statusLabel.text = " ";

        bool wired = pv.Ref.IsMcu ? _mc.IsWired(pv.Ref.McuPin) : _mc.IsWired(pv.Ref.Peripheral, pv.Ref.PeripheralPin);

        if (_state == PlaceState.Idle)
        {
            if (wired)
            {
                string wiredMcuPin = pv.Ref.IsMcu ? pv.Ref.McuPin
                    : (_mc.TryGetMcuPin(pv.Ref.Peripheral, pv.Ref.PeripheralPin, out var found) ? found : null);
                if (wiredMcuPin != null) _mc.Disconnect(wiredMcuPin);
                _gridCanvas.MarkDirtyRepaint();
                return;
            }

            _startPin = pv.Ref;
            _pendingBends.Clear();
            _state = PlaceState.Placing;
            RefreshPinVisualStates();
            return;
        }

        if (_state != PlaceState.Placing) return;

        if (PinRefEquals(pv.Ref, _startPin))
        {
            ResetInteraction();
            return;
        }

        if (pv.Ref.IsMcu == _startPin.IsMcu)
        {
            _statusLabel.text = "connect a microcontroller pin to a component pin";
            return;
        }

        if (wired)
        {
            _statusLabel.text = "that pin is already wired";
            return;
        }

        bool startIsMcu = _startPin.IsMcu;
        string mcuPin = startIsMcu ? _startPin.McuPin : pv.Ref.McuPin;
        RobotPeripheral peripheral = startIsMcu ? pv.Ref.Peripheral : _startPin.Peripheral;
        string peripheralPin = startIsMcu ? pv.Ref.PeripheralPin : _startPin.PeripheralPin;

        var bends = new List<Vector2Int>(_pendingBends);
        if (!startIsMcu) bends.Reverse(); // stored convention: bends run from the mcu-pin side

        if (_mc.TryConnect(mcuPin, peripheral, peripheralPin, bends, out var error))
        {
            ResetInteraction();
            _gridCanvas.MarkDirtyRepaint();
        }
        else
        {
            _statusLabel.text = error; // stay in Placing, bends kept
        }
    }

    void OnLabelPointerDown(PointerDownEvent evt, RobotPeripheral peripheral, VisualElement label)
    {
        if (_state != PlaceState.Idle) return;
        _state = PlaceState.Dragging;
        _draggingPeripheral = peripheral;
        label.CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    void OnLabelPointerMove(PointerMoveEvent evt, RobotPeripheral peripheral)
    {
        if (_state != PlaceState.Dragging || _draggingPeripheral != peripheral) return;
        Vector2 canvasLocal = _gridCanvas.WorldToLocal(evt.position);
        peripheral.WiringGridPosition = WiringGrid.ToGrid(canvasLocal, ViewCenter());
        RepositionPeripheral(peripheral);
        _gridCanvas.MarkDirtyRepaint();
        evt.StopPropagation();
    }

    void OnLabelPointerUp(PointerUpEvent evt, RobotPeripheral peripheral, VisualElement label)
    {
        if (_state != PlaceState.Dragging || _draggingPeripheral != peripheral) return;
        label.ReleasePointer(evt.pointerId);
        _state = PlaceState.Idle;
        _draggingPeripheral = null;
        evt.StopPropagation();
    }

    void RefreshPinVisualStates()
    {
        foreach (var pv in _pinVisuals)
        {
            bool wired = pv.Ref.IsMcu ? _mc.IsWired(pv.Ref.McuPin) : _mc.IsWired(pv.Ref.Peripheral, pv.Ref.PeripheralPin);
            bool isStart = _state == PlaceState.Placing && PinRefEquals(pv.Ref, _startPin);
            Color border = isStart ? Color.yellow : wired ? Color.white : new Color(0f, 0f, 0f, 0.6f);
            pv.Circle.style.borderTopColor = border;
            pv.Circle.style.borderBottomColor = border;
            pv.Circle.style.borderLeftColor = border;
            pv.Circle.style.borderRightColor = border;
        }
    }

    void ResetInteraction()
    {
        _state = PlaceState.Idle;
        _draggingPeripheral = null;
        _pendingBends.Clear();
        RefreshPinVisualStates();
        _gridCanvas?.MarkDirtyRepaint();
    }

    // Draws the box directly from its grid position/size -- exact pixel math, no
    // VisualElement layout/styling involved.
    static void DrawBox(Painter2D painter, Vector2Int topLeftGrid, int widthCells, int heightCells, Vector2 center)
    {
        Vector2 topLeft = WiringGrid.ToPixels(topLeftGrid, center);
        Vector2 size = new Vector2(widthCells, heightCells) * WiringGrid.CellSize;

        painter.fillColor = new Color(1f, 1f, 1f, 0.12f);
        painter.strokeColor = new Color(1f, 1f, 1f, 0.5f);
        painter.lineWidth = 1f;
        painter.BeginPath();
        painter.MoveTo(topLeft);
        painter.LineTo(new Vector2(topLeft.x + size.x, topLeft.y));
        painter.LineTo(topLeft + size);
        painter.LineTo(new Vector2(topLeft.x, topLeft.y + size.y));
        painter.ClosePath();
        painter.Fill();
        painter.Stroke();
    }

    void DrawGrid(MeshGenerationContext ctx)
    {
        float w = _gridCanvas.resolvedStyle.width;
        float h = _gridCanvas.resolvedStyle.height;
        if (w <= 0 || h <= 0 || _mc == null) return;

        Vector2 center = ViewCenter();
        var painter = ctx.painter2D;

        painter.fillColor = new Color(1f, 1f, 1f, 0.15f);
        int minX = Mathf.FloorToInt(-center.x / WiringGrid.CellSize);
        int maxX = Mathf.CeilToInt((w - center.x) / WiringGrid.CellSize);
        int minY = Mathf.FloorToInt(-center.y / WiringGrid.CellSize);
        int maxY = Mathf.CeilToInt((h - center.y) / WiringGrid.CellSize);
        for (int gx = minX; gx <= maxX; gx++)
        {
            for (int gy = minY; gy <= maxY; gy++)
            {
                Vector2 p = WiringGrid.ToPixels(new Vector2Int(gx, gy), center);
                painter.BeginPath();
                painter.Arc(p, 1.5f, 0, 360);
                painter.Fill();
            }
        }

        DrawBox(painter, McuBoxTopLeft(), _mcuBoxWidthCells, McuBoxHeightCells, center);
        foreach (var (label, peripheral, widthCells) in _componentLabels)
            DrawBox(painter, ComponentBoxTopLeft(peripheral, widthCells), widthCells, ComponentBoxHeightCells, center);

        painter.strokeColor = Color.yellow;
        painter.lineWidth = 2f;
        foreach (var pv in _pinVisuals)
        {
            if (!pv.Ref.IsMcu) continue;
            if (!_mc.TryGetRoute(pv.Ref.McuPin, out var route)) continue;
            var target = FindPinVisual(PinRef.Component(route.Peripheral, route.PeripheralPin));
            if (target == null) continue;

            painter.BeginPath();
            painter.MoveTo(WiringGrid.ToPixels(pv.AbsoluteGridPos, center));
            foreach (var bend in route.Bends)
                painter.LineTo(WiringGrid.ToPixels(bend, center));
            painter.LineTo(WiringGrid.ToPixels(target.AbsoluteGridPos, center));
            painter.Stroke();
        }

        if (_state == PlaceState.Placing)
        {
            var startVisual = FindPinVisual(_startPin);
            if (startVisual != null)
            {
                painter.strokeColor = new Color(1f, 1f, 0.4f, 0.8f);
                painter.lineWidth = 2f;
                painter.BeginPath();
                painter.MoveTo(WiringGrid.ToPixels(startVisual.AbsoluteGridPos, center));
                foreach (var bend in _pendingBends)
                    painter.LineTo(WiringGrid.ToPixels(bend, center));
                painter.LineTo(_cursorPixel);
                painter.Stroke();
            }
        }
    }
}
