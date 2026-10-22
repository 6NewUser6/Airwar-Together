using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Airwar.Core;

namespace Airwar.Persistence;

public sealed class PilotProfile
{
    public int Version { get; set; } = 1;
    public string Callsign { get; set; } = "PILOT 01";
    public int Credits { get; set; }
    public string SelectedShip { get; set; } = "spectre";
    public HashSet<string> UnlockedShips { get; set; } = new() { "spectre" };
    public Dictionary<string, int> BestScores { get; set; } = new();
    public Difficulty Difficulty { get; set; } = Difficulty.Normal;
    public float MusicVolume { get; set; } = .5f;
    public float SoundVolume { get; set; } = .7f;
    public float TouchSensitivity { get; set; } = 1.15f;
    public bool ReducedEffects { get; set; }
    public string RecoveryNotice { get; private set; } = "";
    private static string SavePath => ProjectSettings.GlobalizePath("user://pilot_profile.json");
    private string _savePath = SavePath;

    public static PilotProfile Load(string? isolatedPath = null)
    {
        string savePath = isolatedPath ?? SavePath;
        bool invalid = false;
        foreach (string path in new[] { savePath, savePath + ".bak", savePath + ".tmp" })
        {
            if (!File.Exists(path)) continue;
            try
            {
                var profile = JsonSerializer.Deserialize<PilotProfile>(File.ReadAllText(path));
                if (profile is null || profile.Version != 1) throw new InvalidDataException("Unsupported profile version");
                profile.Normalize();
                profile._savePath = savePath;
                if (invalid) profile.RecoveryNotice = "已从备份恢复飞行员档案。";
                return profile;
            }
            catch (Exception e) when (e is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
            { invalid = true; GD.PushWarning("Profile could not be read: " + e.Message); }
        }
        return new() { _savePath = savePath, RecoveryNotice = invalid ? "档案无法读取，已创建新档案。原文件保留。" : "" };
    }

    public void Normalize()
    {
        Callsign = string.IsNullOrWhiteSpace(Callsign) ? "PILOT 01" : Callsign.Trim()[..Math.Min(Callsign.Trim().Length, 16)];
        Credits = Math.Max(0, Credits);
        UnlockedShips ??= new(); UnlockedShips.Add("spectre");
        BestScores ??= new();
        if (!Enum.IsDefined(Difficulty)) Difficulty = Difficulty.Normal;
        if (!UnlockedShips.Contains(SelectedShip) || ShipCatalog.Find(SelectedShip).Id != SelectedShip) SelectedShip = "spectre";
        MusicVolume = float.IsFinite(MusicVolume) ? Mathf.Clamp(MusicVolume, 0, 1) : .5f;
        SoundVolume = float.IsFinite(SoundVolume) ? Mathf.Clamp(SoundVolume, 0, 1) : .7f;
        TouchSensitivity = float.IsFinite(TouchSensitivity) ? Mathf.Clamp(TouchSensitivity, .6f, 1.8f) : 1.15f;
    }

    public bool Save()
    {
        try
        {
            Normalize(); Directory.CreateDirectory(Path.GetDirectoryName(_savePath)!);
            string temporary = _savePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            if (File.Exists(_savePath))
            {
                // Never overwrite a valid recovery copy with a corrupted primary.
                try
                {
                    var current = JsonSerializer.Deserialize<PilotProfile>(File.ReadAllText(_savePath));
                    if (current?.Version == 1) File.Copy(_savePath, _savePath + ".bak", true);
                }
                catch (JsonException) { File.Copy(_savePath, _savePath + ".corrupt", true); }
            }
            File.Move(temporary, _savePath, true); return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { GD.PushError("Could not save pilot profile: " + e.Message); RecoveryNotice = "存档失败，请检查设备剩余空间。"; return false; }
    }

    public bool Unlock(string id)
    {
        var ship = ShipCatalog.Find(id);
        if (ship.Id != id || UnlockedShips.Contains(id) || Credits < ship.Cost) return false;
        Credits -= ship.Cost; UnlockedShips.Add(id); SelectedShip = id; Save(); return true;
    }

    public bool Record(GameSession session)
    {
        string key = session.Difficulty.ToString();
        bool record = session.Score > BestScores.GetValueOrDefault(key);
        BestScores[key] = Math.Max(session.Score, BestScores.GetValueOrDefault(key));
        Credits += session.Reward; Save(); return record;
    }
}
