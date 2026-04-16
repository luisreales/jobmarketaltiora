# Pendiente de Desarrollo — Engine Altiora v2

> Última actualización: 2026-04-13
> Estado auditado directamente del código en producción.

---

## Estado actual del sistema (baseline real)

```
JobInsights:         155 procesados (re-enriched con Fase 0)
MarketClusters:      139 (en DB) → con Signal Filter activo solo se crearán clusters con señal real
Clusters accionables: 20 (4 MVPProduct + 16 Consulting)
ProductSuggestions:  20 generadas y persistidas
LLM calls:           0 productivas (ClusterSynthesisService no implementado)
Signal Filter:       activo (jobCount>=5, uniqueCompanies>=3, uniqueDays>=2)
Decision Engine:     activo (IsActionable: BlueOceanScore>=40 + OpportunityType!=Ignore)
Product Generator:   activo (rule-based, 10 productos, auto-genera en cada ciclo)
```

---

## Pipeline completo (estado actual)

```
SCRAPER ✅
  LinkedIn (Playwright), Upwork (scraper-api Node.js), Indeed
  ↓
RAW DATA ✅
  Tabla JobOffer — Postgres vía EF Core
  ↓
WORKER 1 — INSIGHTS + DATA QUALITY ✅
  MarketIntelligenceHostedService
  JobPreprocessorService → limpia descripción, filtra consultoras
  RuleBasedAiEnrichmentService → clasifica por reglas
  TechCanonicalizer → tokens canónicos (NET, SQL, AZURE…)
  IndustryClassifier → Fintech | Health | Logistics | Ecommerce | SaaS | Unknown
  LeadScoringService → LeadScore 0–100 por lead
  ReenrichStaleInsightsAsync → retroalimenta registros legacy con campos Unknown
  Tabla JobInsight enriquecida
  ↓
WORKER 2 — CLUSTER ENGINE ✅
  ClusterEngine.RebuildClustersAsync()
  Signal Filter: jobCount>=5, uniqueCompanies>=3, uniqueDays>=2
  Agrupa por (PainCategory + TechTop3 + Industry + CompanyType)
  Calcula BlueOceanScore v2 (6 factores)
  ClusterKey determinístico: SHA256(PainCategory|TechTop3|Industry|CompanyType)
  Upsert en MarketClusters
  ↓
DECISION ENGINE ✅
  DecisionEngine.EvaluateClustersAsync()
  OpportunityType: MVPProduct / QuickWin / Consulting / Ignore
  IsActionable: BlueOceanScore >= 40 AND OpportunityType != Ignore
  PriorityScore: BlueOcean*0.50 + directRatio*0.20 + urgency*0.20 + growth*0.10
  ↓
PRODUCT GENERATOR ✅ (nuevo, rule-based)
  ProductGeneratorService.GenerateProductsAsync()
  ProductCatalog: 10 reglas estáticas → mapeo cluster → producto vendible
  Genera: WhyNow, Offer, ActionToday, TechFocus, DealRange
  Upsert en ProductSuggestions (1 por cluster accionable)
  ↓
🔥 LLM SYNTHESIS ❌ (PENDIENTE — Bloque D)
  ClusterSynthesisService NO implementado
  Semantic Kernel disponible pero sin llamadas productivas
  Solo para clusters IsActionable = true
  ↓
API + DASHBOARD ✅
  GET /api/market/clusters, /api/market/clusters/{id}/leads
  GET /api/products, POST /api/products/generate
  /opportunities (clusters + leads)
  /products (product generator — nuevo)
```

---

## Lo que está COMPLETADO ✅

### Bloque A — Data Quality (Fase 0)
- [x] `JobPreprocessorService` con `ShouldSkip()` + `Preprocess()`
- [x] `TechCanonicalizer` con mapa canónico completo
- [x] `IndustryClassifier` con 5 industrias
- [x] `LeadScoringService` con fórmula + recency boost
- [x] Campos nuevos en `JobInsight`: Industry, LeadScore, NormalizedTechStack, TechTokensJson, ClusterId
- [x] Migración EF Core para campos nuevos
- [x] Worker 1 integrado con todos los servicios Fase 0
- [x] `ReenrichStaleInsightsAsync` — retroalimenta los 155 registros legacy automáticamente

### Bloque B — Cluster Engine (Fases 1 + 2)
- [x] Entidad `MarketCluster` con todos los campos v2
- [x] Migración EF Core para `MarketCluster`
- [x] `IClusterEngine` + `ClusterEngine.RebuildClustersAsync`
- [x] Signal Filter (elimina clusters con < 5 jobs, < 3 empresas distintas, < 2 días)
- [x] `BlueOceanScore v2` (6 factores: volumen, growth, directRatio, urgencia, buyingPower, easeOfSale)
- [x] `ClusteringHostedService` con 3 stages: ClusterEngine → DecisionEngine → ProductGenerator

### Bloque C — Decision Engine (Fase 6)
- [x] `IDecisionEngine` + `DecisionEngine.EvaluateClustersAsync`
- [x] Reglas `OpportunityType` recalibradas (MVPProduct/QuickWin/Consulting/Ignore)
- [x] `IsActionable` (BlueOceanScore >= 40), `RecommendedStrategy`, `PriorityScore`

### Bloque E — API y UI
- [x] `MarketClusterController` con 5 endpoints (GET clusters, GET cluster, GET leads, POST rebuild, POST backfill)
- [x] `ClusterService` Angular + modelos Angular actualizados
- [x] `/opportunities` con cards de cluster, leads panel, trends, KPIs
- [x] `ProductController` con 3 endpoints (GET products, GET product/{id}, POST generate)
- [x] `ProductService` Angular + modelos ProductSuggestion
- [x] `/products` — página nueva con top 3 ejecutivo + grid resto + filtros + paginación

### Product Generator (nuevo — no estaba en el plan original)
- [x] `ProductSuggestion` entity + migración (`UpdateProductSuggestionsSnapshot`)
- [x] `IProductGeneratorService` + `ProductGeneratorService`
- [x] `ProductCatalog` — 10 productos con reglas de mapeo:
  1. .NET Azure Migration Sprint ($8K–$15K, 14d)
  2. API Integration Accelerator ($3K–$6K, 7d)
  3. System Performance Audit ($2K–$4K, 5d)
  4. Data Pipeline MVP ($12K–$20K, 21d)
  5. Fintech Compliance Dashboard ($15K–$25K, 30d)
  6. Rapid Delivery Sprint ($2K–$4K, 5d)
  7. DevOps & CI/CD Accelerator ($5K–$9K, 10d)
  8. Security & Identity Sprint ($4K–$8K, 7d)
  9. Microservices Decomposition Sprint ($10K–$18K, 21d)
  10. Platform Stabilization Package ($5K–$12K, 14d — catch-all)
- [x] Auto-generación en cada ciclo del ClusteringHostedService

### Bloque F — Rules Engine (Fase 5)
- [x] Categorías mejoradas en `RuleBasedAiEnrichmentService` (DataEngineering, Security, DevOps, Microservices, Observability)

---

## Lo que está PENDIENTE ❌

### Bloque D — LLM Synthesis (Fase 3) — PRIORIDAD MEDIA

El Semantic Kernel está conectado pero **no hay llamadas productivas**. Los campos `SynthesizedPain`, `SynthesizedMvp`, `SynthesizedLeadMessage` en `MarketCluster` están en la tabla pero todos son `null` con `LlmStatus = "pending"`.

| # | Tarea | Archivo destino | Detalle |
|---|---|---|---|
| D1 | `IClusterSynthesisService` + `ClusterSynthesisService` | `Application/Interfaces/` + `Infrastructure/Services/` | Lee clusters con IsActionable + LlmStatus="pending", llama SK, parsea JSON, guarda síntesis |
| D2 | Prompt template `cluster-synthesis` en `AiPromptTemplates` | seed en `DatabaseSeeder` o endpoint AI Audit | Ver prompt completo en sección FASE 3 del documento original |
| D3 | Campo `ClusterId` nullable en `AiPromptLog` | `Domain/Entities/AiPromptLog.cs` + migración | Para trazabilidad de qué llamada LLM generó qué síntesis |
| D4 | Conectar Stage 3 del `ClusteringHostedService` | `ClusteringHostedService.cs` línea 88 | Reemplazar comentario por llamada real a `IClusterSynthesisService` |

**Criterio de elegibilidad para LLM:**
```
IsActionable = true
AND OpportunityType != "Ignore"
AND LlmStatus = "pending"
AND budget diario no superado (MaxCallsPerDay de config)
```

**Impacto esperado:** los campos `SynthesizedPain`, `SynthesizedMvp`, `SynthesizedLeadMessage` se poblarán en las cards de /opportunities y /products, reemplazando la síntesis rule-based con lenguaje comercial generado.

---

### Bloque G — Calidad — PRIORIDAD BAJA

| # | Tarea | Estado |
|---|---|---|
| G1 | Tests unitarios: `TechCanonicalizer`, `IndustryClassifier`, `LeadScoringService` | ❌ |
| G2 | Tests unitarios: `ClusterEngine`, `BlueOceanScore`, `DecisionEngine` | ❌ |
| G3 | Tests unitarios: `ProductCatalog`, `ProductGeneratorService` | ❌ |
| G4 | E2E Playwright: vista `/opportunities` con clusters reales | ❌ |
| G5 | E2E Playwright: vista `/products` | ❌ |

---

### Mejoras opcionales — PRIORIDAD BAJA

| # | Tarea | Descripción |
|---|---|---|
| M1 | Limpiar clusters legacy con poca señal | Los 139 clusters existentes pre-Signal-Filter no fueron filtrados. Al acumular más datos y hacer rebuild se limpiarán solos. Opción: endpoint `DELETE /api/market/clusters/cleanup` que elimine clusters con JobCount < 5. |
| M2 | Mostrar LLM synthesis en /products | Cuando D1-D4 estén listos, reemplazar `productDescription` rule-based por `SynthesizedMvp` del cluster en la card de /products. |
| M3 | Normalizar salario en ingesta | JobOffer.SalaryRange llega como string libre. Parser para extraer min/max como decimal. |
| M4 | Optimizar bundle size frontend | Bundle actual 744kB (budget 500kB). Revisar imports innecesarios, lazy loading de rutas. |
| M5 | Métricas de performance (query/render) | Añadir timing logging a los endpoints de clusters y products. |

---

## Próxima acción recomendada

**Bloque D — LLM Synthesis** es el único bloque técnico de impacto pendiente.

Orden de implementación:
1. `AiPromptLog.ClusterId` (campo nullable + migración) — 10 min
2. Prompt template `cluster-synthesis` en DatabaseSeeder — 20 min
3. `IClusterSynthesisService` + `ClusterSynthesisService` — 1 sesión
4. Conectar Stage 3 en `ClusteringHostedService` — 5 min

Resultado: las cards de /opportunities y /products mostrarán `SynthesizedPain` y `SynthesizedMvp` generados por LLM para los clusters accionables, reemplazando el texto genérico actual.

---

## Contexto técnico — No tocar

- Pipeline de scraping — nada del plan lo modifica.
- `JobOffer` — sin cambios pendientes.
- `HybridAiEnrichmentService` — shadow mode activo, no modificar.
- `MarketIntelligenceService.GetOpportunitiesAsync` — endpoints legacy siguen funcionando.
- `ApplicationDbContextModelSnapshot.cs` — actualizado, no editar manualmente.


##Modificaciones_Pendiente

Actúa como un Arquitecto Senior en .NET 9 y experto en IA usando Microsoft.SemanticKernel.

CONTEXTO DEL PROYECTO:
Estoy construyendo el "Engine v2" para AltioraTech. Es un sistema B2B que agrupa vacantes tecnológicas en "MarketClusters". 
Actualmente, el sistema agrupa correctamente los jobs y decide si un cluster es "IsActionable". Sin embargo, el contenido comercial (el dolor de mercado y la oferta) se está generando con un sistema basado en reglas estáticas (ProductGeneratorService), lo cual genera textos repetitivos.

OBJETIVO (BLOQUE D - LLM SYNTHESIS):
Necesito implementar el `ClusterSynthesisService` usando Semantic Kernel. Este servicio debe tomar los clusters viables, leer una muestra de las vacantes que lo componen, y pedirle al LLM (Claude 4.6) que haga ingeniería inversa para descubrir el dolor de negocio real y generar una propuesta de MVP.

Por favor, genera el código de producción en C# para completar las siguientes tareas usando Clean Architecture, EF Core y Dependency Injection:

TAREA 1: ACTUALIZACIÓN DEL LOG DE AUDITORÍA
En la entidad `AiPromptLog` (que ya debe existir en `Domain/Entities`), agrega la propiedad `public Guid? ClusterId { get; set; }`. Genera el comando de EF Core (add-migration) sugerido para actualizar la base de datos.

TAREA 2: SERVICIO DE SÍNTESIS (IClusterSynthesisService & ClusterSynthesisService)
1. Crea la interfaz `IClusterSynthesisService` con el método `Task SynthesizePendingClustersAsync(CancellationToken ct)`.
2. Implementa `ClusterSynthesisService`. Debe inyectar el `AppDbContext`, el `Kernel` (Semantic Kernel) y un `ILogger`.
3. Lógica del método:
   - Busca en `MarketClusters` hasta 5 registros donde `IsActionable == true` y `LlmStatus == "pending"`.
   - Por cada cluster, busca en `JobInsights` (o `JobOffers`) las descripciones de hasta 5 vacantes pertenecientes a ese cluster para usarlas como contexto.
   - Concatena y limpia ese texto (usa un helper rápido para quitar texto basura si es posible, limitando el tamaño a max 4000 caracteres para ahorrar tokens).

TAREA 3: EL PROMPT DE SEMANTIC KERNEL
Dentro del servicio, invoca a Semantic Kernel con este System Prompt (asegúrate de forzar la salida en JSON):
"Actúa como un Director de Estrategia B2B para una agencia de ingeniería de élite. 
Analiza este grupo de vacantes tecnológicas de empresas de la industria {cluster.Industry} que usan {cluster.TechTop3}. 
Tu objetivo es identificar el dolor de negocio común y empaquetar una solución (MVP/Auditoría) que podamos venderles hoy.
Devuelve ÚNICAMENTE un objeto JSON válido con esta estructura exacta:
{
  \"pain\": \"Descripción clara de 2 líneas sobre el cuello de botella técnico y de negocio que sufren.\",
  \"mvp\": \"Nombre y descripción de 2 líneas de un paquete de servicios ágil (ej. Auditoría en 7 días, Migración de x a y) para resolverlo.\",
  \"leadMessage\": \"Un mensaje de cold email de 3 líneas, muy directo, ofreciendo este MVP al CTO.\"
}"

TAREA 4: ACTUALIZACIÓN Y GUARDADO
- Parsea el JSON devuelto por el LLM.
- Actualiza el cluster: 
  `cluster.SynthesizedPain = json.pain;`
  `cluster.SynthesizedMvp = json.mvp;`
  `cluster.SynthesizedLeadMessage = json.leadMessage;`
  `cluster.LlmStatus = "completed";`
- Si el LLM falla o el JSON es inválido, haz un catch, loguea el error y marca `cluster.LlmStatus = "failed"`.
- Guarda el registro exacto de la petición y respuesta en un nuevo `AiPromptLog` (asignando el `ClusterId`).
- Llama a `SaveChangesAsync()`.

TAREA 5: INTEGRACIÓN EN EL WORKER
Muestra cómo inyectar y llamar a `_clusterSynthesisService.SynthesizePendingClustersAsync(stoppingToken)` dentro de la Etapa 3 (Stage 3) del `ClusteringHostedService`.

RESTRICCIONES:
- Usa `System.Text.Json` para el parseo.
- Todo debe ser asíncrono.
- Maneja las excepciones por cada cluster de forma individual dentro de un bucle para que, si un cluster falla, el lote completo no se caiga.