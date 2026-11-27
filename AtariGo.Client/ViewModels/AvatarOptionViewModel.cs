using System.Globalization;

using AtariGo.Client.Properties;

namespace AtariGo.Client.ViewModels;

public sealed class AvatarOptionViewModel : ViewModelBase
{
    private const int InitialUnlockedMaximum = 5;
    private const int WinsPerUnlock = 5;

    private bool _isSelected;

    public AvatarOptionViewModel(int avatarId, int wins)
    {
        AvatarId = avatarId;
        RequiredWins = Math.Max(0, avatarId - InitialUnlockedMaximum) * WinsPerUnlock;
        IsUnlocked = wins >= RequiredWins;
        string format = Resources.ResourceManager.GetString(
            "ProfileCustomization_Lbl_UnlockAtWins", CultureInfo.CurrentUICulture)
            ?? string.Empty;
        UnlockRequirement = IsUnlocked
            ? string.Empty : string.Format(CultureInfo.CurrentUICulture, format, RequiredWins);
    }

    public int AvatarId
    {
        get;
    }

    public int RequiredWins
    {
        get;
    }

    public bool IsUnlocked
    {
        get;
    }

    public bool IsLocked => !IsUnlocked;

    public string UnlockRequirement
    {
        get;
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
