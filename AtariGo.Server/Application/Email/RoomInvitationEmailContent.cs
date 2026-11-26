using System.Globalization;
using System.Resources;

using AtariGo.Server.Application.RoomInvitations;

namespace AtariGo.Server.Application.Email;

public static class RoomInvitationEmailContent
{
    private static readonly ResourceManager resources = new ResourceManager(
        "AtariGo.Server.Properties.RoomInvitationEmail",
        typeof(RoomInvitationEmailContent).Assembly);

    public static EmailMessage CreateMessage(RoomInvitationEntry invitation,
        string recipient, string cultureName)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);
        ArgumentNullException.ThrowIfNull(cultureName);
        CultureInfo culture = SelectCulture(cultureName);
        return new EmailMessage
        {
            Recipient = recipient,
            Subject = GetText("Subject", culture),
            Body = string.Format(culture,
                GetText("Body", culture),
                invitation.Sender.UserName, invitation.RoomCode)
        };
    }

    private static string GetText(string part, CultureInfo culture)
    {
        return resources.GetString("RoomInvitation_Email_" + part, culture)
            ?? throw new InvalidOperationException("The invitation email resource is missing.");
    }

    private static CultureInfo SelectCulture(string name)
    {
        try
        {
            return CultureInfo.GetCultureInfo(name);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }
}
