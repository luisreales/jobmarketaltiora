using backend.Application.Contracts;
using backend.Application.Interfaces;
using backend.Controllers;
using backend.Domain.Entities;
using backend.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Xunit;

namespace backend.Tests;

/// <summary>
/// Tests for the two new funnel endpoints in ProductController:
///   POST /api/products/from-opportunity
///   POST /api/products/{id}/synthesize-strategy
///
/// LLM calls are avoided: FakeSkProvider returns null for kernel (triggers 503)
/// or a configured kernel for cache-hit tests (which don't reach the LLM).
/// </summary>
public class ProductControllerFunnelTests
{
    // ── POST /api/products/from-opportunity ───────────────────────────────────

    [Fact]
    public async Task CreateFromOpportunity_WhenOpportunityNotFound_Returns404()
    {
        var db         = TestApplicationDbContextFactory.Create();
        var controller = BuildController(db);

        var request = new CreateProductFromOpportunityRequest(
            OpportunityId: 9999,
            Name: "Migration Accelerator",
            ShortTechnicalDescription: "Automates cloud migrations");

        var result = await controller.CreateFromOpportunity(request, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateFromOpportunity_WhenOpportunityExists_Returns201WithProductDto()
    {
        var db          = TestApplicationDbContextFactory.Create();
        var opportunity = SeedOpportunity(db, company: "TechCo", title: "Cloud Architect");
        var controller  = BuildController(db);

        var request = new CreateProductFromOpportunityRequest(
            OpportunityId: opportunity.Id,
            Name: "Migration Accelerator",
            ShortTechnicalDescription: "Automates cloud migrations in 30 days");

        var result = await controller.CreateFromOpportunity(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(201, created.StatusCode);
        var dto = Assert.IsType<ProductDto>(created.Value);
        Assert.Equal("Migration Accelerator", dto.ProductName);
        Assert.Equal("Automates cloud migrations in 30 days", dto.ProductDescription);
        Assert.Equal("Manual", dto.OpportunityType);
        Assert.Equal(opportunity.Id, dto.OpportunityId);
    }

    [Fact]
    public async Task CreateFromOpportunity_PersistsProductInDb()
    {
        var db          = TestApplicationDbContextFactory.Create();
        var opportunity = SeedOpportunity(db);
        var controller  = BuildController(db);

        await controller.CreateFromOpportunity(
            new CreateProductFromOpportunityRequest(opportunity.Id, "Audit Tool", "Automates audits"),
            CancellationToken.None);

        Assert.Equal(1, db.ProductSuggestions.Count());
    }

    [Fact]
    public async Task CreateFromOpportunity_SetsManualOpportunityTypeAndHighPriority()
    {
        var db          = TestApplicationDbContextFactory.Create();
        var opportunity = SeedOpportunity(db);
        var controller  = BuildController(db);

        await controller.CreateFromOpportunity(
            new CreateProductFromOpportunityRequest(opportunity.Id, "Compliance Bot", "Automates compliance"),
            CancellationToken.None);

        var product = db.ProductSuggestions.Single();
        Assert.Equal("Manual", product.OpportunityType);
        Assert.Equal(100, product.PriorityScore);
        Assert.Equal("pending", product.LlmStatus);
    }

    [Fact]
    public async Task CreateFromOpportunity_WhenSameSourceIdeaIdAlreadyExists_Returns409()
    {
        var db          = TestApplicationDbContextFactory.Create();
        var opportunity = SeedOpportunity(db);
        var controller  = BuildController(db);

        await controller.CreateFromOpportunity(
            new CreateProductFromOpportunityRequest(opportunity.Id, "Compliance Bot", "Automates compliance", "compliance-bot"),
            CancellationToken.None);

        var duplicate = await controller.CreateFromOpportunity(
            new CreateProductFromOpportunityRequest(opportunity.Id, "Compliance Bot", "Automates compliance", "compliance-bot"),
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(duplicate.Result);
        Assert.Equal(409, conflict.StatusCode);
        Assert.Equal(1, db.ProductSuggestions.Count());
    }

    [Fact]
    public async Task UpdateProduct_WhenProductExists_UpdatesEditableFieldsIncludingImageUrl()
    {
        var db = TestApplicationDbContextFactory.Create();
        var product = SeedProduct(db);
        var controller = BuildController(db);

        var result = await controller.UpdateProduct(
            product.Id,
            new UpdateProductRequest(
                ProductName: "Platform Modernization Kit",
                ProductDescription: "End-to-end migration accelerator",
                WhyNow: "Strong demand from enterprise clients",
                Offer: "Fixed $12k sprint",
                ActionToday: "Send proposal today",
                TechFocus: "DotNet + Azure + Angular",
                EstimatedBuildDays: 21,
                MinDealSizeUsd: 12000,
                MaxDealSizeUsd: 25000,
                OpportunityType: "Manual",
                Industry: "SaaS",
                ImageUrl: "/uploads/products/platform-modernization-kit.png",
                Status: "open"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ProductDto>(ok.Value);
        Assert.Equal("Platform Modernization Kit", dto.ProductName);
        Assert.Equal("/uploads/products/platform-modernization-kit.png", dto.ImageUrl);

        var persisted = db.ProductSuggestions.Single(p => p.Id == product.Id);
        Assert.Equal("Platform Modernization Kit", persisted.ProductName);
        Assert.Equal("/uploads/products/platform-modernization-kit.png", persisted.ImageUrl);
    }

    [Fact]
    public async Task ExportProductsCsv_ReturnsFileWithHeadersAndRows()
    {
        var db = TestApplicationDbContextFactory.Create();
        SeedProduct(db);
        var controller = BuildController(db);

        var result = await controller.ExportProductsCsv(new ProductQuery(), CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv", file.ContentType);

        var csv = System.Text.Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains("Id,ProductName,ProductDescription", csv);
        Assert.Contains("Deployment Automation Suite", csv);
    }

    [Fact]
    public async Task UpdateProduct_WhenAbsoluteLocalUploadUrlIsSent_NormalizesToRelativePath()
    {
        var db = TestApplicationDbContextFactory.Create();
        var product = SeedProduct(db);
        var controller = BuildController(db);

        var result = await controller.UpdateProduct(
            product.Id,
            new UpdateProductRequest(
                ProductName: product.ProductName,
                ProductDescription: product.ProductDescription,
                WhyNow: product.WhyNow,
                Offer: product.Offer,
                ActionToday: product.ActionToday,
                TechFocus: product.TechFocus,
                EstimatedBuildDays: product.EstimatedBuildDays,
                MinDealSizeUsd: product.MinDealSizeUsd,
                MaxDealSizeUsd: product.MaxDealSizeUsd,
                OpportunityType: product.OpportunityType,
                Industry: product.Industry,
                ImageUrl: "http://localhost:8080/uploads/products/product-15-demo.png",
                Status: product.Status),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ProductDto>(ok.Value);
        Assert.Equal("/uploads/products/product-15-demo.png", dto.ImageUrl);
    }

    // ── POST /api/products/{id}/synthesize-strategy ───────────────────────────

    [Fact]
    public async Task SynthesizeStrategy_WhenProductNotFound_Returns404()
    {
        var db         = TestApplicationDbContextFactory.Create();
        var controller = BuildController(db, skConfigured: false);

        var result = await controller.SynthesizeStrategy(9999);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task SynthesizeStrategy_WhenSkNotConfigured_Returns503()
    {
        var db         = TestApplicationDbContextFactory.Create();
        var product    = SeedProduct(db, llmStatus: "pending");
        var controller = BuildController(db, skConfigured: false);

        var result = await controller.SynthesizeStrategy(product.Id);

        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(503, problem.StatusCode);
    }

    [Fact]
    public async Task SynthesizeStrategy_WhenAlreadyCompleted_ReturnsCachedResult()
    {
        var db = TestApplicationDbContextFactory.Create();
        var strategyJson = """
            {
              "realBusinessProblem": "They struggle with manual deploys.",
              "financialImpact": "Save 40h/month.",
              "mvpDefinition": "CI/CD pipeline in 2 weeks.",
              "targetBuyer": "CTO — positioning: ROI from day 1.",
              "pricingStrategy": "Fixed $8k sprint.",
              "outreachMessage": "Hi, I noticed your job posting..."
            }
            """;
        var product = SeedProduct(db, llmStatus: "completed", synthesisDetailJson: strategyJson);
        var controller = BuildController(db, skConfigured: true); // SK configured but should not be called

        var result = await controller.SynthesizeStrategy(product.Id);

        var ok  = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ProductDto>(ok.Value);
        Assert.Equal("completed", dto.LlmStatus);
        Assert.Equal(strategyJson, dto.SynthesisDetailJson);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ProductController BuildController(
        backend.Infrastructure.Data.ApplicationDbContext db,
        bool skConfigured = false)
    {
        var provider = new FakeSkProvider(isConfigured: skConfigured);
        return new ProductController(
            db,
            new NullProductGeneratorService(),
            new NullProductSynthesisService(),
            provider,
            NullLogger<ProductController>.Instance);
    }

    private static JobOffer SeedJob(backend.Infrastructure.Data.ApplicationDbContext db)
    {
        var job = new JobOffer
        {
            ExternalId          = Guid.NewGuid().ToString("N"),
            Title               = "Platform Engineer",
            Company             = "Acme Corp",
            Location            = "Remote",
            Description         = "Cloud migration expertise required.",
            Url                 = "https://example.com/job/1",
            Source              = "linkedin",
            SearchTerm          = ".net",
            Category            = "Engineering",
            OpportunityScore    = 80,
            IsConsultingCompany = false,
            CompanyType         = "direct",
            CapturedAt          = DateTime.UtcNow
        };
        db.JobOffers.Add(job);
        db.SaveChanges();
        return job;
    }

    private static Opportunity SeedOpportunity(
        backend.Infrastructure.Data.ApplicationDbContext db,
        string company = "Acme Corp",
        string title   = "Platform Engineer")
    {
        var job = SeedJob(db);
        var opp = new Opportunity
        {
            JobId     = job.Id,
            Company   = company,
            JobTitle  = title,
            LlmStatus = "pending",
            CreatedAt = DateTime.UtcNow
        };
        db.Opportunities.Add(opp);
        db.SaveChanges();
        return opp;
    }

    private static ProductSuggestion SeedProduct(
        backend.Infrastructure.Data.ApplicationDbContext db,
        string llmStatus           = "pending",
        string? synthesisDetailJson = null)
    {
        var product = new ProductSuggestion
        {
            ProductName          = "Deployment Automation Suite",
            ProductDescription   = "CI/CD pipeline accelerator",
            WhyNow               = "Companies hiring DevOps need faster pipelines",
            Offer                = "Fixed-price sprint",
            ActionToday          = "Send cold email to CTO",
            TechFocus            = "Azure+Kubernetes",
            ClusterIdsJson       = "[]",
            EstimatedBuildDays   = 14,
            MinDealSizeUsd       = 5000,
            MaxDealSizeUsd       = 10000,
            TotalJobCount        = 5,
            AvgDirectClientRatio = 0.8,
            AvgUrgencyScore      = 7,
            TopBlueOceanScore    = 6,
            ClusterCount         = 2,
            PriorityScore        = 90,
            OpportunityType      = "Manual",
            Industry             = "Technology",
            LlmStatus            = llmStatus,
            SynthesisDetailJson  = synthesisDetailJson,
            GeneratedAt          = DateTime.UtcNow
        };
        db.ProductSuggestions.Add(product);
        db.SaveChanges();
        return product;
    }

    // ── Fakes ─────────────────────────────────────────────────────────────────

    private sealed class FakeSkProvider(bool isConfigured) : ISemanticKernelProvider
    {
        public bool IsConfigured { get; } = isConfigured;
        public Kernel GetKernel() => null!; // null triggers 503 path in controller
    }

    private sealed class NullProductGeneratorService : IProductGeneratorService
    {
        public Task<int> GenerateProductsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<ProductSuggestion?> GenerateForClusterAsync(int clusterId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProductSuggestion?>(null);
    }

    private sealed class NullProductSynthesisService : IProductSynthesisService
    {
        public Task<ProductSuggestion?> SynthesizeProductAsync(int productId, CancellationToken ct = default)
            => Task.FromResult<ProductSuggestion?>(null);
    }
}
