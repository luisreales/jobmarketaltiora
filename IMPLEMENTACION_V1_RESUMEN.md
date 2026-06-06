# Market Intelligence Engine V1 — Documentación de Implementación

> Fecha: 2026-05-18  
> Rama: master  
> Stack: .NET 9 · EF Core · PostgreSQL · Semantic Kernel 1.45 · Angular 20

---

## Índice

1. [Arquitectura general](#1-arquitectura-general)
2. [Fase 1 — Cleaner Pipeline](#2-fase-1--cleaner-pipeline)
3. [Fase 2 — LLM Synthesis mejorado](#3-fase-2--llm-synthesis-mejorado)
4. [Fase 3 — Semantic Clustering](#4-fase-3--semantic-clustering)
5. [Fase 4 — Opportunity Engine V2](#5-fase-4--opportunity-engine-v2)
6. [Lead Scoring — Consulting Penalty](#6-lead-scoring--consulting-penalty)
7. [Migrations](#7-migrations)
8. [Frontend](#8-frontend)
9. [Registro de DI (Program.cs)](#9-registro-de-di-programcs)
10. [Archivos creados y modificados](#10-archivos-creados-y-modificados)

---

## 1. Arquitectura general

Pipeline de 6 etapas orquestado por `ClusteringHostedService` con intervalo configurable (default 30 min, clave `Jobs:Clustering:IntervalSeconds`). Solo corre cuando hay nuevos `JobInsights` procesados desde el último ciclo.

```
Stage 0  → Semantic Embeddings      ISemanticClusterEngine.GenerateEmbeddingsAsync()
Stage 1  → SHA256 Clustering        IClusterEngine.RebuildClustersAsync()
Stage 2  → Decision Engine          IDecisionEngine.EvaluateClustersAsync()
Stage 2b → Semantic Group Keys      ISemanticClusterEngine.AssignSemanticGroupsAsync()
Stage 3  → Commercial Intelligence  IOpportunityEngineV2.EnrichClustersAsync()
Stage 4  → Product Generator        IProductGeneratorService.GenerateProductsAsync()
Stage 5  → LLM Synthesis            IClusterSynthesisService.SynthesizePendingClustersAsync()
```

Los stages 0 y 2b son **aditivos**: no reemplazan el clustering SHA256 existente, solo enriquecen registros ya existentes.

---

## 2. Fase 1 — Cleaner Pipeline

### IJobDescriptionCleanerService

**Archivo:** `backend/Application/Interfaces/IJobDescriptionCleanerService.cs`

```csharp
public interface IJobDescriptionCleanerService {
    CleanedJobDescription Clean(string rawDescription, string companyName = "");
}

public sealed record CleanedJobDescription(
    string CleanText,
    bool IsConsulting,
    int OriginalLength,
    int CleanedLength);
```

### JobDescriptionCleanerService

**Archivo:** `backend/Infrastructure/Services/JobDescriptionCleanerService.cs`

Pipeline de 4 etapas en orden:

| Etapa | Método | Descripción |
|---|---|---|
| 1 | `RemoveCorporateFluff` | Elimina párrafos con 60+ marcadores de fluff corporativo |
| 2 | `ExtractTechnicalSections` | Retiene solo secciones con 40+ señales técnicas |
| 3 | `NormalizeWhitespace` | Colapsa espacios y líneas vacías múltiples |
| 4 | `TrimToTokenBudget` | Corta a 4,000 caracteres máximo |

**Detalles:**
- `[GeneratedRegex]` para todos los patrones — compilación en tiempo de build, no runtime
- Blacklist extendida de consulting companies (Accenture, Infosys, Wipro, TCS, Cognizant, etc.)
- El flag `IsConsulting` en el resultado se propaga al `LeadScoringService`

---

## 3. Fase 2 — LLM Synthesis mejorado

### ClusterSynthesisService

**Archivo:** `backend/Infrastructure/Services/ClusterSynthesisService.cs`

**Cambios respecto a la versión anterior:**

- Inyecta `IJobDescriptionCleanerService` — reemplaza el método privado `CleanDescription()` interno
- Nuevo campo `businessOpportunity` en el JSON devuelto por el LLM
- Nuevo campo `confidence` (0.0–1.0) que mide coherencia del cluster
- System prompt con reglas anti-buzzword estrictas en español

**Estructura JSON esperada del LLM:**

```json
{
  "pain": "Cuello de botella técnico real en 2 líneas.",
  "businessOpportunity": "Impacto empresarial concreto y por qué urge resolverlo ahora.",
  "mvp": "Nombre y descripción de un servicio ágil.",
  "leadMessage": "Cold email de 3 líneas para el CTO.",
  "confidence": 0.85
}
```

**Dos modos de operación:**

| Modo | Método | Disparador |
|---|---|---|
| Batch | `SynthesizePendingClustersAsync()` | Background worker, máx 5 clusters por ciclo |
| On-demand | `SynthesizeClusterAsync(int clusterId)` | HTTP POST desde UI, cache hit si ya completado |

**Campos persistidos en `MarketCluster`:**

- `SynthesizedPain`
- `SynthesizedBusinessOpportunity` ← nuevo
- `SynthesizedMvp`
- `SynthesizedLeadMessage`
- `LlmConfidence` ← nuevo

---

## 4. Fase 3 — Semantic Clustering

### ISemanticClusterEngine

**Archivo:** `backend/Application/Interfaces/ISemanticClusterEngine.cs`

```csharp
public interface ISemanticClusterEngine {
    Task<int> GenerateEmbeddingsAsync(CancellationToken ct = default);
    Task<int> AssignSemanticGroupsAsync(double similarityThreshold = 0.82, CancellationToken ct = default);
}
```

### SemanticClusterEngine

**Archivo:** `backend/Infrastructure/Services/SemanticClusterEngine.cs`

```csharp
#pragma warning disable SKEXP0001  // ITextEmbeddingGenerationService es experimental en SK 1.45
```

**`GenerateEmbeddingsAsync`:**
- Carga `JobInsights` sin `EmbeddingVectorJson` todavía
- Texto a embeddar: `MainPainPoint + " " + PainCategory + " " + SuggestedSolution`
- Procesa en batches de 20
- Serializa el vector `float[]` a JSON y lo guarda en `EmbeddingVectorJson`
- Registra timestamp en `EmbeddedAt`

**`AssignSemanticGroupsAsync`:**
- Algoritmo Union-Find sobre todos los clusters con embeddings
- Threshold default 0.82 (cosine similarity)
- `SemanticGroupKey` = SHA256 del ID del representante del grupo (determinístico)
- Additive: no toca el `ClusterKey` SHA256 existente

### ClusterSimilarityService

**Archivo:** `backend/Infrastructure/Services/ClusterSimilarityService.cs`

Utilidad estática con tres métodos:

```csharp
static double CosineSimilarity(float[] a, float[] b)
static float[] ComputeCentroid(IReadOnlyList<float[]> vectors)
static float[]? Deserialize(string? json)
static string Serialize(float[] vector)
```

### Configuración de embeddings

En `appsettings.json`, sección `SemanticKernel`:

```json
{
  "SemanticKernel": {
    "Provider": "OpenAI",
    "ApiKey": "sk-...",
    "ModelId": "claude-...",
    "EmbeddingModelId": "text-embedding-3-small"
  }
}
```

Si `EmbeddingModelId` está vacío, los stages 0 y 2b se omiten silenciosamente (no rompen el pipeline).

---

## 5. Fase 4 — Opportunity Engine V2

### IOpportunityEngineV2

**Archivo:** `backend/Application/Interfaces/IOpportunityEngineV2.cs`

```csharp
public interface IOpportunityEngineV2 {
    Task<int> EnrichClustersAsync(CancellationToken ct = default);
}
```

### OpportunityEngineV2

**Archivo:** `backend/Infrastructure/Services/OpportunityEngineV2.cs`

12 campos calculados por reglas (sin LLM):

| Campo | Tipo | Fórmula / Fuente |
|---|---|---|
| `EstimatedTam` | double | Lookup por industry (Fintech=480M, Healthcare=390M, E-commerce=310M…) |
| `BuyingIntentScore` | double | `Urgency×0.5 + DirectRatio×10×0.3 + HiringVelocity×2×0.2`, clamped 0–10 |
| `EnterpriseComplexity` | double | Conteo de tokens en NormalizedTechStack, escala logarítmica, clamped 1–10 |
| `HiringVelocity` | double | `JobCount / max(1, daysSinceCreated)`, normalizado |
| `DeliveryFeasibility` | double | Base 7.0, +1.5 si stack conocido, −2.0 si stack exótico, clamped 1–10 |
| `SalesFriction` | double | +2 consulting, +1 enterprise complexity >7, −1 QuickWin, clamped 1–10 |
| `RevenuePotential` | double | `BlueOceanScore × log10(TAM+1) / 2`, clamped 0–10 |
| `PriorityScoreV2` | int | `BOC×0.35 + BuyIntent×0.25 + Urgency×0.20 + DirectRatio×0.10 + GrowthRate×0.10` × 10 |
| `RecommendedServiceModel` | string? | "Fixed-price Sprint", "Retainer", "Audit + Roadmap", "T&M Block" |
| `SalesAngle` | string? | Rule-based: urgencia alta, compliance, modernización, cloud migration… |
| `WhyNow` | string? | Rule-based: hiring velocity, growth rate, direct client ratio… |
| `EstimatedCloseProbability` | double | Media normalizada de BuyIntent + DirectRatio + InverseFriction |

**Stacks conocidos** (bonus en DeliveryFeasibility):
`react`, `angular`, `vue`, `.net`, `node`, `python`, `postgres`, `redis`, `docker`, `kubernetes`

---

## 6. Lead Scoring — Consulting Penalty

**Archivo:** `backend/Infrastructure/Services/LeadScoringService.cs`

```csharp
private const double ConsultingPenaltyMultiplier = 0.70;

// En Calculate():
var multiplier = insight.IsDirectClient ? 1.0 : ConsultingPenaltyMultiplier;
return (int)Math.Clamp(Math.Round(raw * multiplier), 0, 100);
```

Las consulting companies son clientes indirectos para AltioraTech — reducción del 30% en su LeadScore para priorizarlas por debajo de clientes directos en el pipeline de ventas.

**Fórmula completa del LeadScore:**

```
LeadScore = (OpportunityScore × 0.40
           + UrgencyScore × 5 × 0.20
           + DirectClientBonus (20 si directo, 0 si no)
           + RecencyBoost × 0.20)
           × ConsultingPenalty (0.70 si consulting, 1.0 si directo)
```

Recency buckets: 0–7 días = 100pts, 8–30 días = decay 100→40, 31–90 días = decay 40→5, >90 = 0.

---

## 7. Migrations

Tres migraciones generadas y aplicadas:

| Migración | Fecha | Cambios |
|---|---|---|
| `AddClusterSynthesisFields` | 2026-05-18 | `SynthesizedBusinessOpportunity` (string?), `LlmConfidence` (double?) en `MarketCluster` |
| `AddOpportunityEngineV2Fields` | 2026-05-18 | 12 campos V2 en `MarketCluster` (EstimatedTam, BuyingIntentScore, EnterpriseComplexity, HiringVelocity, DeliveryFeasibility, SalesFriction, RevenuePotential, PriorityScoreV2, RecommendedServiceModel, SalesAngle, WhyNow, EstimatedCloseProbability) |
| `AddJobInsightEmbeddings` | 2026-05-18 | `EmbeddingVectorJson` (string?), `EmbeddedAt` (DateTime?) en `JobInsight`; `SemanticGroupKey` (string?) en `MarketCluster` |

Para aplicar en un entorno nuevo:

```bash
cd backend
dotnet ef database update
```

---

## 8. Frontend

### MarketCluster interface

**Archivo:** `frontend/src/app/models/market.models.ts`

Campos añadidos a la interfaz `MarketCluster`:

```typescript
// LLM synthesis additions
synthesizedBusinessOpportunity?: string;
llmConfidence?: number;

// Opportunity Engine V2
estimatedTam: number;
buyingIntentScore: number;
enterpriseComplexity: number;
hiringVelocity: number;
deliveryFeasibility: number;
salesFriction: number;
revenuePotential: number;
priorityScoreV2: number;
recommendedServiceModel?: string;
salesAngle?: string;
whyNow?: string;
estimatedCloseProbability: number;

// Semantic clustering
semanticGroupKey?: string;
```

### Página /clusters

**Archivos:** `frontend/src/app/pages/clusters/clusters.ts` + `clusters.html`

Funcionalidades:

- Lista paginada de clusters ordenada por `PriorityScoreV2`
- Filtros: OpportunityType, Actionable, Industry
- Cards expandibles con:
  - Badge `PriorityScoreV2` + tipo + actionable + `semanticGroupKey` (8 chars)
  - Scores en columnas: BlueOcean, BuyingIntent, TAM formateado, Close%
  - Barras de progreso para RevenuePotential, DeliveryFeasibility, SalesFriction, EnterpriseComplexity
  - Panel de Sales Intelligence: SalesAngle, WhyNow, RecommendedServiceModel
  - Síntesis LLM completa: Pain, BusinessOpportunity, MVP, Cold Email, porcentaje de Confidence
  - Tabla de top leads con LeadScore coloreado (verde ≥70, amber ≥50)
  - Paginación de leads independiente
  - Botón "Synthesize" si pendiente, "Re-synthesize" si ya completado
- Botón "Rebuild Clusters" con mensaje de resultado

### Routing y navegación

**`frontend/src/app/app.routes.ts`:**
```typescript
{ path: 'clusters', loadComponent: () => import('./pages/clusters/clusters').then(m => m.ClustersPage) }
```

**`frontend/src/app/app.ts`:**
```typescript
{ path: '/clusters', label: 'Clusters' }
```

---

## 9. Registro de DI (Program.cs)

```csharp
// Fase 1 — Cleaner
builder.Services.AddScoped<IJobDescriptionCleanerService, JobDescriptionCleanerService>();

// Fase 3 — Semantic Clustering
builder.Services.AddScoped<ISemanticClusterEngine, SemanticClusterEngine>();

// Fase 4 — Opportunity Engine V2
builder.Services.AddScoped<IOpportunityEngineV2, OpportunityEngineV2>();
```

---

## 10. Archivos creados y modificados

### Creados

| Archivo | Descripción |
|---|---|
| `backend/Application/Interfaces/IJobDescriptionCleanerService.cs` | Contrato + record de resultado |
| `backend/Infrastructure/Services/JobDescriptionCleanerService.cs` | Pipeline 4-etapas con GeneratedRegex |
| `backend/Application/Interfaces/IOpportunityEngineV2.cs` | Contrato V2 |
| `backend/Infrastructure/Services/OpportunityEngineV2.cs` | 12 campos regla-based |
| `backend/Application/Interfaces/ISemanticClusterEngine.cs` | Contrato embeddings + grupos semánticos |
| `backend/Infrastructure/Services/SemanticClusterEngine.cs` | Embeddings + Union-Find |
| `backend/Infrastructure/Services/ClusterSimilarityService.cs` | Cosine similarity, centroid, serialize |
| `backend/Migrations/20260518224811_AddClusterSynthesisFields.*` | Migration BD |
| `backend/Migrations/20260518224950_AddOpportunityEngineV2Fields.*` | Migration BD |
| `backend/Migrations/20260518225541_AddJobInsightEmbeddings.*` | Migration BD |
| `frontend/src/app/pages/clusters/clusters.ts` | Componente Angular |
| `frontend/src/app/pages/clusters/clusters.html` | Template con cards expandibles |

### Modificados

| Archivo | Cambios |
|---|---|
| `backend/Domain/Entities/MarketCluster.cs` | +15 propiedades nuevas |
| `backend/Domain/Entities/JobInsight.cs` | +`EmbeddingVectorJson`, `EmbeddedAt` |
| `backend/Application/Contracts/ClusterContracts.cs` | `MarketClusterDto` actualizado con todos los campos |
| `backend/Infrastructure/Services/ClusterSynthesisService.cs` | `IJobDescriptionCleanerService`, campos V2, nuevo JSON |
| `backend/Infrastructure/Services/ClusteringHostedService.cs` | Stages 0, 2b, 3 activados; Stage 5 renombrado |
| `backend/Infrastructure/Services/LeadScoringService.cs` | Consulting penalty 0.70 |
| `backend/Infrastructure/Services/SemanticKernelProvider.cs` | `IsEmbeddingConfigured`, registro `AddOpenAITextEmbeddingGeneration` |
| `backend/Infrastructure/Services/SemanticKernelOptions.cs` | +`EmbeddingModelId` |
| `backend/Application/Interfaces/ISemanticKernelProvider.cs` | +`bool IsEmbeddingConfigured` |
| `backend/Controllers/MarketClusterController.cs` | `ToDto()` actualizado |
| `backend/Migrations/ApplicationDbContextModelSnapshot.cs` | Snapshot actualizado |
| `backend/Program.cs` | +3 registros de DI |
| `frontend/src/app/models/market.models.ts` | +15 campos en `MarketCluster` |
| `frontend/src/app/app.routes.ts` | +ruta `/clusters` |
| `frontend/src/app/app.ts` | +nav item "Clusters" |
