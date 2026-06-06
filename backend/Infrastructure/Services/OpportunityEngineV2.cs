using backend.Application.Interfaces;
using backend.Domain.Entities;
using backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Infrastructure.Services;

/// <summary>
/// Opportunity Engine V2 — enriches actionable MarketClusters with commercial intelligence.
///
/// PriorityScoreV2 formula:
///   BlueOceanScore * 0.35
///   + BuyingIntent * 0.25
///   + Urgency      * 0.20
///   + DirectRatio  * 0.10
///   + GrowthRate   * 0.10
///
/// All scores are normalized to 0–100 before weighting.
/// Rule-based only — no LLM calls.
/// </summary>
public sealed class OpportunityEngineV2(
    ApplicationDbContext dbContext,
    ILogger<OpportunityEngineV2> logger) : IOpportunityEngineV2
{
    public async Task<int> EnrichClustersAsync(CancellationToken ct = default)
    {
        var clusters = await dbContext.MarketClusters
            .Where(c => c.IsActionable)
            .ToListAsync(ct);

        if (clusters.Count == 0)
        {
            logger.LogDebug("OpportunityEngineV2: no actionable clusters to enrich.");
            return 0;
        }

        // Load technology lifecycle signals to boost/reduce scores based on stack momentum.
        // Empty when Technologies table has not been rebuilt yet — Enrich() handles that gracefully.
        var techSignals = await dbContext.Technologies
            .AsNoTracking()
            .Select(t => new { t.Name, t.LifecycleStage, t.MomentumScore })
            .ToDictionaryAsync(t => t.Name, t => (t.LifecycleStage, t.MomentumScore), ct);

        foreach (var cluster in clusters)
        {
            Enrich(cluster, techSignals);
        }

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("OpportunityEngineV2: enriched {Count} clusters (techSignals={Tech}).",
            clusters.Count, techSignals.Count);
        return clusters.Count;
    }

    // ── Core enrichment ──────────────────────────────────────────────────────────

    private static void Enrich(
        MarketCluster c,
        Dictionary<string, (string LifecycleStage, double MomentumScore)> techSignals)
    {
        var buyingIntent    = ComputeBuyingIntent(c);
        var complexity      = ComputeEnterpriseComplexity(c);
        var velocity        = ComputeHiringVelocity(c);
        var feasibility     = ComputeDeliveryFeasibility(c, complexity);
        var friction        = ComputeSalesFriction(c);
        var tam             = EstimateTam(c);

        // Apply technology momentum — boosts BuyingIntent for stacks with growing techs,
        // and boosts RevenuePotential when legacy tech + migration pain co-occur (consulting signal).
        var (intentBoost, revenueBoost) = ComputeTechMomentumBoost(c, techSignals);
        buyingIntent = Math.Clamp(buyingIntent + intentBoost, 0, 100);

        var revenue          = Math.Clamp(ComputeRevenuePotential(tam, c.DirectClientRatio, buyingIntent) + revenueBoost, 0, 100);
        var closeProbability = ComputeCloseProbability(buyingIntent, c.DirectClientRatio, c.AvgUrgencyScore);
        var priorityV2       = ComputePriorityScoreV2(c.BlueOceanScore, buyingIntent, c.AvgUrgencyScore * 10, c.DirectClientRatio * 100, c.GrowthRate * 100);

        c.BuyingIntentScore         = Math.Round(buyingIntent, 1);
        c.EnterpriseComplexity      = Math.Round(complexity, 1);
        c.HiringVelocity            = Math.Round(velocity, 1);
        c.DeliveryFeasibility       = Math.Round(feasibility, 1);
        c.SalesFriction             = Math.Round(friction, 1);
        c.EstimatedTam              = Math.Round(tam, 1);
        c.RevenuePotential          = Math.Round(revenue, 1);
        c.EstimatedCloseProbability = Math.Round(closeProbability, 3);
        c.PriorityScoreV2           = priorityV2;
        c.RecommendedServiceModel   = ResolveServiceModel(c.OpportunityType);
        c.SalesAngle                = BuildSalesAngle(c);
        c.WhyNow                    = BuildWhyNow(c);
    }

    /// <summary>
    /// Returns (BuyingIntentBoost, RevenueBoost) based on tech lifecycle signals.
    /// Growing/Emerging techs boost buying intent (+5/+8).
    /// Legacy/Declining techs in a Migration cluster boost revenue potential (+8, consulting signal).
    /// Capped to avoid overwhelming base scores.
    /// </summary>
    private static (double IntentBoost, double RevenueBoost) ComputeTechMomentumBoost(
        MarketCluster c,
        Dictionary<string, (string LifecycleStage, double MomentumScore)> techSignals)
    {
        if (techSignals.Count == 0) return (0, 0);

        var tokens = c.NormalizedTechStack
            .Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => !t.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            .Select(t => t.ToUpperInvariant())
            .Distinct()
            .ToList();

        var intentBoost = 0.0;
        var revenueBoost = 0.0;

        foreach (var token in tokens)
        {
            if (!techSignals.TryGetValue(token, out var sig)) continue;

            intentBoost += sig.LifecycleStage switch
            {
                "Emerging" => 8.0,
                "Growing"  => 5.0,
                "Declining" or "Legacy" => -3.0,
                _ => 0.0
            };

            // Legacy/Declining tech in a Migration or CloudModernization cluster = strong consulting signal
            if (sig.LifecycleStage is "Declining" or "Legacy"
                && c.PainCategory is "Migration" or "CloudModernization")
            {
                revenueBoost += 8.0;
            }
        }

        return (Math.Clamp(intentBoost, -15, 15), Math.Clamp(revenueBoost, 0, 20));
    }

    // ── Signal calculators ────────────────────────────────────────────────────────

    /// <summary>
    /// BuyingIntent = DirectClientRatio(40%) + Urgency(30%) + GrowthRate(30%).
    /// Each component normalized to 0–100.
    /// </summary>
    private static double ComputeBuyingIntent(MarketCluster c)
    {
        var direct  = c.DirectClientRatio * 100;               // already 0–1 → 0–100
        var urgency = Math.Clamp(c.AvgUrgencyScore * 10, 0, 100); // 1–10 → 10–100
        var growth  = Math.Clamp(c.GrowthRate * 100, 0, 100);

        return Math.Clamp(direct * 0.40 + urgency * 0.30 + growth * 0.30, 0, 100);
    }

    /// <summary>
    /// Complexity = number of distinct canonical tech tokens, normalized.
    /// 1 token=20, 2=40, 3=55, 4=70, 5+=85.
    /// </summary>
    private static double ComputeEnterpriseComplexity(MarketCluster c)
    {
        var techCount = c.NormalizedTechStack
            .Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => !t.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return techCount switch
        {
            0 => 10,
            1 => 20,
            2 => 40,
            3 => 55,
            4 => 70,
            _ => 85
        };
    }

    /// <summary>HiringVelocity = JobCount / max(1, daysSinceFirst) * 7, normalized to 0–100 (ceiling = 2 jobs/day).</summary>
    private static double ComputeHiringVelocity(MarketCluster c)
    {
        var days = Math.Max(1, (DateTime.UtcNow - c.FirstSeenAt).TotalDays);
        var perWeek = c.JobCount / days * 7.0;
        return Math.Clamp(perWeek / 14.0 * 100, 0, 100); // ceiling: 2 jobs/day = 14/week = 100
    }

    /// <summary>DeliveryFeasibility = 100 - Complexity * 0.60 + KnownStackBonus * 0.40.</summary>
    private static double ComputeDeliveryFeasibility(MarketCluster c, double complexity)
    {
        var stack  = c.NormalizedTechStack.ToUpperInvariant();
        var bonus  = KnownStacks.Any(s => stack.Contains(s, StringComparison.Ordinal)) ? 30.0 : 0.0;
        return Math.Clamp((100 - complexity) * 0.60 + bonus, 0, 100);
    }

    /// <summary>SalesFriction = (1 - DirectClientRatio) * 60 + ConsultingPenalty.</summary>
    private static double ComputeSalesFriction(MarketCluster c)
    {
        var indirect = (1 - c.DirectClientRatio) * 60;
        var penalty  = c.CompanyType.Equals("Consulting", StringComparison.OrdinalIgnoreCase) ? 40.0 : 0.0;
        return Math.Clamp(indirect + penalty, 0, 100);
    }

    /// <summary>EstimatedTam in millions USD via lookup table keyed by Industry + PainCategory.</summary>
    private static double EstimateTam(MarketCluster c)
    {
        // Industry base TAM (millions USD)
        var industryBase = c.Industry switch
        {
            "Fintech"    => 480,
            "Health"     => 350,
            "Ecommerce"  => 300,
            "Logistics"  => 240,
            "SaaS"       => 420,
            "EdTech"     => 160,
            "InsurTech"  => 200,
            "HRTech"     => 180,
            _            => 120
        };

        // PainCategory multiplier
        var painMult = c.PainCategory switch
        {
            "Migration"          => 1.4,
            "Scaling"            => 1.3,
            "CloudModernization" => 1.35,
            "DataEngineering"    => 1.25,
            "Integration"        => 1.2,
            "Microservices"      => 1.15,
            "Security"           => 1.1,
            "DevOps"             => 1.05,
            _                    => 1.0
        };

        return industryBase * painMult;
    }

    /// <summary>RevenuePotential = normalized(TAM * DirectRatio * BuyingIntent / 100).</summary>
    private static double ComputeRevenuePotential(double tam, double directRatio, double buyingIntent)
    {
        var raw = tam * directRatio * buyingIntent / 100.0;
        // Normalize: 500M potential → 100 score
        return Math.Clamp(raw / 500.0 * 100, 0, 100);
    }

    private static double ComputeCloseProbability(double buyingIntent, double directRatio, double avgUrgency)
    {
        var score = buyingIntent * 0.40
                  + directRatio * 100 * 0.40
                  + Math.Clamp(avgUrgency * 10, 0, 100) * 0.20;

        return Math.Clamp(score / 100.0, 0.0, 1.0);
    }

    private static int ComputePriorityScoreV2(
        double blueOcean, double buyingIntent, double urgencyNorm,
        double directNorm, double growthNorm)
    {
        var score = Math.Clamp(blueOcean, 0, 100)    * 0.35
                  + Math.Clamp(buyingIntent, 0, 100)  * 0.25
                  + Math.Clamp(urgencyNorm, 0, 100)   * 0.20
                  + Math.Clamp(directNorm, 0, 100)    * 0.10
                  + Math.Clamp(growthNorm, 0, 100)    * 0.10;

        return (int)Math.Round(Math.Clamp(score, 0, 100));
    }

    // ── Text generators (rule-based, no LLM) ─────────────────────────────────────

    private static string ResolveServiceModel(string opportunityType) => opportunityType switch
    {
        "MVPProduct" => "SaaS MVP",
        "QuickWin"   => "Fixed-Price Sprint",
        "Consulting" => "Retainer",
        _            => "Consulting"
    };

    private static string BuildSalesAngle(MarketCluster c)
    {
        var pain = c.PainCategory switch
        {
            "Migration"          => $"Migración crítica de sistemas legacy en {c.Industry} — ya tiene stack nuevo, le falta ejecución.",
            "Scaling"            => $"Cuellos de botella de escala en {c.Industry} — están creciendo más rápido que su arquitectura.",
            "CloudModernization" => $"Modernización cloud en {c.Industry} — reducción de costos operativos + velocidad de entrega.",
            "DataEngineering"    => $"Pipelines de datos rotos en {c.Industry} — decisiones lentas = revenue perdido.",
            "Integration"        => $"Integraciones fragmentadas en {c.Industry} — cada integración rota cuesta tiempo y clientes.",
            "Microservices"      => $"Transición a microservicios en {c.Industry} — monolito bloqueando time-to-market.",
            "Security"           => $"Gaps de seguridad en {c.Industry} — riesgo de compliance y reputación.",
            "DevOps"             => $"DevOps manual en {c.Industry} — releases lentos = competencia ganando terreno.",
            "Automation"         => $"Procesos manuales en {c.Industry} — automatización = ahorro directo medible.",
            _                    => $"Problema técnico repetido en {c.Industry} — {c.JobCount} empresas lo tienen hoy."
        };

        return c.OpportunityType switch
        {
            "MVPProduct" => $"{pain} Ofrecemos un sprint de validación en 30 días.",
            "QuickWin"   => $"{pain} Quick win en 2 semanas con ROI demostrable.",
            "Consulting" => $"{pain} Engagement de alto valor con CTO directo.",
            _            => pain
        };
    }

    private static string BuildWhyNow(MarketCluster c)
    {
        var urgency = c.AvgUrgencyScore switch
        {
            >= 8 => "señales críticas de urgencia en las vacantes",
            >= 6 => "presión moderada-alta de contratación",
            >= 4 => "demanda sostenida en el mercado",
            _    => "oportunidad de mercado latente"
        };

        var growth = c.GrowthRate switch
        {
            > 0.5  => $"El cluster creció +{c.GrowthRate:P0} en los últimos 7 días.",
            > 0.2  => $"Crecimiento del {c.GrowthRate:P0} en la última semana.",
            > 0    => "Demanda estable con tendencia positiva.",
            < -0.1 => "Ventana de oportunidad antes del declive — actuar rápido.",
            _      => "Mercado consolidado con demanda predecible."
        };

        return $"{c.JobCount} empresas con {urgency}. {growth}";
    }

    // Stacks where AltioraTech has delivery capability — used for feasibility bonus
    private static readonly string[] KnownStacks =
        ["NET", "AZURE", "SQL", "REACT", "ANGULAR", "DOCKER", "KUBERNETES", "KAFKA", "JAVA", "PYTHON"];
}
