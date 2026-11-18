
namespace AtariGo.Server.Data.Entities;

public partial class Profile
{
    public int UserId
    {
        get; set;
    }

    public int AvatarId { get; set; } = DatabaseValues.AvatarIds.Default;

    public string? Description
    {
        get; set;
    }

    public DateTime? LastUpdate
    {
        get; set;
    }

    public virtual User User { get; set; } = null!;
}
