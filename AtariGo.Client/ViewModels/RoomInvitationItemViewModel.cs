using System.Globalization;

using AtariGo.Contracts;

namespace AtariGo.Client.ViewModels;

public sealed class RoomInvitationItemViewModel
{
    public RoomInvitationItemViewModel(RoomInvitation invitation, bool incoming)
    {
        Invitation = invitation.Clone();
        PlayerName = incoming ? invitation.SenderName : invitation.RecipientName;
    }

    public RoomInvitation Invitation { get; }

    public string PlayerName { get; }

    public string RoomCode => Invitation.RoomCode;

    public bool IsPending => Invitation.State == RoomInvitationState.Pending;

    public string StateLabel => Properties.Resources.ResourceManager.GetString(
        "RoomInvitations_State_" + Invitation.State, CultureInfo.CurrentUICulture)
        ?? Invitation.State.ToString();
}
