using System;
using System.Collections.Generic;
using System.IO;
using BlueprintMod.Models;
using UnityEngine;

namespace BlueprintMod;

/// <summary>Reads and writes blueprints as small text files in the game's persistent data folder.</summary>
public static class Storage
{
    private const string Extension = ".vtbp";

    // Files can come from other people, so refuse anything unreasonably large.
    private const int MaxFiles = 500;
    private const long MaxFileBytes = 1024 * 1024;

    public static string Directory
    {
        get
        {
            string dir = Path.Combine(Application.persistentDataPath, "Blueprints");
            System.IO.Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static List<Blueprint> LoadAll()
    {
        List<Blueprint> result = [];
        string[] files = System.IO.Directory.GetFiles(Directory, $"*{Extension}");
        for (int i = 0; i < Math.Min(files.Length, MaxFiles); i++)
        {
            string file = files[i];
            try
            {
                result.Add(Load(file));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException
                or OverflowException or IndexOutOfRangeException)
            {
                Debug.LogWarning($"Can't load blueprint {file}: {e.Message}");
            }
        }
        result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    public static string NextFreeName()
    {
        for (int i = 1; ; i++)
        {
            string name = $"Blueprint {i}";
            if (!File.Exists(PathFor(name)))
                return name;
        }
    }

    public static void Delete(Blueprint blueprint)
    {
        string path = PathFor(blueprint.Name);
        if (File.Exists(path))
            File.Delete(path);
    }

    public static void Save(Blueprint blueprint)
    {
        File.WriteAllLines(PathFor(blueprint.Name), Serializer.Serialize(blueprint));
    }

    private static Blueprint Load(string path)
    {
        if (new FileInfo(path).Length > MaxFileBytes)
            throw new FormatException("file too large");

        return Serializer.Parse(Path.GetFileNameWithoutExtension(path), File.ReadAllLines(path));
    }

    private static string PathFor(string name) => Path.Combine(Directory, name + Extension);
}
