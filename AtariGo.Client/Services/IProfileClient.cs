using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public interface IProfileClient
{
    Task<UserProfile> GetProfileAsync(CancellationToken cancellationToken);

    Task<UpdateProfileResponse> UpdateProfileAsync(
        UpdateProfileRequest request,
        CancellationToken cancellationToken);
}
