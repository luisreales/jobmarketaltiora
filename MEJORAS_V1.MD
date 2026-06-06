```text
Actúa como un Arquitecto Senior en .NET 9, experto en sistemas de Market Intelligence B2B, Semantic Clustering y Microsoft Semantic Kernel.

NO hagas refactors innecesarios.
NO optimices frontend.
NO agregues infraestructura enterprise.
NO hagas mejoras cosméticas.
NO hagas overengineering.
NO priorices tests masivos todavía.

Tu objetivo es convertir el sistema actual en un motor REAL de descubrimiento de oportunidades comerciales B2B para AltioraTech.

# CONTEXTO DEL SISTEMA

El pipeline actual YA funciona:

SCRAPER ✅
- LinkedIn (Playwright)
- Upwork
- Indeed

RAW DATA ✅
- JobOffer persisted en PostgreSQL

WORKER 1 — INSIGHTS + DATA QUALITY ✅
- RuleBasedAiEnrichmentService
- TechCanonicalizer
- IndustryClassifier
- LeadScoringService

WORKER 2 — CLUSTER ENGINE ✅
- ClusterEngine.RebuildClustersAsync()
- BlueOceanScore
- DecisionEngine
- ProductGeneratorService

API + DASHBOARD ✅
- /opportunities
- /products

PROBLEMA:
El sistema todavía NO genera inteligencia comercial real.
Actualmente:
- los clusters existen
- los scores existen
- los productos existen

PERO:
- el contenido comercial sigue siendo genérico
- el LLM no está generando síntesis reales
- el clustering todavía es rígido
- el sistema no entiende problemas equivalentes semánticamente
- no existe ROI real
- no existe TAM real
- no existe priorización comercial fuerte

# TU OBJETIVO

Implementar SOLO estas 4 áreas críticas:

1. CLEANER PIPELINE
2. LLM SYNTHESIS
3. SEMANTIC CLUSTERING
4. OPPORTUNITY ENGINE V2

TODO lo demás queda fuera de alcance.

---------------------------------------------------
# FASE 1 — CLEANER PIPELINE (PRIORIDAD MÁXIMA)
---------------------------------------------------

Implementa un JobDescriptionCleanerService.

OBJETIVO:
Reducir basura antes de enviar datos al LLM.

PROBLEMA ACTUAL:
Las vacantes contienen:
- beneficios
- marketing corporativo
- cultura
- fluff
- texto irrelevante

Eso:
- aumenta tokens
- empeora resultados
- genera síntesis débiles

REQUERIMIENTOS:

1. Crear:
- IJobDescriptionCleanerService
- JobDescriptionCleanerService

2. Funciones:
- RemoveCorporateFluff()
- ExtractTechnicalSections()
- NormalizeWhitespace()
- TrimToTokenBudget()

3. Implementar blacklist configurable de consultoras:
- Globant
- TCS
- NTT Data
- Accenture
- EPAM
- Cognizant
- Deloitte
- Capgemini
- etc.

4. Si CompanyType == Consulting:
- reducir LeadScore
- o descartar completamente según config

5. Remover texto posterior a patrones:
- "About us"
- "Benefits"
- "Why join"
- "Perks"
- "What we offer"
- "Quiénes somos"
- etc.

6. Mantener SOLO:
- título
- responsabilidades
- stack
- arquitectura
- integraciones
- señales técnicas

7. Limitar texto final a máximo 4000 caracteres.

8. Integrar el cleaner ANTES de Semantic Kernel.

OBJETIVO FINAL:
Reducir tokens y mejorar calidad de síntesis.

---------------------------------------------------
# FASE 2 — LLM SYNTHESIS (PRIORIDAD ALTA)
---------------------------------------------------

Implementar ClusterSynthesisService productivo.

ACTUALMENTE:
- Semantic Kernel está conectado
- pero no existen llamadas productivas
- LlmStatus sigue "pending"

IMPLEMENTAR:

1. IClusterSynthesisService
2. ClusterSynthesisService
3. integración en ClusteringHostedService Stage 3

LÓGICA:

Buscar hasta 5 clusters donde:
- IsActionable == true
- LlmStatus == "pending"

Por cada cluster:
- cargar hasta 5 jobs del cluster
- usar texto limpiado por JobDescriptionCleanerService
- concatenar contexto
- llamar Semantic Kernel

PROMPT DEL LLM:

[SYSTEM]
Actúa como un Director de Estrategia B2B para una agencia de ingeniería de élite.

Analiza este grupo de vacantes tecnológicas.

Tu objetivo es descubrir:
- el cuello de botella técnico real
- el impacto empresarial
- el MVP o auditoría que puede venderse inmediatamente
- una estrategia comercial clara

Devuelve ÚNICAMENTE JSON válido.

Formato:
{
  "pain": "",
  "businessOpportunity": "",
  "mvp": "",
  "leadMessage": "",
  "confidence": 0.0
}

REGLAS:
- NO responder genérico
- NO repetir buzzwords
- inferir problemas reales
- pensar como CTO + consultor estratégico

GUARDAR:
- SynthesizedPain
- SynthesizedMvp
- SynthesizedLeadMessage
- LlmStatus

REGISTRAR:
- AiPromptLog
- ClusterId
- prompt
- response
- latency
- tokens

SI FALLA:
- LlmStatus = "failed"
- continuar con siguiente cluster

TODO asíncrono.

---------------------------------------------------
# FASE 3 — SEMANTIC CLUSTERING
---------------------------------------------------

PROBLEMA ACTUAL:
El sistema agrupa por:
- PainCategory
- TechTop3
- Industry

Eso es demasiado rígido.

IMPLEMENTAR:

1. SemanticClusterEngine

OBJETIVO:
Detectar problemas equivalentes aunque cambie wording.

Ejemplos:
- "Legacy modernization"
- "Monolith migration"
- "System modernization"

=> mismo problema real.

REQUERIMIENTOS:

1. Crear embeddings para:
- MainPainPoint
- PainDescription
- SuggestedSolution

2. Implementar similitud coseno.

3. Crear:
- ClusterSimilarityService

4. Agrupar usando threshold configurable:
- similarity >= 0.82

5. Mantener ClusterKey determinístico.

6. NO romper clusters existentes.

7. Permitir fallback a clustering actual.

OBJETIVO:
Clusters basados en problema real, no texto exacto.

---------------------------------------------------
# FASE 4 — OPPORTUNITY ENGINE V2
---------------------------------------------------

OBJETIVO:
Transformar clusters en oportunidades comerciales reales.

IMPLEMENTAR:

1. OpportunityEngineV2

CALCULAR:

- Estimated TAM
- Buying Intent
- Enterprise Complexity
- Hiring Velocity
- Delivery Feasibility
- Sales Friction
- Revenue Potential

GENERAR:

- ROI score
- Estimated deal size
- Recommended service model
- Priority rank
- Recommended action

FÓRMULA BASE:

PriorityScore =
(
BlueOceanScore * 0.35 +
BuyingIntent * 0.25 +
Urgency * 0.20 +
DirectClientRatio * 0.10 +
GrowthRate * 0.10
)

GENERAR:
- RecommendedAction
- SalesAngle
- WhyNow
- EstimatedCloseProbability

OBJETIVO:
Que el sistema diga:
"ATACA ESTE MERCADO ESTA SEMANA"

---------------------------------------------------
# RESTRICCIONES IMPORTANTES
---------------------------------------------------

NO HACER:
- frontend redesign
- Angular refactors
- bundle optimization
- Kubernetes
- observability enterprise
- full E2E massive testing
- overengineering
- microservicios innecesarios
- CQRS extra
- clean architecture obsesiva

NO TOCAR:
- scraping pipeline estable
- JobOffer schema salvo necesidad crítica
- ProductGenerator existente salvo integración mínima

---------------------------------------------------
# PRIORIDAD DE EJECUCIÓN
---------------------------------------------------

1. Cleaner pipeline
2. LLM synthesis
3. Semantic clustering
4. Opportunity engine

---------------------------------------------------
# EXPECTATIVA
---------------------------------------------------

Quiero código REAL de producción:
- .NET 9
- EF Core
- async/await
- ILogger
- Dependency Injection
- System.Text.Json
- Semantic Kernel
- manejo individual de errores
- código mantenible
- sin pseudo código

Entrega:
- archivos completos
- clases completas
- interfaces
- integración DI
- migraciones necesarias
- cambios en workers
- ejemplos de prompts
- ejemplos de entidades si aplica
- explicación técnica breve y concreta
```

Actúa como un arquitecto senior de AI systems especializado en embeddings, semantic clustering y market intelligence.

CONTEXTO:
Actualmente mi sistema agrupa vacantes usando agrupaciones demasiado literales.

PROBLEMA:
“Legacy modernization”
“DDD migration”
“Microservices transition”
“Kafka scaling”

terminan separados aunque representan el mismo problema empresarial.

OBJETIVO:
Diseñar un Semantic Cluster Engine real.

QUIERO:
- agrupar problemas equivalentes
- detectar patrones de mercado
- identificar dolores repetidos
- detectar oportunidades emergentes

NO quiero un sistema basado solo en texto exacto.

QUIERO:
- embeddings
- semantic similarity
- weighted clustering
- tech stack normalization
- signal scoring

STACK:
- .NET 9
- PostgreSQL
- Semantic Kernel
- workers
- EF Core

ENTREGABLES:
1. Arquitectura recomendada.
2. Estrategia de embeddings.
3. Cómo almacenar embeddings.
4. Cómo calcular similitud.
5. Algoritmo recomendado.
6. Cómo evitar clusters basura.
7. Cómo detectar clusters emergentes.
8. Cómo combinar:
   - tech stack
   - urgency
   - industry
   - hiring volume
9. Cómo detectar “market waves”.
10. Cómo transformar esto en una ventaja competitiva real.
Actúa como un CTO fundador de una startup AI B2B.

CONTEXTO:
Estoy construyendo AltioraTech:
un sistema que transforma vacantes tecnológicas en oportunidades comerciales accionables.

IMPORTANTE:
NO quiero overengineering.
NO quiero perder meses construyendo features irrelevantes.

QUIERO:
- foco absoluto en monetización
- foco absoluto en señal comercial
- foco absoluto en detectar oportunidades vendibles

Tu trabajo es actuar como advisor técnico y estratégico.

Cada recomendación debe responder:
1. ¿Esto genera dinero?
2. ¿Esto mejora la inteligencia comercial?
3. ¿Esto mejora la detección de oportunidades?
4. ¿Esto aumenta la calidad del insight?
5. ¿Esto mejora la ventaja competitiva?

Si una tarea NO impacta monetización o inteligencia comercial, debes decir explícitamente que no es prioridad.

QUIERO:
- roadmap realista
- prioridades correctas
- qué NO construir todavía
- qué sí construir primero
- cómo validar rápido
- cómo evitar desperdiciar tiempo

El objetivo final NO es scraping.
El objetivo final es:
detectar oportunidades tecnológicas B2B antes que el mercado.

Actúa como un CTO, estratega B2B y experto en sistemas de inteligencia comercial basados en IA.

CONTEXTO:
Estoy construyendo un sistema llamado AltioraTech.
El sistema agrupa vacantes tecnológicas en clusters de problemas de mercado.

Ya tengo:
- scraping
- clustering
- scoring
- MarketClusters
- Semantic Kernel conectado

Pero todavía NO tengo síntesis comercial inteligente.

OBJETIVO:
Necesito diseñar el `ClusterSynthesisService`.

El objetivo NO es clasificar vacantes.
El objetivo es:
hacer ingeniería inversa del problema empresarial real detrás de múltiples vacantes similares.

INPUT:
Un cluster contiene:
- Industry
- TechTop3
- PainCategory
- JobCount
- DirectClientRatio
- sample job descriptions

OUTPUT ESPERADO:
1. El verdadero cuello de botella técnico.
2. El impacto de negocio.
3. Un MVP o auditoría vendible.
4. Un mensaje de outreach para CTOs.

QUIERO:
- prompts optimizados para Claude/GPT-5
- JSON schema estricto
- estrategia anti-hallucination
- reducción de tokens
- síntesis altamente accionable
- lenguaje comercial fuerte
- diferenciación entre:
  - consulting
  - MVP product
  - quick win
  - enterprise modernization

ENTREGABLES:
1. Arquitectura del ClusterSynthesisService.
2. Estrategia de prompting.
3. Prompt final optimizado.
4. Ejemplos reales de output.
5. Cómo detectar oportunidades repetibles.
6. Cómo inferir problemas empresariales reales desde stacks técnicos.
7. Cómo detectar “buying intent”.
8. Cómo evitar outputs genéricos.
9. Cómo convertir clusters en ofertas vendibles.
10. Cómo evolucionar esto hacia un AI sales strategist.


Actúa como un Principal Engineer especializado en sistemas de Market Intelligence B2B, NLP pipelines y optimización de costos LLM.

CONTEXTO:
Estoy construyendo un engine llamado AltioraTech que analiza vacantes tecnológicas para detectar oportunidades comerciales B2B.

PROBLEMA ACTUAL:
Estoy enviando descripciones completas de vacantes al LLM y eso está destruyendo:
- precisión semántica
- clustering
- costos de tokens
- calidad de síntesis

Necesito construir un `JobDescriptionCleanerService` en .NET 9 que transforme una descripción de vacante cruda en una señal técnica compacta y útil para IA.

OBJETIVO:
El servicio debe:
1. Eliminar texto corporativo irrelevante.
2. Detectar y excluir consultoras/staffing companies.
3. Extraer solo:
   - tech stack
   - arquitectura
   - responsabilidades técnicas
   - señales de migración/escalabilidad/deuda técnica
4. Reducir el texto final a máximo 4000 caracteres.
5. Preparar texto optimizado para Semantic Kernel.

IMPORTANTE:
NO quiero un parser genérico.
Quiero un sistema orientado a detectar:
- modernization
- migration
- scalability
- event-driven systems
- cloud adoption
- integration pain
- DevOps bottlenecks
- AI adoption
- legacy systems

REQUERIMIENTOS:
- Clean Architecture
- .NET 9
- Regex optimization
- bajo consumo de memoria
- asincrónico si aplica
- extensible
- production-ready

ENTREGABLES:
1. Arquitectura recomendada.
2. Clase `JobDescriptionCleanerService`.
3. Regex recomendadas.
4. Estrategia de limpieza por etapas.
5. Lista negra configurable de consultoras.
6. Estrategia de compresión semántica.
7. Ejemplos input/output reales.
8. Métricas que debo registrar.
9. Cómo reducir costos de tokens al máximo.
10. Cómo preparar el output ideal para clustering semántico.