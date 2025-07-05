using Paws.Core.Abstractions;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OsuParsers.Database;
using OsuParsers.Decoders;
using Realms;

namespace DbTestPlugin;

/// <summary>
/// A simple plugin to test the framework's ability to interact with the osu!lazer and osu!stable databases.
/// </summary>
public class DbTestPlugin : IFunctionalExplicitPlugin
{
    private IHostServices _hostServices = null!;

    // --- IPlugin Properties ---
    public Guid Id => new("a1b2c3d4-e5f6-4a9b-8c7d-6e5f4a3b2c1d");
    public string Name => "DB Test";
    public string Description => "A plugin to test reading/writing to Lazer and Stable databases.";
    public string Version => "1.0.1";

    // --- IFunctionalExplicitPlugin Properties ---
    public string IconName => "database"; // A placeholder name for an icon in the UI

    /// <summary>
    /// This method is called by the Paws framework when the plugin is loaded.
    /// It's the entry point for the plugin's backend logic.
    /// </summary>
    public void Initialize(IHostServices hostServices)
    {
        // We receive the host services from the framework and store the reference.
        // This is how the plugin will talk to the rest of Paws.
        _hostServices = hostServices;
        _hostServices.LogMessage("DB Test Plugin Initialized!", LogLevel.Information, Name);
    }

    /// <summary>
    /// This method is called by the framework when the plugin's UI sends a command.
    /// It acts as a router to the correct internal method.
    /// </summary>
    public async Task<object?> ExecuteCommandAsync(string commandName, object? payload)
    {
        switch (commandName)
        {
            case "get-beatmap-count":
                return GetBeatmapCount();

            case "test-lazer-write":
                return await TestLazerWriteAsync();

            case "get-stable-beatmap-count":
                return await GetStableBeatmapCountAsync();
            
            case "test-stable-write":
                return await TestStableWriteAsync();

            default:
                _hostServices.LogMessage($"Unknown command received: {commandName}", LogLevel.Warning, Name);
                return null;
        }
    }

    /// <summary>
    /// Performs a read-only test by counting the beatmaps in the lazer database.
    /// </summary>
    private string GetBeatmapCount()
    {
        try
        {
            using var db = _hostServices.GetLazerDatabase();

            if (db == null)
                return "Error: Lazer database path is not set or file is inaccessible.";
            
            var beatmaps = db.DynamicApi.All("Beatmap");
            return $"Found {beatmaps.Count} beatmap difficulties in the lazer database.";
        }
        catch (Exception ex)
        {
            _hostServices.LogMessage($"Error reading from Lazer DB: {ex.Message}", LogLevel.Error, Name);
            return $"An unexpected error occurred during read: {ex.Message}";
        }
    }

    /// <summary>
    /// Performs a write test by safely modifying a beatmap set in the lazer database.
    /// </summary>
    private async Task<string> TestLazerWriteAsync()
    {
        try
        {
            string resultMessage = "Could not find an unprotected beatmap set to test with.";

            await _hostServices.PerformLazerWriteAsync(db =>
            {
                dynamic? firstSet = db.DynamicApi.All("BeatmapSet").Filter("Protected == false").FirstOrDefault();

                if (firstSet != null)
                {
                    bool originalValue = firstSet.DeletePending;
                    firstSet.DeletePending = !originalValue;
                    firstSet.DeletePending = originalValue;

                    dynamic? firstBeatmap = null;
                    foreach (var beatmap in firstSet.Beatmaps)
                    {
                        firstBeatmap = beatmap;
                        break;
                    }

                    resultMessage = $"Success! Performed a safe test write on Lazer beatmap set: '{firstBeatmap?.Metadata?.Title ?? "[No Title Found]"}'";
                }
            });

            return resultMessage;
        }
        catch (LazerIsRunningException ex)
        {
            return $"Error: {ex.Message}";
        }
        catch (Exception ex)
        {
            _hostServices.LogMessage($"Unexpected error during Lazer write test: {ex.Message}", LogLevel.Error, Name);
            return $"An unexpected error occurred: {ex.Message}";
        }
    }
    
    /// <summary>
    /// Performs a read test by counting the beatmaps in the stable database.
    /// </summary>
    private async Task<string> GetStableBeatmapCountAsync()
    {
        try
        {
            _hostServices.LogMessage("Requesting stable osu!.db...", LogLevel.Information, Name);
            var osuDb = await _hostServices.GetStableOsuDbAsync() as OsuDatabase;

            if (osuDb == null)
                return "Error: Could not parse osu!.db. Is the stable path set correctly in settings?";

            return $"Found {osuDb.Beatmaps.Count} beatmap difficulties in the stable database.";
        }
        catch (Exception ex)
        {
            _hostServices.LogMessage($"Error reading from stable DB: {ex.Message}", LogLevel.Error, Name);
            return $"An unexpected error occurred: {ex.Message}";
        }
    }

    /// <summary>
    /// Performs a safe write test for the stable database.
    /// </summary>
    private async Task<string> TestStableWriteAsync()
    {
        try
        {
            string resultMessage = "Write test completed.";

            await _hostServices.PerformStableWriteAsync(stablePath =>
            {
                var dbPath = Path.Combine(stablePath, "osu!.db");
                if (!File.Exists(dbPath))
                {
                    resultMessage = "Error: osu!.db not found in the stable directory.";
                    return;
                }

                var db = DatabaseDecoder.DecodeOsu(dbPath);
                var originalName = db.PlayerName;
                db.PlayerName = "Paws Write Test!";
                
                var tempPath = Path.Combine(Path.GetTempPath(), "paws_stable_write_test.db");
                
                // Note: The public API for saving is on the object itself.
                db.Save(tempPath);
                
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                    resultMessage = $"Success! Safely wrote a test DB for player '{originalName}'.";
                }
                else
                {
                    resultMessage = "Error: A test DB was created in memory but failed to write to the temp directory.";
                }
            });

            return resultMessage;
        }
        catch (StableIsRunningException ex)
        {
            return $"Error: {ex.Message}";
        }
        catch (Exception ex)
        {
            _hostServices.LogMessage($"Unexpected error during stable write test: {ex.Message}", LogLevel.Error, Name);
            return $"An unexpected error occurred: {ex.Message}";
        }
    }
}