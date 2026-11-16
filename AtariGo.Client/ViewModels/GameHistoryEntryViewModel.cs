using System.Globalization;

using AtariGo.Client.Models;
using AtariGo.Client.Properties;

namespace AtariGo.Client.ViewModels;

public sealed class GameHistoryEntryViewModel
{
    public GameHistoryEntryViewModel(GameHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        DateLabel = entry.PlayedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        OpponentName = entry.OpponentName;
        StoneColorLabel = Localize("GameHistory_Color_" + entry.StoneColor);
        OpponentStoneColorLabel = Localize("GameHistory_Color_" + entry.OpponentStoneColor);
        ResultLabel = Localize("GameHistory_Result_" + GetResultCode(entry));
    }

    public string DateLabel { get; }

    public string OpponentName { get; }

    public string ResultLabel { get; }

    public string StoneColorLabel { get; }

    public string OpponentStoneColorLabel { get; }

    private static string GetResultCode(GameHistoryEntry entry)
    {
        string result = entry.State switch
        {
            "Cancelled" => "Cancelled",
            "Waiting" => "Waiting",
            "InProgress" => "InProgress",
            _ => entry.Result
        };
        if (entry.State == "Abandoned" && result == "Pending")
        {
            result = "Abandoned";
        }
        return result;
    }

    private static string Localize(string key)
    {
        return Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    }
}
