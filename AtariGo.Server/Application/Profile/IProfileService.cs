namespace AtariGo.Server.Application.Profile;

public interface IProfileService
{
    Task<UserProfileSnapshot?> GetAsync(int userId, CancellationToken cancellationToken);

    Task<ProfileUpdateResult> UpdateAsync(
        ProfileUpdateInput input,
        CancellationToken cancellationToken);
}
