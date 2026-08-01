using System;
using System.IO;
using System.Text.Json;
using Obcred.Models;

namespace Obcred.Services;

public class UserSettingsService : IUserSettingsService
{
    private readonly string _settingsFilePath;
    public UserSettings CurrentSettings { get; private set; }

    public UserSettingsService()
    {
        // This gets the C:\Users\Username\AppData\Local folder
        string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        
        // Create a dedicated folder for your software
        string myAppFolder = Path.Combine(appDataFolder, "IntegritiEFakturi");
        Directory.CreateDirectory(myAppFolder); // Creates it if it doesn't exist
        
        _settingsFilePath = Path.Combine(myAppFolder, "user-settings.json");
        LoadSettings();
    }

    private void LoadSettings()
    {
        if (File.Exists(_settingsFilePath))
        {
            string json = File.ReadAllText(_settingsFilePath);
            var loaded = JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();

            // Detect a pre-encryption plaintext password so we can migrate it.
            bool legacyPlaintextPassword =
                !string.IsNullOrEmpty(loaded.CertPassword) && !SecretProtector.IsProtected(loaded.CertPassword);

            // Keep the password decrypted in memory (UjpService needs it to open the .pfx).
            loaded.CertPassword = SecretProtector.Unprotect(loaded.CertPassword);
            CurrentSettings = loaded;

            // One-time migration: re-write the file so the password is stored encrypted.
            if (legacyPlaintextPassword)
                SaveSettings(CurrentSettings);
        }
        else
        {
            CurrentSettings = new UserSettings();
        }
    }

    public void SaveSettings(UserSettings settings)
    {
        // Keep the plaintext password in memory for use this session...
        CurrentSettings = settings;

        // ...but never write it to disk in the clear: encrypt it with DPAPI at rest.
        string plaintextPassword = settings.CertPassword;
        settings.CertPassword = SecretProtector.Protect(plaintextPassword);

        string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsFilePath, json);

        // Restore the in-memory copy to plaintext.
        settings.CertPassword = plaintextPassword;
    }

    // Helper to check if the user needs to see the setup screen on startup
    public bool IsConfigured()
    {
        // Check if they have EITHER a File OR a USB Thumbprint
        bool hasCert = !string.IsNullOrWhiteSpace(CurrentSettings.CertPath) || 
                       !string.IsNullOrWhiteSpace(CurrentSettings.CertThumbprint);
                       
        // They must have a cert AND an EDB
        return hasCert && !string.IsNullOrWhiteSpace(CurrentSettings.SellerEdb);
    }
}