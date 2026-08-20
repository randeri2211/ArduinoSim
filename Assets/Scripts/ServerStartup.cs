using UnityEngine;
using System;
using System.Diagnostics;   // for Process
using System.IO;

public class RunPythonOnStart : MonoBehaviour
{
    private Process process;
    [Header("Python Settings")]
    public string pythonFileName = "Main.py";
    public string pythonExePath = "python"; // or "python3" if on mac/linux
    void Start()
    {
        // Build path to the Python file inside Assets/Scripts/
        string pythonScriptPath = Path.Combine(Application.dataPath, "Scripts", pythonFileName);

        // Check paths
        if (!File.Exists(pythonScriptPath))
        {
            UnityEngine.Debug.LogError($"Python script not found: {pythonScriptPath}");
            return;
        }

        string resolvedExe = ResolvePythonExecutable();

        // Create process info
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = resolvedExe,
            WorkingDirectory = Path.GetDirectoryName(pythonScriptPath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        // ArgumentList handles quoting/escaping itself -- avoids manual quote-wrapping
        // bugs with paths that contain spaces (this project's folder does: "My project").
        psi.ArgumentList.Add(pythonScriptPath);

        // Python fully-buffers stdout/stderr by default when they're not a real
        // terminal (i.e. redirected, as they are here) -- without this, print()
        // output can sit unflushed for a long time, making a working process look
        // like it's hung. Unbuffered mode avoids needing flush=True everywhere.
        psi.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";

        // Hand Constants.cs's values to the Python side via environment variables,
        // rather than keeping a separately-maintained copy in Utils.py -- Unity is
        // the one launching this process, so it can just tell Python the real values.
        psi.EnvironmentVariables["ROBOT_PORT"] = Constants.Port.ToString();
        psi.EnvironmentVariables["ROBOT_PWM_MAX"] = Constants.PwmMax.ToString();

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
        UnityEngine.Debug.Log($"starting server ({resolvedExe})");
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }

    // Resolves pythonExePath against the live system PATH (User + Machine), bypassing
    // the Editor's own inherited process environment -- which goes stale if Python was
    // installed/added to PATH after the Editor was launched, since a running process
    // never re-reads environment variable changes on its own.
    string ResolvePythonExecutable()
    {
        if (Path.IsPathRooted(pythonExePath) && File.Exists(pythonExePath))
            return pythonExePath;

        string[] candidateNames = { pythonExePath, pythonExePath + ".exe" };

        // Machine entries come first in a real process's effective PATH, with User
        // entries appended after -- match that order (this is why `where python`
        // found C:\Python312 before the WindowsApps stub, but checking User first
        // here previously found the stub instead).
        foreach (var target in new[] { EnvironmentVariableTarget.Machine, EnvironmentVariableTarget.User })
        {
            string path = Environment.GetEnvironmentVariable("PATH", target);
            if (string.IsNullOrEmpty(path)) continue;

            foreach (var dir in path.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                foreach (var candidateName in candidateNames)
                {
                    string candidate = Path.Combine(dir.Trim(), candidateName);
                    if (File.Exists(candidate))
                        return candidate;
                }
            }
        }

        UnityEngine.Debug.LogWarning($"Could not resolve '{pythonExePath}' against the live system PATH; falling back to the OS's own resolution (may use a stale environment).");
        return pythonExePath;
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
