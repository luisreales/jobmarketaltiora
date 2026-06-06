# IMPLEMENTACION_V1.MD — Plan de Ejecución Real

> Generado tras auditoría completa del codebase.
> Stack: .NET 9 · EF Core · PostgreSQL · Semantic Kernel · Angular 20

---

## ESTADO ACTUAL DEL CÓDIGO (verificado)

### Lo que YA existe y funciona:

| Componente | Archivo | Estado |
|---|---|---|
| Cleaner básico | `JobPreprocessorService.cs` | ✅ Existe, incompleto |
| LLM Synthesis service | `ClusterSynthesisService.cs` | ✅ Existe, NO auto-wired |
| Cluster pipeline worker | `ClusteringHostedService.cs` | ✅ Existe, Stage 4 comentado |
| Clustering rígido SHA256 | `ClusterEngine.cs` | ✅ Existe, sin embeddings |
| Decision Engine básico | `DecisionEngine.cs` | ✅ Existe, fórmula incompleta |
| SK Provider | `SemanticKernelProvider.cs` | ✅ Configurado |

### Lo que NO existe todavía:

| Componente | Estado |
|---|---|
| `IJobDescriptionCleanerService` + implementación dedicada | ❌ Falta |
| Auto-trigger de síntesis LLM en pipeline | ❌ Comentado/desactivado |
| `SemanticClusterEngine` con embeddings | ❌ Falta |
| `ClusterSimilarityService` con coseno | ❌ Falta |
| Columna de embeddings en `JobInsight` | ❌ Falta migration |
| `OpportunityEngineV2` con TAM/BuyingIntent/etc. | ❌ Falta |
| Campos nuevos en `MarketCluster` | ❌ Falta migration |

---

## FASE 1 — CLEANER PIPELINE

### Diagnóstico
`JobPreprocessorService` ya limpia descripciones pero:
- Trunca a **2000 chars** (necesita 4000)
- No tiene interfaz `IJobDescriptionCleanerService`
- `ClusterSynthesisService.BuildPromptAsync()` usa `i.Job!.Description` RAW — sin limpiar
- `ClusterSynthesisService` tiene un `CleanDescription()` duplicado y más débil

### Archivos a crear

**`backend/Application/Interfaces/IJobDescriptionCleanerService.cs`**
```csharp
namespace backend.Application.Interfaces;

public interface IJobDescriptionCleanerService
{
    CleanedJobDescription Clean(string rawDescription, string companyName = "");
}

public sealed record CleanedJobDescription(
    string CleanText,        // texto final listo para LLM
    bool IsConsulting,       // detectado como consultora
    int OriginalLength,
    int CleanedLength);
```

**`backend/Infrastructure/Services/JobDescriptionCleanerService.cs`** (nueva clase)
```csharp
// Implementa IJobDescriptionCleanerService
// Delega limpieza básica en JobPreprocessorService.RemoveFluffTail()
// Agrega ExtractTechnicalSections() con Regex
// Trunca a 4000 chars (no 2000)
// Detecta consultoras via JobPreprocessorService.DetectConsultingSignals()
```

Funciones a implementar:
1. `ExtractTechnicalSections(string text)` — Regex para capturar líneas con: tech, stack, arquitectura, migración, integración, kubernetes, microservicios, cloud, scaling, legacy, CI/CD, etc.
2. `RemoveCorporateFluff(string text)` — wraps RemoveFluffTail con markers extendidos (ver lista abajo)
3. `NormalizeWhitespace(string text)` — colapsa espacios, elimina líneas vacías
4. `TrimToTokenBudget(string text, int maxChars = 4000)` — trunca respetando límite
5. `IsConsultingCompany(string company, string description)` — usa ConsultingSignals de JobPreprocessorService

Fluff markers adicionales (sobre los que ya tiene JobPreprocessorService):
```
"why join", "why work with us", "our values", "we believe",
"culture", "diversity", "inclusion", "equal opportunity", "eeo",
"competitive salary", "salary range", "compensation", "stock options",
"healthcare", "dental", "401k", "pto", "vacation", "remote work policy",
"quiénes somos", "por qué trabajar", "nuestros valores"
```

Blacklist consultoras adicionales:
```
"cognizant", "infosys", "wipro", "hcl", "tech mahindra",
"mphasis", "hexaware", "mindtree", "l&t technology",
"thoughtworks", "slalom", "booz allen", "kpmg", "pwc",
"ey technology", "ernst & young"
```

### Cambio en ClusterSynthesisService

En `BuildPromptAsync()` — línea 169 — cambiar:
```csharp
// ANTES (usa descripción RAW):
.Select(i => i.Job!.Description)

// DESPUÉS (usa descripción limpiada):
.Select(i => new { i.LeadScore, i.Job!.Description, i.Job.Company })
// luego aplicar cleaner.Clean(desc, company).CleanText
```

Inyectar `IJobDescriptionCleanerService` en `ClusterSynthesisService` y eliminar el método privado `CleanDescription()` que ya no será necesario.

### Registro DI en Program.cs
```csharp
builder.Services.AddScoped<IJobDescriptionCleanerService, JobDescriptionCleanerService>();
```

### Impacto en LeadScore de consultoras
En `RuleBasedAiEnrichmentService` o `LeadScoringService`: si `IsConsulting == true`, reducir `LeadScore` en 30% (configurable). Verificar dónde está la lógica actual antes de modificar.

---

## FASE 2 — LLM SYNTHESIS AUTO-TRIGGER

### Diagnóstico
`ClusterSynthesisService.SynthesizePendingClustersAsync()` YA está implementado correctamente. El problema es que en `ClusteringHostedService.cs` la Stage 4 está **comentada** con el mensaje:
```csharp
// Stage 4 (LLM Synthesis) is on-demand only — triggered from the UI per cluster.
```

### Cambio en ClusteringHostedService

Agregar Stage 4 después de ProductGenerator:
```csharp
// Stage 4 — LLM Synthesis (batch, up to 5 actionable pending clusters)
var synthService = scope.ServiceProvider.GetRequiredService<IClusterSynthesisService>();
await synthService.SynthesizePendingClustersAsync(cancellationToken);
logger.LogInformation("ClusteringHostedService stage=LLMSynthesis completed.");
```

### Mejorar el prompt en ClusterSynthesisService

Prompt del sistema actual es funcional pero incompleto. Agregar campos:
```json
{
  "pain": "",
  "businessOpportunity": "",
  "mvp": "",
  "leadMessage": "",
  "confidence": 0.0
}
```

Actualizar `SynthesisResult` record y `ParseLlmResponse()` para incluir `BusinessOpportunity` y `Confidence`.

Actualizar `MarketCluster` entity con campo `SynthesizedBusinessOpportunity` (string?) y `LlmConfidence` (double?) — requiere migration.

Prompt del sistema mejorado:
```
Actúa como un Director de Estrategia B2B para una agencia de ingeniería de élite.

Analiza este grupo de vacantes tecnológicas de empresas {industry} que usan {techTop3}.

Tu objetivo es hacer ingeniería inversa del problema empresarial real y empaquetar
una solución vendible inmediatamente.

Devuelve ÚNICAMENTE JSON válido sin texto adicional:
{
  "pain": "cuello de botella técnico real en 2 líneas",
  "businessOpportunity": "impacto empresarial concreto y por qué urge resolverlo",
  "mvp": "nombre y descripción de servicio ágil (Auditoría 7 días / Migración X→Y)",
  "leadMessage": "cold email de 3 líneas para CTO, muy directo, sin buzzwords",
  "confidence": 0.0
}

REGLAS:
- NO generar texto genérico
- NO repetir buzzwords como "transformación digital"
- Inferir problemas reales desde el stack técnico
- confidence: 0.0–1.0 según claridad del cluster
```

---

## FASE 3 — SEMANTIC CLUSTERING

### Diagnóstico
`ClusterEngine` usa SHA256(PainCategory|TechTop3|Industry|CompanyType) — completamente literal.
No existen: embeddings, `SemanticClusterEngine`, ni `ClusterSimilarityService`.

### Estrategia de Embeddings

**Almacenamiento:** JSON array de floats en nueva columna `EmbeddingVectorJson` (text) en `JobInsight`. No usar pgvector para evitar dependencia extra. Calcular similitud coseno en memoria (los clusters son pequeños).

**Generación:** Usar `ITextEmbeddingGenerationService` de Semantic Kernel sobre el campo `MainPainPoint + " " + PainCategory + " " + SuggestedSolution` del `JobInsight`.

### Migration requerida

Nueva columna en `JobInsight`:
```csharp
public string? EmbeddingVectorJson { get; set; }  // float[] serializado
public DateTime? EmbeddedAt { get; set; }
```

### Archivos a crear

**`backend/Application/Interfaces/ISemanticClusterEngine.cs`**
```csharp
public interface ISemanticClusterEngine
{
    Task<int> GenerateEmbeddingsAsync(CancellationToken ct = default);
    Task<Dictionary<string, List<int>>> BuildSemanticGroupsAsync(
        IList<JobInsight> insights,
        double similarityThreshold = 0.82,
        CancellationToken ct = default);
}
```

**`backend/Infrastructure/Services/ClusterSimilarityService.cs`**
```csharp
// Implementa coseno entre dos float[]
// static double CosineSimilarity(float[] a, float[] b)
// static float[] Deserialize(string json)
// Usado internamente por SemanticClusterEngine
```

**`backend/Infrastructure/Services/SemanticClusterEngine.cs`**
```csharp
// Usa ISemanticKernelProvider para obtener ITextEmbeddingGenerationService
// Genera embeddings para MainPainPoint+PainCategory+SuggestedSolution
// Agrupa por similitud coseno >= threshold (default 0.82)
// Genera ClusterKey determinístico desde el centroide semántico
// Fallback a ClusterEngine original si SK no configurado
```

### Integración en ClusteringHostedService

`SemanticClusterEngine` reemplaza parcialmente a `ClusterEngine`:
1. Si SK disponible: ejecutar `SemanticClusterEngine.GenerateEmbeddingsAsync()` para los insights sin embeddings
2. Usar grupos semánticos para enriquecer los clusters existentes (NO romper clusters actuales)
3. Fallback automático si falla o SK no disponible

El clustering semántico es **aditivo**, no reemplaza el clustering SHA256. Permite mergear clusters similares que el SHA256 separaría.

### Nota sobre dependencias
`Microsoft.SemanticKernel.Connectors.OpenAI` ya incluye `ITextEmbeddingGenerationService`. Verificar que el modelo configurado soporte embeddings (OpenAI: `text-embedding-3-small`). Si el modelo actual es solo chat, agregar configuración separada para embeddings en `SemanticKernelOptions`.

---

## FASE 4 — OPPORTUNITY ENGINE V2

### Diagnóstico
`DecisionEngine.ComputePriorityScore()` usa fórmula simple:
```
PriorityScore = BlueOcean*0.50 + direct*0.20 + urgency*0.20 + growth*0.10
```

Faltan: TAM, BuyingIntent, EnterpriseComplexity, HiringVelocity, DeliveryFeasibility, SalesFriction, RevenuePotential, SalesAngle, WhyNow, EstimatedCloseProbability.

### Migration requerida

Nuevos campos en `MarketCluster`:
```csharp
// OpportunityEngineV2 output
public double EstimatedTam { get; set; }            // millones USD
public double BuyingIntentScore { get; set; }        // 0–100
public double EnterpriseComplexity { get; set; }     // 0–100
public double HiringVelocity { get; set; }           // jobs/semana normalizado
public double DeliveryFeasibility { get; set; }      // 0–100
public double SalesFriction { get; set; }            // 0–100 (lower = better)
public double RevenuePotential { get; set; }         // 0–100
public int PriorityScoreV2 { get; set; }             // score con nueva fórmula
public string? RecommendedServiceModel { get; set; } // "Consulting" | "SaaS MVP" | "QuickWin"
public string? SalesAngle { get; set; }              // argumento de venta principal
public string? WhyNow { get; set; }                  // urgencia de mercado
public double EstimatedCloseProbability { get; set; } // 0.0–1.0
```

### Archivos a crear

**`backend/Application/Interfaces/IOpportunityEngineV2.cs`**
```csharp
public interface IOpportunityEngineV2
{
    Task<int> EnrichClustersAsync(CancellationToken ct = default);
}
```

**`backend/Infrastructure/Services/OpportunityEngineV2.cs`**

Fórmula de PriorityScoreV2 (exacta del spec):
```
PriorityScoreV2 = (
  BlueOceanScore      * 0.35
  + BuyingIntent      * 0.25
  + Urgency           * 0.20
  + DirectClientRatio * 0.10
  + GrowthRate        * 0.10
)
```

Cálculo de cada componente:
- **BuyingIntent** = `DirectClientRatio*40 + AvgUrgencyScore*10*30 + GrowthRate*100*30` (normalizado 0–100)
- **EstimatedTam** = lookup por Industry + PainCategory (tabla hardcoded configurable)
- **EnterpriseComplexity** = derivado de NormalizedTechStack (más techs = más complejo)
- **HiringVelocity** = `JobCount / DaysSinceFirstSeen` normalizado
- **DeliveryFeasibility** = inverso de EnterpriseComplexity + componente de stack conocido
- **SalesFriction** = `(1 - DirectClientRatio) * 60 + CompanyType=="Consulting" ? 40 : 0`
- **RevenuePotential** = `EstimatedTam * DirectClientRatio * BuyingIntent / 100`
- **SalesAngle** = texto generado por reglas (no LLM) según PainCategory + OpportunityType
- **WhyNow** = generado por reglas según GrowthRate + UrgencyScore
- **EstimatedCloseProbability** = `(BuyingIntent * 0.40 + DirectClientRatio*100 * 0.40 + AvgUrgencyScore*10 * 0.20) / 100`
- **RecommendedServiceModel** = regla: MVPProduct→"SaaS MVP", QuickWin→"Fixed-Price Sprint", Consulting→"Retainer"

### Integración en ClusteringHostedService

Agregar Stage 5 (después de DecisionEngine, antes de ProductGenerator):
```csharp
// Stage 3 — Opportunity Engine V2
var opportunityEngineV2 = scope.ServiceProvider.GetRequiredService<IOpportunityEngineV2>();
var enriched = await opportunityEngineV2.EnrichClustersAsync(cancellationToken);
logger.LogInformation("ClusteringHostedService stage=OpportunityEngineV2 enriched={Count}", enriched);
```

### Registro DI en Program.cs
```csharp
builder.Services.AddScoped<IOpportunityEngineV2, OpportunityEngineV2>();
```

---

## ORDEN DE EJECUCIÓN (prioridad real)

### Paso 1 — FASE 1 (Cleaner) + FASE 2 (Auto-wiring)
Estos dos van juntos. El cleaner mejora inmediatamente la calidad de síntesis.
El auto-wiring hace que la síntesis ocurra sin intervención manual.

Archivos a crear/modificar:
1. `[CREAR]` `backend/Application/Interfaces/IJobDescriptionCleanerService.cs`
2. `[CREAR]` `backend/Infrastructure/Services/JobDescriptionCleanerService.cs`
3. `[MODIFICAR]` `backend/Infrastructure/Services/ClusterSynthesisService.cs` — inyectar cleaner, mejorar prompt, agregar `businessOpportunity` y `confidence`
4. `[MODIFICAR]` `backend/Infrastructure/Services/ClusteringHostedService.cs` — activar Stage 4
5. `[MODIFICAR]` `backend/Domain/Entities/MarketCluster.cs` — agregar `SynthesizedBusinessOpportunity`, `LlmConfidence`
6. `[CREAR]` migration: `AddClusterSynthesisFields`
7. `[MODIFICAR]` `backend/Program.cs` — registrar `IJobDescriptionCleanerService`

### Paso 2 — FASE 4 (Opportunity Engine V2)
Impacto inmediato en scoring sin dependencias externas.

Archivos a crear/modificar:
1. `[CREAR]` `backend/Application/Interfaces/IOpportunityEngineV2.cs`
2. `[CREAR]` `backend/Infrastructure/Services/OpportunityEngineV2.cs`
3. `[MODIFICAR]` `backend/Domain/Entities/MarketCluster.cs` — agregar campos V2
4. `[CREAR]` migration: `AddOpportunityEngineV2Fields`
5. `[MODIFICAR]` `backend/Infrastructure/Services/ClusteringHostedService.cs` — agregar Stage OpportunityEngineV2
6. `[MODIFICAR]` `backend/Program.cs` — registrar `IOpportunityEngineV2`

### Paso 3 — FASE 3 (Semantic Clustering)
Requiere modelo de embeddings configurado. Implementar último por dependencia de infraestructura LLM.

Archivos a crear/modificar:
1. `[CREAR]` `backend/Application/Interfaces/ISemanticClusterEngine.cs`
2. `[CREAR]` `backend/Infrastructure/Services/ClusterSimilarityService.cs`
3. `[CREAR]` `backend/Infrastructure/Services/SemanticClusterEngine.cs`
4. `[MODIFICAR]` `backend/Domain/Entities/JobInsight.cs` — agregar `EmbeddingVectorJson`, `EmbeddedAt`
5. `[CREAR]` migration: `AddJobInsightEmbeddings`
6. `[MODIFICAR]` `backend/Infrastructure/Services/ClusteringHostedService.cs` — agregar Stage embeddings
7. `[MODIFICAR]` `backend/Infrastructure/Services/SemanticKernelOptions.cs` — agregar config embeddings
8. `[MODIFICAR]` `backend/Program.cs` — registrar `ISemanticClusterEngine`

---

## RESTRICCIONES (del spec, confirmadas)

- NO tocar: scraping pipeline, JobOffer schema, ProductGenerator existente (salvo integración mínima)
- NO hacer: frontend redesign, Angular refactors, overengineering, microservicios, CQRS extra
- NO crear tests masivos todavía
- MANTENER: ClusterKey determinístico SHA256 como fallback
- MANTENER: pipeline de clusters existente funcionando en paralelo

---

## TOTAL DE ARCHIVOS

| Tipo | Cantidad |
|---|---|
| Archivos nuevos | 6 |
| Archivos modificados | 8 |
| Migraciones | 3 |
| **Total** | **17** |
