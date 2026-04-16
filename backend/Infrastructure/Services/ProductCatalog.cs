using backend.Domain.Entities;

namespace backend.Infrastructure.Services;

/// <summary>
/// Static rule-based product catalog.
/// Maps a MarketCluster to the best matching sellable product.
/// No LLM involved — 100% deterministic.
///
/// Rules are evaluated in priority order — first match wins.
/// The catch-all entry (Platform Stabilization) is always last.
/// </summary>
public static class ProductCatalog
{
    public sealed record CatalogEntry(
        string ProductName,
        string ProductDescription,
        string ActionToday,
        int EstimatedBuildDays,
        decimal MinDealSizeUsd,
        decimal MaxDealSizeUsd,
        Func<MarketCluster, bool> Matches);

    private static bool TechContains(MarketCluster c, params string[] tokens) =>
        tokens.Any(t => c.NormalizedTechStack.Contains(t, StringComparison.OrdinalIgnoreCase));

    private static readonly IReadOnlyList<CatalogEntry> Entries =
    [
        // 1. .NET Azure Migration Sprint
        new CatalogEntry(
            ProductName: ".NET Azure Migration Sprint",
            ProductDescription: "Sprint estructurado de 14 días para migrar componentes .NET críticos a Azure con zero downtime. Incluye análisis de dependencias, plan de migración y entrega documentada.",
            ActionToday: "Identificar top 5 empresas .NET migrando a Azure y enviar propuesta de sprint hoy",
            EstimatedBuildDays: 14,
            MinDealSizeUsd: 8_000m,
            MaxDealSizeUsd: 15_000m,
            Matches: c =>
                (c.PainCategory.Contains("Migration", StringComparison.OrdinalIgnoreCase) ||
                 c.PainCategory.Contains("Modernization", StringComparison.OrdinalIgnoreCase) ||
                 c.PainCategory.Contains("Cloud", StringComparison.OrdinalIgnoreCase)) &&
                TechContains(c, "AZURE", "NET")),

        // 2. API Integration Accelerator
        new CatalogEntry(
            ProductName: "API Integration Accelerator",
            ProductDescription: "Conecta tus sistemas con APIs externas en 7 días. Reduce errores manuales un 90%, integra pagos, CRMs y herramientas externas con arquitectura robusta y tests.",
            ActionToday: "Contactar empresas con múltiples integraciones pendientes — alta tasa de conversión",
            EstimatedBuildDays: 7,
            MinDealSizeUsd: 3_000m,
            MaxDealSizeUsd: 6_000m,
            Matches: c =>
                c.PainCategory.Contains("Integration", StringComparison.OrdinalIgnoreCase) ||
                TechContains(c, "REST", "GRAPHQL", "KAFKA", "WEBHOOK")),

        // 3. System Performance Audit
        new CatalogEntry(
            ProductName: "System Performance Audit",
            ProductDescription: "Auditoría de rendimiento de 5 días: identificamos cuellos de botella, optimizamos caching con Redis y entregamos plan de mejora con ROI calculado.",
            ActionToday: "Ofrecer auditoría gratuita de 1 hora para cerrar el deal de auditoría completa",
            EstimatedBuildDays: 5,
            MinDealSizeUsd: 2_000m,
            MaxDealSizeUsd: 4_000m,
            Matches: c =>
                c.PainCategory.Contains("Scaling", StringComparison.OrdinalIgnoreCase) ||
                c.PainCategory.Contains("Performance", StringComparison.OrdinalIgnoreCase) ||
                TechContains(c, "REDIS", "ELASTICSEARCH", "MEMCACHED", "CDN")),

        // 4. Data Pipeline MVP
        new CatalogEntry(
            ProductName: "Data Pipeline MVP",
            ProductDescription: "Pipeline de datos end-to-end en 21 días: ingesta desde múltiples fuentes, transformación, calidad de datos y entrega a BI/analytics listo para producción.",
            ActionToday: "Solicitar acceso a datos de muestra para preparar demo del pipeline en 48h",
            EstimatedBuildDays: 21,
            MinDealSizeUsd: 12_000m,
            MaxDealSizeUsd: 20_000m,
            Matches: c =>
                c.PainCategory.Contains("Data", StringComparison.OrdinalIgnoreCase) ||
                c.PainCategory.Contains("Analytics", StringComparison.OrdinalIgnoreCase) ||
                TechContains(c, "PYTHON", "AIRFLOW", "SPARK", "DBT", "DATABRICKS", "SNOWFLAKE")),

        // 5. Fintech Compliance Dashboard
        new CatalogEntry(
            ProductName: "Fintech Compliance Dashboard",
            ProductDescription: "Dashboard de compliance financiero en 30 días: KYC, AML, audit trail completo y reportes regulatorios automatizados para empresas fintech reguladas.",
            ActionToday: "Preparar deck de compliance para fintechs reguladas — alto ticket, pocas llamadas necesarias",
            EstimatedBuildDays: 30,
            MinDealSizeUsd: 15_000m,
            MaxDealSizeUsd: 25_000m,
            Matches: c =>
                c.Industry.Equals("Fintech", StringComparison.OrdinalIgnoreCase) &&
                c.DirectClientRatio >= 0.6),

        // 6. Rapid Delivery Sprint (5 días)
        new CatalogEntry(
            ProductName: "Rapid Delivery Sprint (5 días)",
            ProductDescription: "Sprint express de 5 días: entrega un feature crítico o corrección bloqueante con riesgo mínimo. Ideal para empresas con alta urgencia y deadline inminente.",
            ActionToday: "Llamar hoy a los 3 leads con mayor urgencia — están listos para cerrar",
            EstimatedBuildDays: 5,
            MinDealSizeUsd: 2_000m,
            MaxDealSizeUsd: 4_000m,
            Matches: c =>
                c.OpportunityType.Equals("QuickWin", StringComparison.OrdinalIgnoreCase) &&
                c.AvgUrgencyScore >= 7.0),

        // 7. DevOps & CI/CD Accelerator
        new CatalogEntry(
            ProductName: "DevOps & CI/CD Accelerator",
            ProductDescription: "Implementación CI/CD completa en 10 días: pipelines automatizados, infra-as-code, gates de calidad y despliegues sin downtime.",
            ActionToday: "Enviar audit gratuito de pipeline actual a los 3 leads con más señales DevOps",
            EstimatedBuildDays: 10,
            MinDealSizeUsd: 5_000m,
            MaxDealSizeUsd: 9_000m,
            Matches: c =>
                c.PainCategory.Contains("DevOps", StringComparison.OrdinalIgnoreCase) ||
                c.PainCategory.Contains("CI", StringComparison.OrdinalIgnoreCase) ||
                TechContains(c, "DOCKER", "KUBERNETES", "TERRAFORM", "GITHUB", "GITLAB", "JENKINS", "ANSIBLE")),

        // 8. Security & Identity Sprint
        new CatalogEntry(
            ProductName: "Security & Identity Sprint",
            ProductDescription: "Hardening de seguridad en 7 días: autenticación robusta, gestión de secretos, OAuth/OIDC y eliminación de vulnerabilidades críticas OWASP.",
            ActionToday: "Ofrecer security scan rápido gratuito — alta conversión en empresas reguladas",
            EstimatedBuildDays: 7,
            MinDealSizeUsd: 4_000m,
            MaxDealSizeUsd: 8_000m,
            Matches: c =>
                c.PainCategory.Contains("Security", StringComparison.OrdinalIgnoreCase) ||
                c.PainCategory.Contains("Auth", StringComparison.OrdinalIgnoreCase) ||
                TechContains(c, "OAUTH", "KEYCLOAK", "JWT", "SAML", "IAM", "LDAP")),

        // 9. Microservices Decomposition Sprint
        new CatalogEntry(
            ProductName: "Microservices Decomposition Sprint",
            ProductDescription: "Descomposición de monolito en 21 días: bounded contexts definidos, APIs internas, messaging async con Kafka/RabbitMQ y observabilidad desde el día 1.",
            ActionToday: "Preparar propuesta de domain mapping para el monolito del lead principal",
            EstimatedBuildDays: 21,
            MinDealSizeUsd: 10_000m,
            MaxDealSizeUsd: 18_000m,
            Matches: c =>
                c.PainCategory.Contains("Microservice", StringComparison.OrdinalIgnoreCase) ||
                c.PainCategory.Contains("Architecture", StringComparison.OrdinalIgnoreCase) ||
                TechContains(c, "RABBITMQ", "GRPC", "SERVICE MESH", "ISTIO")),

        // 10. Platform Stabilization Package (catch-all)
        new CatalogEntry(
            ProductName: "Platform Stabilization Package",
            ProductDescription: "Paquete de estabilización de plataforma en 14 días: diagnóstico técnico completo, plan de mejora priorizado por impacto y primeras entregas medibles.",
            ActionToday: "Hacer discovery call con el lead principal para definir alcance en 30 minutos",
            EstimatedBuildDays: 14,
            MinDealSizeUsd: 5_000m,
            MaxDealSizeUsd: 12_000m,
            Matches: _ => true)
    ];

    /// <summary>
    /// Returns the first catalog entry matching the cluster.
    /// Rules are evaluated in priority order — first match wins.
    /// The last entry (Platform Stabilization) always matches, so this never returns null
    /// for actionable clusters.
    /// </summary>
    public static CatalogEntry? FindBestMatch(MarketCluster cluster) =>
        Entries.FirstOrDefault(e => e.Matches(cluster));
}
