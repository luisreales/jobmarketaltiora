Actúa como un Lead Engineer de Validación para AltioraTech Engine V2.

NO desarrolles nuevas features grandes.

Tu objetivo es validar que el pipeline completo funciona end-to-end con datos reales.

# OBJETIVO

Verificar que:

JobOffers
→ JobInsights
→ MarketClusters
→ Semantic Groups
→ Opportunity Intelligence
→ ProductSuggestions
→ LLM Synthesis

funcionan correctamente con datos reales.

# TAREAS

## 1. Generar dataset real

Ejecutar scraping manual para obtener:

* mínimo 300 JobOffers nuevos
* LinkedIn
* Indeed
* Upwork

Verificar:

* companies reales
* tech stacks variados
* industrias distintas

## 2. Validar Worker 1

Verificar:

* cleaner pipeline funcionando
* fluff removido
* normalized tech stack correcto
* consulting blacklist funcionando
* embeddings generados
* LeadScore calculado

Generar reporte:

* jobs procesados
* jobs descartados
* top technologies
* top industries
* token reduction %

## 3. Validar Cluster Engine

Verificar:

* clusters reales creados
* SemanticGroupKey asignado
* similitud semántica funcionando
* no existen clusters basura

Detectar:

* clusters duplicados
* false positives
* false negatives

Generar tabla:

* cluster
* jobs
* companies
* semantic similarity
* BlueOceanScore

## 4. Validar OpportunityEngineV2

Verificar:

* PriorityScoreV2 coherente
* BuyingIntentScore coherente
* RevenuePotential coherente
* SalesFriction coherente

Detectar:

* scores absurdos
* oportunidades falsas
* rankings incorrectos

Generar:
TOP 10 clusters accionables reales.

## 5. Validar LLM Synthesis

Verificar:

* LlmStatus completed
* JSON válido
* pain útil
* MVP útil
* leadMessage útil

Detectar:

* buzzwords
* respuestas genéricas
* hallucinations

Calificar:

* usefulness score 1-10
* commercial quality 1-10

## 6. Generar reporte final

Crear VALIDATION_REPORT.md con:

* pipeline status
* métricas
* errores detectados
* ejemplos reales
* clusters útiles
* clusters basura
* oportunidades reales
* oportunidades falsas
* recomendaciones

# IMPORTANTE

NO agregar:

* frontend nuevo
* arquitectura nueva
* infra nueva

SOLO validar calidad real del intelligence engine.
