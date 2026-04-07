using backend.Application.Interfaces;
using backend.Infrastructure.Providers;
using backend.Infrastructure.Scraping;
using backend.Infrastructure.Sessions;
using backend.Infrastructure.Data;
using backend.Infrastructure.Repositories;
using backend.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient();

builder.Services.AddScoped<IJobRepository, JobRepository>();
builder.Services.AddScoped<IProviderSessionRepository, ProviderSessionRepository>();
builder.Services.AddSingleton<IBrowserPool, BrowserPool>();
builder.Services.AddScoped<IPlaywrightScraper, PlaywrightScraper>();
builder.Services.AddScoped<ISessionManager, LinkedInSessionManager>();
builder.Services.AddScoped<IJobProvider, LinkedInProvider>();
builder.Services.AddScoped<IJobProvider, IndeedProvider>();
builder.Services.AddScoped<IJobOrchestrator, JobOrchestrator>();
builder.Services.Configure<LinkedInAuthOptions>(builder.Configuration.GetSection("LinkedInAuth"));
builder.Services.AddSingleton<ILinkedInAuthService, LinkedInAuthService>();
builder.Services.AddHostedService<JobsAutomationHostedService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // Allow local/lab frontend origins during development (localhost, 127.0.0.1, LAN IPs).
            policy
                .SetIsOriginAllowed(origin =>
                {
                    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                    {
                        return false;
                    }

                    if (!uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
                        !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                        uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    return System.Net.IPAddress.TryParse(uri.Host, out _);
                })
                .AllowAnyHeader()
                .AllowAnyMethod();
            return;
        }

        policy.WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
    await DatabaseSeeder.SeedAsync(dbContext);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();
app.Run();
