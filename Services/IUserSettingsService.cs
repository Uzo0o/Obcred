using Obcred.Models;

namespace Obcred.Services;

public interface IUserSettingsService
{
    UserSettings CurrentSettings { get; }
    void SaveSettings(UserSettings settings);
    bool IsConfigured();
}