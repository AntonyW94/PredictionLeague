var builder = WebApplication.CreateBuilder(args);

// Add services for API controllers
builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // This tells the server to serve the Blazor WebAssembly files
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// This serves the static files from the Client project's wwwroot folder (like index.html, css, etc.)
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

// This maps your API controllers (e.g., AuthController, LeaguesController)
app.MapControllers();

// This is a fallback that ensures any route not handled by the API
// is sent to the Blazor app's index.html file to handle client-side routing.
app.MapFallbackToFile("index.html");

app.Run();