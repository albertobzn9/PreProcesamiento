# Product Requirements

[← Volver al índice de documentación](../README.md)

## Propósito del Documento

Este documento reúne los requisitos y decisiones de uso del Video Batch Processor. Describe qué problema resuelve la aplicación, qué debe hacer, qué queda fuera de alcance y cómo se propone entregarla por módulos.

## Identificación Del Proyecto

**Nombre:** Video Batch Processor

**Tipo:** Aplicación de escritorio (C# / Avalonia UI) para preprocesamiento masivo de videos de la tarea de conflicto mediado por cruces.

**Objetivo:** Normalizar videos (crop, rotación, detección de luces, segmentación y exportación de clips) antes de DeepLabCut (DLC).

**Desarrolladores:** Eric (backend por módulos) + AB (documentación, revisión, integración y frontend)

**Fecha Inicial:** 01-05-2026

**Update:** 12-07-26

**Estado:** Backend base en desarrollo; diseño del frontend en refinamiento.

**Stack:** C# / Avalonia UI


## Resumen Del Proyecto

Desarrollar una aplicación de escritorio nativa (Windows/macOS) para automatizar el pre-procesamiento masivo de videos de laboratorio. Básicamente, toma los videos crudos de las sesiones con ratas y los convierte en videos pequeños y ordenados listos para la inferencia en DLC.

- **Meta:** Normalizar videos (recorte, rotación, orientación y segmentación) en un solo paso antes de ingresarlos a **DeepLabCut**.
- **Prioridad:** Mantener la integridad de los frames (sin pérdida visual) y ofrecer una UX sencilla para usuarios no técnicos.

### Requisitos Iniciales Conservados

El requerimiento técnico inicial del 01-05-2026 está consolidado aquí, no como
un documento paralelo. Se conservan estas decisiones: aplicación local para
Windows y macOS; carga de lotes de 10 o más videos; zona de arrastre; preview
con crop, rotación y espejo; coordenadas visibles de crop (`x`, `y`, `width`,
`height`); estado por archivo y progreso global; y exportación sin pedir al
usuario que instale herramientas de video.

La idea antigua de quitar ciegamente los mismos minutos al inicio y final de
todo video no se conserva como regla automática: ahora el programa debe
preservar ITIs y habituación, y recortar los segmentos que realmente detecte.
El recorte de habituación final sigue siendo una decisión explícita del usuario.

## Contexto Y Problema

En el laboratorio grabamos sesiones de comportamiento de ratas en la tarea CMC (Conflicto Mediado por Cruces). Una sesión dura ~35-45 minutos y tiene unos 30 ensayos aproximadamente, donde la rata decide si cruzar o no una rejilla electrificada para obtener comida. También hay eventos donde la luz se enciende del mismo lado. Entre ensayo y ensayo hay ITIs: en algunas fases son cortos, pero en el entrenamiento de cruces peligrosos pueden ser largos y vale la pena conservarlos si se quiere segmentar todo el video sin perder contexto.

Actualmente:

- Tenemos videos larguísimos de los que no todos los momentos son relevantes
- El tiempo de inferencia en DLC es de tres veces el tiempo del video, por lo que es mejor tener videos cortos y enfocados
- Las cámaras a veces quedan rotadas o al revés
- La cámara de la caja no siempre está en la misma posición entre protocolos
- No hay una forma automática de cortar cada ensayo por separado

Necesitamos una herramienta que haga todo esto en un solo paso, sin tener que editar videos manualmente.

## Características Generales Previstas

La idea general de este proyecto es que se puedan normalizar/estandarizar todos los videos. La idea es que cumpla las siguientes características:

1. **Batch processing:** el usuario puede cargar una carpeta, seleccionar archivos individuales o arrastrar y soltar 10 o más videos. El programa reconoce etapa, día y rata, los agrupa por protocolo y evita mezclarlos en el procesamiento. Antes de iniciar, el usuario puede quitar sesiones de la lista y ve el estado de cada archivo.
2. **Configuración de cámara:** el usuario define recorte, orientación, ROIs y calibración para cada grupo de videos que comparte una misma posición de cámara.
3. **Rotación/reflejo:** el usuario puede rotar 180° o reflejar los videos cuando lo necesite, o dejar la imagen intacta.
4. **Identificación de luces:** el programa detecta cuándo se prenden las tres luces a partir de ROIs y umbrales calibrados por el usuario.
5. **Recorte por segmento:** una vez que identifica los eventos de luz, recorta automáticamente habituación, eventos/ensayos e ITIs.
6. **Asociación conductual:** usa CSV V1 o `.mat` histórico para completar etiquetas conductuales de los eventos y conservar su trazabilidad con el video.
7. **Recorte de habituación:** informa excepciones de habituación final y permite seleccionar qué sesiones largas se quieren recortar.

## Requisitos Funcionales

### 1. Cargar Videos Por Montón

El usuario selecciona una carpeta, archivos individuales o los arrastra a una zona de carga. El programa lee el nombre de cada video y sabe de qué etapa es, qué día, qué rata, etc. Por ejemplo, puede leer archivos legacy como `exp_0126_dis_d9r4.mp4` o nombres del estándar del lab cuando existan.

Antes de procesar, muestra una lista ordenada por protocolo, fase, día y rata. La lista permite confirmar que los videos pertenecen al grupo esperado, quitar los que no aplican, asignarles una configuración de cámara y ver si están pendientes, listos, en proceso o con un aviso; no debe mezclar automáticamente sesiones de protocolos distintos.

### 2. Recortar La Caja Una Vez Y Aplicar Al Lote

El usuario dibuja un rectángulo sobre la caja donde está la rata. Al confirmarlo, la interfaz muestra las coordenadas `x`, `y`, `width` y `height`. Esa configuración se aplica a los videos que comparten la misma posición de cámara.

La app no asume que todo el protocolo conserva una sola cámara ni que el cambio ocurre exactamente dos veces. Antes de continuar, muestra frames representativos de las sesiones ordenadas. Si el usuario identifica que la cámara se movió, crea otro `CameraProfile`, indica desde qué sesión aplica y vuelve a definir crop, orientación, ROIs y calibración para ese grupo. Esto evita que el LED de ruido quede fuera de su ROI y parezca apagado cuando el problema real es el encuadre.

### 3. Enderezar Si Está Chueco

A veces la cámara queda rotada 180° o en espejo. El usuario puede girar o reflejar el video con un botón. Esa decisión forma parte del `CameraProfile` y se aplica solo a las sesiones asignadas a ese perfil.

### 4. Marcar Las Luces Una Vez Y Aplicar Al Lote

En la caja hay tres lucecitas que señalizan los ensayos:

- Una luz del lado **izquierdo** → la comida está disponible a la izquierda
- Una luz del lado **derecho** → la comida está disponible a la derecha
- Un LED en la parte superior derecha que se enciende cuando suena el **ruido blanco** (no tenemos audio en los videos, usamos el LED para saber cuándo hay amenaza). Es particularmente pequeño y necesita revisión ampliada durante la configuración.

En un frame de referencia, el usuario marca una ROI rectangular pequeña para cada luz. Después calibra el umbral con ejemplos claros de OFF y ON para **cada** luz: puede usar un frame oscuro compartido, pero las referencias ON pueden ser frames distintos porque `FoodLeft` y `FoodRight` no se encienden simultáneamente. La app propone un umbral a partir de esas referencias, muestra el brillo medido y permite confirmarlo o ajustarlo.

Para `NoiseLed`, la vista debe ampliar la ROI y avisar si queda fuera del frame o del crop. Con esa configuración confirmada, el programa detecta automáticamente cuándo se encienden y apagan las luces en las sesiones asignadas al mismo `CameraProfile`.

### 5. Partir Los Ensayos Automáticamente

Con la información de las luces, el programa sabe:

- **Cuándo empieza un ensayo/segmento** (luz de comida en seguros; LED/ruido previo en riesgo si se quiere conservar el periodo de advertencia)
- **Cuándo termina** (se apaga la luz)
- **Una heurística visual inicial** de si la rata necesitó cruzar o ya estaba del mismo lado
- **De qué lado es la comida** (luz izquierda o derecha)
- **Si es ensayo seguro, conflicto con comida o solo ruido** (según luces y, cuando exista, `tipo_evento` de la fuente conductual)
- **Cuánto dura el ITI** (entre ensayos)

Como contexto experimental, el primer ensayo de la sesión siempre es seguro/de comida. Esto sirve como referencia o sanity check, pero el programa debe seguir etiquetando los eventos por detección de luces y por la fuente conductual, no por asumir la secuencia.

En ensayos de riesgo/conflicto, el LED de ruido blanco y el sonido se encienden primero; unos segundos después se enciende la luz de comida. Ese delay es intencional: avisa a la rata que hay amenaza antes de que aparezca la oportunidad de comida. En MATLAB, el evento empieza a correr cuando se prende la luz de comida, no cuando se prende el LED.

El programa corta cada ensayo o segmento relevante en su propio video, usando la nomenclatura de output definida en [Naming Convention](../reference/naming-convention.md):

- `abs_2601_f5_d9r4_m_e1_p_cr_stx.mp4` → evento 1, peligroso, con cruce
- `abs_2601_f5_d9r4_m_e2_s_cr_stx.mp4` → evento 2, seguro, con cruce
- `abs_2601_f5_d9r4_m_e3_p_nc_stx.mp4` → evento 3, peligroso, sin cruce
- `abs_2601_f5_d9r4_m_iti1_na_na_stx.mp4` → ITI posterior al evento 1
- `abs_2601_f5_d9r4_m_hab_na_na_stx.mp4` → habituación
- ...
- También corta los ensayos donde la rata **no cruzó** (esos son importantes para el análisis).

> Decisión tomada: los clips de salida agregan el campo `resultado`, usando `cr` para cruce, `nc` para no cruce, `to` para timeout, `rv` para una excepción pendiente de revisión y `na` cuando no aplica.

### 6. Saber Si La Rata Cruzó O No

Esto se necesita para etiquetar correctamente los ensayos. Tenemos dos formas de saberlo:

**Por la luz:** si en el ensayo anterior la luz se encendió del mismo lado, la rata ya está en ese lado, así que no necesita cruzar. Pero esto no siempre funciona (a veces la rata no cruzó y se quedó donde estaba). Esta inferencia puede servir como heurística inicial.

**Por la fuente conductual:** cada sesión puede tener un `.mat` histórico o un CSV V1 nuevo de CajaValentia. Registra latencias, descarga, desplazamiento y tipo de evento. El programa lo usa para determinar cruce, no cruce o timeout cuando el registro existe; en los dos patrones excepcionales definidos abajo, genera una alerta `rv` para decisión humana.

Combinando ambas fuentes se obtiene mejor trazabilidad: el video define los tiempos visuales y la fuente conductual define las etiquetas. La asociación no presupone relojes idénticos: mide el desfase entre el inicio visual de comida y el inicio MATLAB estimado, además del tiempo que la luz sigue visible después del palanqueo registrado. El LED de ruido conserva por separado el periodo visual previo de advertencia. La UI debe mostrar estas discrepancias para revisión antes de exportar. Ver [Sincronización video-conducta de CajaValentia](sincronizacion-video-mat-cajavalentia.md) para el procedimiento y los campos que deben conservarse.

### 7. Recortar La Habituación

Al inicio y al final de cada sesión hay unos minutos donde la rata se acostumbra a la caja (habituación). Aunque el protocolo original describe 5 min de exposición al contexto al inicio y al final, en los videos reales la habituación final suele depender del corte manual y puede variar (por ejemplo ~3–8 min).

Con los valores iniciales del proyecto, la app solo destaca las excepciones útiles para revisión: habituación final mayor a 5 min o menor a 3 min. Para las sesiones mayores a 5 min presenta una tabla con duración y una palomita por sesión; el usuario puede seleccionar cuáles recortar a 5 min o dejar todas intactas. Para las menores a 3 min muestra una advertencia informativa, porque no hay material suficiente para llegar a 5 min y esa diferencia debe considerarse después en el análisis. Las sesiones entre esos límites no generan ruido visual innecesario. Ambos límites deben poder configurarse por lote.

### 8. Alertas De Revisión Conductual

La app debe señalar patrones que no puede decidir automáticamente y entregar la
evidencia para que el investigador defina su criterio de análisis. En concreto:

- `InterEventCrossing`: el `Lado` se repite, pero `Desplaz > 1 s`; la rata pudo
  haberse cruzado durante el ITI.
- `ShortSideChange`: `Lado` cambia, pero `Desplaz <= 1 s`; la rata pudo haber
  iniciado desde la zona media de la caja.

Por cada alerta, mostrar protocolo, fase, rata, día, número de evento, valores
raw de `Lado` y `Desplaz`, referencia al clip/video y a la fuente conductual. El reporte del
lote debe agruparlas por rata y sesión, por ejemplo: "protocolo 0126, rata 2,
CS: tres hallazgos `InterEventCrossing` en los días 1, 2 y 3". La aplicación no
decide si se cuentan o excluyen; conserva la decisión del investigador como
parte de la revisión.

### 9. Herramientas De Video Y Calidad De Exportación

La aplicación usa `ffprobe` para conocer duración, fps y dimensiones del video,
y FFmpeg para exportar. Estas herramientas deben quedar administradas por la
aplicación: el investigador no instala ni configura ejecutables manualmente.
La forma concreta de distribuirlas por sistema operativo (incluidas en el
paquete o preparadas en una carpeta interna) se decide en la implementación,
pero no debe alterar ese flujo de uso.

Cada clip final debe aplicar sus límites de tiempo, crop, rotación y espejo en
una sola exportación cuando sea posible. Esto evita recodificar un video entero
y volverlo a recodificar por cada clip. El perfil inicial de referencia para
DLC es H.264 (`libx264`) con `CRF 18`; debe validarse con videos reales antes de
tratarlo como configuración fija del laboratorio.

El uso de `-ss` y `-to` corresponde a los límites de cada segmento que el
programa planeó. No se aplica por defecto un trim simétrico o asimétrico global
a todas las sesiones, pues podría borrar ITIs o habituación que se necesita
conservar.

## Plan De Entrega Por Módulos

Para no hacer todo de golpe, dividimos el programa en partes chiquitas e independientes. Cada parte se puede probar por separado antes de juntar todo. El orden sería:

1. **Leer nombres de archivos** — el programa entiende la nomenclatura y extrae la información del video
2. **Leer videos y extraer frames** — puede abrir un video, decirnos cuántos fps tiene, su resolución, etc.
3. **Recortar y rotar** — aplica crop y rotación a un video
4. **Detectar luces** — analiza frames y dice qué luces están encendidas
5. **Identificar ensayos** con la secuencia de luces — define dónde empieza y termina cada ensayo
6. **Leer la fuente conductual** (CSV V1 o `.mat` histórico) para saber si la rata cruzó
7. **Exportar clips** como videos individuales
8. **Juntar todo** en una interfaz fácil de usar

Cada parte se puede hacer y probar por separado. Esto permite que un ayudante (Eric, servicio social) haga varias de estas partes durante junio, y después yo pueda continuar integrando.

## Fuera De Alcance

- No entrena modelos de DeepLabCut
- No clasifica conductas
- No analiza el comportamiento de la rata
- No hace tracking postural
- Solo procesa video — la ciencia la hacemos después

## Entrega De La Aplicación

La meta de entrega es una aplicación de escritorio local para Windows y macOS,
con dependencias de video administradas internamente. Una distribución
autocontenida es deseable. Native AOT y el objetivo histórico de menos de 150
MB quedan como metas de empaquetado que se evaluarán cuando exista el pipeline
real de FFmpeg: no deben imponerse antes de medir el peso y compatibilidad en
ambos sistemas.

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
