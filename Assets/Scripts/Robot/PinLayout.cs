using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

// Loads a component/microcontroller's pin capability map from its sidecar JSON file
// (same base name as the prefab, e.g. "Motors/MotorBody.prefab" + "Motors/MotorBody.json").
// Both sides of a wire use the same shape: a microcontroller pin's tags are what it
// offers (e.g. "A0": ["Analog","Digital"]), a component pin's tags are what it needs
// (e.g. a motor's signal pin: ["PWM"]) -- two pins are wire-compatible if their tag
// sets overlap at all.
public class PinFile
{
    public Dictionary<string, List<string>> pins;
}

public static class PinLayout
{
    public static Dictionary<string, List<string>> Load(string resourcesPath)
    {
        var json = Resources.Load<TextAsset>(resourcesPath);
        if (json == null)
        {
            Debug.LogError($"PinLayout: missing {resourcesPath}.json");
            return new Dictionary<string, List<string>>();
        }
        return JsonConvert.DeserializeObject<PinFile>(json.text).pins;
    }

    public static bool Compatible(List<string> a, List<string> b) => a.Any(b.Contains);
}
