using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using AtariGo.Contracts;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;
using AtariGo.Server.Tests.PasswordRecovery;

namespace AtariGo.Server.Tests.Authentication;

[TestClass]
[TestCategory("Integration")]
[TestCategory("SqlServer")]
public sealed class RegistrationGrpcServiceTests
{
    [TestMethod]
    public async Task RegisterAccount_ValidRequest_PersistsCompleteAccountWithoutMfaAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        var client = new Registration.RegistrationClient(host.Channel);

        RegisterAccountResponse response = await client.RegisterAccountAsync(ValidRequest());

        await using AtariGoDbContext context = host.Database.CreateContext();
        User user = await context.Users.Include(account => account.Credential)
            .Include(account => account.Profile)
            .SingleAsync(account => account.UserName == "Muñoz");
        Assert.AreEqual(RegistrationResult.Success, response.Result);
        Assert.AreEqual("player+go@example.test", user.Email);
        Assert.AreEqual("Active", user.State);
        Assert.IsNotNull(user.Profile);
        Assert.IsNotNull(user.Credential);
        Assert.AreEqual(PasswordVerificationResult.Success,
            new PasswordHasher<User>().VerifyHashedPassword(user,
                user.Credential.PasswordHash, " ValidPassword9 "));
        Assert.AreEqual(0, await context.AuthenticationCodes.CountAsync());
        Assert.AreEqual(0, await context.Sessions.CountAsync(
            session => session.UserId == user.UserId));
        Assert.AreEqual(0, host.Email.Codes.Count);
    }

    [TestMethod]
    public async Task RegisterAccount_InvalidAndDuplicateRequests_PreserveExistingAccountsAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        var client = new Registration.RegistrationClient(host.Channel);
        RegisterAccountRequest invalidName = ValidRequest();
        invalidName.UserName = "name@domain";
        RegisterAccountRequest invalidEmail = ValidRequest();
        invalidEmail.Email = "Display <player@example.test>";
        RegisterAccountRequest duplicateName = ValidRequest();
        duplicateName.UserName = "Owner";
        RegisterAccountRequest duplicateEmail = ValidRequest();
        duplicateEmail.Email = "Owner@example.test";

        Assert.AreEqual(RegistrationResult.Error,
            (await client.RegisterAccountAsync(invalidName)).Result);
        Assert.AreEqual(RegistrationResult.Error,
            (await client.RegisterAccountAsync(invalidEmail)).Result);
        Assert.AreEqual(RegistrationResult.UsernameTaken,
            (await client.RegisterAccountAsync(duplicateName)).Result);
        Assert.AreEqual(RegistrationResult.EmailTaken,
            (await client.RegisterAccountAsync(duplicateEmail)).Result);

        await using AtariGoDbContext context = host.Database.CreateContext();
        Assert.AreEqual(2, await context.Users.CountAsync());
        Assert.AreEqual(2, await context.Credentials.CountAsync());
        Assert.AreEqual(0, await context.AuthenticationCodes.CountAsync());
    }

    private static RegisterAccountRequest ValidRequest()
    {
        return new RegisterAccountRequest
        {
            UserName = " Muñoz ",
            Email = " player+go@example.test ",
            Password = " ValidPassword9 ",
            PasswordConfirmation = " ValidPassword9 "
        };
    }
}
