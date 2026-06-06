using backend.Domain.Entities;
using backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Infrastructure.Services;

/// <summary>
/// Seeds the AiPromptTemplates table with all system-reserved default prompts.
/// Runs on startup after migrations. Only inserts records that don't exist yet —
/// existing DB records (user-edited versions) are never overwritten.
/// </summary>
public static class AiPromptSeeder
{
    private static readonly List<AiPromptTemplate> Defaults =
    [
        new()
        {
            Key       = AiPromptTemplateKeys.MarketJobAnalysis,
            Version   = "v2",
            IsActive  = true,
            UpdatedBy = "system-seed",
            Template  =
                """
                Act as a CTO and B2B sales strategist for AltioraTech, an elite engineering agency.

                Analyze the technical hiring context below and infer:
                1. The underlying technical pain the company is trying to solve by hiring
                2. The monetizable service opportunity for AltioraTech
                3. A concrete offer (sprint, audit, migration, MVP)
                4. Your confidence level in this inference

                Return ONLY valid JSON — no markdown, no extra text:
                {
                  "pain_point": "Specific technical bottleneck in 1-2 lines. Name the stack and the real problem.",
                  "business_opportunity": "Concrete business impact and why this is urgent now.",
                  "altioratech_offer": "Service name + delivery format (e.g. '7-day API Audit', '.NET→Azure Migration Sprint', 'Kafka Integration in 2 weeks').",
                  "confidence": 0.0
                }

                Rules:
                - No buzzwords like 'digital transformation' or 'innovative solutions'
                - Infer the real problem from the tech stack and responsibilities
                - confidence is 0.0–1.0 based on how clearly the job signals a specific pain
                - If the description is generic or boilerplate, set confidence below 0.4

                Context:
                {{TechnicalContext}}
                """,
        },
        new()
        {
            Key       = AiPromptTemplateKeys.ClusterSynthesis,
            Version   = "v1",
            IsActive  = true,
            UpdatedBy = "system-seed",
            Template  =
                """
                Actúa como un Director de Estrategia B2B para una agencia de ingeniería de élite.

                Analiza este grupo de vacantes tecnológicas de empresas del sector {industry} que usan {techTop3}.

                Tu objetivo es hacer ingeniería inversa del problema empresarial real detrás de estas vacantes
                y empaquetar una solución vendible inmediatamente.

                Devuelve ÚNICAMENTE un objeto JSON válido con esta estructura exacta, sin texto adicional, sin markdown, sin bloques de código:
                {
                  "pain": "Cuello de botella técnico real en 2 líneas. Sé específico con el stack y el problema.",
                  "businessOpportunity": "Impacto empresarial concreto y por qué urge resolverlo ahora.",
                  "mvp": "Nombre y descripción de un servicio ágil (ej. Auditoría 7 días, Migración X→Y, Sprint de Integración).",
                  "leadMessage": "Cold email de 3 líneas para el CTO. Muy directo. Sin buzzwords. Con cifras o plazos concretos.",
                  "confidence": 0.0
                }

                REGLAS ESTRICTAS:
                - NO generar texto genérico ni buzzwords como "transformación digital" o "soluciones innovadoras"
                - Inferir el problema real desde el stack técnico y las responsabilidades descritas
                - confidence es 0.0–1.0 según qué tan claro es el problema común en el cluster
                - Si el cluster mezcla problemas distintos, bajar confidence por debajo de 0.6
                """,
        },
        new()
        {
            Key       = AiPromptTemplateKeys.ProductSynthesis,
            Version   = "product-synthesis-v2",
            IsActive  = true,
            UpdatedBy = "system-seed",
            Template  =
                """
                Actúa como un Director de Estrategia B2B para una agencia de ingeniería de élite.
                Recibirás la descripción de un producto/servicio tecnológico y las empresas objetivo que lo necesitan.
                Tu objetivo es generar un plan de ataque táctico y accionable para vender este producto HOY.
                Devuelve ÚNICAMENTE un objeto JSON válido con esta estructura exacta, sin texto adicional, sin markdown, sin bloques de código:
                {
                  "implementacion": "Pasos detallados de implementación del servicio, numerados, con duración estimada por paso.",
                  "requerimientos": "Requisitos técnicos y de negocio necesarios del cliente para ejecutar este sprint.",
                  "tiempo_y_tecnologias": "Desglose de tiempos por fase y stack tecnológico recomendado con justificación.",
                  "empresas_objetivo": "Lista de 3-5 empresas concretas del contexto con un mensaje personalizado de apertura para cada una."
                }
                """,
        },
    ];

    public static async Task SeedAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        var existingKeys = await db.AiPromptTemplates
            .AsNoTracking()
            .Select(t => t.Key)
            .ToHashSetAsync(ct);

        var toInsert = Defaults
            .Where(d => !existingKeys.Contains(d.Key))
            .ToList();

        if (toInsert.Count == 0) return;

        foreach (var t in toInsert)
            t.UpdatedAt = DateTime.UtcNow;

        db.AiPromptTemplates.AddRange(toInsert);
        await db.SaveChangesAsync(ct);
    }
}
