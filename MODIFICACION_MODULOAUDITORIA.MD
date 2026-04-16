Actúa como Senior Fullstack Engineer. Necesito conectar el botón "✨ Generar Plan de Ataque (IA)" en la vista de detalles del producto con el motor de IA, integrándolo profundamente con el módulo de Auditoría y realizando tareas de limpieza técnica.

UBICACIÓN DEL BOTÓN:
- URL: http://localhost:4200/products/{id}
- Componente Angular: 'ProductDetailComponent' (o similar en 'src/app/pages/product-detail/').

REQUERIMIENTOS TÉCNICOS:

1. FLUJO DE EJECUCIÓN (BACKEND):
- Crea o ajusta el endpoint 'POST /api/products/{id}/synthesize' en 'ProductController'.
- El endpoint debe:
    a) Cargar el template de prompt 'market-job-analysis' desde la base de datos (gestionado en AI Audit).
    b) Reemplazar placeholders {{Title}}, {{Company}}, {{Description}} con data real de los clusters asociados al producto.
    c) Invocar al LLM usando 'IProductSynthesisService' o 'IClusterSynthesisService'.
    d) Actualizar el 'ProductSuggestion' y sus 'MarketClusters' relacionados con los campos: 'SynthesizedPain', 'SynthesizedMvp', 'SynthesizedLeadMessage'.
    e) Cambiar 'LlmStatus' a 'completed'.
    f) REGISTRO OBLIGATORIO: Guardar la ejecución en 'AiPromptLog' incluyendo el 'ClusterId' para trazabilidad.

2. GESTIÓN DE TEMPLATES (AI AUDIT):
- Asegura que el formulario "Prompt Admin" en '/ai-audit' persista correctamente el template en la DB para que el proceso anterior lo use como base.

3. INTEGRACIÓN UI (ANGULAR):
- En 'ProductDetailComponent':
    a) Implementar un spinner/loading state en el botón violeta al procesar.
    b) Al finalizar con éxito, refrescar el modelo local para mostrar el Plan de Ataque generado (Implementación, Requerimientos, Tiempos, Empresas Objetivo).
    c) Manejar errores con notificaciones (Toast/Alert) y permitir reintentar.

4. OPTIMIZACIÓN Y LIMPIEZA DE AUDITORÍA:
- Refactoriza la tabla de 'AI Audit':
    a) Añadir filtros por 'ClusterId', 'Status' y 'Model'.
    b) Implementar ordenamiento DESC por fecha (mostrar lo más reciente primero).
    c) Añadir enlaces desde la tabla de auditoría hacia el detalle del producto/cluster correspondiente.
- CLEANUP (P6): Implementar 'DELETE /api/market/clusters/cleanup' para eliminar clusters con 'JobCount < 5' que no tengan síntesis previa.

5. MODERNIZACIÓN ANGULAR 20:
- Migrar 'product-detail.html' y 'opportunities.html' al nuevo Control Flow (@if, @for).
- Implementar Lazy Loading en 'app.routes.ts' para reducir el bundle size de 761kB.

PASOS A SEGUIR:
1. Analiza 'ProductController', 'ProductSynthesisService' y 'AiAuditService'.
2. Localiza los templates HTML y rutas en el frontend.
3. Propón el plan de modificación de archivos detallado antes de proceder.