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

var webRootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(Path.Combine(webRootPath, "uploads", "products"));

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
builder.Services.AddMemoryCache();

builder.Services.AddScoped<IJobRepository, JobRepository>();
builder.Services.AddScoped<IProviderSessionRepository, ProviderSessionRepository>();
builder.Services.AddScoped<ICompanyClassifier, CompanyClassifier>();
builder.Services.AddScoped<IOpportunityScorer, OpportunityScorer>();
builder.Services.AddScoped<IOpportunityScorerService, OpportunityScorerService>();
builder.Services.AddScoped<IJobProcessingService, JobProcessingService>();
// Fase 0 — Data Quality Layer
builder.Services.AddSingleton<TechCanonicalizer>();
builder.Services.AddSingleton<IndustryClassifier>();
builder.Services.AddScoped<JobPreprocessorService>();
builder.Services.AddScoped<LeadScoringService>();
builder.Services.AddScoped<RuleBasedAiEnrichmentService>();
builder.Services.AddScoped<IAiEnrichmentService, HybridAiEnrichmentService>();
builder.Services.AddScoped<IMarketIntelligenceService, MarketIntelligenceService>();
// Bloque B + C — Cluster + Decision Engine + Product Generator
builder.Services.AddScoped<IClusterEngine, ClusterEngine>();
builder.Services.AddScoped<IDecisionEngine, DecisionEngine>();
builder.Services.AddScoped<IProductGeneratorService, ProductGeneratorService>();
// Bloque D — LLM Synthesis
builder.Services.AddScoped<IClusterSynthesisService, ClusterSynthesisService>();
builder.Services.AddScoped<IProductSynthesisService, ProductSynthesisService>();
builder.Services.AddHostedService<ClusteringHostedService>();
builder.Services.Configure<SemanticKernelOptions>(builder.Configuration.GetSection("SemanticKernel"));
builder.Services.Configure<MarketIntelligenceEnrichmentOptions>(builder.Configuration.GetSection("Jobs:MarketIntelligence"));
builder.Services.AddSingleton<ISemanticKernelProvider, SemanticKernelProvider>();
builder.Services.AddSingleton<MarketIntelligenceExecutionTracker>();
builder.Services.AddSingleton<IBrowserPool, BrowserPool>();
builder.Services.AddScoped<IPlaywrightScraper, PlaywrightScraper>();
builder.Services.AddScoped<ISessionManager, LinkedInSessionManager>();
builder.Services.AddHttpClient<IUpworkScraperClient, UpworkScraperClient>((serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var baseUrl = configuration.GetValue<string>("UpworkScraper:BaseUrl") ?? "http://localhost:3000";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue<int?>("UpworkScraper:TimeoutSeconds") ?? 120, 10, 600));
});
builder.Services.AddScoped<IJobProvider, LinkedInProvider>();
builder.Services.AddScoped<IJobProvider, IndeedProvider>();
builder.Services.AddScoped<IJobProvider, UpworkProvider>();
builder.Services.AddScoped<IJobOrchestrator, JobOrchestrator>();
builder.Services.Configure<LinkedInAuthOptions>(builder.Configuration.GetSection("LinkedInAuth"));
builder.Services.AddSingleton<ILinkedInAuthService, LinkedInAuthService>();
builder.Services.AddHostedService<JobsAutomationHostedService>();
builder.Services.AddHostedService<JobPostProcessingHostedService>();
var marketIntelligenceWorkerEnabled = builder.Configuration.GetValue<bool?>("Jobs:MarketIntelligence:EnableHostedService") ?? false;
if (marketIntelligenceWorkerEnabled)
{
    builder.Services.AddHostedService<MarketIntelligenceHostedService>();
}

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
    await dbContext.Database.MigrateAsync();
    await DatabaseSeeder.SeedAsync(dbContext);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers();
app.Run();
