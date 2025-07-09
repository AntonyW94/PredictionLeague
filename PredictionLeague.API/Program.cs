using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using PredictionLeague.API.Middleware;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Domain.Models;
using PredictionLeague.Infrastructure.Data;
using PredictionLeague.Infrastructure.Identity;
using PredictionLeague.Infrastructure.Repositories;
using PredictionLeague.Infrastructure.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<IDbConnectionFactory, SqlConnectionFactory>();

builder.Services.AddScoped<ILeagueRepository, LeagueRepository>();
builder.Services.AddScoped<IMatchRepository, MatchRepository>();
builder.Services.AddScoped<IRoundRepository, RoundRepository>();
builder.Services.AddScoped<IRoundResultRepository, RoundResultRepository>();
builder.Services.AddScoped<ISeasonRepository, SeasonRepository>();
builder.Services.AddScoped<ITeamRepository, TeamRepository>();
builder.Services.AddScoped<IUserPredictionRepository, UserPredictionRepository>();

builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();
builder.Services.AddScoped<ILeagueService, LeagueService>();
builder.Services.AddScoped<ILeagueMemberService, LeagueMemberService>();
builder.Services.AddScoped<IMatchService, MatchService>();
builder.Services.AddScoped<IPredictionService, PredictionService>();
builder.Services.AddScoped<IRoundService, RoundService>();
builder.Services.AddScoped<ISeasonService, SeasonService>();
builder.Services.AddScoped<ITeamService, TeamService>();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddUserStore<DapperUserStore>()
    .AddRoleStore<DapperRoleStore>()
    .AddSignInManager<SignInManager<ApplicationUser>>()
    .AddDefaultTokenProviders();

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Unspecified;
});

builder.Services.ConfigureExternalCookie(options =>
{
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["Secret"];
if (secretKey == null)
    throw new Exception("JWT Secret Not Found");

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
})
.AddGoogle(options =>
{
    var googleAuthenticationSection = builder.Configuration.GetSection("Authentication:Google");

    var googleClientId = googleAuthenticationSection["ClientId"];
    if (googleClientId == null)
        throw new Exception("Google ClientId Not Found");

    var googleClientSecret = googleAuthenticationSection["ClientSecret"];
    if (googleClientSecret == null)
        throw new Exception("Google ClientSecret Not Found");

    options.ClientId = googleClientId;
    options.ClientSecret = googleClientSecret;
    options.CallbackPath = "/signin-google";
    options.SignInScheme = IdentityConstants.ExternalScheme;
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ApiUser", policy =>
    {
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
    });

    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAssertion(context => true)
        .Build();
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin", policy =>
    {
        // Replace with your actual Blazor client's URL for production
        policy.WithOrigins("https://localhost:7132")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

var app = builder.Build();

app.UseMiddleware<ErrorHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowSpecificOrigin");
app.UseRouting();
app.UseCookiePolicy();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    foreach (ApplicationUserRole role in Enum.GetValues(typeof(ApplicationUserRole)))
    {
        var roleExist = await roleManager.RoleExistsAsync(role.ToString());
        if (!roleExist)
        {
            await roleManager.CreateAsync(new IdentityRole(role.ToString()));
        }
    }
}

app.Run();