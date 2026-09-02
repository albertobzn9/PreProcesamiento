# Video Batch Processor — Propuesta de Solución

## Stack tecnológico

| Capa | Tecnología |
|------|-----------|
| UI | Avalonia UI (cross-platform: Windows + macOS) |
| Backend | C# (.NET 10, target actual del repo) |
| Video I/O actual | OpenCvSharp |
| Exportación de video prevista | FFmpeg (via FFmpeg.AutoGen o proceso externo) |
| Imágenes | OpenCvSharp / SkiaSharp según el adaptador |
| Datos conductuales | CSV V1 de CajaValentia y adaptador para `.mat` históricos |

**¿Por qué este stack?**
- C#/Avalonia es cross-platform nativo (no electron)
- OpenCvSharp ya se está usando para lectura de video y acceso a frames
- FFmpeg sigue siendo el candidato natural para exportación/corte de clips
- SkiaSharp sigue siendo útil del lado UI cuando convenga

---

## Arquitectura general

```
┌──────────────────────────────────────────────────────────┐
│                        UI (Avalonia)                     │
│  ┌─────────────────┐ ┌──────────┐ ┌───────────────────┐  │
│  │ VideoLoadView   │ │ CropView │ │ LightMarkerView   │  │
│  └─────────────────┘ └──────────┘ └───────────────────┘  │
│  ┌─────────────────┐ ┌────────────────┐ ┌─────────────┐  │
│  │ SegmentTimeline │ │ HabituationView│ │ ExportView  │  │
│  │ View            │ │                │ │             │  │
│  └─────────────────┘ └────────────────┘ └─────────────┘  │
└───────────────────────────┬──────────────────────────────┘
                            │ llama a
┌───────────────────────────▼──────────────────────────────┐
│                  Core Library (Backend)                  │
│  ┌──────────────┐ ┌────────────────────┐ ┌────────────┐  │
│  │Nomenclature  │ │SessionMetadata     │ │VideoReader │  │
│  │Parser        │ │Resolver            │ │            │  │
│  └──────────────┘ └────────────────────┘ └────────────┘  │
│  ┌──────────────┐ ┌────────────────────┐ ┌─────────────┐ │
│  │FrameAnalyzer │ │LightDetection      │ │LightTimeline│ │
│  │              │ │                    │ │Builder      │ │
│  └──────────────┘ └────────────────────┘ └─────────────┘ │
│  ┌──────────────┐ ┌────────────────────┐ ┌────────────┐  │
│  │BehavioralData│ │BehavioralVideoSync │ │SegmentPlan-│  │
│  │              │ │hronizer            │ │ner         │  │
│  └──────────────┘ └────────────────────┘ └────────────┘  │
│  ┌────────────────────┐ ┌──────────────┐ ┌────────────┐  │
│  │VideoTransformConfig│ │ClipExporter  │ │BatchOrches-│  │
│  │                    │ │+ FFmpeg      │ │trator      │  │
│  └────────────────────┘ └──────────────┘ └────────────┘  │
└──────────────────────────────────────────────────────────┘
```

**Cómo leer este documento**
- Las **vistas UI** son pantallas con las que interactúa el usuario.
- Los **módulos del backend** son piezas de lógica que hacen trabajo concreto.
- Los **modelos de datos** son paquetes de información que viajan entre módulos.
- Los **nombres técnicos** son los nombres en inglés que usaría el código.

---

## Modelo De Datos Conceptual

Esta sección **no describe módulos ejecutables**. Describe las piezas de información que el sistema necesita compartir entre la UI, el parser, la detección, el segmentado y la exportación.

Piensa en esta sección como un inventario de preguntas que el sistema debe poder contestar:
- ¿De qué sesión estamos hablando?
- ¿Qué luces se prendieron y cuándo?
- ¿Qué evento conductual corresponde?
- ¿Qué clip exacto se va a exportar?

| Modelo | Qué representa | Campos clave |
|--------|----------------|--------------|
| `SessionMetadata` | Identidad de una sesión completa. | `scheme`, `initials`, `dateCode`, `phaseCode`, `day`, `rat`, `sex`, `treatment`, `sourceVideoPath`, `sourceBehavioralPath`, tipo de fuente y avisos. |
| `BatchManifest` | Archivo/configuración que completa datos que no vienen en nombres legacy. | `metadataDefaults`, overrides por archivo, ruta conductual explícita, treatment, sex, initials. |
| `SessionCaptureManifest` | Paquete externo futuro `session_manifest_v1.csv`, creado por CajaValentia al coordinar OBS y una sesión conductual. No sustituye el `BatchManifest` interno del lote. | `schemaVersion`, `sessionId`, estado, video, CSV principal, CSV de palanqueos opcional, confirmación OBS, inicio `R0` y fin de sesión. |
| `CameraProfile` | Configuración visual reutilizable para un grupo de sesiones con el mismo encuadre. | `profileId`, sesiones asignadas, crop, rotation, flip, ROIs y calibración. |
| `LightCalibration` | Evidencia usada para escoger y confirmar el umbral de una luz. | `lightId`, frames OFF/ON, medianas de brillo, umbral sugerido, umbral aceptado. |
| `LightSample` | Estado de las tres luces en un frame o tiempo específico. | `frameIndex`, `timeSeconds`, `foodLeft`, `foodRight`, `noiseLed`, brillo por ROI. |
| `LightTransition` | Cambio estable de una luz entre OFF y ON o entre ON y OFF. | `lightId`, `from`, `to`, `frameIndex`, `timeSeconds`, confianza. |
| `BehavioralEvent` | Un evento/fila leído desde CSV V1 o MAT histórico. | `eventIndex`, `side`, `stim`, `eventType`, `leverLatency`, `absoluteTime`, `leftLeverPresses`, `rightLeverPresses`, `crossingLatency`, `crossingTrialIndex` opcional en el CSV actual de CajaValentia. |
| `BehavioralPressEvent` | Presión individual opcional leída de `stem_palanqueos.csv`. | evento de sesión, tiempo, fase, ensayo nullable, tipo textual, lado y contadores raw. |
| `VideoSegment` | Parte lógica de una sesión que el programa propone revisar o exportar como clip. | `segmentCode`, límites visuales, `warningStart`, `foodLightStart`, `matEventIndex`, estimación de desfase, confianza, tipo y resultado. |
| `ExportClip` | Instrucción final para generar un archivo de salida. | `inputVideoPath`, `outputPath`, `segment`, `transformConfig`, `namingMetadata`. |
| `BatchReport` | Evidencia de lo procesado. | sesiones exportadas, bloqueadas o fallidas; clips creados, desfase video-conducta, avisos técnicos, errores y configuración usada. |

La distinción importante es esta:
- `BehavioralEvent` describe lo que registró la fuente conductual
- `VideoSegment` describe lo que se va a cortar del video
- `ExportClip` describe el archivo final que se escribirá en disco
- `CameraProfile` describe qué configuración visual corresponde a un grupo de sesiones

---

## Convención De Nombres Técnicos

Esta sección aclara qué representa cada nombre técnico: un módulo organiza una responsabilidad del backend, una clase implementa parte de esa responsabilidad, un método ejecuta una acción y un modelo de datos transporta información.

| Tipo de cosa | Ejemplo | Qué significa |
|--------------|---------|---------------|
| Módulo | `LightDetection` | Bloque de funcionalidad dentro del backend. |
| Clase | `LightDetector` | Implementación concreta dentro de un módulo. |
| Método | `Analyze(...)` | Acción puntual que ejecuta una clase. |
| Modelo de datos | `LightSample` | Resultado o paquete de información que circula entre módulos. |

Regla práctica:
- `LightDetection` = el módulo
- `LightDetector` = la clase principal dentro de ese módulo
- `Analyze(...)` = el método que corre la detección
- `LightSample` = el dato que sale de esa detección

Las explicaciones del proyecto pueden estar en español, pero los nombres de módulos, clases, métodos y campos internos se mantienen principalmente en inglés para que el código sea consistente.

| Concepto | Nombre técnico | Uso |
|----------|----------------|-----|
| Luz de comida izquierda | `FoodLeft` | ROI/lectura de la luz de comida izquierda. |
| Luz de comida derecha | `FoodRight` | ROI/lectura de la luz de comida derecha. |
| LED de ruido blanco | `NoiseLed` | ROI/lectura del LED asociado al ruido blanco/conflicto. |
| Adaptador de brillo por ROI | `IFrameBrightnessSource` | Interfaz que define cómo obtener brillo promedio sin unir el detector a OpenCV o a la UI. |

---

## Módulos del Backend

Para que todas las secciones se lean igual, cada módulo se describe con este formato:
- **Función en simple:** qué hace en lenguaje directo
- **Input:** qué información necesita para trabajar
- **Output:** qué resultado produce
- **Dependencias:** de qué otro módulo o dato necesita apoyarse
- **Validación/Pruebas:** cómo se puede validar sin correr todo el programa

### Árbol simple de dependencias

```text
NomenclatureParser -> SessionMetadataResolver
VideoReader -> FrameAnalyzer -> BrightnessAdapter -> LightDetection -> LightTimelineBuilder -> BehavioralVideoSynchronizer -> SegmentPlanner -> ClipExporter
BehavioralSourceResolver -> IBehavioralSessionReader ------------------------------^
VideoTransformConfig -----------------------------------------------> ClipExporter
BatchOrchestrator -> coordina todo
```

### Estado Verificado Del Backend (20 De Julio De 2026)

Esta tabla solo resume qué módulos existen. Los resultados de pruebas reales,
archivos de evidencia y el siguiente paso operativo viven en
[Current Project Status](current-status.md), para no mantener dos historiales
detallados que puedan divergir.

| Módulo | Estado | Evidencia o siguiente límite |
|--------|--------|------------------------------|
| `NomenclatureParser` | Implementado | 40 pruebas. Lee los tres esquemas, distingue sesiones fuente de output, omite F1/CM al cargar lotes y construye nombres de output. |
| `SessionMetadataResolver` | Implementado | Completa metadata y resuelve una fuente conductual explícita, CSV V1, MAT legacy o ausencia. |
| `BehavioralData` | Implementado parcialmente | Resuelve rutas, lee el CSV V1 histórico de 9 columnas y el MAT Level-5 binario N×8/N×9 mediante `MatV5MatrixReader`. Falta aceptar el CSV actual de CajaValentia de 10 columnas, MAT HDF5/v7.3 si llegara a usarse y el manifiesto externo de captura. |
| `VideoReader` | Implementado | 20 pruebas con video sintético y runtime nativo de OpenCV en macOS; incluye preview JPEG reducido, validado manualmente en la UI de macOS. |
| `VideoTransformConfig` / `VideoTransformPreviewRenderer` | Implementado | 6 pruebas de perfil y conversión de coordenadas. Conserva crop en píxeles fuente y aplica crop, giro de 180° y espejo al JPEG de preview. `CameraSetupProfileStore` conserva localmente transformación y ROIs por ruta de video en coordenadas fuente, y las convierte al frame preparado solo al medir. El XLSX diagnóstico exporta un perfil versionado reutilizable; falta conectar su importación a la UI y asignarlo a muchas sesiones. |
| `FrameAnalyzer` | Implementado | 20 pruebas. Mide brillo de ROI rectangular o circular y puede devolver el recorte de esa ROI. |
| `LightDetection` | Implementado | 21 pruebas. Convierte brillo ya medido en estados ON/OFF, incluida la desactivación explícita de `NoiseLed` durante `CS`. |
| `BrightnessAdapter` (`FrameAnalyzerBrightnessSource`) | Implementado | 3 pruebas. Recibe un `Mat`, delega la medición a `FrameAnalyzer` y expone el brillo mediante `IFrameBrightnessSource` para `LightDetection`. |
| `LightCalibration` | Implementado y validado manualmente | 3 pruebas. Conserva referencias OFF/ON, calcula medianas y propone un umbral. La UI navega por frames, conserva la evidencia al reabrirse y, si se ajustan ROIs, vuelve a medir los mismos frames antes de actualizar los umbrales. Falta decidir con el lado opuesto si la referencia de comida puede seguir compartiéndose. |
| `LightTimelineBuilder` / `LightTimelineScanner` | Implementados | 11 pruebas. Estabiliza cambios ON/OFF por luz, ignora artefactos aislados y escanea video real aplicando crop, giro, espejo, ROIs y umbrales ya configurados. Puede limitarse a un intervalo de frames y reporta progreso real. |
| `BehavioralVideoSynchronizer` | Implementado como base por sesión | 3 pruebas. Reutiliza la comparación de lado y tiempo para estimar el desfase de una sesión, limita la comparación al rango realmente analizado y devuelve `Ready`, `Warning` o `Blocked`. Informa posibles desajustes; nunca intercambia fuentes ni segmenta. Falta el coordinador que lo ejecute sobre toda una carpeta. |
| `SegmentPlanner` | Implementado y validado para CS | 4 pruebas. Solo crea segmentos desde filas conductuales empatadas; genera eventos, ITIs y habituación cuando el rango cubre el video completo. En `exp_0526_cs_d4r4` validó 67/67 eventos y planeó 135 segmentos. Clasifica por lado anterior: mismo lado = no cruce; cambio = cruce; primer evento = no aplica. Falta `SoundOnly`, UI de resumen y batch. |
| `ClipExporter` | Implementado para un clip CS | 3 pruebas. Exporta un segmento individual mediante FFmpeg, con límites de frames exactos, crop, giro/espejo y escritura temporal segura. Probado con un video sintético real. Falta conectarlo a la UI y al lote. |
| Orquestación | Siguiente etapa para CS | `BatchOrchestrator` aplicará el flujo ya validado a una carpeta de sesiones CS. La futura importación de sesiones de CajaValentia se conecta a la orquestación, no a la UI ni a la detección de luces. |

El backend actual es una base probada, no un pipeline de procesamiento completo. La
UI debe conectarse primero a módulos implementados y no simular que las etapas
planeadas ya existen.

### 1. NomenclatureParser

**Función en simple:** Lee el nombre de un archivo y saca de ahí la información que ya viene escrita.

**Recibe:** un nombre de archivo o ruta.

**Entrega:** una estructura parseada con campos como esquema, fecha, fase, día, rata, sexo, tratamiento o código de segmento, según la nomenclatura detectada.

**Depende de:** solo texto. No necesita video, UI ni `.mat`.

```
Input legacy:  "exp_0126_dis_d9r4.mp4"
Output:        { Scheme="LegacySession", DateCode="0126", PhaseCode="dis",
                 Day=9, Rat=4 }
```

También parsea la nomenclatura estándar del lab y la nomenclatura de output del Video Batch Processor:

```
Input lab:     "abs_2601_f5_d9r4_m_stx.mp4"
Output:        { Scheme="LabStandard", Initials="abs", DateCode="2601",
                 PhaseCode="f5", Day=9, Rat=4, Sex="m",
                 Treatment="stx" }

Input output:  "abs_2601_f5_d9r4_m_e1_p_cr_stx.mp4"
Output:        { Scheme="VideoBatchOutput", Initials="abs", DateCode="2601",
                 PhaseCode="f5", Day=9, Rat=4, Sex="m",
                 SegmentCode="e1", TrialTypeCode="p",
                 ResultCode="cr", Treatment="stx" }
```

Campos principales:
- `Scheme`: qué nomenclatura se detectó (`LegacySession`, `LabStandard`, `VideoBatchOutput`)
- `DateCode`: código de fecha del experimento
- `PhaseCode`: fase o código experimental
- `Day`: día de entrenamiento o prueba
- `Rat`: identificador de la rata
- `Sex`: sexo del animal si la nomenclatura lo incluye
- `SegmentCode`: parte del video (`e1`, `e2`, `iti1`, `hab`, etc.), asignada por el programa al crear un clip de output
- `TrialTypeCode`: tipo de evento del clip de output (`s`, `p`, etc.)
- `ResultCode`: resultado del evento (`cr`, `nc`, `to`, `na`)
- `Treatment`: tratamiento (`stx`, `dzp`, etc.)

**Prueba aislada:** Sí. Se prueba con strings de entrada y salidas esperadas.

---

### 2. SessionMetadataResolver

**Función en simple:** Toma lo que el parser entendió del nombre y completa los datos faltantes de la sesión.

**Recibe:** salida de `NomenclatureParser`, configuración del lote (`BatchManifest`), ruta del video y, cuando exista, información capturada en frontend.

**Entrega:** un `SessionMetadata` lo más completo posible.

**Depende de:** `NomenclatureParser` y datos opcionales del usuario.

```
Resolve(parsedName, batchManifest, videoPath) -> SessionMetadata
```

Su trabajo práctico es:
1. reconocer qué esquema de nombre se está usando
2. confiar en lo que ya viene en el nombre
3. completar lo que falta con defaults del lote o datos dados por el usuario
4. dejar marcados como pendientes los datos que no existan

En otras palabras: el `NomenclatureParser` solo lee nombres; el `SessionMetadataResolver` arma la ficha final de la sesión.

Si el usuario no quiere o no puede proporcionar ciertos datos, este módulo debe poder devolver metadata parcial con warnings, en lugar de bloquear todo el flujo.

**Prueba aislada:** Sí. Se prueba con nombres y manifests sintéticos.

---

### 3. VideoReader

**Función en simple:** Abre un video y permite leer sus frames y metadatos.

**Recibe:** ruta de un archivo de video.

**Entrega:** acceso a FPS, resolución, duración y frames específicos.

**Depende de:** la biblioteca que realiza la lectura de video. Actualmente OpenCvSharp.

```
Open(path) -> VideoHandle
GetMetadata() -> { Fps, Width, Height, Duration, Codec }
GetFrame(index) -> Bitmap
GetFrameAtTime(seconds) -> Bitmap
GetTotalFrames() -> int
```

**Alcance:** No hace procesamiento, solo lectura. La implementación actual usa OpenCvSharp. FFmpeg sigue planteado para la etapa de exportación.

**Estado actual:** ya existe una primera implementación en `src/VideoBatchProcessor.Core/VideoReader`. Sus pruebas pasan en macOS con `./scripts/test-macos.sh`.

**Prueba aislada:** Sí. Con un solo video de prueba se valida.

---

### 4. FrameAnalyzer

**Función en simple:** Mide el brillo de regiones concretas de un frame y puede devolver el recorte de esas regiones para inspección.

**Recibe:** un frame y una o más ROIs de luz.

**Entrega:** una medición visual por ROI, como su brillo promedio, y opcionalmente el recorte de esa ROI.

**Depende de:** frames reales obtenidos por `VideoReader` o por cualquier otra fuente de imagen.

```
Analyze(frame, rois, frameIndex, timestamp) -> FrameAnalysisResult
AnalyzeSingle(frame, roi) -> RoiAnalysisResult
ValidateRois(rois, frameWidth, frameHeight) -> invalidRois[]
```
La selección de crop y de luces viene de la UI. Este módulo no decide esas regiones; este módulo las aplica sobre los píxeles reales.

Aquí vive la lógica base para:
- medir las regiones de las tres luces que el usuario marcó
- comprobar si una ROI quedó fuera del frame
- obtener un crop de una ROI cuando la revisión lo necesite

**Estado actual:** `FrameAnalyzer` ya existe y está probado. El crop, rotación y
flip de la **sesión completa** pertenecen a `VideoTransformConfig` y a la futura
exportación; todavía no son métodos de este módulo.

**Prueba aislada:** Sí. Opera sobre cualquier imagen, no requiere video completo.

---

### 5. LightDetection

**Función en simple:** Decide si cada una de las tres luces está prendida o apagada en un momento dado.

**Recibe:** brillo medido en las ROIs de `FoodLeft`, `FoodRight` y `NoiseLed`, junto con umbrales y referencia de frame/tiempo.

**Entrega:** un `LightSample`, es decir, una lectura de estado de luces para ese frame.

**Depende de:** una fuente de brillo por ROI. Con frames reales, esa fuente será
el `BrightnessAdapter`, que usa `FrameAnalyzer`.

En simple: este módulo responde la pregunta "en este frame, ¿qué luces están ON y cuáles OFF?".

```
LightDetectionConfig = {
  FoodLeft:  LightRoi,
  FoodRight: LightRoi,
  NoiseLed:  LightRoi,
}

LightDetector.Analyze(frameBrightnessSource, frameIndex, timeSeconds) -> LightSample
  LightSample = {
    FrameIndex:  int,
    TimeSeconds: float,
    FoodLeft:    LightReading,
    FoodRight:   LightReading,
    NoiseLed:    LightReading,
  }
```

`LightReading` conserva:
- el brillo medido
- el umbral usado
- el estado booleano final (`IsOn`)

El brillo puede venir de un adaptador como `IFrameBrightnessSource`, que permite probar el detector con datos sintéticos o conectarlo después a OpenCV.

La UI no debe pedir un único frame con las tres luces ON: `FoodLeft` y
`FoodRight` no se encienden al mismo tiempo. Para la primera calibración basta
con dos referencias: un frame donde las tres luces estén OFF y un frame activo.
En `CS` (`FoodOnly`), el frame activo requiere una luz de comida; el LED queda
desactivado para esa sesión y no produce transiciones. En `CP` o `DIS`
(`FoodAndNoise`), el frame activo requiere una luz de comida y `NoiseLed`.
El usuario indica cuál lado de comida está encendido. Esa luz y, cuando aplica,
el LED reciben una referencia directa; la otra luz de comida usa
provisionalmente el mismo umbral y se etiqueta como referencia compartida hasta
validarla con video real.

`LightCalibration` propone un umbral entre las medianas de brillo de ambos
grupos. La primera interfaz lo guarda como umbral inicial; el ajuste manual del
umbral se añadirá después de validar esta base con sesiones reales. La
calibración de `NoiseLed` necesita zoom porque su ROI es pequeña.

**Estado actual:** `LightDetector` y `BrightnessAdapter` están implementados y
probados juntos con frames sintéticos de OpenCV. `FrameAnalyzerBrightnessSource`
recibe un `Mat`, convierte puntualmente `LightRoi`/`LightId` a
`RoiDefinition`/`TipoLed` y expone el brillo mediante `IFrameBrightnessSource`;
no reimplementa el cálculo de brillo. Falta validarlo con una sesión real de la
caja y referencias OFF/ON elegidas por el investigador.

**Prueba aislada:** Sí. Con fuentes sintéticas de brillo se prueba sin abrir video ni UI.

---

### 6. LightTimelineBuilder

**Función en simple:** Convierte muchas lecturas frame por frame en una historia más estable de encendidos y apagados reales.

**Recibe:** una secuencia ordenada de `LightSample`. `LightTimelineScanner` es
el adaptador que abre un video, aplica la configuración de cámara y produce
esas muestras reales antes de llamar al builder.

**Entrega:** un `LightTimeline`, que es un resumen ordenado de transiciones relevantes.

**Depende de:** `LightDetection`.

```
LightTimelineBuilder.Build(samples[]) -> LightTimeline

LightTimelineScanner.Scan(video, transformConfig, lightConfig, scanRange?)
  -> LightTimelineScanResult

LightTimeline = {
  SamplesAnalyzed: int,
  Transitions:     LightTransition[]
}
```

La idea práctica es esta:
1. revisar muchas lecturas seguidas de cada luz
2. ignorar parpadeos o ruido de muy corta duración
3. registrar solo cambios estables: cuándo una luz realmente prendió y cuándo realmente se apagó

Eso evita que un frame brillante aislado se interprete como un evento real.

Cada `LightTransition` conserva el frame y tiempo donde comenzó el cambio
candidato, además del frame que confirmó su estabilidad. Así no desplaza el
inicio visual artificialmente tres frames hacia adelante solo por esperar la
confirmación.

**Estado actual:** implementado. El builder tiene pruebas aisladas para ON/OFF,
artefactos de un frame, independencia de las tres luces y orden de muestras.
El scanner tiene pruebas con video MJPEG sintético que confirman transiciones
reales después de aplicar las mismas transformaciones usadas por la calibración,
el respeto de un intervalo seleccionado y el progreso hasta 100%. La interfaz
ya permite ejecutar el escaneo una vez que ROIs y calibración están guardadas,
seleccionando el intervalo por tiempo o por frames. `LightEventIntervalBuilder`
empareja los cambios ON→OFF de cada luz para exportar periodos visuales
revisables. `LightTimelineDiagnosticExcelExporter` incluye esos periodos, los
cambios raw, las ROIs y la fuente conductual disponible.
`LightTimelineDiagnosticComparer` estima un desfase propio de la sesión con
coincidencias repetidas de lado y tiempo. Deja explícitas las señales visuales
sin fuente conductual compatible y las filas conductuales sin señal visual, en
vez de desplazar toda la tabla por posición. `BehavioralVideoSynchronizer`
reutiliza ese resultado antes de segmentar y considera solo las filas cuya hora
visual estimada cae dentro del rango realmente analizado. La primera validación
real con `exp_0526_cs_d4r4` recuperó los 28 eventos MAT del intervalo de diez
minutos y aisló una señal breve durante habituación.

**Prueba aislada:** Sí. Con secuencias sintéticas de `LightSample` y un video
sintético pequeño.

---

### 7. BehavioralVideoSynchronizer

**Función en simple:** Antes de recortar nada, confirma si las luces detectadas
en un video y las filas conductuales que lo acompañan tienen evidencia temporal
suficiente para trabajar juntas.

**Recibe:** intervalos visuales de comida, eventos normalizados de MAT/CSV,
rango de frames realmente analizado, FPS y la ruta de la fuente conductual.

**Entrega:** un resultado de sincronización con desfase estimado, empates,
anomalías y un estado: `Ready`, `Warning` o `Blocked`.

**Depende de:** `LightTimelineBuilder`/`LightEventIntervalBuilder` y
`BehavioralData`. No abre videos ni archivos: recibe la evidencia ya leída y
normalizada para que pueda probarse de forma aislada.

```text
LightEventInterval[] + BehavioralEvent[] + scanRange + fps
  -> BehavioralVideoSynchronizer
  -> { status, offset, matches, findings }
```

Reglas actuales:
1. usa solo luces de comida y el lado `0`/`1` para buscar un patrón temporal
   repetido;
2. estima un desfase propio de la sesión, sin usar un offset fijo;
3. al analizar solo una parte del video, no castiga filas que están después de
   ese intervalo;
4. bloquea si no hay evidencia, no hay patrón estable o no hay ningún empate;
5. avisa si hay señales extra, filas sin señal o todavía hay muy pocos empates;
6. nunca renombra, intercambia MAT/CSV ni toma una decisión experimental.

Un resultado `Ready` significa que hay al menos tres empates y ninguna
discrepancia dentro del rango revisado. `Warning` conserva los avisos, pero la
sesión puede planearse y exportarse con ese estado registrado. `Blocked` impide
el procesamiento automático de esa sesión hasta que se corrija su pareja de
archivos o su calibración.

**Prueba aislada:** Sí. Hay pruebas para una sesión consistente, una señal
visual extra y un patrón incompatible que sugiere una posible asociación errónea.

---

### 8. SegmentPlanner

**Función en simple:** Construye los clips a partir del inventario conductual
real y usa el video para validarlos, ubicarlos en el reloj visual y conservar
sus límites observables. Una luz aislada nunca crea por sí sola un ensayo.

**Recibe:** `BehavioralEvent[]`, intervalos de luces, resultado no bloqueado de
`BehavioralVideoSynchronizer`, metadatos del video y reglas de segmentación.

**Entrega:** `VideoSegment[]`.

**Depende de:** un `BehavioralVideoSynchronizationResult` que no esté bloqueado,
`LightTimelineBuilder` y, cuando exista, `IBehavioralSessionReader`.

Ojo importante:
- `LightTimeline` **no es un módulo**, es un dato construido por `LightTimelineBuilder`
- `BehavioralEvent` **no es un módulo**, es un dato leído por una fuente conductual

```
Input:  BehavioralEvent[] + matched visual intervals + synchronization + SegmentConfig
Output: VideoSegment[]

VideoSegment = {
  StartFrame:         int,
  EndFrame:           int,
  Side:               enum { Left, Right, None },
  SegmentCode:        string,  // e1, e2, iti1, hab, habini, habfin
  TrialType:          enum { Safe, Conflict, SoundOnly, ITI, Habituation },
  Result:             enum { Crossing, NoCrossing, Timeout, NotApplicable },
  BehavioralEventIndex:int?,   // evento/fila correspondiente de la fuente conductual
  LeverLatencySeconds: float?, // columna Latencia / latencia_s
  CrossingLatencyMat: float?,  // Desplaz raw, conservado para trazabilidad
  PreviousKnownSide:  enum?,   // último Lado válido antes del evento: Left o Right
  SideChanged:        bool?,   // current Side difiere de PreviousKnownSide
  WarningStart:       int?,    // frame donde prende NoiseLed, si aplica
  FoodLightStart:     int?,    // frame donde prende FoodLeft/FoodRight
  FoodLightEnd:       int?,    // frame donde se apaga la luz de comida
  WarningToFoodSeconds: float?, // segundos entre NoiseLed y luz de comida
  BehavioralPressSeconds: float?,  // TiempoAbs / tiempo_absoluto_s
  BehavioralStartEstimateSeconds: float?, // tiempo absoluto - latencia
  VideoBehavioralStartGapSeconds: float?, // inicio visual - inicio conductual estimado
  PostPressLightTailSeconds: float?, // fin visual - tiempo de palanqueo conductual
  DurationSeconds:    float,
}
```

#### Regla Rectora: Qué Decide Cada Fuente

1. **MAT/CSV decide el inventario conductual:** qué eventos existieron, su
   orden, lado, tipo, palanqueo, desplazamiento y candidato a cruce/no cruce.
2. **El video decide la evidencia visual:** cuándo se ve el LED, cuándo prende
   y apaga la comida, y cuál es el desfase propio de esa sesión.
3. **El sincronizador conecta ambos relojes:** traduce cada inicio conductual
   estimado al reloj del video y exige coincidencias repetidas de lado y tiempo.
4. **El planner crea un clip de evento solo para una fila conductual validada.**
   Una señal visual sin fila compatible queda como hallazgo; nunca se convierte
   en un ensayo ni desplaza la tabla por orden.
5. **El resultado del lote se decide por lado:** con un lado previo válido,
   mismo lado significa no cruce y cambio de lado significa cruce. `Desplaz`
   se conserva como evidencia raw, pero no cambia esa etiqueta automática.

Por eso los clips no se recortan "solo desde las luces" ni "solo desde el
MAT": el MAT/CSV define qué debe existir y el video aporta el desfase y los
límites visibles que rodean al evento.

Decisiones principales que toma este módulo:
1. parte de las filas MAT/CSV ya validadas; no deriva la lista de ensayos desde
   encendidos de luz aislados
2. traduce cada fila al reloj del video con el desfase de esa sesión y la ancla
   a su evidencia visual compatible
3. conserva `FoodLightStart`/`FoodLightEnd` y, en riesgo, `WarningStart` para
   que el clip incluya el contexto visual que MATLAB no registra directamente
4. genera `ITI` entre el final visual de un evento y el inicio visual del
   siguiente; los ITIs no se descartan automáticamente
5. genera habituación inicial solo si el análisis cubre el inicio real del
   video; genera habituación final solo si cubre el final real
6. marca como aviso cualquier luz que permanezca encendida dentro de la
   habituación final esperada, en vez de ocultarla
7. produce una lista trazable para exportación por lote

Para eventos con comida, `FoodLightStart` es la referencia visual para comparar
con el inicio MATLAB estimado; no se debe asumir que ambos ocurren en el mismo
instante. También se mide cuánto tiempo queda prendida la luz después del
palanqueo registrado en `TiempoAbs`. El LED de ruido conserva el inicio visual
de advertencia del mismo evento. Las diferencias por sesión se resumen con una
medida robusta como la mediana; nunca se resta un delay fijo supuesto para todos
los videos. La especificación completa está en
[Sincronización video-conducta de CajaValentia](sincronizacion-video-mat-cajavalentia.md).

**Lógica de segmentación:**
1. Cada fila MAT/CSV compatible genera un candidato; si no tiene ancla visual,
   se reporta y no se exporta automáticamente.
2. En eventos seguros, la luz de comida ON/OFF delimita la evidencia visual.
3. En riesgo/conflicto, un `NoiseLed` compatible antes de comida adelanta el
   inicio del clip para conservar LED/ruido previo; la fila conductual sigue
   definiendo la identidad del evento.
4. Si un `BehavioralEvent` tiene `EventType=SoundOnly` (`tipo_evento=2`), el
   ancla será `NoiseLed`; ese camino se implementa después de validar videos
   reales de solo sonido.
5. El final del evento con comida conserva `FoodLightEnd`, aunque el palanqueo
   MATLAB haya ocurrido antes; esa diferencia es parte de la evidencia.
6. Entre el final visual de un evento y el inicio del siguiente se genera `ITI`.
7. Desde inicio de video al primer evento validado se genera habituación inicial;
   desde el último fin visual a fin de video se genera habituación final. La
   regla de luces apagadas se verifica como calidad, no se asume ciegamente.
8. El primer ensayo siempre es seguro/de comida; sirve como contexto del
   protocolo, no sustituye la sincronización ni la detección.
9. Para cruce/no cruce/timeout: después del primer evento con lado conocido,
   mismo `Lado` significa no cruce y cambio de `Lado` significa cruce. El primer
   evento queda como `No aplica` porque no hay lado anterior; `Lado=-2` es
   timeout. `Desplaz` y latencia de palanqueo se conservan como datos raw, pero
   no bloquean ni cambian la clasificación automática del lote.

**Timing:** en ensayos de riesgo/conflicto, el clip puede empezar en `WarningStart` para conservar el LED/ruido previo. `FoodLightStart` y `FoodLightEnd` son límites visuales que se comparan con el inicio MATLAB estimado y `TiempoAbs`; no se asumen idénticos. En un evento `SoundOnly`, `WarningStart` es el inicio relevante y `FoodLightStart` queda vacío.

**Estado actual:** la primera versión ya recibe empates de sincronización y
planea eventos con comida, ITIs y habituación solo cuando el rango cubre el
inicio/final real. En la validación completa CS de `exp_0526_cs_d4r4` produjo
1 habituación inicial, 67 eventos, 66 ITIs y 1 habituación final a partir de
67/67 empates MAT-video. El exportador diagnóstico deja esa propuesta en la
hoja `Segmentos planeados`, con duración total y comparación de lado visible,
para revisión antes de generar clips. `SoundOnly`,
asociación LED de riesgo con videos reales, conexión UI de revisión y
exportación siguen fuera de esta primera versión.

**Prueba aislada:** Sí. Hay pruebas para habituación/eventos/ITI/final, rango
parcial, sincronización bloqueada y clasificación por comparación de lados.

**Aclaración de nombres:** `LightTimeline` y `BehavioralEvent` son estructuras de datos, no módulos.

---

### 9. BehavioralData

**Función en simple:** Encuentra y lee la información conductual que acompaña a
un video. Puede venir de un CSV nuevo o de un `.mat` histórico, pero el resto
del programa recibe siempre los mismos datos normalizados.

**Recibe:** ruta del video y, opcionalmente, una ruta indicada explícitamente
por el usuario o el manifest.

**Entrega:** una `BehavioralSourceResolution` y, cuando se lee la fuente, un
`BehavioralSessionData` con `BehavioralEvent[]` y presiones opcionales.

```
videoPath + explicitSourcePath?
  -> BehavioralSourceResolver
  -> IBehavioralSessionReader
  -> BehavioralSessionData
```

#### Resolución de fuente

La prioridad es deliberada y debe ser visible para el usuario:

1. ruta explícita (`BehavioralSourcePath`; `MatPath` sigue como alias legacy);
2. `stem.csv` si existe y tiene el encabezado exacto CSV V1;
3. `stem.mat` legacy si existe;
4. fuente ausente.

`stem_palanqueos.csv` nunca se toma como tabla principal. Solo se asocia como
evidencia hermana después de resolver una fuente principal. Si el CSV principal
tiene encabezado válido pero una fila corrupta, el lector reporta el error: no
cae silenciosamente al `.mat`. Si el encabezado CSV no es V1, el resolver puede
usar el MAT disponible, dejando un warning auditable.

#### Modelo normalizado

```text
BehavioralEvent = {
  EventNumber,
  Side,                       // 1=izquierda, 0=derecha, -2=timeout
  Stimulus,
  LeverLatencySeconds,
  AbsoluteTimeSeconds,
  LeftLeverPresses,
  RightLeverPresses,
  CrossingLatencySeconds,
  EventType,                  // SafeFood, ConflictWithFood o SoundOnly
  RawValues,
}
```

La regla temporal permanece igual para ambas fuentes:

```text
inicio MATLAB estimado = tiempo absoluto - latencia de palanqueo
```

No se usa `6.77 s` ni otro offset universal. Para `SoundOnly` (`tipo_evento=2`),
la referencia visual es `NoiseLed`; no se exige luz de comida.

#### Lectores

- `CsvV1BehavioralSessionReader` está implementado. Exige exactamente las nueve
  columnas acordadas, usa punto decimal con cultura invariante, preserva orden y
  valores raw. También lee el CSV de palanqueos si está disponible y conserva
  `ensayo=NA` como valor ausente.
- `LegacyMatBehavioralSessionReader` usa `MatV5MatrixReader` para leer MAT
  Level-5 sin MATLAB ni modificar el archivo. Extrae `Resultados` o una única
  matriz N×8/N×9, que `LegacyMatEventMapper` normaliza: para N×8 deriva el tipo
  desde `Estim`; para N×9 usa `TipoEvento`. MAT HDF5/v7.3 no forma parte aún de
  este alcance.

La definición exacta de las columnas, CSV V1 y palanqueos vive en el
[handoff de CajaValentia](handoff-cajavalentia-csv-backend.md) y en
[Formato MAT histórico](../reference/mat-format.md). Este documento define
cómo se conectan esos datos con el producto, sin duplicar toda la especificación.

**Prueba aislada:** Sí. Hay pruebas para prioridad de rutas, CSV válido y
malformado, punto decimal bajo cultura española, `lado=-2`, `tipo_evento=2`,
`NA` en palanqueos y normalización de matrices MAT N×8/N×9.

---

### 10. VideoTransformConfig / VideoTransformPreview

**Función en simple:** Guardar y previsualizar cómo se debe transformar el video sin generar todavía el clip final.

**Recibe:** decisiones del usuario sobre crop, giro opcional de 180°, espejo y
calidad.

**Entrega:** una configuración reutilizable para preview y exportación dentro de un `CameraProfile`.

**Depende de:** `VideoReader`, una futura implementación de preview de
transformaciones y `ClipExporter` en la etapa de salida. No depende de
`FrameAnalyzer`, cuyo alcance es medir ROIs de luces.

```
TransformConfig = {
  CropRect: { x, y, width, height },
  Rotation: None | UpsideDown,
  Flip,
  OutputQuality,
}
```

La UI necesita mostrar crop/giro de 180°/espejo como preview. Internamente el
crop se conserva como `x`, `y`, `width`, `height`; para el usuario se muestran
sus dos esquinas mediante los límites izquierda, arriba, derecha y abajo. El
flujo normal de exportación debe aplicar
los límites del segmento + crop + rotate + flip en un solo comando FFmpeg por
clip cuando sea posible. Así se evita recodificar primero el video completo y
después volver a recodificar cada clip.

Un `CameraProfile` agrupa esta transformación, las ROIs y su calibración. El
usuario puede crear otro perfil desde la primera sesión donde cambió el encuadre
y asignarlo a las sesiones posteriores. No se asume un número fijo de cambios de
cámara ni que un perfil sirva para todo el protocolo.

**Estado actual:** `VideoTransformConfig` y `VideoTransformPreviewRenderer` ya
existen en `VideoBatchProcessor.Core`. La configuración conserva el recorte en
píxeles del video fuente y aplica, en este orden, crop, giro de 180° y espejo a
un JPEG de preview. La interfaz usa esta misma ruta y no transforma la imagen
solo con JavaScript. El crop se elige en un modal amplio con Cropper.js 1.6.2
local: el video se ajusta completo y fijo al área de trabajo, mientras el
usuario ajusta el marco o los límites de sus dos esquinas. La aplicación ya
conserva localmente transformación y ROIs por ruta de video en coordenadas del
video fuente. El XLSX de diagnóstico incluye una hoja `Perfil de camara`
versionada con esas mismas decisiones, dimensiones, crop, giro, espejo y
umbrales. Falta conectar la importación de ese perfil y asignarlo a varias
sesiones de un mismo encuadre.

Un módulo `VideoCropRotate` puede existir como helper opcional para previews o casos especiales, pero no debe ser el camino principal del batch.

**Prueba aislada:** Sí. Con frames o videos cortos de prueba.

---

### 11. ClipExporter

**Función en simple:** Toma un plan de clips y escribe los archivos finales de video.

**Recibe:** una solicitud `ClipExportRequest`: video fuente, metadata,
segmento planeado, transformación, ruta de salida y opciones de FFmpeg.

**Entrega:** archivos de salida ya exportados.

**Depende de:** `SegmentPlanner`, `VideoTransformConfig` y la capa de exportación.

```
ExportAsync(clipExportRequest) -> ClipExportResult
```

Cada `ExportClip` incluye video de entrada, segmento, configuración de transformaciones y nombre final. Usa FFmpeg con `-ss`/`-to` sobre el reloj del video fuente y filtros de crop/rotación/flip en un solo paso por clip. Nombra cada archivo según la nomenclatura de output del Video Batch Processor.

Esta primera versión recibe la metadata que ya leyó `VideoReader` y ejecuta
FFmpeg como proceso interno. La detección y empaquetado de FFmpeg/ffprobe para
macOS y Windows será responsabilidad de la aplicación antes del primer release;
no será una tarea de la interfaz ni del investigador.

El perfil inicial previsto para clips destinados a DLC es H.264 (`libx264`) con
`CRF 18`, sujeto a validación con videos reales. No se debe aplicar padding ni
un trim global fijo a todas las sesiones: `-ss` y `-to` representan exactamente
los límites del segmento planeado, incluida la decisión explícita sobre
habituación final. Cada clip se escribe primero como archivo temporal y solo se
renombra como salida final si FFmpeg termina correctamente.

Al finalizar, el exportador entrega al `BatchReport` una entrada por clip con
video fuente, MAT/CSV asociado, perfil de cámara, desfase de la sesión,
segmento y archivo creado. Así un lote puede auditarse o reproducirse después.

Para un evento `SoundOnly`, el exportador no debe inventar `s`, `p` o `na` en
el campo de tipo. El código corto de output sigue pendiente de acuerdo del lab;
hasta entonces debe conservar el segmento para revisión y emitir un warning en
lugar de exportarlo con una etiqueta falsa.

**Estado actual:** listo para exportar un clip CS individual. Se comprobó con
un video de tres segundos: exportó exactamente 30 frames (1 s) y aplicó un
crop de 160×120 píxeles junto con giro/espejo.

**Prueba aislada:** Sí. Hay pruebas para límites exactos, filtros de cámara y
limpieza de salida parcial si FFmpeg falla.

---

### 12. BatchOrchestrator

**Función en simple:** Coordina todo el pipeline de principio a fin para una carpeta de videos.

**Recibe:** una configuración de procesamiento por lote.

**Entrega:** un `BatchReport`.

**Depende de:** todos los módulos anteriores.

```
Run(config) -> BatchReport
  config = {
    InputDir,
    OutputDir,
    CameraProfiles: CameraProfile[],
    BatchManifestPath,            // configuración interna de este producto
    SessionCaptureManifestPath?,  // paquete externo futuro de CajaValentia
    HabituationConfig: { TargetFinal, WarnShortFinal },
  }
```

Flujo:
1. escanea la carpeta y encuentra videos CS candidatos
2. parsea nombres con `NomenclatureParser` y completa metadata con
   `SessionMetadataResolver` y `BatchManifest`
3. resuelve exactamente una fuente conductual por video: una ruta explícita
   gana; si no existe, busca el stem correspondiente. Sin fuente o con más de
   una candidata, marca la sesión `Blocked`; nunca adivina una pareja.
4. asigna el `CameraProfile` confirmado y lee metadata con `VideoReader`
5. obtiene brillo por ROI con `FrameAnalyzer`, lo entrega mediante el
   `BrightnessAdapter` y detecta luces con `LightDetection`
6. construye transiciones estables con `LightTimelineBuilder` y lee el MAT/CSV
   con `IBehavioralSessionReader`
7. usa `BehavioralVideoSynchronizer` como puerta de calidad: conserva warnings
   y bloquea asociaciones sin evidencia suficiente
8. planea segmentos con `SegmentPlanner` solo para sesiones no bloqueadas
9. exporta automáticamente los segmentos CS planeados con `ClipExporter`
10. continúa con la siguiente sesión si una falla y genera un `BatchReport`
    con estados `Exported`, `ExportedWithWarnings`, `Blocked` o `Failed`

**Prueba aislada:** Parcial. Se puede validar con mocks, pero su valor real aparece al integrar todo.

---

### Integración Futura: CajaValentia, OBS Y Sesión Capturada

**Función en simple:** Recibir una sesión que CajaValentia ya terminó de
grabar, comprobar que sus archivos pertenecen juntos y entregarla al pipeline
normal. Este producto no controla OBS ni la caja; solo consume el paquete
cerrado.

**Recibe:** una ruta explícita a `session_manifest_v1.csv`, más el perfil de
cámara que se aplicará a ese video.

**Entrega:** una entrada validada para `BatchOrchestrator`: identidad de sesión,
video, CSV conductual principal, CSV de palanqueos opcional y timestamps que
permiten auditar el orden de captura.

**Depende de:** `SessionMetadataResolver`, `BehavioralData`, `VideoReader` y,
después, `BatchOrchestrator`. La futura clase propuesta es
`SessionCaptureManifestReader`; no está implementada todavía.

```text
CajaValentia crea session_id y manifiesto
  -> OBS confirma grabación
  -> CajaValentia crea R0 y ejecuta la tarea
  -> video + CSV + manifiesto status=completed
  -> SessionCaptureManifestReader valida el paquete
  -> BatchOrchestrator ejecuta el pipeline normal
```

Reglas que toda actualización de arquitectura debe conservar:

1. `BatchManifest` es configuración interna del lote; `SessionCaptureManifest`
   es evidencia externa de una sola sesión. Nunca se intercambian ni se usan
   como si fueran el mismo archivo.
2. Solo se procesa automáticamente un manifiesto con `status=completed`, video
   existente y CSV principal existente. Rutas explícitas del manifiesto ganan a
   cualquier búsqueda por nombre.
3. `toc(R0)` y los timestamps visuales se conservan raw. El programa calcula
   diferencias para revisión, pero no reescribe los tiempos conductuales.
4. El lector debe identificar la versión del CSV: mantener soporte histórico
   de 9 columnas y añadir el formato actual de CajaValentia de 10 columnas,
   incluyendo `ensayo_cruce`.
5. Un fallo de OBS, ruta faltante o manifiesto incompleto produce un error
   revisable; nunca un lote que adivina qué video corresponde a qué CSV.

**Prueba aislada:** Sí. Fixtures de manifiesto válido, incompleto, fallido,
rutas cruzadas, CSV de 9/10 columnas y video ausente. Las pruebas iniciales se
harán sin animales.

La especificación completa de este límite entre repositorios vive en
[CajaValentia Session Capture Integration](cajavalentia-session-capture-integration.md).

---

## Arquitectura Del Frontend (Avalonia)

El frontend se divide en bloques de trabajo, no solo en pantallas. Cada bloque
recoge una decisión del usuario, muestra evidencia y entrega información al
siguiente; ninguno interpreta por sí mismo el video o la fuente conductual.

| Bloque UI y vistas | Función en simple | Recibe | Entrega | Conexión con backend |
|--------------------|-------------------|--------|---------|----------------------|
| `SessionSetup` (`VideoLoadView`) | Cargar una carpeta, confirmar qué sesiones se procesarán y mostrar un primer frame de una sesión fuente. | Carpeta elegida y nombres de videos. | Lista de `SessionMetadata`, avisos, grupos de trabajo y preview raw del video seleccionado. | Implementado y validado manualmente en macOS. Usa `NomenclatureParser`, `SessionMetadataResolver` y `VideoReader`. |
| `CameraSetup` (`CropView`, `LightMarkerView`, `LightCalibrationView`) | Preparar cómo se verá y medirá un grupo de videos con el mismo encuadre. | Frame representativo, decisiones de crop/orientación y ROIs. | `CameraProfileDraft`: transformación, ROIs, referencias OFF/ON y umbrales aceptados. | Giro de 180°, espejo, crop en modal y preview transformado ya están integrados por sesión. `LightMarkerView` permite marcar y validar las tres ROIs en coordenadas reales del video preparado; al guardarlas, `CameraSetupProfileStore` conserva transformación y ROIs por ruta de video. `LightCalibrationView` recorre frames, mide referencias OFF/ON reales y conserva umbrales en memoria. Falta una asignación explícita de perfil a muchas sesiones y confirmar con video real si el umbral de comida puede compartirse entre ambos lados. |
| `ProcessingSummary` (`SegmentTimelineView`, `HabituationView`) | Mostrar el plan automático antes de exportar un lote. | Estado de sincronización, segmentos, eventos conductuales, tipo de fuente, advertencias, comparación de lados y duraciones. | Resumen trazable de clips propuestos y avisos técnicos. | Se diseña ahora; primero mostrará `BehavioralVideoSynchronizer` y después se conectará a `SegmentPlanner`. |
| `BatchExport` (`ExportView`) | Ejecutar el lote y mostrar qué se exportó o falló. | Clips confirmados, opciones de salida y progreso. | `BatchReport`, logs y acceso a la carpeta de salida. | Se diseña ahora; se conecta después a `ClipExporter` y `BatchOrchestrator`. |

### Estado De La UI

`CameraProfileDraft` y el resumen temporal del lote son **estado de interfaz**:
existen mientras el usuario configura o consulta el resultado. Solo el perfil
confirmado pasa a configuración reutilizable cuando el backend lo guarda. Esto
evita que la pantalla se convierta en otra fuente de verdad distinta al Core.

### Dirección De Implementación De La Interfaz

El frontend definitivo usa HTML/CSS local como superficie visual dentro del
`NativeWebView` oficial de Avalonia 12. Avalonia sigue siendo la aplicación de
escritorio y C# sigue siendo la fuente de lógica: abre selectores nativos,
llama al Core, procesa video y guarda configuraciones. El HTML solo presenta
pantallas y envía acciones puntuales a C#.

La primera prueba verificada cubre carga de sesiones: HTML solicita archivos,
C# abre el selector nativo, `SessionSetupService` interpreta los nombres y la
lista vuelve a HTML. La interfaz XAML anterior fue retirada: el archivo XAML
solo aloja el `NativeWebView`, mientras que la presentación vive en
`WebUi/index.html`.

### 13. AppShell + SessionSetup

**Función en simple:** Es la puerta de entrada del programa. Presenta el flujo
de trabajo, deja elegir videos o una carpeta y ayuda a confirmar que cada sesión
tiene identidad y fuente conductual reconocibles antes de seguir.

**Recibe:** rutas de videos elegidas por el usuario y, después, los valores que
el usuario complete para metadata faltante.

**Entrega:** una lista revisable de `SessionMetadata`: nombre interpretado,
campos faltantes, fuente conductual resuelta (CSV V1, MAT legacy o ausente) y
sus avisos. No inicia procesamiento de video.

**Depende de:** `NomenclatureParser`, `SessionMetadataResolver` y, de manera
indirecta, `BehavioralSourceResolver`.

**Validación/Pruebas:** cargar nombres legacy, estándar y desconocidos; comprobar
que los campos manuales se actualizan, que CSV/MAT se muestran correctamente y
que una fuente ausente o inválida aparece como aviso, no como un bloqueo oculto.
Si se carga un clip con nomenclatura de output, la UI debe identificarlo pero
rechazarlo como entrada: es evidencia generada, no el video completo de sesión.
Al elegir una carpeta, busca de forma recursiva en sus subcarpetas y omite por
defecto Luz-Comida (`f1`) y Condicionamiento al Miedo (`cm`/`f3`); muestra el
conteo de sesiones omitidas sin confundirlas con errores de nomenclatura.
Cuando detecta más de cinco nombres no compatibles, ofrece quitarlos todos de
la lista del lote. Esa acción nunca borra los archivos físicos del disco.

### 14. CameraSetup

**Función en simple:** Permite escoger un video representativo y decir cómo se
debe ver la caja: orientación, espejo y recorte. Agrupa esas decisiones en un
perfil reutilizable para las sesiones que comparten el mismo encuadre.

**Recibe:** una sesión confirmada, un frame de preview y decisiones del usuario
sobre crop, rotación, espejo y pertenencia a un perfil de cámara.

**Entrega:** `CameraProfileDraft`, una propuesta de configuración visual todavía
editable. No reexporta ni modifica el video fuente.

**Depende de:** `VideoReader` para abrir el video y de la futura capa de
preview de transformaciones. `FrameAnalyzer` puede validar que un crop o ROI
quede dentro del frame.

**Validación/Pruebas:** abrir un video de prueba, cambiar orientación y crop,
confirmar que las coordenadas se conservan y crear dos perfiles para videos con
encuadres distintos.

**Estado actual:** `VideoTransformConfig` ya guarda giro de 180°, espejo y
crop; la interfaz permite elegirlos sobre un preview real y C# devuelve el JPEG
transformado. El recorte se selecciona en un modal amplio: el video se muestra
completo y fijo; el usuario ajusta el marco o los límites izquierda, arriba,
derecha y abajo en píxeles fuente. Ninguna acción
reescribe, renombra o exporta el video fuente. Esta configuración se mantiene
por sesión mientras el lote está abierto.

**Estado de validación:** el flujo mínimo se aprobó manualmente en macOS el
12-07-2026 con una sesión real: giro de 180°, espejo, recorte visual, límites
numéricos y restablecimiento a video completo.

**Estado actual:** `LightMarkerView` ya permite marcar, mover y ajustar las tres
ROIs circulares sobre el preview preparado, con zoom de trackpad o rueda de
mouse para los indicadores pequeños. El modo `Mano` o una pulsación de
`Espacio` alternan el desplazamiento de la imagen; el botón central también
permite navegar sin modificar una ROI. La interfaz las convierte a coordenadas
reales del video preparado y C# las valida con `FrameAnalyzer` antes de
conservarlas para la sesión actual. La medición circular excluye las esquinas
del cuadro envolvente. Cambiar crop, giro o espejo invalida las ROIs anteriores
para evitar aplicar una región en un encuadre distinto.

**Estado actual:** `LightCalibration` ya puede recorrer un video por frame,
mostrar el frame preparado con las tres ROIs superpuestas y guardar las dos
referencias mínimas. La barra usa índices de frame y muestra un tiempo estimado;
C# mide el frame completo tras aplicar crop, giro y espejo, no el JPEG reducido.
El usuario puede regresar al editor de ROIs desde el frame de calibración que
está revisando. Al guardar una ROI, C# conserva los mismos frames OFF/ON,
vuelve a medirlos con las nuevas regiones y actualiza sus umbrales; así no se
pierde la evidencia escogida por el investigador. Cada referencia guarda un
token de esa medición, por lo que al confirmar no se vuelve a buscar un frame
que podría variar según códec. El flujo se validó manualmente en macOS el
13-07-2026; después se guardará un `CameraProfile` reutilizable.

### 15. LightCalibration

**Función en simple:** Deja marcar dónde están las tres luces y escoger umbrales
de encendido/apagado con ejemplos visuales. Su trabajo termina al guardar una
calibración revisable; no decide ensayos ni exporta clips.

**Recibe:** frames ya preparados por `CameraSetup`, tres ROIs, un ejemplo OFF y
un ejemplo activo: solo comida para `CS`, o comida + `NoiseLed` para `CP`/`DIS`.

**Entrega:** ROIs y `LightCalibration` por `FoodLeft`, `FoodRight` y, cuando
aplica, `NoiseLed`, incluidas las referencias usadas para justificar cada
umbral. En `CS`, la ROI del LED se conserva como referencia espacial pero su
lectura queda desactivada. La luz de comida sin ejemplo ON directo queda marcada
como calibración compartida provisional.

**Depende de:** `FrameAnalyzer` para medir las ROIs y `LightDetector` para
probar el umbral. La lectura continua desde video real espera
`BrightnessAdapter`; esta vista no debe recrear esa lógica.

**Validación/Pruebas:** verificar que la barra muestre el frame seleccionado y
que el frame OFF tenga las tres luces apagadas. Para `CS`, confirmar una luz de
comida activa sin exigir LED; para `CP`/`DIS`, confirmar comida y LED activos.
Probar después ambos lados de comida para decidir si la referencia compartida
basta o requiere referencias separadas.

---

## Punto De Partida Para Integrar El Frontend

El frontend puede empezar antes de que el backend esté completo, siempre que
respete el estado real de cada módulo. La primera integración útil no es
"procesar todo": es cargar sesiones, mostrar un frame, capturar ROIs y dejar
lista la configuración que el backend usará después.

### Ruta Que Ya Se Puede Conectar

```text
Carpeta elegida por usuario
  -> NomenclatureParser
  -> SessionMetadataResolver
  -> lista de sesiones para revisión

Video elegido
  -> VideoReader
  -> frame de preview
  -> UI de crop y ROIs
  -> FrameAnalyzer para validar o medir esas ROIs
```

Con `BrightnessAdapter` ya implementado, esa segunda ruta queda así:

```text
frame real -> FrameAnalyzer -> BrightnessAdapter -> LightDetection -> LightSample
```

La UI debe mostrar el resultado, no repetir la detección. La timeline y el
diagnóstico de sincronización ya están conectados; `SegmentPlanner` ya propone
habituación, eventos e ITIs en el XLSX de diagnóstico para CS. El resumen de
esa propuesta en la UI y la exportación de clips siguen pendientes.

### Regla De Separación

Una vista Avalonia recoge decisiones del usuario y muestra resultados. No debe
calcular brillo, interpretar eventos ni asociar filas del `.mat`. Cuando una
operación combine varios módulos, una clase de coordinación fuera de la vista
llamará al Core; más adelante ese papel lo asumirá `BatchOrchestrator`.

### Uso Del Prototipo Actual

`src/LightEventDetector` es un prototipo visual útil: ya demuestra apertura de
video, navegación de frames y dibujo de ROIs. Puede reutilizarse como referencia
de interacción, pero su `LightEventCore` no debe crecer como segundo backend.

Al integrar el frontend definitivo, el proyecto Avalonia deberá referenciar
`VideoBatchProcessor.Core` y sustituir gradualmente las llamadas directas del
prototipo por los módulos Core. Así habrá una sola fuente de lógica para parser,
lectura de video, análisis de frame y detección de luces.

---

## Flujo de uso completo

```
1. Usuario abre el programa
2. Selecciona carpeta con videos -> se listan y agrupan por metadata
3. Para cada perfil: dibuja crop, elige orientación, marca las 3 ROIs y calibra OFF/ON por luz
4. Los parámetros y la asignación explícita de perfiles se guardan en configuración
5. Programa empareja video/MAT, escanea luces, estima el desfase y bloquea solo sesiones inválidas
6. Programa genera segmentos CS: habituación inicial, eventos, ITIs y habituación final
7. Usuario consulta el resumen automático y da clic en "Procesar todo"
8. Pipeline exporta los clips de sesiones no bloqueadas
9. Al terminar: carpeta de salida + reporte
```

---

## Orden Lógico De Implementación

```
Paso 1:  NomenclatureParser      -> independiente
         SessionMetadataResolver -> independiente
         FrameAnalyzer           -> independiente
         VideoReader             -> independiente

Paso 2:  BrightnessAdapter       -> conecta FrameAnalyzer con LightDetection
         LightDetection          -> depende de BrightnessAdapter solo cuando se conecta a frames reales
         LightTimelineBuilder    -> depende de LightDetection
         BehavioralData          -> resolución, CSV V1 histórico y lector MAT Level-5 ya implementados;
                                  falta compatibilidad con el CSV actual de 10 columnas

Paso 3:  BehavioralVideoSynchronizer -> depende de timeline + fuente conductual normalizada;
                                      bloquea o avisa antes de segmentar
         SegmentPlanner          -> depende de sincronización no bloqueada + reglas de segmento
         CameraProfile           -> ya conserva localmente transformaciones y ROIs por video;
                                  falta asignación explícita por grupo de sesiones
         VideoTransformConfig    -> implementado: usa VideoReader y preview de transformaciones

Paso 4:  ClipExporter            -> depende de SegmentPlanner + VideoTransformConfig
         BatchOrchestrator       -> depende de todo lo anterior
         SessionCaptureManifestReader -> valida una sesión terminada de CajaValentia
                                          antes de entregarla a BatchOrchestrator

Paso 5:  UI base                 -> carga, lista, preview, CameraSetup, ROIs y calibración inicial integrados y validados
                                  falta validar la referencia compartida con el lado opuesto
         UI de timeline/diagnóstico -> integrada; falta consumir el estado del
                                  sincronizador como resumen de procesamiento
         UI de segmentación/export -> espera SegmentPlanner, ClipExporter y
                                  BatchOrchestrator
         Pruebas con datos reales
```

Cada módulo implementado debe tener pruebas unitarias y poder revisarse antes de
continuar con el siguiente. Los módulos planeados todavía no tienen código ni
pruebas.

---

## Archivos de configuración

El programa guarda/lee un archivo YAML o JSON con la configuración de cada lote:

```yaml
# config_video_batch.yaml
input_dir: "/videos/exp_0126_dis/"
output_dir: "/videos/procesados/"
batch_manifest_path: "/videos/exp_0126_dis/batch_manifest.yaml"
# Futuro: ruta explícita al paquete de una sesión terminada por CajaValentia.
# session_capture_manifest_path: "/sesiones/cv-20260713/session_manifest_v1.csv"
metadata_defaults:
  initials: "abs"
  sex: "m"
  treatment: "stx"
camera_profiles:
  - id: "center-camera"
    assigned_sessions:
      - "exp_0126_cs_d1r1"
      - "exp_0126_cs_d1r2"
    crop: { x: 50, y: 30, width: 900, height: 500 }
    rotation: 0
    flip: none
    lights:
      food_left:  { x: 120, y: 80, width: 20, height: 20, threshold: 180 }
      food_right: { x: 780, y: 80, width: 20, height: 20, threshold: 180 }
      noise_led:  { x: 450, y: 120, width: 12, height: 12, threshold: 160 }
    calibration:
      food_left:  { off_frames: [0, 15], on_frames: [120, 135], accepted_threshold: 180 }
      food_right: { off_frames: [0, 15], on_frames: [240, 255], accepted_threshold: 180 }
      noise_led:  { off_frames: [0, 15], on_frames: [360, 375], accepted_threshold: 160 }
detection:
  min_on_frames: 3
  min_off_frames: 3
habituation:
  target_final_seconds: 300
  warn_short_final_seconds: 180
use_mat_parser: true
export:
  include_events: true
  include_itis: true
  include_habituation: true
  include_warning_period: true
  segment_padding_seconds: 0
  quality: "research_archive"
```

Los perfiles se asignan explícitamente a sesiones o a un grupo confirmado por
el usuario. Nunca se infieren por el orden de archivos ni se extienden a otro
protocolo sin una asignación visible en la UI.

Esto permite reprocesar sin tener que configurar cada vez.
