using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class BuildUI : MonoBehaviour
{
    public VisualTreeAsset buildUiUxml;
    public GameObject arduinoUnoPrefab;
    public Camera playerCamera;
    public float placementMaxDistance = 3f;

    static readonly string[] ShapeNames = { "Cube", "Sphere", "Capsule", "Cylinder" };

    UIDocument _doc;
    VisualElement _root;
    Button _createRobotButton;
    VisualElement _buildPanel;
    DropdownField _shapeDropdown;
    Button _spawnShapeButton;
    DropdownField _categoryDropdown;
    DropdownField _componentDropdown;
    Button _spawnComponentButton;

    // Resolved once in RefreshComponentDropdown (where Resources.LoadAll + the
    // RobotPeripheral filter already disambiguate the real prefab from any raw mesh
    // source file sharing its folder/base name) and reused directly at spawn time,
    // rather than re-deriving a string path and calling Resources.Load again -- doing
    // that re-lookup was ambiguous whenever a prefab and its own .obj/.fbx source shared
    // the same name in the same folder, and could silently resolve to the raw mesh.
    readonly Dictionary<string, GameObject> _componentPrefabs = new();

    void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        if (buildUiUxml == null)
        {
            Debug.LogError("BuildUI: buildUiUxml is not assigned.");
            return;
        }

        _root = buildUiUxml.CloneTree();
        _doc.rootVisualElement.Add(_root);

        _createRobotButton = _root.Q<Button>("CreateRobotButton");
        _buildPanel = _root.Q<VisualElement>("BuildPanel");
        _shapeDropdown = _root.Q<DropdownField>("ShapeDropdown");
        _spawnShapeButton = _root.Q<Button>("SpawnShapeButton");
        _categoryDropdown = _root.Q<DropdownField>("CategoryDropdown");
        _componentDropdown = _root.Q<DropdownField>("ComponentDropdown");
        _spawnComponentButton = _root.Q<Button>("SpawnComponentButton");

        _shapeDropdown.choices = new List<string>(ShapeNames);
        _shapeDropdown.index = 0;

        var categories = new List<string>();
        foreach (var cat in Constants.ComponentCategories)
            categories.Add(cat.Category);
        _categoryDropdown.choices = categories;
        _categoryDropdown.index = categories.Count > 0 ? 0 : -1;
        _categoryDropdown.RegisterValueChangedCallback(_ => RefreshComponentDropdown());
        RefreshComponentDropdown();

        _createRobotButton.clicked += CreateRobot;
        _spawnShapeButton.clicked += SpawnSelectedShape;
        _spawnComponentButton.clicked += SpawnSelectedComponent;
    }

    void Update()
    {
        if (_root == null) return;

        // Steps aside whenever another full-panel UI (Wiring, Code, ...) claims the
        // shared UI slot -- doesn't claim the slot itself since it has no toggle key of
        // its own, it's just always-on-while-editing unless something else is showing.
        bool editing = Parameters.EDITING && UIState.Open == UIPanel.None;
        _root.style.display = editing ? DisplayStyle.Flex : DisplayStyle.None;
        if (!editing) return;

        bool hasRobot = Microcontroller.Registry.Count > 0;
        _createRobotButton.style.display = hasRobot ? DisplayStyle.None : DisplayStyle.Flex;
        _buildPanel.style.display = hasRobot ? DisplayStyle.Flex : DisplayStyle.None;
    }

    void RefreshComponentDropdown()
    {
        var entry = FindCategory(SelectedCategory());
        var items = new List<string>();
        _componentPrefabs.Clear();
        if (entry.ResourcesPath != null)
        {
            // Resources.LoadAll also picks up raw imported mesh source files (.obj/.fbx)
            // sitting in the same folder, not just actual .prefab assets -- both produce
            // a GameObject and there's no runtime-safe way to tell them apart by file type
            // (AssetDatabase/PrefabUtility are Editor-only). Only list things that are
            // actually usable robot components.
            foreach (var prefab in Resources.LoadAll<GameObject>(entry.ResourcesPath))
            {
                if (prefab.GetComponentInChildren<RobotPeripheral>() == null) continue;
                items.Add(prefab.name);
                _componentPrefabs[prefab.name] = prefab;
            }
        }
        _componentDropdown.choices = items;
        _componentDropdown.index = items.Count > 0 ? 0 : -1;
    }

    string SelectedCategory()
    {
        if (_categoryDropdown.choices == null || _categoryDropdown.index < 0) return null;
        return _categoryDropdown.choices[_categoryDropdown.index];
    }

    (string Category, string ResourcesPath) FindCategory(string name)
    {
        foreach (var c in Constants.ComponentCategories)
            if (c.Category == name) return c;
        return (null, null);
    }

    Vector3 GetPlacementPosition()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        float distance = placementMaxDistance;
        if (Physics.Raycast(ray, out var hit, placementMaxDistance))
            distance = Mathf.Max(0.1f, hit.distance - 0.1f); // stop just short of whatever's in the way
        return ray.origin + ray.direction * distance;
    }

    void CreateRobot()
    {
        if (arduinoUnoPrefab == null)
        {
            Debug.LogError("BuildUI: arduinoUnoPrefab is not assigned.");
            return;
        }

        var instance = Instantiate(arduinoUnoPrefab, GetPlacementPosition(), arduinoUnoPrefab.transform.rotation);
        var mc = instance.GetComponent<Microcontroller>();
        if (mc != null)
        {
            if (string.IsNullOrEmpty(mc.RobotId))
                mc.RobotId = "Robot1";
            mc.Pins = PinLayout.Load(Constants.ArduinoUnoPinsResourcePath);
        }
    }

    void SpawnSelectedShape()
    {
        if (_shapeDropdown.index < 0) return;

        PrimitiveType type = _shapeDropdown.choices[_shapeDropdown.index] switch
        {
            "Cube" => PrimitiveType.Cube,
            "Sphere" => PrimitiveType.Sphere,
            "Capsule" => PrimitiveType.Capsule,
            "Cylinder" => PrimitiveType.Cylinder,
            _ => PrimitiveType.Cube
        };

        var go = GameObject.CreatePrimitive(type);
        go.transform.position = GetPlacementPosition();

        // SphereCollider/CapsuleCollider (Unity's defaults for these primitives) don't
        // actually support non-uniform scaling in physics -- they stay a true
        // sphere/capsule sized off roughly the largest scale axis, even once
        // ShapeTransformGizmo has visually stretched the mesh into an ellipsoid. A
        // convex MeshCollider tracks the real (possibly non-uniformly scaled) mesh
        // exactly, so placement/touch detection matches what's actually on screen.
        // BoxCollider (Cube) already scales correctly per-axis and doesn't need this.
        if (type != PrimitiveType.Cube)
        {
            var oldCollider = go.GetComponent<Collider>();
            // DestroyImmediate, not Destroy -- BeginPlacement below calls
            // PlacementPreview.Begin(), which collects colliders via
            // GetComponentsInChildren in this same frame. Destroy() defers actual
            // removal until end of frame, so that scan would still see (and cache) this
            // collider, then it'd vanish out from under that cached reference later,
            // throwing MissingReferenceException the next time it's touched.
            if (oldCollider != null) DestroyImmediate(oldCollider);
            go.AddComponent<MeshCollider>().convex = true;
        }

        BeginPlacement(go, null);
    }

    void SpawnSelectedComponent()
    {
        if (_componentDropdown.index < 0) return;

        var entry = FindCategory(SelectedCategory());
        string prefabName = _componentDropdown.choices[_componentDropdown.index];
        if (!_componentPrefabs.TryGetValue(prefabName, out var prefab) || prefab == null)
        {
            Debug.LogError($"BuildUI: no resolved prefab for '{prefabName}' -- try reselecting the category.");
            return;
        }

        var go = Instantiate(prefab, GetPlacementPosition(), prefab.transform.rotation);
        var peripheral = go.GetComponentInChildren<RobotPeripheral>();
        if (peripheral != null)
        {
            // Every component lives at "<Category>/<Name>/<Name>" -- one subfolder per
            // component, named to match its own prefab/json. Only the JSON needs a
            // string-path lookup (no ambiguity risk -- nothing else produces a TextAsset
            // at that path).
            peripheral.Pins = PinLayout.Load($"{entry.ResourcesPath}/{prefabName}/{prefabName}");
            peripheral.WiringGridPosition = WiringGrid.RandomPositionNearOrigin();
        }
        BeginPlacement(go, prefabName);
    }

    void BeginPlacement(GameObject go, string prefabBaseName)
    {
        // Only one placement can be held at a time -- spawning a new one cancels
        // whatever's still being positioned instead of leaving it floating.
        if (PlacementPreview.Current != null)
            Destroy(PlacementPreview.Current.gameObject);

        var preview = go.AddComponent<PlacementPreview>();
        preview.Begin(playerCamera, prefabBaseName);
    }
}
