# Resumen General del Desarrollo — Altiora Platform

> Última actualización: 2026-04-14

---

## Pipeline de inteligencia — Estado actual

```
SCRAPER ✅
  LinkedIn (Playwright), Upwork (scraper-api Node.js), Indeed
  ↓
RAW DATA ✅
  Tabla JobOffer — Postgres vía EF Core
  ↓
INSIGHTS (Worker) ✅
  MarketIntelligenceHostedService → RuleBasedAiEnrichmentService
  Tabla JobInsight (155 registros procesados)
  ↓
🔥 CLUSTER ENGINE (FALTA)
  Agrupar insights similares entre sí (no solo por PainCategory string)
  Algoritmo de clustering semántico sobre MainPainPoint + TechStack
  Detectar "el mismo problema real" aunque venga con distintas palabras
  ↓
🔥 OPPORTUNITY ENGINE (FALTA)
  Convertir clusters en oportunidades de negocio reales con tamaño de mercado
  Calcular TAM por cluster (frecuencia × score promedio × urgencia)
  Priorizar qué problema atacar primero con criterio de ROI estimado
  Asignar MVPs diferenciados por cluster, no plantillas genéricas
  ↓
LLM (SOLO PARA SÍNTESIS) — parcialmente conectado
  SemanticKernel + gpt5 vía flow.ciandt.com
  Actualmente: shadow mode, no impacta decisiones productivas
  Falta: activar para síntesis de clusters y generación de mensajes personalizados
  ↓
DASHBOARD (DECISIONES) ✅ base implementada
  /opportunities con KPIs, trends y leads por pain point
  Falta: vista de clusters reales, ROI estimado, acciones de outreach
```

---

## 1. Objetivo del proyecto

Plataforma de inteligencia comercial para AltioraTech que transforma vacantes scrapeadas en oportunidades de negocio B2B accionables. Cubre desde la captura automatizada hasta la identificación de leads con mensaje de contacto listo.

---

## 2. Backend (.NET 9) — estado completo

### 2.1. Filtros y paginación server-side
- Endpoint `/api/jobs/jobs/query` con `page`, `pageSize`, `sortBy`, `sortDirection`, `source`, `search`, `minSalary`, `maxSalary`.
- Repositorio con consultas filtradas, ordenadas y paginadas directamente en SQL.
- La UI eliminó dependencia de filtrado local en memoria.

### 2.2. Scraping multi-provider
- `LinkedInProvider` — Playwright headless con sesión persistida.
- `UpworkProvider` — delega a `scraper-api` Node.js en puerto 3000.
- `IndeedProvider` — integrado en orquestador.
- `JobOrchestrator` — coordina providers y post-procesado.
- `JobsAutomationHostedService` — scraping automático por queries configuradas en `appsettings.json`.
- `JobPostProcessingHostedService` — post-procesado en lotes cada 20s.

### 2.3. Market Intelligence Engine (Engine Altiora)

**Implementado:**
- `MarketIntelligenceHostedService` — worker background cada 60s, lotes de 50 jobs.
- `RuleBasedAiEnrichmentService` — clasifica 5 categorías: Migration, Scaling, Integration, Automation, CloudModernization. Genera `SuggestedSolution` y `LeadMessage` por plantillas.
- `HybridAiEnrichmentService` — orquesta entre reglas y SK, shadow mode por defecto.
- `OpportunityScorerService` / `OpportunityScorer` — score 0-100 basado en señales de texto.
- `CompanyClassifier` — distingue cliente directo vs consultora.
- `SemanticKernelProvider` — conectado a `gpt5` via `flow.ciandt.com`. Verificado funcional con smoke test.
- `MarketIntelligenceExecutionTracker` — singleton que rastrea ejecuciones del worker.

**Entidades:**
- `JobInsight` — 155 registros procesados. Campos: `MainPainPoint`, `PainCategory`, `PainDescription`, `TechStack`, `OpportunityScore`, `UrgencyScore`, `SuggestedSolution`, `LeadMessage`, `ConfidenceScore`, `DecisionSource`, `EngineVersion`.
- `AiPromptLog` — 8 registros de trazabilidad. Campos: provider, modelId, promptHash, promptText, responseText, cacheHit, isSuccess, status, tokens, latencyMs.
- `AiPromptTemplate` — 1 template activo (`market-job-analysis`), gestionable desde UI.

### 2.4. Endpoints de negocio

| Endpoint | Estado | Descripción |
|---|---|---|
| `GET /api/market/opportunities` | ✅ | Oportunidades agrupadas por pain point, paginadas |
| `GET /api/market/leads` | ✅ | Leads filtrados por pain point, score, source |
| `GET /api/market/trends` | ✅ | Tendencias por categoría en ventana de tiempo |
| `GET /api/ai/logs` | ✅ | Logs de prompts paginados con filtros |
| `GET /api/ai/summary` | ✅ | KPIs de uso LLM en ventana configurable |
| `GET /api/ai/prompt-template/:key` | ✅ | Lee template activo |
| `PUT /api/ai/prompt-template/:key` | ✅ | Actualiza template desde UI |
| `POST /api/ai/worker/run` | ✅ | Trigger manual del worker |
| `GET /api/semantic/hello` | ✅ | Smoke test SK — guarda log con respuesta real |

### 2.5. Corrección del smoke test SK (último cambio)
- El endpoint `/api/semantic/hello` ahora guarda siempre `ResponseText` real en `AiPromptLogs`.
- Si el modelo retorna vacío: reintento controlado con `gpt-4o-mini` como fallback.
- Verificado: respuesta `"Hi there! How can I help you today?"`, `status: success`, `ModelId: gpt5`.
- Latencia observada: ~7.5s (modelo razonador, esperado).

---

## 3. Frontend (Angular 20) — estado completo

### 3.1. Páginas implementadas

| Ruta | Componente | Estado |
|---|---|---|
| `/jobs` | Dashboard | ✅ Filtros server-side, paginación, cards, estado en query params |
| `/jobs/:id` | JobDetail | ✅ Detalle de vacante, navegación back preserva página previa |
| `/opportunities` | Opportunities | ✅ KPIs, trends, cards por pain point, leads por pain point, paginación |
| `/ai-audit` | AiAudit | ✅ Tabla de logs, KPIs, gestión de prompt template, run worker manual |
| `/scraping` | Search | ✅ Formulario de scraping manual |

### 3.2. Servicios frontend

- `JobsService` → `GET /api/jobs/jobs/query`
- `MarketService` → endpoints `/api/market/*`
- `AiAuditService` → endpoints `/api/ai/*`

### 3.3. Features transversales
- Tailwind CSS con pipeline PostCSS configurado.
- Persistencia de estado de lista en query params (página, filtros, sort) — restauración al navegar atrás.
- Suite E2E con Playwright — 6 casos cubiertos en `/jobs`.

---

## 4. Infraestructura

- Docker Compose con 4 servicios: `backend` (8080), `frontend` (4200), `postgres` (5432), `scraper-api` (3000).
- Todos los contenedores levantados y saludables (verificado 2026-04-12).
- Variable de entorno `FLOW_API_KEY` para API key de SK (pendiente de configurar en docker-compose para producción).
- Warning activo: `JOBS_MARKET_PROMPT_TEMPLATE` env var no seteada — se usa el template de DB correctamente.

---

## 5. Calidad y pruebas

- Suite E2E Playwright en `/frontend/e2e/tests/jobs.spec.ts` — 6 casos en verde.
- Tests unitarios pendientes para rules engine y scoring.
- Tests de integración backend pendientes para contratos de paginación.

---

## 6. Qué falta — Backlog priorizado

### 🔥 Alta prioridad — Cluster Engine (no existe)

El problema actual: `GetOpportunitiesAsync` agrupa por `MainPainPoint` **string exacto**. Eso significa que "Legacy Modernization" y "Legacy Migration" son oportunidades distintas aunque sean el mismo problema real. No hay clustering semántico.

**Lo que falta construir:**

**Backend:**
1. `ClusterEngine` — servicio que agrupa `JobInsights` con lógica semántica (similitud de pain point + tech stack común). Primera versión puede ser con reglas de sinónimos; segunda versión con embeddings.
2. `JobInsightCluster` — nueva entidad o tabla de clusters con: `ClusterId`, `Label`, `PainCategories[]`, `JobCount`, `AvgScore`, `RepresentativeInsightId`.
3. Actualizar `GetOpportunitiesAsync` para devolver clusters reales, no agrupaciones por string.
4. Lógica de re-clusterización periódica (worker o trigger manual).

**Frontend:**
5. Actualizar `/opportunities` para mostrar clusters, no solo categorías — cards más ricas con tech stack agregado y tamaño de cluster.

### 🔥 Alta prioridad — Opportunity Engine (incompleto)

El problema actual: `OpportunityScore` es un entero calculado por señales de texto. No hay criterio de ROI, no hay priorización entre oportunidades, no hay tamaño de mercado estimado.

**Lo que falta construir:**

**Backend:**
6. `OpportunityEngine` — calcula TAM estimado por cluster: `(jobCount × avgScore × urgencyFactor)`.
7. Ranking de oportunidades por ROI estimado, no solo por frecuencia.
8. Campos nuevos en `MarketOpportunityDto`: `estimatedMarketSize`, `roiRank`, `directClientRatio`, `recommendedAction`.
9. MVP diferenciado por cluster (no plantilla genérica por categoría) — aquí entra el LLM.

**Frontend:**
10. Dashboard de decisión: "Esta semana ataca X porque tiene mayor ROI estimado y N clientes directos".

### Media prioridad — LLM para síntesis real

El LLM está conectado pero en shadow mode. Falta activarlo para tareas de alto valor donde las reglas no escalan:

11. Activar SK para generar `LeadMessage` personalizado por empresa (no plantilla genérica).
12. Activar SK para síntesis de cluster: dado un cluster de 40 jobs, generar descripción del problema de negocio en 2 frases.
13. Comparar calidad rules vs LLM en `AiPromptLogs` — ya existe la infraestructura, falta la métrica de comparación.

### Media prioridad — Calidad de datos

14. Normalizar salario desde ingestión — el filtro `minSalary/maxSalary` falla cuando los valores vienen en formatos distintos.
15. Mejorar `RuleBasedAiEnrichmentService` — actualmente 5 categorías muy amplias. Añadir al menos 5 más: DataEngineering, Security, DevOps, Microservices, Observability.
16. Blacklist de consultoras mejorada — la config existe (`ConsultingCompanyBlacklist`) pero no se aplica al scoring de forma diferenciada.

### Baja prioridad — Infraestructura y calidad

17. Tests unitarios del rules engine y opportunity scorer.
18. Tests de integración backend para contratos de paginación/filtros.
19. Más casos E2E: flujo `/opportunities`, detalle desde lead, filtros combinados.
20. Optimizar bundle size frontend (budget warning activo en build).
21. Métricas de performance: tiempo de respuesta de query y render de lista.
22. Configurar `FLOW_API_KEY` como secret en docker-compose para entornos no-dev.

---

## 7. Datos actuales en producción local

| Tabla | Registros |
|---|---|
| JobOffers | 1 (scraping pendiente de reejecutar) |
| JobInsights | 155 |
| AiPromptLogs | 8 |
| AiPromptTemplates | 1 |

> JobOffers tiene solo 1 registro porque el scraping no se ha vuelto a ejecutar tras el último rebuild. Los 155 insights corresponden a un scraping anterior. Ejecutar scraping manual desde `/scraping` para recuperar volumen.

---

## 8. Próximos pasos inmediatos recomendados

1. **Cluster Engine v1** — implementar agrupación semántica básica con sinónimos sobre `JobInsights` existentes. Sin LLM todavía.
2. **Ejecutar scraping** — poblar `JobOffers` con volumen real para validar el pipeline completo.
3. **Activar LLM para LeadMessage** — con los 155 insights existentes ya hay datos para comparar calidad rules vs LLM.
4. **Normalizar salario** — bloquea el filtro salarial para una fracción importante de vacantes.

---

---

# Sesión 2026-04-14 — Bloque D (LLM Synthesis) + Product Refactor

## Resumen ejecutivo

Implementación completa del **Bloque D (LLM Synthesis)** para clusters y productos, refactor del esquema `ProductSuggestion` de 1:1 a N:1 por ProductName, nueva página de detalle de producto con plan táctico IA, y fix crítico de timeout en llamadas al LLM.

---

## D1. ClusterSynthesisService — LLM para clusters

**Archivos:**
- `backend/Application/Interfaces/IClusterSynthesisService.cs`
- `backend/Infrastructure/Services/ClusterSynthesisService.cs`

- `SynthesizeClusterAsync(id, ct)` — on-demand con cache hit (`LlmStatus == "completed"` → retorna sin llamar LLM)
- `SynthesizePendingClustersAsync(ct)` — batch, procesa hasta 5 clusters en cola por ciclo
- Carga hasta 5 descripciones de jobs del cluster como contexto
- Prompt: Director de Estrategia B2B, output JSON forzado `{pain, mvp, leadMessage}`
- Guarda en `cluster.SynthesizedPain`, `SynthesizedMvp`, `SynthesizedLeadMessage`, `LlmStatus`
- Si falla → `LlmStatus = "failed"`, catch individual por cluster (un fallo no cancela el lote)
- Registra en `AiPromptLog` con `ClusterId` para trazabilidad completa

## D2. AiPromptLog — campo ClusterId

**Archivo:** `backend/Domain/Entities/AiPromptLog.cs`

- Nueva propiedad `public int? ClusterId { get; set; }` (nullable FK a MarketClusters)
- Migración EF Core: `AddClusterIdToAiPromptLog`

## D3. ClusteringHostedService — Stage 3 activado

**Archivo:** `backend/Infrastructure/Services/ClusteringHostedService.cs`

- Stage 3 conectado: `clusterSynthesis.SynthesizePendingClustersAsync(ct)` (ya no es comentario)

## D4. Endpoint on-demand clusters

**Archivo:** `backend/Controllers/MarketClusterController.cs`

- `POST /api/market/clusters/{id}/synthesize` — retorna `MarketClusterDto` con campos synthesized
- CancellationToken independiente de 310 s (no depende del timeout del request HTTP)

---

## D5. ProductSynthesisService — LLM para productos

**Archivos:**
- `backend/Application/Interfaces/IProductSynthesisService.cs`
- `backend/Infrastructure/Services/ProductSynthesisService.cs`

- Prompt genera plan táctico: `{implementacion, requerimientos, tiempo_y_tecnologias, empresas_objetivo}`
- Carga nombres de empresas de los clusters del producto (top 10 por LeadScore)
- Resultado en `product.SynthesisDetailJson`, `LlmStatus = "completed"` o `"failed"`
- `POST /api/products/{id}/synthesize` agregado a `ProductController`

---

## Refactor ProductSuggestion — Esquema N:1

### Problema anterior
Esquema 1:1 generaba una fila por cluster con el mismo producto → métricas fragmentadas, tarjetas duplicadas en UI.

### Nuevo esquema
**Clave única:** `ProductName` (índice UNIQUE en DB)

Campos nuevos de agregación:
| Campo | Descripción |
|---|---|
| `ClusterIdsJson` | JSON array de int con todos los cluster IDs contribuyentes |
| `TotalJobCount` | suma de JobCount de todos los clusters |
| `AvgDirectClientRatio` | promedio ponderado por JobCount |
| `AvgUrgencyScore` | promedio ponderado por JobCount |
| `TopBlueOceanScore` | máximo del grupo |
| `ClusterCount` | número de clusters contribuyentes |
| `SynthesisDetailJson` | JSON del plan táctico generado por LLM |
| `LlmStatus` | "pending" / "completed" / "failed" |

**Migración:** `RefactorProductSuggestions` con dedup SQL previo al índice UNIQUE:
```sql
DELETE FROM "ProductSuggestions"
WHERE "Id" NOT IN (SELECT MAX("Id") FROM "ProductSuggestions" GROUP BY "ProductName");
```

**ProductGeneratorService reescrito:** agrupa por ProductName, agrega métricas, `MostFrequent()` para OpportunityType e Industry, orphan deletion por ProductName.

---

## Fix crítico — Timeout LLM (100 s → 300 s)

### Síntoma
```
HttpClient.Timeout of 100 seconds elapsing
```

### Fix
**`SemanticKernelOptions.cs`** — `TimeoutSeconds = 300` configurable

**`SemanticKernelProvider.cs`:**
```csharp
var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(300) };
builder.AddOpenAIChatCompletion(..., httpClient: httpClient);
```

**Ambos endpoints synthesize** — CancellationToken propio de 310 s (desacoplado del request HTTP)

---

## Frontend — Nueva página `/products/:id`

**Archivos:**
- `frontend/src/app/pages/product-detail/product-detail.ts`
- `frontend/src/app/pages/product-detail/product-detail.html`

- Header: badges, nombre, tech tokens, price block, 4 KPIs, WhyNow/Oferta/Acción Hoy
- CTA "✨ Generar Plan de Ataque (IA)" → `POST /api/products/{id}/synthesize`
- Plan dashboard: 3 cards (Implementación / Requerimientos+Tiempos / Empresas Objetivo)
- Botón "↻ Regenerar plan" si ya existe síntesis
- Ruta agregada: `{ path: 'products/:id', component: ProductDetail }`

## Frontend — Catálogo `/products` actualizado

- Search client-side por productName, techFocus, industry, whyNow
- Campos corregidos: `totalJobCount`, `avgDirectClientRatio`, `topBlueOceanScore`
- Cards enlazan a `/products/:id`
- `ProductSuggestion` model reescrito para nuevo esquema

---

## Estado final del pipeline (2026-04-14)

```
Cluster Engine:       ✅ Signal Filter + BlueOceanScore v2
Decision Engine:      ✅ OpportunityType + IsActionable + PriorityScore
Product Generator:    ✅ 10 productos rule-based, agrupados por ProductName N:1
LLM Synthesis:        ✅ Clusters (batch + on-demand) — SynthesizedPain/Mvp/LeadMessage
                      ✅ Productos (on-demand) — plan táctico 4 campos
SK HttpClient:        ✅ 300 s timeout
Build:                ✅ 0 errores
```

## Pendiente post-sesión

| # | Tarea | Prioridad |
|---|---|---|
| P1 | Migrar `product-detail.html` de `*ngIf/*ngFor` a `@if/@for` (Angular 20) | Baja |
| P2 | Lazy loading de rutas (bundle 761kB → meta 500kB) | Media |
| P3 | Upwork Node.js microservice (U-1 a U-6) | Media |
| P4 | JobOrchestrator refactor con Strategy Pattern + Polly | Media |
| P5 | Tests unitarios (G1–G5) | Baja |
| P6 | `DELETE /api/market/clusters/cleanup` — purgar clusters legacy | Baja |
