namespace AtariGo.Server.Application.Authentication;

public sealed record LoginPreparation(LoginOperationResult Result,
    LoginCodeDelivery? Delivery = null)
{
    public bool StartsLogin
    {
        get; init;
    }
}
