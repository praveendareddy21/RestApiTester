using RestApiTester.Components;
using RestApiTester.Models;
using RestApiTester.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<OAuth2Settings>(builder.Configuration.GetSection("OAuth2"));
builder.Services.AddSingleton<OAuth2TokenService>();

builder.Services.AddScoped<OpenApiParserService>();
builder.Services.AddScoped<ApiExecutorService>();
builder.Services.AddScoped<SoapToJsonConverter>();
builder.Services.AddHttpClient();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
