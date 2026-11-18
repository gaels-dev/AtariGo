using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

internal sealed class StubProfileClient : IProfileClient
{
    public UserProfile Profile { get; set; } = new UserProfile
    {
        UserName = "PlayerOne",
        Email = "first@example.test",
        Wins = 3,
        AvatarId = 1
    };

    public UpdateProfileResponse UpdateResponse { get; set; } = new UpdateProfileResponse
    {
        Status = ProfileUpdateStatus.Updated,
        Profile = new UserProfile
        {
            UserName = "NewName",
            Email = "first@example.test",
            Wins = 3,
            AvatarId = 4
        }
    };

    public UpdateProfileRequest? LastRequest { get; private set; }

    public Task<UserProfile>? ProfileResponse { get; set; }

    public Task<UserProfile> GetProfileAsync(CancellationToken cancellationToken)
    {
        return ProfileResponse ?? Task.FromResult(Profile);
    }

    public Task<UpdateProfileResponse> UpdateProfileAsync(
        UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(UpdateResponse);
    }
}
