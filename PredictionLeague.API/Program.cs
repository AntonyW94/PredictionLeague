// In PredictionLeague.API/Program.cs

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using PredictionLeague.Core.Models;
using PredictionLeague.Core.Repositories;
using PredictionLeague.Core.Services;
using PredictionLeague.Infrastructure.Identity;
using PredictionLeague.Infrastructure.Repositories;
using PredictionLeague.Infrastructure.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// --- 1. Add Connection String and DI Registrations ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddScoped<IMatchRepository, MatchRepository>();
builder.Services.AddScoped<IUserPredictionRepository, UserPredictionRepository>();
builder.Services.AddScoped<ILeagueRepository, LeagueRepository>();
builder.Services.AddScoped<IGameWeekResultRepository, GameWeekResultRepository>();
builder.Services.AddScoped<IGameYearRepository, GameYearRepository>();

builder.Services.AddScoped<ILeagueService, LeagueService>();
builder.Services.AddScoped<IPredictionService, PredictionService>();
builder.Services.AddScoped<IGameWeekService, GameWeekService>();
builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();

// --- 2. Configure ASP.NET Core Identity with our custom stores ---
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddUserStore<DapperUserStore>()
    .AddRoleStore<DapperRoleStore>()
    .AddDefaultTokenProviders();

// --- 3. Configure Authentication with JWT ---
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["Secret"];

builder.Services.AddAuthentication(opt =>
{
    opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
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
});

// Other services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowAll");

// >> THIS IS THE CRUCIAL FIX <<
// You must add UseAuthentication() AND UseAuthorization()
// The order is important: Authentication (who are you?) happens before Authorization (what are you allowed to do?).
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();




//using FluentValidation;
//using FluentValidation.AspNetCore;
//using Microsoft.AspNetCore.Authentication.JwtBearer;
//using Microsoft.AspNetCore.Identity;
//using Microsoft.IdentityModel.Tokens;
//using Microsoft.OpenApi.Models;
//using PredictionLeague.Core.Models;
//using PredictionLeague.Core.Repositories;
//using PredictionLeague.Core.Services;
//using PredictionLeague.Infrastructure.Identity;
//using PredictionLeague.Infrastructure.Repositories;
//using PredictionLeague.Infrastructure.Services;
//using System.Text;

//var builder = WebApplication.CreateBuilder(args);
//// --- 1. Add Connection String and DI Registrations ---

//var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

//// Add services to the container.
//builder.Services.AddScoped<IMatchRepository, MatchRepository>();
//builder.Services.AddScoped<IUserPredictionRepository, UserPredictionRepository>();
//builder.Services.AddScoped<ILeagueRepository, LeagueRepository>();
//builder.Services.AddScoped<IGameWeekResultRepository, GameWeekResultRepository>();
//builder.Services.AddScoped<IGameYearRepository, GameYearRepository>();

//builder.Services.AddScoped<ILeagueService, LeagueService>();
//builder.Services.AddScoped<IPredictionService, PredictionService>();
//builder.Services.AddScoped<IGameWeekService, GameWeekService>();
//builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();

//// --- 2. Configure ASP.NET Core Identity ---
//// NOTE: This uses the modern 'AdaskoTheBeAsT.Identity.Dapper' package.
//builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
//    {
//        options.SignIn.RequireConfirmedAccount = false;
//        // Add other identity options here if needed
//    })
//    // These two lines are the crucial change:
//    .AddUserStore<DapperUserStore>()
//    .AddRoleStore<DapperRoleStore>()
//    .AddDefaultTokenProviders();

//// --- 3. Configure FluentValidation ---
//builder.Services.AddValidatorsFromAssemblyContaining<Program>();

////var jwtSettings = builder.Configuration.GetSection("JwtSettings");
////var secretKey = jwtSettings["Secret"];

////builder.Services.AddAuthentication(opt =>
////    {
////        opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
////        opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
////    })
////    .AddJwtBearer(options =>
////    {
////        options.TokenValidationParameters = new TokenValidationParameters
////        {
////            ValidateIssuer = true,
////            ValidateAudience = true,
////            ValidateLifetime = true,
////            ValidateIssuerSigningKey = true,
////            ValidIssuer = jwtSettings["Issuer"],
////            ValidAudience = jwtSettings["Audience"],
////            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
////        };
////    });


//builder.Services.AddControllers();

//// Add FluentValidation services
//builder.Services.AddFluentValidationAutoValidation();
//builder.Services.AddValidatorsFromAssemblyContaining<Program>(); // This finds all validators in your API project

//builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen(c =>
//{
//    c.SwaggerDoc("v1", new OpenApiInfo { Title = "PredictionLeague.API", Version = "v1" });

//    // Define the BearerAuth security scheme
//    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
//    {
//        Description = @"JWT Authorization header using the Bearer scheme. <br/> 
//                      Enter 'Bearer' [space] and then your token in the text input below. <br/> 
//                      Example: 'Bearer 12345abcdef'",
//        Name = "Authorization",
//        In = ParameterLocation.Header,
//        Type = SecuritySchemeType.ApiKey,
//        Scheme = "Bearer"
//    });

//    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
//    {
//        {
//            new OpenApiSecurityScheme
//            {
//                Reference = new OpenApiReference
//                {
//                    Type = ReferenceType.SecurityScheme,
//                    Id = "Bearer"
//                },
//                Scheme = "oauth2",
//                Name = "Bearer",
//                In = ParameterLocation.Header,
//            },
//            new List<string>()
//        }
//    });
//});

//// --- 7. Configure CORS ---
//builder.Services.AddCors(options =>
//{
//    options.AddPolicy("AllowAll",
//        builder =>
//        {
//            builder.AllowAnyOrigin()
//                .AllowAnyMethod()
//                .AllowAnyHeader();
//        });
//});

//var app = builder.Build();

//// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI();
//}

//app.UseHttpsRedirection();

//// The order of these is important!
//app.UseCors("AllowAll"); // Enable CORS
//app.UseAuthentication(); // First, who are you?
//app.UseAuthorization(); // Second, what are you allowed to do?

//app.MapControllers();

//app.Run();