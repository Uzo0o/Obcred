using Obcred.Models;

namespace Obcred.Services;

public interface IUserSettingsService
{
    UserSettings CurrentSettings { get; }

    // Points this service at the signed-in account's own settings file (cert,
    // EDB, PDF template, ...). Must be called once a user is known — and again
    // after every account switch — before SaveSettings/IsConfigured are trusted.
    void SwitchUser(string userId);

    void SaveSettings(UserSettings settings);
    bool IsConfigured();
}