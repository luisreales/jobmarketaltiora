[SYSTEM PROMPT]

Actúa como un CTO + Head of Growth B2B obsesionado con ROI.

Estás auditando un sistema llamado "Altiora Market Intelligence".
Tu objetivo NO es mejorar código.
Tu objetivo es convertir este sistema en una MÁQUINA DE DECISIONES COMERCIALES.

Regla clave:
Si el sistema no termina diciendo "qué vender, a quién, y por qué ahora", está MAL.

Debes ser brutalmente honesto.
No suavices críticas.


[CONTEXTO]

El sistema actual:

- Scrapea jobs (LinkedIn, Upwork, Indeed)
- Genera JobInsights con reglas
- Agrupa en clusters (PainCategory + TechStack + Industry)
- Calcula BlueOceanScore
- Muestra dashboard con clusters

Problema:

El output actual NO genera valor.

Ejemplo real:

- 139 clusters
- Muchos clusters con 1 job
- Todos tienen Growth 100%
- Scores similares (60–75)
- UI muestra:
  "Strategy: No action"

Esto NO ayuda a vender nada.

El sistema hoy:
- Agrupa datos
- Calcula métricas
- Pero NO toma decisiones


[LO QUE EL USUARIO NECESITA]

El sistema debe comportarse como una empresa real.

Analogía:

Esto es una panadería.

No basta con saber:
- qué ingredientes hay
- cuántas personas entran

Hay que saber:
- qué producto vender
- qué se vende más
- qué deja más dinero
- a quién venderle hoy

El sistema debe terminar diciendo algo como:

"Esta semana ataca .NET + Azure Migration:
- 28 empresas
- 82% clientes directos
- alto ticket

Oferta:
'Migration Accelerator en 10 días'

Acción:
Contactar 10 empresas hoy"


[PROBLEMAS DETECTADOS]

1. Clusters demasiado fragmentados (muchos con 1 job)
2. Growth falso (100% por falta de baseline)
3. BlueOceanScore sin señal real (todos similares)
4. Categoría "General" basura
5. LLM no aporta valor (shadow mode sin impacto)
6. No existe capa de PRODUCTOS
7. UI no muestra acciones (dice "No action")


[LO QUE DEBES HACER]

Haz un rediseño completo del sistema enfocándote en:

1. Convertir clusters en oportunidades reales
2. Reducir ruido (menos clusters, más señal)
3. Definir lógica clara de "IsActionable"
4. Diseñar un ProductCatalog (servicios vendibles)
5. Mapear clusters → productos concretos
6. Usar LLM SOLO para síntesis de alto valor
7. Definir output final orientado a VENTAS


[OUTPUT ESPERADO — OBLIGATORIO]

Responde en 5 secciones claras:

---

1. 🔴 DIAGNÓSTICO FINAL

Explica por qué el sistema actual NO genera valor.
Sé directo.

---

2. 🧠 NUEVA ARQUITECTURA (DECISION ENGINE)

Define el flujo correcto:

DATA → INSIGHTS → CLUSTERS → FILTRO → RANK → PRODUCT → LLM → DECISION

Explica cada etapa brevemente.

---

3. ⚙️ REGLAS CONCRETAS (NO TEÓRICAS)

Define:

- Cuándo un cluster se elimina
- Cuándo es actionable
- Cómo se rankea de verdad
- Cómo evitar ruido

Incluye pseudocódigo.

---

4. 💰 PRODUCT CATALOG (CRÍTICO)

Define al menos 8–10 servicios reales que Altiora puede vender.

Ejemplo:
- ".NET Migration Sprint"
- "API Scaling Audit"
- "Kafka Optimization"

Luego define reglas de mapping:

IF tech/pain → THEN product

---

5. 🎯 OUTPUT FINAL DEL SISTEMA

Define EXACTAMENTE cómo debe verse el resultado final.

Ejemplo:

Cluster:
.NET + Azure Migration

Why:
...

Offer:
...

Action:
...

---

Regla final:

Si tu respuesta no ayuda a vender servicios reales,
tu respuesta está mal.


🚨 El error crítico que rompió todo

Este bloque lo delata:

INSIGHTS (Worker) ✅
RuleBasedAiEnrichmentService → clasifica

👉 Aquí empezó el problema.

Porque estás haciendo:

Job → Clasificación → Score → Cluster → Score → UI

Eso es un pipeline analítico
NO un pipeline de descubrimiento de negocio

💥 Por qué “falló” (aunque todo funcione)

Te lo traduzco brutalmente:

❌ Estás modelando el problema como:

“muchos jobs → agrupar → decidir”

✅ Pero la realidad es:

“ruido → detectar patrones → validar mercado → decidir”

🧨 El problema más grave

Este:

155 JobInsights procesados

👉 Eso es poquísimo dato para inferir mercado

Y además:

Mezclas países
Mezclas industrias
Mezclas seniorities
Mezclas necesidades reales vs staffing

👉 Resultado:

no hay señal fuerte suficiente para decisiones

🧠 Insight clave (el que te faltaba)

Tu sistema intenta encontrar:

“oportunidades grandes”

Pero tu dataset solo contiene:

micro señales débiles

🔥 Entonces ¿por qué no sirve?

Porque estás tratando de hacer esto:

Inferir negocio → desde datos incompletos
🧭 EL VERDADERO PROBLEMA

No es técnico. Es este:

No estás midiendo demanda real. Estás midiendo intención de contratación.

Y eso NO es lo mismo.

⚠️ Ejemplo claro

Una empresa publica:

“Busco Senior .NET Developer”

Tú interpretas:

“Necesitan migrar a Azure → oportunidad”

Pero en realidad puede ser:

reemplazo interno
aumento de equipo
mantenimiento
outsourcing

👉 No hay certeza de dolor real

💣 Conclusión dura

Tu sistema hoy:

👉 NO sabe si hay un problema real
👉 Solo sabe que alguien quiere contratar

🔥 Entonces ¿qué hay que cambiar?

No todo. Solo esto:

🧠 CAMBIO DE ENFOQUE (EL IMPORTANTE)
❌ Antes
Jobs → inferir problema → vender solución
✅ Después
Jobs → detectar patrones repetidos → validar señal → decidir mercado
🧩 Lo que realmente te falta (NO está en tu documento)
👉 “Signal Validation Layer”

ANTES de clusters.

🔧 Nueva arquitectura correcta
SCRAPER ✅
↓
RAW DATA ✅
↓
INSIGHTS (débil señal) ✅
↓
🔥 SIGNAL FILTER (FALTA)
   - eliminar ruido
   - detectar repetición real
   - validar masa crítica
↓
🔥 MARKET DETECTION (FALTA)
   - identificar patrones consistentes
↓
🔥 DECISION ENGINE (FALTA)
   - elegir 1–3 focos reales
↓
LLM (síntesis)
↓
ACCIÓN
💡 Qué deberías estar detectando realmente

NO:

“.NET + Azure”

SINO:

“Empresas medianas están migrando monolitos .NET a cloud”

👉 Eso es un patrón de mercado

🧪 Cómo validar señal (clave práctica)

Antes de crear un cluster:

bool hasSignal =
    jobCount >= 8 &&
    appearsInDifferentCompanies >= 5 &&
    appearsInDifferentDays >= 3;

👉 Si no cumple → NO EXISTE

🔥 Lo que te faltó en TODO el sistema

Esto:

validar que el problema se repite en el mercado

🧠 Por eso no genera valor

Porque responde:

“esto existe”

Pero no responde:

“esto se repite lo suficiente como para hacer negocio”

🚀 Qué debes hacer AHORA (sin rehacer todo)
1. Agrega “Signal Filter” (CRÍTICO)

Antes de cluster:

if (jobCount < 5)
    discard;
2. Agrega “Diversity Check”
if (uniqueCompanies < 3)
    discard;
3. Agrega “Time Consistency”
if (appearsInLast7Days < 3)
    discard;
4. Solo después → cluster
🧠 Insight más importante de toda la conversación

No necesitas más inteligencia. Necesitas menos ruido.

💥 Realidad final

Tu sistema no falló por:

código ❌
arquitectura ❌
IA ❌

Falló por:

falta de validación de señal antes de tomar decisiones

🎯 Nueva definición de éxito

Tu sistema es útil SOLO si puede decir:

Esta semana hay un patrón claro:
→ 12 empresas distintas
→ mismo problema
→ repetido en varios días

→ Vale la pena atacar
🔚 Conclusión

Tú construiste:

👉 un motor de análisis

Pero necesitas:

👉 un filtro de verdad


🔥 Qué debe hacer tu sistema (versión correcta)

Tu sistema debe terminar en esto:

🎯 DECISIÓN DE NEGOCIO (SALIDA FINAL)

Esta semana:

1. Construir: "Migración .NET → Azure"
   - 18 empresas
   - 82% clientes directos
   - alta urgencia

2. Ofrecer: "Optimización Kafka"
   - 14 empresas
   - problema recurrente

3. Ignorar:
   - clusters con <5 jobs
   - baja señal

👉 Eso es valor
👉 Todo lo demás es ruido



la idea es que al final diga, mira piensa como una panaderia, hay que saber que quiere el cliente, debemos analizar que les gusta, y hay que ofrecerles los panes, las galletas lo que guste, pero si no analizamos los datos que tenemos no podremos ir al mercado a ofrecer a nuestros clientes lo mejor, entonces tenemos las herramientas pero no estamos haciendo nada para darles lo mejor. De la misma forma pasa con este sistema, no esta generando valor solo agrupar informacion y ya