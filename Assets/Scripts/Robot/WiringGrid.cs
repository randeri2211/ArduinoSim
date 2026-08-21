using UnityEngine;

// Shared grid/coordinate math for the wiring schematic -- used by BuildUI (spawn-time
// component placement) and WiringUI (rendering, hit-testing, dragging). The coordinate
// space itself is unbounded (plain Vector2Int, no min/max); only the canvas that views
// it is a fixed pixel size, centered on (0,0) (the microcontroller's fixed anchor).
public static class WiringGrid
{
    public const int CellSize = 28; // pixels per grid cell
    public const int MaxSpawnDistance = 6; // grid cells from (0,0)

    // Uniform-over-a-disc random point within MaxSpawnDistance of the origin (sqrt on
    // the radius sample avoids clustering points near the center).
    public static Vector2Int RandomPositionNearOrigin()
    {
        float angle = Random.value * Mathf.PI * 2f;
        float radius = Mathf.Sqrt(Random.value) * MaxSpawnDistance;
        int x = Mathf.RoundToInt(Mathf.Cos(angle) * radius);
        int y = Mathf.RoundToInt(Mathf.Sin(angle) * radius);
        return new Vector2Int(x, y);
    }

    public static Vector2 ToPixels(Vector2Int gridPos, Vector2 viewCenterPixels) =>
        viewCenterPixels + new Vector2(gridPos.x, gridPos.y) * CellSize;

    public static Vector2Int ToGrid(Vector2 pixelPos, Vector2 viewCenterPixels)
    {
        Vector2 local = (pixelPos - viewCenterPixels) / CellSize;
        return new Vector2Int(Mathf.RoundToInt(local.x), Mathf.RoundToInt(local.y));
    }
}
