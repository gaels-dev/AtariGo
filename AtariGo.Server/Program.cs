using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;
using AtariGo.Server.Application.Authentication;
using AtariGo.Server.Security;
using AtariGo.Server.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration
    .GetConnectionString("AtariGoConnection")
    ?? throw new InvalidOperationException(
        "No se encontró la conexión AtariGoConnection.");

builder.Services.AddDbContext<AtariGoDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddSingleton<IRegistrationValidator, RegistrationValidator>();
builder.Services.AddSingleton<ISessionTokenProvider, SessionTokenProvider>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

builder.Services.AddGrpc();

var app = builder.Build();

app.MapGrpcService<AuthenticationGrpcService>();
app.MapGrpcService<LeaderboardGrpcService>();
app.MapGet("/", () =>
    "Communication with gRPC endpoints must be made through a gRPC client.");

app.Run();
