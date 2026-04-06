# LinkedIn Jobs MVP (Nuevo Proyecto)

## Objetivo
Construir un proyecto enfocado en capturar ofertas laborales relacionadas con .NET desde LinkedIn (y fuentes compatibles), permitiendo buscar por descripcion del trabajo, almacenar resultados y consultar historial.

## Alcance del MVP
- Pantalla inicial con 2 opciones: Bookings o LinkedIn.
- Flujo LinkedIn:
  - Busqueda por descripcion/keywords.
  - Captura y almacenamiento de datos de la oferta.
  - Listado de ofertas guardadas.
  - Vista de detalle por oferta.

## Datos a almacenar por oferta
- Titulo del puesto.
- Empresa.
- Ubicacion.
- Descripcion completa.
- URL de la oferta.
- Contacto disponible (reclutador/perfil/email si existe).
- Salario o rango salarial (si aplica).
- Fecha de publicacion.
- Seniority / tipo de contrato.
- Metadatos de captura: fuente, fecha/hora de captura.

## Arquitectura sugerida
- Backend: .NET 9 + Web API.
- Frontend: Angular 20.
- Persistencia: PostgreSQL.
- Extraccion: estrategia hibrida (interceptacion/API primero, Playwright como fallback).
- Observabilidad: logging estructurado y metricas basicas de scraping.

## Endpoints base del modulo LinkedIn (propuestos)
- GET /api/linkedin/search
- GET /api/linkedin/jobs
- GET /api/linkedin/jobs/{id}
- POST /api/linkedin/jobs/{id}/save

## Consideraciones
- Implementar feature flag para habilitar/deshabilitar scraping.
- Manejar rate limiting, reintentos con backoff y errores por bloqueos.
- Mantener interfaces internas para migrar a proveedor/API oficial sin romper frontend.

## Estado
Documento inicial para refinamiento tecnico y funcional.