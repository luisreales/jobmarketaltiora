# Integracion 100% Gratis con Playwright (Sin n8n)

## 1. Objetivo
Usar un flujo completamente gratuito para obtener resultados de LinkedIn sin pagar n8n ni APIs de busqueda, ejecutando todo desde el backend .NET con Playwright.

## 2. Estrategia aplicada
Se elimina la dependencia de n8n y de APIs de buscador pagas.
El backend ahora:
1. Construye un query de Google por proveedor.
2. Navega con Playwright a resultados de Google.
3. Extrae titulo, URL y snippet de resultados.
4. Filtra por dominio objetivo (LinkedIn/Indeed).
5. Normaliza y persiste en PostgreSQL.
6. Deduplica por Source + ExternalId.

## 3. Flujo tecnico real en este proyecto

### 3.1 Endpoint principal
- POST /api/jobs/search/scrape

Este endpoint ya activa el orquestador y ahora usa Playwright para scraping real (no datos sinteticos).

### 3.2 Pipeline
1. Recibe query, location, limit y providers.
2. Por cada proveedor soportado:
- Construye query Google con site:...
- Ejecuta scraping paginado (10 resultados por pagina).
- Aplica filtros por URL valida del proveedor.
3. Mapea resultados a JobOffer.
4. Guarda con upsert en DB.

## 4. Configuracion Playwright
En appsettings:
- Jobs:Playwright:Headless
- Jobs:Playwright:SlowMoMs
- Jobs:Playwright:NavigationTimeoutMs
- Jobs:Playwright:DelayBetweenPagesMs
- Jobs:Playwright:MaxPagesPerSearch

Esto permite controlar rendimiento, estabilidad y comportamiento anti-bloqueo sin costo.

## 5. Datos guardados
- ExternalId (hash corto de URL cuando no exista id externo)
- Title
- Company (inferido desde title cuando sea posible)
- Location
- Description (snippet)
- Url
- Source
- SearchTerm
- CapturedAt
- MetadataJson (origen, pagina, rank)

## 6. Ventajas de este enfoque
- Costo de herramientas: 0
- Sin dependencia operativa de n8n
- Sin cuota diaria de APIs de busqueda
- Todo centralizado en backend y PostgreSQL

## 7. Limitaciones y mitigacion
- Cambios en DOM de Google pueden romper selectores.
  - Mitigacion: parser defensivo y logs estructurados.
- Puede haber bloqueos por trafico alto.
  - Mitigacion: delays entre paginas, headful opcional, menor frecuencia.
- Precision de company/location depende del snippet.
  - Mitigacion: enriquecer en etapas posteriores.

## 8. Criterio de exito
- El backend obtiene resultados de LinkedIn con Playwright sin APIs pagas.
- Guarda datos en PostgreSQL sin duplicados.
- El frontend consume los resultados por API existente.

## 9. Siguiente mejora recomendada
- Persistir estado de navegador/sesion para estabilidad.
- Agregar retries con backoff por proveedor.
- Mejorar inferencia de company/location con regex y heuristicas.
