using backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Infrastructure.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (!await dbContext.ProviderSessions.AnyAsync(cancellationToken))
        {
            dbContext.ProviderSessions.Add(new ProviderSession
            {
                Provider = "linkedin",
                Username = "not-logged-in",
                IsAuthenticated = false
            });
        }

        if (!await dbContext.JobOffers.AnyAsync(cancellationToken))
        {
            var now = DateTime.UtcNow;
            dbContext.JobOffers.AddRange(
                new JobOffer
                {
                    ExternalId = "seed-001",
                    Title = "Senior .NET Developer",
                    Company = "Contoso Tech",
                    Location = "Remote - LATAM",
                    Description = "Build backend services with .NET 9, PostgreSQL and Azure.",
                    Url = "https://www.linkedin.com/jobs/view/seed-001",
                    Contact = "recruiter@contoso.example",
                    SalaryRange = "USD 4500 - 6500",
                    PublishedAt = now.AddDays(-2),
                    Seniority = "Senior",
                    ContractType = "Full-time",
                    Source = "linkedin",
                    SearchTerm = ".NET",
                    CapturedAt = now.AddHours(-1),
                    MetadataJson = "{\"seed\":true}"
                },
                new JobOffer
                {
                    ExternalId = "seed-002",
                    Title = "Backend Engineer C#",
                    Company = "Fabrikam Data",
                    Location = "Bogota, Colombia",
                    Description = "Design APIs and data pipelines with C#, .NET and SQL.",
                    Url = "https://www.linkedin.com/jobs/view/seed-002",
                    Contact = null,
                    SalaryRange = null,
                    PublishedAt = now.AddDays(-1),
                    Seniority = "Mid-Senior level",
                    ContractType = "Contract",
                    Source = "linkedin",
                    SearchTerm = "C# backend",
                    CapturedAt = now.AddMinutes(-20),
                    MetadataJson = "{\"seed\":true}"
                });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
