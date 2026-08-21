public class Constants
{
    public const bool DEBUG = true;
    public const int Port = 7003;
    public const int PwmMax = 255;

    // Component category -> Resources folder to scan. Adding a category is just
    // adding a line here (plus creating that Resources/Arduino/<Category>/ folder).
    public static readonly (string Category, string ResourcesPath)[] ComponentCategories =
    {
        ("Motors", "Arduino/Motors"),
        ("Sensors", "Arduino/Sensors"),
    };
}