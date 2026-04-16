# ENGINE REAL ALTIORA

## Objetivo del feature
Construir una capa de inteligencia comercial sobre el scraping existente para transformar vacantes crudas en oportunidades accionables para Altiora.

El nuevo engine debe:
- Detectar problemas reales de negocio desde descripciones de vacantes.
- Asignar prioridad comercial con scoring.
- Proponer una solucion MVP inicial.
- Generar un mensaje de contacto reutilizable.
- Exponer agregaciones de mercado para decisiones semanales.

## Principios de arquitectura
- No romper el pipeline de scraping actual.
- Procesamiento 100 por ciento asincrono en segundo plano.
- Persistencia separada entre datos crudos y datos enriquecidos.
- Endpoints orientados a negocio, no solo a visualizacion tecnica.
- Evolucion por fases con entregables pequeños y verificables.

## Estado actual aprovechable
La plataforma ya cuenta con:
- Backend .NET 9 con repositorios, orquestador y workers.
- Frontend Angular 20 con dashboard moderno y paginacion backend.
- Postgres y EF Core.
- Scraping por proveedores (LinkedIn y Upwork).
- Pipeline Docker y pruebas E2E.

## Arquitectura objetivo
Capa 1: Scraping Layer (existente)
- Captura jobs crudos y los guarda sin bloqueo.

Capa 2: Raw Storage (existente)
- Tabla de vacantes sin enrichment obligatorio.

Capa 3: Intelligence Layer (nuevo)
- Worker que toma jobs pendientes.
- Clasificacion de empresa.
- Deteccion de pain point.
- Scoring de oportunidad y urgencia.
- Generacion de solucion sugerida y mensaje de lead.
- Persistencia en tabla de insights.

Capa 4: Business Output (nuevo)
- Endpoint de oportunidades de mercado.
- Endpoint de leads accionables.
- Endpoint de tendencias.

## Modelo de datos propuesto
Entidad principal recomendada: JobInsight ligada por FK a JobOffer.

Campos requeridos:
- Id
- JobId
- MainPainPoint
- PainCategory
- TechStack
- IsDirectClient
- CompanyType
- OpportunityScore (0 a 100)
- UrgencyScore (1 a 10)
- SuggestedSolution
- LeadMessage
- IsProcessed
- ProcessedAt
- EngineVersion
- ConfidenceScore

Indices recomendados:
- (IsProcessed, ProcessedAt)
- (MainPainPoint, ProcessedAt)
- (IsDirectClient, OpportunityScore)
- (JobId) unico

## Pipeline de enrichment
Pipeline por job:
1. Normalize
2. Classify Company
3. Extract Pain Point
4. Score Opportunity
5. Generate MVP Suggestion
6. Generate Lead Message
7. Persist Insight
8. Mark Raw Job as Processed

Reglas iniciales (sin LLM):
- legacy, monolith, migration -> Migration
- scale, high traffic, performance -> Scaling
- integration, api, third-party -> Integration
- manual, spreadsheet, repetitive -> Automation
- cloud, aws, azure, gcp modernization -> Cloud Modernization

## Donde entra Semantic Kernel
Semantic Kernel se aplica en la Capa 3 (Intelligence Layer), dentro del worker asincrono.

Flujo recomendado con SK:
1. Worker toma jobs pendientes.
2. Invoca orquestador `IInsightOrchestrator`.
3. Orquestador ejecuta funciones SK por etapas:
  - ExtractPainPointFunction
  - ScoreOpportunityFunction
  - SuggestSolutionFunction
  - GenerateLeadMessageFunction
4. Resultado estructurado se valida contra schema JSON.
5. Se persiste `JobInsight` y se marca job como procesado.

Implementacion tecnica sugerida:
- Paquete: `Microsoft.SemanticKernel`.
- Servicio: `SemanticKernelEnrichmentService : IAiEnrichmentService`.
- Prompt templates versionados por funcion.
- Configuracion por proveedor de modelo (OpenAI/Azure OpenAI/Gemini via adapter).
- Fallback automatico a `RuleBasedAiEnrichmentService` si falla LLM.

Contrato de salida obligatorio del LLM (JSON estricto):
- mainPainPoint
- painCategory
- techStack
- opportunityScore
- urgencyScore
- suggestedSolution
- leadMessage
- confidenceScore

Regla operacional:
- Si `confidenceScore < 0.65`, no se publica como decision final; pasa a estado `NeedsReview`.

## Alternativa sin Semantic Kernel (LLM directo)
Si no se usa SK, se puede implementar con un cliente LLM directo.

Patron recomendado:
1. `ILlmClient` con metodo `CompleteJsonAsync(prompt, schema)`.
2. `LlmEnrichmentService` que construye prompts por etapa.
3. Validacion JSON estricta con reintento (max 2).
4. Guardrails:
  - limite de tokens
  - timeout por request
  - circuit breaker
  - fallback a reglas

Ventajas de SK vs directo:
- SK: mejor orquestacion de funciones, templates y plugins.
- Directo: menor complejidad inicial, mas rapido de arrancar.

Recomendacion para este proyecto:
- V1: Rule-based + arquitectura lista para SK.
- V1.1: LLM directo en modo sombra (sin impactar decision final).
- V2: Activar Semantic Kernel como orquestador principal.

## Actualizacion implementada (backend-first)

Se implemento una version operativa enfocada en control de costo, trazabilidad y consumo desde interfaz.

### 1) Optimizacion de consumo de tokens SK
Implementado en el flujo hibrido de enrichment:
- Servicio: `HybridAiEnrichmentService`.
- Modo por defecto: shadow mode (reglas siguen como decision final).
- Politicas de ahorro activas:
  - muestreo configurable por porcentaje (`SamplingRatePercent`),
  - salto por descripciones cortas (`MinDescriptionLength`),
  - tope diario de llamadas (`MaxCallsPerDay`),
  - cache por hash de prompt (`PromptHash`) con ventana (`CacheHours`),
  - versionado de prompt (`PromptVersion`).

Configuracion en `Jobs:MarketIntelligence`:
- `UseSemanticKernel`
- `ShadowMode`
- `SamplingRatePercent`
- `MinDescriptionLength`
- `MaxCallsPerDay`
- `CacheHours`
- `PromptVersion`

### 2) Tabla de auditoria de prompts y respuestas
Se agrego persistencia para trazabilidad y control de costo:
- Entidad: `AiPromptLog`.
- Objetivo: registrar prompts, respuestas, latencia, estado, cache y tokens aproximados.

Campos relevantes:
- `JobId`
- `Provider`, `ModelId`, `PromptVersion`
- `PromptHash`, `PromptText`, `ResponseText`
- `CacheHit`, `IsSuccess`, `Status`, `ErrorMessage`
- `PromptTokens`, `CompletionTokens`, `TotalTokens`
- `LatencyMs`, `CreatedAt`

Indice recomendado/implementado:
- `CreatedAt`
- `(Provider, ModelId, CreatedAt)`
- `(PromptHash, CreatedAt)`
- `JobId`

### 3) Endpoints para consultar desde interfaz
Se agrego modulo de observabilidad de IA:
- `GET /api/ai/logs`
  - listado paginado y filtrable por proveedor, estado y rango de fecha.
- `GET /api/ai/summary`
  - resumen de uso en ventana de tiempo (`windowDays`):
  - total llamadas, exitos, fallos, cache hits, tokens totales y latencia promedio.

Uso en frontend sugerido:
- Vista de auditoria con tabla de logs (`/api/ai/logs`).
- Tarjetas KPI de consumo y salud (`/api/ai/summary`).

### 4) Prueba tecnica de conectividad SK
Se agrego endpoint de smoke test:
- `GET /api/semantic/hello?prompt=Hello`

Este endpoint valida que:
- la configuracion SK esta activa,
- el proveedor responde,
- y retorna texto no vacio.

### 5) Estado operativo actual
- Backend compila y pruebas pasan.
- SK puede invocarse en prueba simple.
- Enrichment sigue protegido por fallback a reglas.
- Existe trazabilidad completa de prompts/respuestas para monitoreo y optimizacion.


🧩 2. CREATE AI ENRICHMENT SERVICE WITH SEMANTIC KERNEL (CRITICAL)

Replace the mock implementation with a real AI integration using `Microsoft.SemanticKernel`.

1. Setup: Show the exact `Program.cs` snippet to register Semantic Kernel using `builder.Services.AddKernel().AddOpenAIChatCompletion("gpt-4o-mini", "YOUR_API_KEY");`.
2. Interface: `public interface IAiEnrichmentService { Task<PainPointResult> AnalyzeJobAsync(string jobDescription); }`
3. Implementation: Create `SemanticKernelEnrichmentService`.
4. The Prompt: Inside `AnalyzeJobAsync`, use `kernel.InvokePromptAsync`. The prompt MUST instruct the LLM to act as a B2B Business Analyst. It must read the `jobDescription` and extract the data strictly as a JSON object that matches the `PainPointResult` class. 
5. JSON Parsing: Ensure the LLM response is requested in JSON format (using OpenAI JSON mode if possible) and deserialize the string result into the `PainPointResult` object using `System.Text.Json`.

The System Prompt for the LLM inside the code should look like this:
"You are a B2B SaaS Business Analyst. Read the following job description and identify the core technical pain point the company is facing. 
Return ONLY a valid JSON object with these properties:
- Name: (Short name of the pain point, e.g., 'API Scaling')
- Category: (e.g., 'Migration', 'Scaling', 'Integration', 'Automation')
- Description: (A detailed 2-sentence explanation of the business problem)
- TechStack: (Comma separated technologies mentioned)
- SuggestedSolution: (A 2-sentence description of an MVP or Productized Service to solve it)
- LeadMessage: (A highly converting, professional cold outreach message to offer the solution)"

## Fases de implementacion

### Fase 1 - Base de datos y contratos
Entregable:
- Nueva entidad JobInsight.
- Migracion EF Core.
- Repositorio para insights.
- Contratos base de servicios de enrichment.

Definition of done:
- Migracion aplicada en local.
- CRUD basico de insights validado.

### Fase 2 - Worker de inteligencia
Entregable:
- Nuevo BackgroundService MarketIntelligenceWorker.
- Lectura por lotes de jobs sin procesar.
- Guardado de insight y marcado de procesado.
- Reintentos controlados por job.

Definition of done:
- Worker estable en Docker.
- No bloquea scraping.
- Procesa lote de prueba extremo a extremo.

### Fase 3 - Enrichment rules engine v1
Entregable:
- Implementacion inicial de IAiEnrichmentService basada en reglas.
- Company classifier reutilizando logica existente.
- Opportunity scorer v1.
- Generador de mensaje y solucion sugerida basado en templates.

Definition of done:
- Al menos 5 categorias de dolor detectadas.
- Scoring visible por insight.
- Mensaje de lead generado por cada insight.

### Fase 3.1 - Integracion LLM en modo sombra
Entregable:
- Servicio `LlmEnrichmentService` sin bloquear reglas actuales.
- Persistencia dual: `RuleResult` y `LlmResult` para comparar precision.
- Telemetria de costo, latencia y confidence.

Definition of done:
- El worker guarda resultado LLM sin afectar salida productiva.
- Reporte de comparacion reglas vs LLM disponible.

### Fase 3.2 - Semantic Kernel como orquestador
Entregable:
- `SemanticKernelEnrichmentService` con funciones separadas por etapa.
- Templates versionados y testeables.
- Politica de fallback a reglas cuando falle SK.

Definition of done:
- Pipeline productivo usando SK con fallback estable.
- Validacion de schema JSON al 100 por ciento en respuestas LLM.

### Fase 4 - Endpoints de negocio
Entregable:
- GET /api/market/opportunities
- GET /api/market/leads
- GET /api/market/trends

Respuesta esperada para opportunities:
- painPoint
- companies
- avgScore
- avgUrgency
- suggestedMvp

Definition of done:
- Endpoints paginados y filtrables.
- Pruebas de integracion en backend.

### Fase 5 - UI Market Intelligence
Entregable:
- Nueva vista Angular: opportunities.
- Tarjetas de problema (no tarjetas de vacante).
- Tabla de leads accionables por problema.
- Filtros por score, urgencia y proveedor.
- Modulo de auditoria LLM para consumo y trazabilidad.

Definition of done:
- Flujo completo desde insight hasta lead visible.
- Navegacion consistente con estado de pagina y filtros.
- Vista de auditoria conectada a endpoints de IA con paginacion y filtros.

### Fase 5.1 - UI AI Audit (nuevo)
Entregable:
- Nueva vista Angular: `ai-audit`.
- Tabla de logs consumiendo `GET /api/ai/logs`.
- Filtros por proveedor, estado y rango de fechas.
- KPIs de uso consumiendo `GET /api/ai/summary`.

KPI minimos en UI:
- totalCalls
- successCalls
- failedCalls
- cacheHits
- totalTokens
- averageLatencyMs

Definition of done:
- El usuario puede auditar prompts y respuestas sin entrar a base de datos.
- El usuario puede identificar consumo de tokens y tasa de fallback.
- La pantalla conserva estado (filtros/pagina) al navegar.

### Fase 6 - Calidad y observabilidad
Entregable:
- Logs estructurados por etapa del pipeline.
- Metricas basicas: jobs procesados, errores, tiempo promedio.
- Tests unitarios del rules engine.
- Tests E2E de oportunidades y leads.

Definition of done:
- Reporte de salud del worker.
- Suites de pruebas en verde.

## Endpoints funcionales detallados

### GET /api/market/opportunities
Objetivo:
- Mostrar donde existe mayor demanda repetida.

Filtros sugeridos:
- fromDate
- toDate
- minOpportunityScore
- minUrgencyScore
- source

### GET /api/market/leads
Objetivo:
- Listar empresas concretas para outreach.

Campos sugeridos:
- company
- title
- painPoint
- opportunityScore
- urgencyScore
- suggestedSolution
- leadMessage
- source
- url

### GET /api/market/trends
Objetivo:
- Identificar crecimiento por pain category en ventanas de tiempo.

## Riesgos y mitigaciones
- Riesgo: Falsos positivos en deteccion de pain point.
  - Mitigacion: ConfidenceScore y revision de reglas por version.

- Riesgo: Worker saturando base de datos.
  - Mitigacion: Lotes pequenos, delay configurable, indices.

- Riesgo: Duplicidad de insights.
  - Mitigacion: Unicidad por JobId.

- Riesgo: Calidad de mensaje comercial baja.
  - Mitigacion: Plantillas por categoria y pruebas A/B internas.

## Backlog tecnico inicial
Backend:
1. Crear entidad JobInsight + configuracion EF.
2. Generar migracion y aplicar.
3. Crear IAiEnrichmentService.
4. Crear RuleBasedAiEnrichmentService.
5. Crear ILlmClient y LlmEnrichmentService (modo sombra).
6. Crear SemanticKernelEnrichmentService.
7. Crear OpportunityScorerService.
8. Crear MarketIntelligenceWorker.
9. Crear MarketIntelligenceController.
10. Crear DTOs de oportunidades, leads y trends.

Frontend:
1. Crear modelos market.
2. Crear MarketService.
3. Crear pagina opportunities standalone.
4. Crear vista de leads por pain point.
5. Integrar filtros y paginacion.
6. Agregar pruebas E2E.
7. Crear modelos de auditoria IA (`AiPromptLog`, `AiUsageSummary`).
8. Crear `AiAuditService` para `/api/ai/logs` y `/api/ai/summary`.
9. Crear pagina `ai-audit` con tabla, filtros y KPIs.
10. Agregar pruebas E2E de flujo de auditoria IA.

## Criterios de aceptacion del feature
- El scraping no se degrada por enrichment.
- Cada job nuevo termina con insight o con estado de error recuperable.
- Oportunidades muestran agrupacion real por problema.
- Leads muestran mensaje listo para contacto.
- Dashboard de oportunidades usable para priorizacion semanal.

## Prompt recomendado para iniciar con Copilot
Act as a senior .NET 9 architect working in an existing Clean Architecture project.

Goal:
Implement a Market Intelligence Engine on top of existing raw job scraping data.

Constraints:
- Do not modify current scraping flow performance.
- All intelligence processing must run in a BackgroundService.
- Persist enrichment in a new JobInsight table linked to JobOffer by JobId.
- Start with rule-based enrichment, no external AI API yet.

Tasks for first iteration:
1. Add JobInsight entity and EF configuration.
2. Create migration and repository methods for pending jobs and persisted insights.
3. Add IAiEnrichmentService and RuleBasedAiEnrichmentService.
4. Add OpportunityScorerService.
5. Add MarketIntelligenceWorker to process unprocessed jobs in batches.
6. Add GET /api/market/opportunities endpoint returning grouped opportunities.

Deliverables:
- Compilable code.
- Basic unit tests for rules and scoring.
- Seed-safe defaults and logs for each processing stage.

## Prompt Copilot para Semantic Kernel
Act as a senior .NET 9 engineer. Integrate Semantic Kernel into an existing Clean Architecture backend.

Requirements:
- Add `SemanticKernelEnrichmentService` implementing `IAiEnrichmentService`.
- Use separate SK functions for:
  1) pain extraction
  2) scoring
  3) MVP suggestion
  4) lead message
- Each function must return strict JSON and be validated before persistence.
- Add fallback to `RuleBasedAiEnrichmentService` when SK call fails or confidence is low.
- Run only inside the BackgroundService, never in scraping request path.
- Add structured logs for model, latency, token usage, and fallback reason.

Output expected:
- Service interfaces and implementations.
- Prompt templates for each SK function.
- DI registration in Program.cs.
- Unit tests for JSON parsing and fallback policy.

## Roadmap de evolucion
V1:
- Rules engine + endpoints base + UI opportunities.

V1.1:
- Ajuste de reglas con feedback real.

V2:
- Integracion opcional con LLM economico para mejorar precision de pain y mensaje.

V3:
- Motor de recomendacion de MVP y priorizacion automatica por ROI estimado.

¿Cómo funciona esto en la realidad (El Flujo)?
Tu Worker toma 50 vacantes de la base de datos.

Por cada vacante, llama a _aiService.AnalyzeJobAsync(job.Description).

Semantic Kernel empaqueta la descripción junto con tu instrucción (el prompt) y llama a OpenAI (te sugiero usar gpt-4o-mini por velocidad y costo).

El LLM razona: "Aquí piden a alguien que sepa WCF, SOAP y .NET Framework 4.5. ¡Ah! El problema es migración legacy".

El LLM devuelve un texto JSON puro.

C# deserializa ese JSON en tu objeto PainPointResult.

El Worker guarda ese resultado brillante directamente en Postgres.

Consejo para optimizar costos de AltioraTech:
No le pases descripciones de trabajos de "Junior Frontend" o vacantes genéricas a Semantic Kernel (te costará tokens por nada). En tu Worker, pon una regla en C# antes de llamar al Kernel:
if (job.Salary < 3000 || job.Description.Length < 100) continue;
Pasa por el LLM solo la "crema" de las vacantes.
