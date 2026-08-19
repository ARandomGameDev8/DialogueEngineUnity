using System.IO;
using UnityEngine;

public class File_S
{
    public string absolute_path;
    private StreamReader reader;

    public File_S(string path)
    {
        this.absolute_path = path;

        if (!File.Exists(absolute_path))
        {
            Debug.LogError($"File not found: {absolute_path}");
            Debug.LogError($"Full path: {Path.GetFullPath(absolute_path)}");
            return;
        }

        try
        {
            reader = new StreamReader(absolute_path);
            Debug.Log($"Successfully opened file: {absolute_path}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to open file {absolute_path}: {e.Message}");
        }
    }

    public StreamReader get_reader()
    {
        if (reader == null)
        {
            Debug.LogError("StreamReader is null - file may not exist or failed to open");
        }
        return reader;
    }

    // FIX: Added Close() so the StreamReader is properly disposed after compilation
    public void Close()
    {
        reader?.Close();
        reader?.Dispose();
        reader = null;
    }
}
