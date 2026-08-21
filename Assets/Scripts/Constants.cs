public class Constants
{
    public const bool DEBUG = true;
    public const int Port = 7003;
    public const int PwmMax = 255;

    // The Uno prefab is wired into BuildUI as a direct Inspector reference, not
    // Resources.Load'ed by path, so its pin-layout JSON sidecar needs its own known path.
    public const string ArduinoUnoPinsResourcePath = "Arduino/Arduino UNO";

    // Component category -> Resources folder to scan. Adding a category is just
    // adding a line here (plus creating that Resources/Arduino/<Category>/ folder).
    public static readonly (string Category, string ResourcesPath)[] ComponentCategories =
    {
        ("Motors", "Arduino/Motors"),
        ("Sensors", "Arduino/Sensors"),
    };
}