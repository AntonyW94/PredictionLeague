using Blazored.LocalStorage;
using PredictionLeague.Web.Client.Authentication;
using PredictionLeague.Web.Client.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

//builder.Services.AddBlazoredLocalStorage();
//builder.Services.AddAuthorizationCore(); 

//builder.Services.AddHttpClient<IAuthService, AuthService>(client =>
//{
//    client.BaseAddress = new Uri("https://localhost:7075/");
//});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode();

app.Run();

