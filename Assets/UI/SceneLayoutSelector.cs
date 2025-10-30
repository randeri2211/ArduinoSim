using UnityEngine;
using UnityEngine.UIElements;

public class SceneLayoutSelector : MonoBehaviour
{
    public GameObject boxPref;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        VisualElement foldout = root.Q<Foldout>("Menu");

        Button Box = foldout.Q<Button>("Box");
        Button Sensor = foldout.Q<Button>("Sensor");

        Box.clicked += () => SpawnBox();

        Sensor.clicked += () =>
        {
            Debug.Log("wire clicked");
        };
    }

    void SpawnBox()
    {
        
        GameObject instance = Instantiate(boxPref, new Vector3(2.95f,0.71f,1.64f), Quaternion.identity);
        // instance.name = "SpawnedObject";
        // instance.transform.SetParent(transform, worldPositionStays: true);

    }
}
