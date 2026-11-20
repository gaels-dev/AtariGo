using System.Globalization;
using System.Text;

using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Server.Application.Friends;

namespace AtariGo.Server.Services;

internal static class FriendRequestPageTokens
{
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;
    private const int MaximumTokenLength = 32;

    public static SentFriendRequestsQuery Parse(PageRequest? page)
    {
        int size = page?.PageSize ?? 0;
        if (size == 0)
        {
            size = DefaultPageSize;
        }

        if (size < 1 || size > MaximumPageSize)
        {
            throw InvalidPage();
        }

        return new SentFriendRequestsQuery(size, Decode(page?.PageToken ?? string.Empty));
    }

    public static string Encode(long? requestId)
    {
        return requestId.HasValue
            ? Convert.ToBase64String(Encoding.UTF8.GetBytes(
                requestId.Value.ToString(CultureInfo.InvariantCulture)))
            : string.Empty;
    }

    private static long Decode(string token)
    {
        if (token.Length == 0)
        {
            return 0;
        }

        if (token.Length > MaximumTokenLength)
        {
            throw InvalidPage();
        }

        string value;
        try
        {
            value = Encoding.UTF8.GetString(Convert.FromBase64String(token));
        }
        catch (FormatException)
        {
            throw InvalidPage();
        }

        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture,
                out long requestId) || requestId <= 0)
        {
            throw InvalidPage();
        }

        return requestId;
    }

    private static RpcException InvalidPage()
    {
        return new RpcException(new Status(StatusCode.InvalidArgument, "Invalid request page."));
    }
}
