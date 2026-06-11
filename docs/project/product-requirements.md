# Product Requirements

[← Volver al índice de documentación](../README.md)

## Propósito del Documento

Este documento reúne las notas de producto originales del Video Batch Processor sin resumir contenido. Su objetivo es describir qué problema resuelve la aplicación, qué debe hacer, qué queda fuera de alcance y cómo se propone entregar el proyecto por módulos.

## Identificación Del Proyecto

**Nombre:** Video Batch Processor

**Tipo:** Aplicación de escritorio (C# / Avalonia UI) para preprocesamiento masivo de videos de la tarea de conflicto mediado por cruces.

**Objetivo:** Normalizar videos (crop, rotación, detección de luces, segmentación y exportación de clips) antes de DeepLabCut.

**Desarrolladores:** Eric (backend, junio 2026) + AB (post-integración)

**Fecha Inicial:** 01-05-2026

**Update:** 31-05-26

**Estado:** Definición Técnica

**Stack:** C# / Avalonia UI

**Referencia original:** Guia Estándar de Desarrollo de Apps (documento interno no incluido en este repositorio)

## Resumen Del Proyecto

Un programa de escritorio para Windows y Mac que prepara automáticamente los videos del laboratorio antes de analizarlos con DeepLabCut. Básicamente, toma los videos crudos de las sesiones con ratas y los convierte en videos pequeños y ordenados, cada uno con un ensayo/evento, ITI o habituación, listos para procesar.

Desarrollar una aplicación de escritorio nativa (Windows/macOS) para automatizar el pre-procesamiento masivo de videos de laboratorio.

- **Meta:** Normalizar videos (recorte, rotación y cropping) en un solo paso antes de ingresarlos a **DeepLabCut**.
- **Prioridad:** Mantener la integridad de los frames (sin pérdida visual) y ofrecer una UX sencilla para usuarios no técnicos.

## Contexto Y Problema

En el laboratorio grabamos sesiones de comportamiento de ratas en la tarea CMC (Conflicto Mediado por Cruces). Una sesión dura ~50 minutos y tiene unos 30 ensayos, donde la rata decide si cruzar o no una rejilla para obtener comida. También hay eventos donde la luz se enciende del mismo lado. Entre ensayo y ensayo hay ITIs: en algunas fases son cortos, pero en CP pueden ser largos y vale la pena conservarlos si se quiere segmentar todo el video sin perder contexto.

Actualmente:

- Tenemos videos larguísimos de los que solo usamos pedazos
- Las cámaras a veces quedan rotadas o al revés
- La caja de comportamiento no siempre está en la misma posición entre protocolos
- No hay una forma estándar de cortar cada ensayo por separado
- Dado el tiempo de inferencia de DeepLabCut es mejor tener videos cortos y enfocados

Necesitamos una herramienta que haga todo esto en un solo paso, sin tener que editar video manualmente.

## Características Generales Previstas

La idea general de este proyecto es que se puedan normalizar/estandarizar todos los videos. La idea es que cumpla las siguientes características:

1. Batch processing: que el usuario pueda subir diferentes videos con una nomenclatura estándar y el programa en automático reconozca qué etapa, día y rata es el video.
2. Cropping del video: que a través de una interfaz el usuario pueda seleccionar qué partes del video desea recortar y eso se aplique a todo el batch. Se asume que durante todo el protocolo la cámara quedó fija. Se asume que entre protocolo y protocolo la cámara se mueve debido al moldeamiento (la cámara pasa del centro al lateral y al terminar el moldeamiento se mueve de regreso al centro).
3. Rotación/reflejo del video: que el usuario pueda seleccionar si se rota 180° o se refleja en espejo con un botón, y que eso se aplique a todo el batch. También dar la opción para que no se haga en caso de que el video esté bien.
4. Identificación de ensayos: utilizar algoritmos de visión artificial para reconocer cuándo se prende cada una de las tres luces. Para que sea más fácil identificar las luces, el usuario puede marcar con círculos dónde se encuentran.
5. Recorte por segmento: una vez que el programa identifica los eventos de luz, que recorte en automático la habituación, los ensayos/eventos y los ITIs.
6. Recorte de habituación: mostrar al usuario cuánto tiempo dura la habituación inicial y final y permitir recortar al tiempo deseado. Si en 10 videos de 50 se observa que la habituación final dura más de 7 minutos pero solo se ocupan 5 min, el programa debe permitir recortarla. También debe mostrar cuando las habituaciones duren menos de un tiempo establecido por el usuario.

## Requisitos Funcionales

### 1. Cargar Videos Por Montón

El usuario selecciona una carpeta con muchos videos. El programa lee el nombre de cada video y sabe de qué etapa es, qué día, qué rata, etc. Por ejemplo, puede leer archivos legacy como `exp_0126_dis_d9r4.mp4` o nombres del estándar del lab cuando existan.

### 2. Recortar La Caja Una Vez Y Aplicar Al Lote

En el primer video, el usuario dibuja un rectángulo sobre la caja donde está la rata. Como la cámara no se mueve durante todo un protocolo, ese recorte se aplica a todos los videos del lote. Si la cámara se movió entre protocolos (pasa cuando la movemos del centro al lateral por el moldeamento), se puede redefinir.

### 3. Enderezar Si Está Chueco

A veces la cámara queda rotada 180° o en espejo. El usuario puede girar o reflejar el video con un botón, y eso se aplica a todo el lote.

### 4. Marcar Las Luces Una Vez Y Aplicar Al Lote

En la caja hay tres lucecitas que señalizan los ensayos:

- Una luz del lado **izquierdo** → la comida está disponible a la izquierda
- Una luz del lado **derecho** → la comida está disponible a la derecha
- Un LED en la parte superior derecha que se enciende cuando suena el **ruido blanco** (no tenemos audio en los videos, usamos el LED para saber cuándo hay amenaza)

En el primer video, el usuario marca con un círculo dónde está cada una de estas tres luces. El programa entonces **detecta automáticamente cuándo se encienden y apagan** en todos los videos.

### 5. Partir Los Ensayos Automáticamente

Con la información de las luces, el programa sabe:

- **Cuándo empieza un ensayo/segmento** (luz de comida en seguros; LED/ruido previo en riesgo si se quiere conservar el periodo de advertencia)
- **Cuándo termina** (se apaga la luz)
- **Cuándo el ensayo es de cruce o no** (si el ensayo anterior fue del mismo o el lado contrario)
- **De qué lado es la comida** (luz izquierda o derecha)
- **Si es ensayo seguro o de conflicto** (solo luz = seguro; luz + LED ruido = conflicto)
- **Cuánto dura el ITI** (entre ensayos)

Como contexto experimental, el primer ensayo de la sesión siempre es seguro/de comida. Esto sirve como referencia o sanity check, pero el programa debe seguir etiquetando los eventos por detección de luces y por el `.mat`, no por asumir la secuencia.

En ensayos de riesgo/conflicto, el LED de ruido blanco y el sonido se encienden primero; unos segundos después se enciende la luz de comida. Ese delay es intencional: avisa a la rata que hay amenaza antes de que aparezca la oportunidad de comida. En MATLAB, el evento empieza a correr cuando se prende la luz de comida, no cuando se prende el LED.

El programa corta cada ensayo o segmento relevante en su propio video, usando la nomenclatura de output definida en [Naming Convention](../reference/naming-convention.md):

- `abs_2601_f5_d9r4_m_e1_p_cr_stx.mp4` → evento 1, peligroso, con cruce
- `abs_2601_f5_d9r4_m_e2_s_cr_stx.mp4` → evento 2, seguro, con cruce
- `abs_2601_f5_d9r4_m_e3_p_nc_stx.mp4` → evento 3, peligroso, sin cruce
- `abs_2601_f5_d9r4_m_iti1_na_na_stx.mp4` → ITI posterior al evento 1
- `abs_2601_f5_d9r4_m_hab_na_na_stx.mp4` → habituación
- ...
- También corta los ensayos donde la rata **no cruzó** (esos son importantes para el análisis).

> Decisión tomada: los clips de salida agregan el campo `resultado`, usando `cr` para cruce, `nc` para no cruce, `to` para timeout y `na` cuando no aplica.

### 6. Saber Si La Rata Cruzó O No

Esto se necesita para etiquetar correctamente los ensayos. Tenemos dos formas de saberlo:

**Por la luz:** si en el ensayo anterior la luz se encendió del mismo lado, la rata ya está en ese lado, así que no necesita cruzar. Pero esto no siempre funciona (a veces la rata no cruzó y se quedó donde estaba). Esta inferencia puede servir como heurística inicial.

**Por el archivo .mat:** para cada sesión tenemos un archivo .mat (del programa Caja Valentia) que registra toda la información de la sesión: latencia de cruce, si recibió descarga, etc. El programa puede leer ese archivo y saber con certeza si la rata cruzó y en cuánto tiempo.

Combinando ambas fuentes se obtiene mejor trazabilidad: el video define los tiempos visuales y el `.mat` define las etiquetas conductuales.

### 7. Recortar La Habituación

Al inicio y al final de cada sesión hay unos minutos donde la rata se acostumbra a la caja (habituación). Aunque el protocolo original describe 5 min de exposición al contexto al inicio y al final, en los videos reales la habituación final suele depender del corte manual y puede variar (por ejemplo ~3–8 min). El programa muestra cuánto dura y permite al usuario decir "déjame solo 5 minutos de habituación". Si la habituación del video es más corta, avisa. De esta manera permite al usuario saber cuál fue la habituación más corta dentro de una etapa de entrenamiento. Digamos que en discriminación hubo un día donde la habituación final de una rata solo duró 3 min 15 seg; de esa manera el usuario puede decidir si excluye esa habituación o si recorta todas las habituaciones finales a ese tiempo.

## Plan De Entrega Por Módulos

Para no hacer todo de golpe, dividimos el programa en partes chiquitas e independientes. Cada parte se puede probar por separado antes de juntar todo. El orden sería:

1. **Leer nombres de archivos** — el programa entiende la nomenclatura y extrae la información del video
2. **Leer videos y extraer frames** — puede abrir un video, decirnos cuántos fps tiene, su resolución, etc.
3. **Recortar y rotar** — aplica crop y rotación a un video
4. **Detectar luces** — analiza frames y dice qué luces están encendidas
5. **Identificar ensayos** con la secuencia de luces — define dónde empieza y termina cada ensayo
6. **Leer archivos .mat** para saber si la rata cruzó
7. **Exportar clips** como videos individuales
8. **Juntar todo** en una interfaz fácil de usar

Cada parte se puede hacer y probar por separado. Esto permite que un ayudante (Eric, servicio social) haga varias de estas partes durante junio, y después yo pueda continuar integrando.

## Fuera De Alcance

- No entrena modelos de DeepLabCut
- No clasifica conductas
- No analiza el comportamiento de la rata
- No hace tracking postural
- Solo procesa video — la ciencia la hacemos después

## Outputs Esperados

Una carpeta con videos organizados por sesión/rata. Los clips usan la nomenclatura de output del Video Batch Processor:

```
videos_procesados/
├── abs_2601_f5_d9r4_m/
│   ├── abs_2601_f5_d9r4_m_e1_p_cr_stx.mp4      (evento 1, peligroso, con cruce)
│   ├── abs_2601_f5_d9r4_m_e2_s_cr_stx.mp4      (evento 2, seguro, con cruce)
│   ├── abs_2601_f5_d9r4_m_e3_s_nc_stx.mp4      (evento 3, seguro, sin cruce)
│   ├── abs_2601_f5_d9r4_m_iti1_na_na_stx.mp4   (ITI posterior al evento 1)
│   ├── abs_2601_f5_d9r4_m_hab_na_na_stx.mp4    (habituación)
│   ├── ...
│   └── abs_2601_f5_d9r4_m_e30_s_cr_stx.mp4     (evento 30, seguro, con cruce)
├── abs_2601_f5_d9r3_m/
│   └── ...
└── reporte.csv            (resumen de todo lo procesado)
```

Cada video de ensayo o ITI es un clip corto (de segundos a ∼3 minutos máximo). Los clips de habituación pueden durar más. Todos quedan con la caja recortada, bien orientados y nombrados de manera que cualquier otro programa (DeepLabCut, BORIS, etc.) sepa exactamente qué son.
