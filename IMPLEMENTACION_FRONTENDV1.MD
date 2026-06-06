# TASK — Finalización Frontend V1 (Market Intelligence Engine)

## Contexto

El backend del Market Intelligence Engine V1 ya está funcional end-to-end.

El frontend actual ya tiene:
- Angular 20 standalone
- Tailwind
- Angular Material
- Lazy loading
- Página `/clusters`
- ClusterService completo
- Cards expandibles
- LLM synthesis renderizada
- Métricas V2
- Leads lazy-loaded
- Rebuild manual
- Synthesize on-demand

NO reescribir arquitectura.
NO crear nuevos módulos.
NO cambiar stack.
NO introducir NgModules.
NO romper rutas existentes.

El objetivo es dejar el frontend en estado “production-ready analytics dashboard”.

---

# Objetivos del Sprint Frontend Final

Implementar únicamente los gaps reales detectados en la validación del backend.

---

# 1. Dashboard Ejecutivo Global (NUEVO)

## Crear nueva ruta

/dashboard

## Crear:

- dashboard.page.ts
- dashboard.page.html
- dashboard.page.scss (solo si realmente hace falta)

## Crear DashboardService

Consumirá:
- GET /api/market/clusters
- GET /api/jobs
- GET /api/opportunities
- GET /api/products

NO crear backend nuevo.
Usar agregación desde frontend con RxJS forkJoin.

---

## El dashboard debe mostrar:

### KPIs superiores

Cards responsive:

- Total JobOffers
- JobInsights procesados
- Pendientes
- Clusters accionables
- Clusters Ignore
- Productos generados
- % Direct Clients
- LeadScore promedio

---

## Gráficas

Usar ng2-charts.

### 1. Industry Distribution
Pie chart.

### 2. LeadScore Distribution
Bar chart:
- HIGH
- MEDIUM
- LOW

### 3. Opportunity Priority Ranking
Horizontal bar:
- top 10 clusters
- usar priorityScoreV2

### 4. Revenue Potential vs JobCount
Scatter chart.

---

## Tabla Top Opportunities

Mostrar:

- Cluster label
- Industry
- PriorityScoreV2
- RevenuePotential
- BuyingIntent
- CloseProbability
- JobCount

Acciones:
- Open cluster
- Trigger synthesis

---

# 2. Estado del Pipeline (NUEVO)

## Crear componente reusable:

pipeline-status.component

Debe visualizar:

| Stage | Estado |
|---|---|
| Worker 1 |
| Cluster Engine |
| Semantic Clustering |
| Opportunity Engine |
| LLM Synthesis |
| Product Generator |

---

## Reglas visuales

### Verde
Funcionando.

### Amarillo
Warning.

### Rojo
Blocked/Error.

---

## Detectar automáticamente:

### Semantic Clustering bloqueado

Si:
semanticGroupKey == null en todos los clusters

Mostrar:

"Semantic embeddings not configured"

Badge rojo.

---

## Detectar backlog

Si:
pendientes > 0

Mostrar:

"Worker backlog pending"

---

# 3. Centro de Acciones Operacionales (NUEVO)

Agregar panel admin dentro de `/clusters`.

## Sección: Maintenance Actions

Botones:

### Backfill Insights

POST:
/api/market/clusters/backfill-insights

Mostrar:
- loading
- success snackbar
- error snackbar

---

### Rebuild Clusters

POST:
/api/market/clusters/rebuild

Mostrar:
- tiempo ejecución
- resultado

---

### Cleanup Small Clusters

DELETE:
/api/market/clusters/cleanup?minJobs=3

Agregar confirm modal antes.

---

### Re-synthesize Pending Clusters

Implementar batch UI:

1. obtener clusters
2. detectar:
   llmStatus == completed && llmConfidence == null

3. disparar synthesize(id)

Mostrar:
- progress bar
- contador completados
- errores

---

# 4. Mejoras UX en Clusters (IMPORTANTE)

## Agregar sorting

Dropdown:

- PriorityScoreV2
- RevenuePotential
- BuyingIntent
- LeadScore
- JobCount
- CloseProbability

---

## Agregar búsqueda textual

Buscar por:
- Label
- Industry
- PainCategory
- TechStack

Con debounce RxJS.

---

## Agregar chips visuales

### Cluster confidence

Si:
jobCount == 1

Mostrar:
"Low Signal"

Rojo.

---

### High Opportunity

Si:
priorityScoreV2 >= 75

Mostrar:
"High Opportunity"

Verde.

---

### Unknown Industry

Si:
industry == Unknown

Mostrar:
"Needs Backfill"

Amber.

---

# 5. Vista Semantic Groups (Preparación futura)

Aunque embeddings estén bloqueados, preparar UI.

## Crear:

/semantic-groups

---

## Mostrar:

- semanticGroupKey
- clusters relacionados
- similarity placeholder
- total jobs

---

## Si no hay embeddings:

Mostrar empty state elegante:

"Semantic clustering not available yet"

con CTA:
"Configure EmbeddingModelId"

NO mockear datos falsos.

---

# 6. Correcciones UI detectadas

## Fix singular/plural

"1 empresas" → "1 empresa"

Crear helper reutilizable.

---

## Confidence badge

Actualmente confidence solo existe en 1 cluster.

Reglas:

### null
Mostrar:
"Legacy synthesis"

### >= 0.8
verde

### >= 0.6
amber

### < 0.6
rojo

---

## SalesFriction

Actualmente siempre 0 en actionable clusters.

NO mostrar barra roja vacía.

Mostrar:
- "Low Friction"
o
- porcentaje real

---

# 7. Performance

## Implementar:

### trackBy en todos los ngFor

### ChangeDetectionStrategy.OnPush

en:
- clusters
- dashboard
- leads tables
- products

---

## Virtual scrolling

Solo para:
tabla leads.

Usar CDK Virtual Scroll.

---

# 8. Error Handling Enterprise-grade

Crear interceptor global si no existe:

- network errors
- timeout
- 500
- retries simples

---

## Toast strategy

NO usar alert().

Usar:
MatSnackBar.

---

# 9. Estados vacíos reales

Implementar empty states para:

- no clusters
- no leads
- no synthesis
- no products
- semantic unavailable

Con diseño profesional.

NO pantallas vacías.

---

# 10. Diseño visual

Objetivo visual:

- Linear
- Vercel Analytics
- Datadog
- PostHog

NO estilo corporativo viejo.
NO gradientes exagerados.
NO glassmorphism.
NO sombras pesadas.

---

# 11. Restricciones

NO:
- introducir NgRx
- introducir SSR
- introducir microfrontends
- reestructurar app
- cambiar routing actual
- duplicar modelos
- crear APIs nuevas
- mockear datos inexistentes

---

# 12. Resultado esperado

Al finalizar debe existir:

✔ Dashboard ejecutivo real
✔ Monitoreo visual del pipeline
✔ Operaciones administrativas completas
✔ UX production-ready
✔ Manejo de estados vacíos
✔ Performance optimizada
✔ Semantic Groups preparado
✔ Error handling sólido
✔ Clusters listos para demo comercial

El código debe ser:
- clean architecture
- tipado estricto
- standalone
- responsive
- lazy-loaded
- sin warnings TypeScript
- sin any innecesarios
- sin lógica duplicada