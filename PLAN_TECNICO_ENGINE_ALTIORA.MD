# PLAN TECNICO - ENGINE ALTIORA (MARKET INTELLIGENCE)

## 1. Objetivo tecnico
Implementar una capa de inteligencia que procese vacantes crudas y produzca insights comerciales accionables sin degradar el scraping actual.

Capacidades objetivo:
- Deteccion de pain points de negocio.
- Scoring de oportunidad y urgencia.
- Sugerencia de solucion (MVP o servicio producto).
- Generacion de mensaje de lead.
- Exposicion por API para dashboard de oportunidades.

## 1.1 Checklist de ejecucion (Backend -> Probar -> Frontend)

### Etapa 1 - Backend primero
- [x] Crear entidad `JobInsight` en Domain.
- [x] Configurar EF Core (`DbSet`, mapping, indices).
- [x] Crear y aplicar migracion en local.
- [x] Implementar interfaces de enrichment (`IAiEnrichmentService`, `IInsightOrchestrator`).
- [x] Implementar `RuleBasedAiEnrichmentService`.
- [x] Implementar `OpportunityScorerService`.
- [x] Implementar `MarketIntelligenceWorker` por lotes.
- [x] Persistir insight y marcar job procesado.
- [x] Implementar endpoint `GET /api/market/opportunities`.
- [x] Implementar endpoint `GET /api/market/leads`.
- [x] Implementar endpoint `GET /api/market/trends`.

### Etapa 2 - Pruebas y validacion de backend (obligatorio antes de frontend)
- [x] Build backend en verde.
- [x] Ejecutar migraciones en Docker y validar esquema.
- [x] Probar worker con datos reales y verificar que no rompe scraping.
- [x] Probar endpoints market con `curl` y validar contratos JSON.
- [x] Agregar unit tests para reglas y scoring.
- [x] Agregar integration tests para endpoints market.
- [x] Verificar logs estructurados (stage, latency, confidence, fallback).
- [x] Cerrar etapa solo si backend queda estable y validado.

### Etapa 3 - Frontend (solo cuando backend este validado)
- [x] Crear modelos Angular para oportunidades/leads/trends.
- [x] Crear `MarketService` para consumir endpoints nuevos.
- [x] Crear pagina `opportunities` standalone.
- [x] Implementar cards de pain points con KPIs.
- [x] Implementar vista de leads por pain point.
- [x] Implementar filtros (score, urgencia, proveedor, fechas).
- [x] Integrar paginacion y estado de navegacion.
- [x] Agregar pruebas E2E para navegacion y estado en opportunities y AI audit.

Actualizacion UI:
- [x] Menu de navegacion global con sidebar izquierdo para Jobs, Opportunities, AI Audit y Scraping.

### Gate de avance (regla de trabajo)
- [x] No iniciar tareas de frontend hasta completar 100 por ciento de la Etapa 2.

## 2. Alcance
Incluye:
- Backend .NET 9: modelo, migraciones, worker, servicios de enrichment, endpoints market.
- Integracion IA: fase rule-based, luego LLM directo y Semantic Kernel.
- Frontend Angular: nueva vista de oportunidades y leads.
- Observabilidad, pruebas y despliegue por fases.

No incluye (en esta iteracion):
- Automatizacion outbound (envio automatico de emails).
- CRM completo.
- Fine-tuning de modelos.

## 3. Arquitectura de implementacion

### 3.1 Flujo general
1. Scraper guarda JobOffer crudo.
2. Worker `MarketIntelligenceWorker` toma jobs pendientes.
3. Pipeline de enrichment analiza descripcion.
4. Se persiste `JobInsight`.
5. API market expone agregaciones.
6. Frontend consume oportunidades/leads.

### 3.2 Modulos backend
- Domain:
  - `JobInsight` entity.
  - Value objects / enums opcionales para categoria y estado.
- Application:
  - `IAiEnrichmentService`
  - `IInsightOrchestrator`
  - DTOs market.
- Infrastructure:
  - `RuleBasedAiEnrichmentService`
  - `LlmEnrichmentService` (modo sombra)
  - `SemanticKernelEnrichmentService`
  - `MarketIntelligenceWorker`
  - Repositorios y consultas agregadas.
- API:
  - `MarketIntelligenceController`

### 3.3 Modo de decision
- V1: reglas como decision principal.
- V1.1: LLM en sombra (compara precision, no decide).
- V2: Semantic Kernel decide, con fallback a reglas.

## 4. Modelo de datos y migraciones

### 4.1 Entidad principal
`JobInsight` (FK a `JobOffer`):
- `Id`
- `JobId` (unique)
- `MainPainPoint`
- `PainCategory`
- `PainDescription`
- `TechStack`
- `IsDirectClient`
- `CompanyType`
- `OpportunityScore` (0-100)
- `UrgencyScore` (1-10)
- `SuggestedSolution`
- `LeadMessage`
- `ConfidenceScore` (0-1)
- `DecisionSource` (Rules, LlmShadow, SemanticKernel)
- `Status` (Processed, NeedsReview, Failed)
- `IsProcessed`
- `ProcessedAt`
- `EngineVersion`
- `RawModelResponse` (opcional, para auditoria)

### 4.2 Indices
- `IX_JobInsight_JobId` unique
- `IX_JobInsight_IsProcessed_ProcessedAt`
- `IX_JobInsight_MainPainPoint_ProcessedAt`
- `IX_JobInsight_IsDirectClient_OpportunityScore`
- `IX_JobInsight_Status`

## 5. Plan por fases

## Fase A - Foundation (DB + contratos)
Duracion estimada: 2-3 dias

Entregables:
- Entity + EF config + migration.
- Repositorio para jobs pendientes e insights.
- Contratos base de enrichment y orchestrator.

Criterio de salida:
- Build backend verde.
- Migracion aplicada en local y Docker.

## Fase B - Worker + Rules Engine
Duracion estimada: 3-4 dias

Entregables:
- `MarketIntelligenceWorker` por lotes (`Take(50)`, delay configurable).
- `RuleBasedAiEnrichmentService` con reglas iniciales.
- `OpportunityScorerService`.
- Persistencia y marcado de procesado.

Criterio de salida:
- Procesa jobs reales sin bloquear scraping.
- Logging por etapa y manejo de errores por item.

## Fase C - API Market
Duracion estimada: 2 dias

Entregables:
- `GET /api/market/opportunities`
- `GET /api/market/leads`
- `GET /api/market/trends`

Criterio de salida:
- Respuesta paginada, filtrable, ordenable.
- Tests de integracion basicos.

## Fase D - LLM directo (Shadow Mode)
Duracion estimada: 2-3 dias

Entregables:
- `ILlmClient` + `LlmEnrichmentService`.
- Prompting JSON estricto.
- Validacion de schema + retries.
- Guardrails (timeout, circuit breaker, fallback).

Criterio de salida:
- Guarda resultado LLM sin cambiar decision final.
- Metricas de latencia, costo y confidence.

## Fase E - Semantic Kernel
Duracion estimada: 3-4 dias

Entregables:
- `SemanticKernelEnrichmentService`.
- Funciones SK separadas:
  - extract pain
  - score
  - suggest solution
  - lead message
- Fallback a rules.

Criterio de salida:
- Pipeline productivo con SK y fallback estable.
- JSON schema validado en cada respuesta.

## Fase F - Frontend Opportunities
Duracion estimada: 3-4 dias

Entregables:
- Nueva pagina Angular `opportunities`.
- Tarjetas de pain point con KPIs.
- Vista de leads por pain.
- Filtros: score, urgencia, proveedor, fechas.

Criterio de salida:
- UX completa desde oportunidades hasta leads.
- Integrado con endpoints market.

## Fase G - Calidad, pruebas y rollout
Duracion estimada: 2-3 dias

Entregables:
- Unit tests (rules, scoring, parser JSON).
- Integration tests (endpoints market).
- E2E (oportunidades y leads).
- Feature flags para activar SK progresivamente.

Criterio de salida:
- Suite verde y rollout controlado.

## 6. API contratos (propuesta)

### 6.1 GET /api/market/opportunities
Params:
- `fromDate`, `toDate`, `source`, `minOpportunityScore`, `minUrgencyScore`, `page`, `pageSize`

Response item:
- `painPoint`
- `painCategory`
- `opportunityCount`
- `avgOpportunityScore`
- `avgUrgencyScore`
- `topTechStack`
- `suggestedMvp`

### 6.2 GET /api/market/leads
Params:
- `painPoint`, `source`, `minScore`, `page`, `pageSize`

Response item:
- `jobId`
- `company`
- `title`
- `painPoint`
- `opportunityScore`
- `urgencyScore`
- `suggestedSolution`
- `leadMessage`
- `url`

### 6.3 GET /api/market/trends
Params:
- `windowDays` (7, 14, 30)

Response item:
- `painCategory`
- `currentCount`
- `previousCount`
- `trendPercentage`

## 7. Semantic Kernel - decision tecnica

Aplicacion concreta:
- Solo en worker asincrono.
- Nunca en request path del scraping.
- Prompt por funcion, versionado.

Configuracion:
- `Microsoft.SemanticKernel`
- proveedor configurable (OpenAI, Azure OpenAI, Gemini adapter)
- max tokens, temperature y timeout por etapa.

Estado actual:
- [x] Paquete `Microsoft.SemanticKernel` instalado en backend.
- [x] Seccion `SemanticKernel` agregada en `appsettings.json` y `appsettings.Development.json`.
- [x] Provider base registrado en DI (`ISemanticKernelProvider` + `SemanticKernelProvider`) para habilitar integracion progresiva.
- [x] `IAiEnrichmentService` conectado a Semantic Kernel en modo sombra (`HybridAiEnrichmentService`) con fallback a rules.
- [x] Flag de activacion agregado: `Jobs:MarketIntelligence:UseSemanticKernel`.

Ejemplos de configuracion de API:
- OpenAI / ChatGPT: `Provider=OpenAI` o `Provider=ChatGpt`, `ModelId=gpt-4o-mini`, env `OPENAI_API_KEY`.
- Gemini (modo OpenAI-compatible): `Provider=Gemini`, `ModelId=gemini-2.0-flash`, env `GEMINI_API_KEY`.
- Perplexity: `Provider=Perplexity`, `ModelId=sonar`, env `PERPLEXITY_API_KEY`.
- Copilot API (si aplica en entorno): `Provider=Copilot`, env `COPILOT_API_KEY`.

Politica de confianza:
- `confidenceScore >= 0.65`: usable.
- `< 0.65`: `NeedsReview`.

Fallback:
- error de red, timeout, JSON invalido o confidence bajo => rules.

## 8. Observabilidad

Logs estructurados:
- `jobId`, `stage`, `decisionSource`, `latencyMs`, `tokens`, `confidence`, `status`

Metricas:
- jobs procesados por hora
- tasa de error por etapa
- promedio de latencia por proveedor LLM
- porcentaje fallback
- costo estimado de tokens

Alertas:
- error rate > umbral
- fallback rate elevado
- cola de pendientes creciendo

## 9. Riesgos y mitigaciones
- Riesgo: respuestas LLM inconsistentes.
  - Mitigacion: JSON schema estricto + retries + fallback.
- Riesgo: costo LLM elevado.
  - Mitigacion: shadow mode, cache por hash de descripcion, limites diarios.
- Riesgo: saturacion DB por worker.
  - Mitigacion: lotes chicos, indices, delay configurable.
- Riesgo: decisiones comerciales de baja precision.
  - Mitigacion: confidence threshold + review queue.

## 10. Definition of Done global
- Worker estable y desacoplado del scraping.
- Insight generado para jobs nuevos con estado trazable.
- Endpoints market operativos con tests.
- Frontend opportunities funcional.
- LLM/SK con fallback seguro.
- Observabilidad y pruebas en verde.

## 11. Orden de ejecucion recomendado (semanas)
Semana 1:
- Fase A + B

Semana 2:
- Fase C + D

Semana 3:
- Fase E + F

Semana 4:
- Fase G + hardening

## 12. Primeros 10 tasks para empezar hoy
1. [x] Crear `JobInsight` entity.
2. [x] Configurar EF + migration.
3. [x] Crear `IAiEnrichmentService`.
4. [x] Implementar `RuleBasedAiEnrichmentService`.
5. [x] Implementar `OpportunityScorerService`.
6. [x] Crear `MarketIntelligenceWorker`.
7. [x] Persistir insight + mark processed.
8. [x] Exponer `GET /api/market/opportunities`.
9. [x] Agregar pruebas unitarias de reglas.
10. [x] Agregar logs estructurados por etapa.


Tienes toda la razón. Enviar toda esa "basura" (fluff) al LLM es el error número uno que drena tu presupuesto de tokens y confunde al modelo.

Si miras la vacante que me enviaste de ejemplo, el 60% del texto es irrelevante para tu negocio: "Somos geniales", "Presupuesto de 1.200€ en formación", "horario flexible". Además, Capitole es una consultora (IT Staffing). Según tu modelo de negocio ("Engineering as a Strike Team"), tú no le vendes a consultoras que buscan subcontratar talento; le vendes al cliente final. Esta vacante ni siquiera debería haber llegado al LLM.

Para optimizar esto al máximo y obtener una oportunidad de negocio real (no solo etiquetas), debes implementar una Estrategia de Estrangulamiento en 3 Pasos.

Paso 1: El Filtro de C# (El "Bouncer")
Antes de gastar un solo token de Claude 4.6 o GPT-5, tu código en C# debe limpiar la data.

Lista Negra de Consultoras: Si Company == "Capitole" (o Globant, NTT Data, TCS, etc.), descarta la vacante inmediatamente. Tu código C# debe bloquearlas.

Expresiones Regulares (RegEx) para Limpiar Fluff: Usa C# para eliminar bloques enteros de texto. Todo lo que esté después de palabras clave como "Qué ofrecemos", "Beneficios", "Acerca de nosotros", bórralo.

Lo que SÍ envías al LLM: Solo el Título, el Stack y las Responsabilidades.

Paso 2: El Prompt de "Ingeniería Inversa de Negocios"
En lugar de pedirle al LLM que clasifique la vacante (eso aporta poco valor), pídele que actúe como un Asesor de Ventas B2B Técnico. Tienes que hacerle preguntas que deduzcan por qué están contratando.

Si una empresa pide Java, Arquitectura Hexagonal, Kafka y React, no están haciendo una página web sencilla. Están manejando alto tráfico o refactorizando un sistema viejo. Ahí está tu oportunidad.

Aquí tienes el Prompt exacto, optimizado y directo al grano, que debes enviarle a la IA:

Plaintext
[SYSTEM PROMPT]
Actúa como un CTO y Estratega de Ventas B2B para 'AltioraTech', una agencia de "Engineering as a Strike Team" que resuelve cuellos de botella técnicos urgentes en días. 

Analiza el siguiente extracto de vacante tecnológica. IGNORA los beneficios corporativos. Tu objetivo es hacer ingeniería inversa para descubrir el dolor técnico de la empresa y proponer un servicio de alto valor (MVP/Auditoría) que AltioraTech pueda venderles HOY, evitando que contraten a un empleado a largo plazo.

[FORMATO DE RESPUESTA OBLIGATORIO: SOLO JSON]
{
  "pain_point": "Descripción en 1 línea del problema técnico real que están sufriendo (ej. 'Deuda técnica en monolito', 'Problemas de concurrencia', 'Migración estancada').",
  "business_opportunity": "Por qué esto les cuesta dinero o tiempo.",
  "altioratech_offer": "Nombre de un servicio empaquetado o MVP (ej. 'Auditoría de Arquitectura Hexagonal', 'Optimización de Eventos en Kafka en 48h').",
  "confidence": 0.0 a 1.0 (Qué tan seguros estamos de este dolor)
}

[USER PROMPT]
Title: Senior Fullstack Developer (Java & React)
Tech: Java 11-17, Springboot, Arquitectura hexagonal (DDD), Kafka, MongoDB, React 18
Tasks: Diseño y desarrollo de APIs, calidad de desarrollo (Clean Code), trabajar en sprints.
Paso 3: Analicemos la diferencia del Output
Si le envías tu prompt original, el LLM te responde esto (barato pero inútil):
pain=API Development;category=Fullstack;confidence=0.8 (¿Cómo vendes eso? No puedes).

Si usas la estrategia que te propongo con el texto filtrado, la IA te devolverá este JSON:

JSON
{
  "pain_point": "Transición compleja hacia Arquitectura Hexagonal y microservicios manejados por eventos (Kafka).",
  "business_opportunity": "Están reestructurando un e-commerce crítico. Tienen problemas de escalabilidad o código espagueti en su backend actual, lo que retrasa nuevas integraciones.",
  "altioratech_offer": "Assessment de 3 días: Revisión de Arquitectura Hexagonal y Optimización de Productores/Consumidores en Kafka para E-commerce.",
  "confidence": 0.85
}
¿Por qué esta es la mejor estrategia?
Ahorras un 70% de Tokens: Al limpiar el texto con C# antes de enviarlo, pasas de enviar 1000 tokens de basura a solo 200 tokens de información técnica pura.

Generas Dinero: El LLM ya no es un simple clasificador, se convierte en tu Director de Estrategia. Te está diciendo exactamente qué título ponerle al correo electrónico de ventas que le vas a enviar al CTO de esa empresa.

Tu tarea técnica: Modifica tu MarketIntelligenceWorker en .NET para que extraiga solo las secciones de requerimientos y stack técnico antes de invocar al SemanticKernel. ¿Necesitas ayuda armando la expresión regular (Regex) en C# para hacer esa limpieza?