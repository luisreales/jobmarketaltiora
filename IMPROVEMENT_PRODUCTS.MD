TAREA 4: FRONTEND - NUEVA PÁGINA DE DETALLE DE PRODUCTO (Angular)
No uses paneles expandibles. Quiero una página dedicada para el detalle del producto.
1. Crea un nuevo componente en Angular llamado `ProductDetailComponent`.
2. Configura la ruta en tu archivo de routing: `{ path: 'products/:id', component: ProductDetailComponent }`.
3. En la tarjeta de `products.html`, cambia el botón "✨ Detalle del Producto" para que navegue a esta nueva ruta usando `[routerLink]="['/products', p.id]"`.
4. Diseño de la nueva página (`product-detail.html`):
   - Un Header (Tailwind) con el Nombre del Producto, las Tecnologías (badges) y el precio estimado.
   - Un botón de acción primario: "Generar Plan de Ataque (IA)" que llame al endpoint de síntesis on-demand para este producto.
   - Si la síntesis ya existe, muestra un dashboard dividido en tarjetas que contenga la respuesta del LLM (Tarea 2):
     * Tarjeta 1: Pasos de Implementación.
     * Tarjeta 2: Requerimientos y Tiempos.
     * Tarjeta 3: Empresas Objetivo (Leads recomendados).
   - Un botón para volver al catálogo de productos (`routerLink="/products"`).