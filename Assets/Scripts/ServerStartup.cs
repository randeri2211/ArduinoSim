using UnityEngine;
using System.Diagnostics;   // for Process
using System.IO;

public class RunPythonOnStart : MonoBehaviour
{
    private Process process;
    private bool done = false;
    [Header("Python Settings")]
    public string pythonFileName = "Main.py";
    public string pythonExePath = "python"; // or "python3" if on mac/linux
    void Update()
    {
        if(done)
        {
            return;
        }
        // Build path to the Python file inside Assets/Scripts/
        string pythonScriptPath = Path.Combine(Application.dataPath, "Scripts", pythonFileName);

        // Check paths
        if (!File.Exists(pythonScriptPath))
        {
            UnityEngine.Debug.LogError($"Python script not found: {pythonScriptPath}");
            return;
        }

        // Create process info
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = pythonExePath,                // expects python in PATH
            Arguments = $"\"{pythonScriptPath}\"",   // quote path in case of spaces
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        // Start the process
        process = new Process();
        process.StartInfo = psi;

        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                UnityEngine.Debug.Log($"[Python]: {e.Data}");
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                UnityEngine.Debug.LogError($"[Python ERROR]: {e.Data}");
        };
        UnityEngine.Debug.Log("starting server");
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        done = true;
    }

    void OnDisable()
    {
        if (process == null) return;
        try
        {
            if (!process.HasExited)
            {
                // Try graceful first (no window to close, so go straight to kill)
                // Kill the whole tree (child processes) where supported.
                process.Kill();
                process.WaitForExit(2000);
            }
        }
        catch { /* ignore */ }
        finally
        {
            try { process.Dispose(); } catch { }
            process = null;
        }
    }
}
