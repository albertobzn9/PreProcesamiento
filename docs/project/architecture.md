# Video Batch Processor — Propuesta de Solución

## Stack tecnológico

| Capa | Tecnología |
|------|-----------|
| UI | Avalonia UI (cross-platform: Windows + macOS) |
| Backend | C# (.NET 10, target actual del repo) |
| Video I/O actual | OpenCvSharp |
| Exportación de video prevista | FFmpeg (via FFmpeg.AutoGen o proceso externo) |
| Imágenes | OpenCvSharp / SkiaSharp según el adaptador |
| Archivos `.mat` | Librería para leer MATLAB `.mat` (CSV export o librería .NET) |

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
│  │MatParser     │ │SegmentPlanner      │ │ClipExporter│  │
│  │              │ │                    │ │+ FFmpeg    │  │
│  └──────────────┘ └────────────────────┘ └────────────┘  │
│  ┌────────────────────┐ ┌─────────────────────────────┐  │
│  │VideoTransformConfig│ │BatchOrchestrator            │  │
│  │                    │ │                             │  │
│  └────────────────────┘ └─────────────────────────────┘  │
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
- ¿Qué evento del `.mat` corresponde?
- ¿Qué clip exacto se va a exportar?

| Modelo | Qué representa | Campos clave |
|--------|----------------|--------------|
| `SessionMetadata` | Identidad de una sesión completa. | `scheme`, `initials`, `dateCode`, `phaseCode`, `day`, `rat`, `sex`, `treatment`, `sourceVideoPath`, `sourceMatPath`. |
| `BatchManifest` | Archivo/configuración que completa datos que no vienen en nombres legacy. | `metadataDefaults`, overrides por archivo, ruta del `.mat`, treatment, sex, initials. |
| `CameraProfile` | Configuración visual reutilizable para un grupo de sesiones con el mismo encuadre. | `profileId`, sesiones asignadas, crop, rotation, flip, ROIs y calibración. |
| `LightCalibration` | Evidencia usada para escoger y confirmar el umbral de una luz. | `lightId`, frames OFF/ON, medianas de brillo, umbral sugerido, umbral aceptado. |
| `LightSample` | Estado de las tres luces en un frame o tiempo específico. | `frameIndex`, `timeSeconds`, `foodLeft`, `foodRight`, `noiseLed`, brillo por ROI. |
| `LightTransition` | Cambio estable de una luz entre OFF y ON o entre ON y OFF. | `lightId`, `from`, `to`, `frameIndex`, `timeSeconds`, confianza. |
| `MatEvent` | Un evento/fila leído desde el `.mat`. | `eventIndex`, `side`, `stim`, `eventType`, `leverLatency`, `absoluteTime`, `leftLeverPresses`, `rightLeverPresses`, `crossingLatency`, `result`. |
| `VideoSegment` | Parte lógica de una sesión que el programa propone revisar o exportar como clip. | `segmentCode`, límites visuales, `warningStart`, `foodLightStart`, `matEventIndex`, estimación de desfase, confianza, tipo y resultado. |
| `ExportClip` | Instrucción final para generar un archivo de salida. | `inputVideoPath`, `outputPath`, `segment`, `transformConfig`, `namingMetadata`. |
| `BehavioralReviewFinding` | Excepción conductual detectada que requiere decisión humana. | tipo, sesión, evento, lados, `Desplaz`, evidencia `.mat`, frames y explicación. |
| `BatchReport` | Evidencia de lo procesado. | clips exportados, desfase video-MAT, hallazgos conductuales agrupados por sesión/rata, warnings, discrepancias, errores y configuración usada. |

La distinción importante es esta:
- `MatEvent` describe lo que MATLAB registró
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
VideoReader -> FrameAnalyzer -> BrightnessAdapter -> LightDetection -> LightTimelineBuilder -> SegmentPlanner -> ClipExporter
MatParser --------------------------------------------------------------^
VideoTransformConfig -----------------------------------------------> ClipExporter
BatchOrchestrator -> coordina todo
```

### Estado Verificado Del Backend (10 De Julio De 2026)

| Módulo | Estado | Evidencia o siguiente límite |
|--------|--------|------------------------------|
| `NomenclatureParser` | Implementado | 35 pruebas. Lee los tres esquemas y construye nombres de output existentes. |
| `SessionMetadataResolver` | Implementado | 18 pruebas. Completa metadata sin adivinar campos faltantes. |
| `VideoReader` | Implementado | 18 pruebas con video sintético y runtime nativo de OpenCV en macOS. |
| `FrameAnalyzer` | Implementado | 18 pruebas. Mide brillo de ROI y puede devolver el recorte de esa ROI. |
| `LightDetection` | Implementado | 20 pruebas. Convierte brillo ya medido en estados ON/OFF. |
| Adaptador OpenCV a `LightDetection` | Siguiente entrega | Conectará un `Mat` real con `IFrameBrightnessSource`; no debe duplicar la lógica de brillo. |
| `LightTimelineBuilder`, `MatParser`, `SegmentPlanner`, transformaciones, exportación y orquestación | Planeados | Se implementarán y probarán por separado después de la ruta frame-a-luz. |

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
`FoodRight` no se encienden al mismo tiempo. Para cada luz, el usuario marca una
ROI rectangular y elige una o más referencias claras de OFF y ON.
`LightCalibration` propone un umbral entre las medianas de brillo de ambos
grupos, pero el usuario puede confirmarlo o ajustarlo. La calibración de
`NoiseLed` necesita zoom porque su ROI es pequeña.

**Estado actual:** `LightDetector` está implementado y probado con fuentes
sintéticas. Falta el adaptador que reciba un `Mat` real, use `FrameAnalyzer` y
exponga ese brillo mediante `IFrameBrightnessSource`. El adaptador hará la
conversión puntual entre `LightRoi`/`LightId` y `RoiDefinition`/`TipoLed`; no
debe reimplementar el cálculo de brillo.

**Prueba aislada:** Sí. Con fuentes sintéticas de brillo se prueba sin abrir video ni UI.

---

### 6. LightTimelineBuilder

**Función en simple:** Convierte muchas lecturas frame por frame en una historia más estable de encendidos y apagados reales.

**Recibe:** una secuencia de `LightSample`.

**Entrega:** un `LightTimeline`, que es un resumen ordenado de transiciones relevantes.

**Depende de:** `LightDetection`.

```
Build(samples[], detectionConfig) -> LightTimeline

LightTimeline = {
  Samples:     LightSample[],
  Transitions: LightTransition[],
  Warnings:    LightTransition[]   // NoiseLed
  FoodLights:  LightTransition[]   // FoodLeft/FoodRight
}
```

La idea práctica es esta:
1. revisar muchas lecturas seguidas de cada luz
2. ignorar parpadeos o ruido de muy corta duración
3. registrar solo cambios estables: cuándo una luz realmente prendió y cuándo realmente se apagó

Eso evita que un frame brillante aislado se interprete como un evento real.

**Prueba aislada:** Sí. Con secuencias sintéticas de `LightSample`.

---

### 7. SegmentPlanner

**Función en simple:** Decide dónde empieza y dónde termina cada clip, y con qué etiqueta debe salir. Los archivos `.mat` se sincronizan con el video de forma aproximada y revisable.

**Recibe:** `LightTimeline` + `MatEvent[]` opcional + `SessionMetadata` + reglas de segmentación.

**Entrega:** `VideoSegment[]`.

**Depende de:** `LightTimelineBuilder` y, cuando exista, `MatParser`.

Ojo importante:
- `LightTimeline` **no es un módulo**, es un dato construido por `LightTimelineBuilder`
- `MatEvent` **no es un módulo**, es un dato leído por `MatParser`

```
Input:  LightTimeline + MatEvent[]? + SessionMetadata + SegmentConfig
Output: VideoSegment[]

VideoSegment = {
  StartFrame:         int,
  EndFrame:           int,
  Side:               enum { Left, Right, None },
  SegmentCode:        string,  // e1, e2, iti1, hab, habini, habfin
  TrialType:          enum { Safe, Conflict, SoundOnly, ITI, Habituation },
  Result:             enum { Crossing, NoCrossing, Timeout, PendingReview, NotApplicable },
  MatEventIndex:      int?,    // evento/fila correspondiente del .mat
  LeverLatencyMat:    float?,  // columna Latencia del .mat
  CrossingLatencyMat: float?,  // Desplaz, usado con SideChanged para detectar excepciones
  PreviousKnownSide:  enum?,   // último Lado válido antes del evento: Left o Right
  SideChanged:        bool?,   // current Side difiere de PreviousKnownSide
  CrossingContext:    enum?,   // SideChange, InterEventCandidate, None o RequiresReview
  BehavioralReviewFinding: BehavioralReviewFinding?, // excepción, si existe
  WarningStart:       int?,    // frame donde prende NoiseLed, si aplica
  FoodLightStart:     int?,    // frame donde prende FoodLeft/FoodRight
  FoodLightEnd:       int?,    // frame donde se apaga la luz de comida
  WarningToFoodSeconds: float?, // segundos entre NoiseLed y luz de comida
  MatPressSeconds:    float?,  // TiempoAbs: segundos desde inicio de habituación
  MatEventStartEstimateSeconds: float?, // TiempoAbs - Latencia
  VideoMatStartGapSeconds: float?, // FoodLightStart - MatEventStartEstimateSeconds
  PostPressLightTailSeconds: float?, // FoodLightEnd - TiempoAbs
  MatchConfidence:    enum,    // alta, media, baja o requiere revisión
  DurationSeconds:    float,
}
```

```text
BehavioralReviewFinding = {
  Type:                enum { InterEventCrossing, ShortSideChange },
  Session:             SessionMetadata, // protocolo, fase, rata y día
  MatEventIndex:       int,
  PreviousKnownSide:   enum?,
  CurrentSide:         enum?,
  DisplacementSeconds: float?,
  VideoEvidence:       { startFrame, endFrame, clipReference },
  MatEvidence:         { lado, desplaz, latencia, tiempoAbs },
  Explanation:         string,
  InvestigatorDecision: enum { Pending, CountAsCrossing, DoNotCount, Other },
}
```

`BehavioralReviewFinding` no altera los valores raw del `.mat`. Mientras
`InvestigatorDecision` sea `Pending`, el segmento conserva
`Result = PendingReview`; la UI debe pedir una decisión antes de asignar una
etiqueta final de output como `cr` o `nc`. Si se exporta antes para facilitar la
revisión, usa temporalmente el código `rv`.

Decisiones principales que toma este módulo:
1. usa las transiciones de luces para ubicar los límites de cada evento
2. determina si el evento es seguro, conflicto con comida o solo ruido
3. detecta huecos entre eventos para marcar `ITI`
4. detecta zonas sin eventos al inicio o final para marcar habituación
5. si hay `.mat`, empareja el evento visual con el evento conductual correspondiente
6. produce una lista para revisión y luego exportación

Para eventos con comida, `FoodLightStart` es la referencia visual para comparar
con el inicio MATLAB estimado; no se debe asumir que ambos ocurren en el mismo
instante. También se mide cuánto tiempo queda prendida la luz después del
palanqueo registrado en `TiempoAbs`. El LED de ruido conserva el inicio visual
de advertencia del mismo evento. Las diferencias por sesión se resumen con una
medida robusta como la mediana; nunca se resta un delay fijo supuesto para todos
los videos. La especificación completa está en
[Sincronización video-MAT de CajaValentia](sincronizacion-video-mat-cajavalentia.md).

**Lógica de segmentación:**
1. Cuando una luz de comida pasa de OFF a ON -> inicio visual del evento
2. Cuando se apaga la luz de comida -> fin visual del evento
3. Si el LED de ruido se enciende antes de la luz de comida -> periodo de advertencia de riesgo/conflicto
4. Si un `MatEvent` tiene `EventType=SoundOnly` (`TipoEvento=2`), LED sin luz de comida -> tipo `SoundOnly`
5. LED de ruido asociado a un evento con comida -> tipo `Conflict`
6. Luz de comida sin LED de ruido asociado -> tipo `Safe`
7. Entre ensayos/eventos sin luces relevantes -> `ITI`
8. Al inicio/fin del video sin luces -> `Habituation`
9. El primer ensayo de la sesión siempre es seguro/de comida; usarlo como referencia contextual, no como sustituto de la detección
10. Para determinar cruce/no cruce/timeout: clasifica automáticamente como cruce solo cambio de `Lado` + `Desplaz > 1 s`. Si el lado se mantiene igual con `Desplaz > 1 s`, crea un hallazgo `InterEventCrossing`; si cambia con `Desplaz <= 1 s`, crea un hallazgo `ShortSideChange`. Ambos conservan video y datos `.mat`, pero requieren decisión del investigador, no una etiqueta automática final. La latencia de palanqueo solo sirve como señal de revisión.

**Timing:** en ensayos de riesgo/conflicto, el clip puede empezar en `WarningStart` para conservar el LED/ruido previo. `FoodLightStart` y `FoodLightEnd` son límites visuales que se comparan con el inicio MATLAB estimado y `TiempoAbs`; no se asumen idénticos. En un evento `SoundOnly`, `WarningStart` es el inicio relevante y `FoodLightStart` queda vacío.

**Prueba aislada:** Sí. Con `LightTimeline` y `MatEvent` sintéticos se prueba sin necesidad de video.

**Aclaración de nombres:** `LightTimeline` y `MatEvent` son estructuras de datos, no módulos.

---

### 8. MatParser

**Función en simple:** Lee el archivo `.mat` de una sesión y lo convierte en eventos que el resto del sistema pueda usar.

**Recibe:** la ruta de un `.mat`.

**Entrega:** `SessionData`, que contiene una lista de `MatEvent`.

**Depende de:** una estrategia de lectura del formato `.mat`.

```
Read(matPath) -> SessionData

SessionData = {
  Eventos: MatEvent[] donde
    MatEvent = {
      EventIndex:       int,    // columna Ensayo del .mat
      LeverLatency:     float,  // duración MATLAB desde inicio de evento a palanqueo
      AbsoluteTime:      float,  // TiempoAbs: segundos desde R0 de MATLAB, previo a habituación
      CrossingLatency:  float,  // columna Desplaz: se combina con cambio de Lado
      Result:           enum,   // cruce, no cruce o timeout
      Side:             int,    // columna Lado: 0=izq, 1=der, -2=timeout
      StimElect:        int,    // columna Estim: 1=descarga activa
      EventType:        enum?,  // columna TipoEvento si existe: SafeFood, ConflictWithFood, SoundOnly
    }
}
```

El `.mat` normalmente tiene una variable `Resultados` (array N×8 histórico o N×9 con evento de solo ruido), pero algunos archivos pueden usar como nombre de variable el identificador de la sesión. El `MatParser` debe buscar la primera variable no interna que sea una matriz numérica con 8 o 9 columnas. Las columnas son:

| Col | Nombre | Significado |
|-----|--------|-------------|
| 0 | Ensayo | Número de evento |
| 1 | Lado | 0=izq, 1=der, -2=no cruzó/timeout |
| 2 | EstimElectrico | 1=descarga activa |
| 3 | Latencia | Duración MATLAB desde inicio de evento hasta palanqueo (~límite de fase=timeout) |
| 4 | TiempoAbs | Segundos desde R0 de MATLAB, previo a mensajes y habituación; `TiempoAbs - Latencia` estima inicio MATLAB para comparación con video. |
| 5 | PalancasIzq | Presiones acumuladas palanca izquierda |
| 6 | PalancasDer | Presiones acumuladas palanca derecha |
| 7 | Desplazamiento | Cambio de Lado + >1 = cruce automático. Lado igual + >1 = `InterEventCrossing` para decisión humana. Cambio de lado + <=1 = `ShortSideChange` para decisión humana. ~límite de fase = timeout. |
| 8 | TipoEvento | Solo en N×9: 0=seguro con comida, 1=conflicto con comida, 2=solo ruido |

**Estrategia de parseo:** el producto debe leer directamente el `.mat` fuente
con una librería .NET compatible con los archivos reales de MATLAB y validada
contra fixtures de 8 y 9 columnas. `MathNet.Numerics` sirve para cálculo
numérico, no es un lector de archivos `.mat`. CSV o JSON pueden usarse para
pruebas o diagnóstico, pero nunca deben convertirse en una entrada obligatoria
del producto.

**Estado actual:** pendiente. Antes de codificar, se debe elegir el lector con
una prueba mínima sobre los fixtures aprobados y documentar la variable/matriz
detectada, sin modificar el archivo fuente.

**Prueba aislada:** Sí. Con un `.mat` de prueba se valida.

---

### 9. VideoTransformConfig / VideoTransformPreview

**Función en simple:** Guardar y previsualizar cómo se debe transformar el video sin generar todavía el clip final.

**Recibe:** decisiones del usuario sobre crop, rotación, flip y calidad.

**Entrega:** una configuración reutilizable para preview y exportación dentro de un `CameraProfile`.

**Depende de:** `VideoReader`, una futura implementación de preview de
transformaciones y `ClipExporter` en la etapa de salida. No depende de
`FrameAnalyzer`, cuyo alcance es medir ROIs de luces.

```
TransformConfig = {
  CropRect,
  Rotation,
  Flip,
  OutputQuality,
}
```

La UI necesita mostrar crop/rotación/flip como preview, pero el flujo normal de exportación debe aplicar `trim + crop + rotate + flip` en un solo comando FFmpeg por clip cuando sea posible. Así se evita recodificar primero el video completo y después volver a recodificar cada clip.

Un `CameraProfile` agrupa esta transformación, las ROIs y su calibración. El
usuario puede crear otro perfil desde la primera sesión donde cambió el encuadre
y asignarlo a las sesiones posteriores. No se asume un número fijo de cambios de
cámara ni que un perfil sirva para todo el protocolo.

Un módulo `VideoCropRotate` puede existir como helper opcional para previews o casos especiales, pero no debe ser el camino principal del batch.

**Prueba aislada:** Sí. Con frames o videos cortos de prueba.

---

### 10. ClipExporter

**Función en simple:** Toma un plan de clips y escribe los archivos finales de video.

**Recibe:** `ExportClip[]` y configuración de FFmpeg.

**Entrega:** archivos de salida ya exportados.

**Depende de:** `SegmentPlanner`, `VideoTransformConfig` y la capa de exportación.

```
Export(exportClips[], ffmpegConfig) -> List<FileInfo>
```

Cada `ExportClip` incluye video de entrada, segmento, configuración de transformaciones y nombre final. Usa FFmpeg con `-ss`/`-to` y filtros de crop/rotación/flip en un solo paso por clip. Nombra cada archivo según la nomenclatura de output del Video Batch Processor.

Para un evento `SoundOnly`, el exportador no debe inventar `s`, `p` o `na` en
el campo de tipo. El código corto de output sigue pendiente de acuerdo del lab;
hasta entonces debe conservar el segmento para revisión y emitir un warning en
lugar de exportarlo con una etiqueta falsa.

**Prueba aislada:** Sí. Con un video de prueba y segmentos sintéticos.

---

### 11. BatchOrchestrator

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
    ManifestPath,
    HabituationConfig: { TargetFinal, WarnShortFinal },
    UseMatParser: bool,
  }
```

Flujo:
1. escanea la carpeta y encuentra videos candidatos
2. parsea nombres con `NomenclatureParser`
3. completa metadata con `SessionMetadataResolver` y `BatchManifest`
4. asigna el `CameraProfile` correspondiente y lee metadata del video con `VideoReader`
5. obtiene brillo por ROI con `FrameAnalyzer`, lo entrega mediante el `BrightnessAdapter` y detecta luces con `LightDetection`
6. construye transiciones estables con `LightTimelineBuilder`
7. si hay `.mat`, lo lee con `MatParser`
8. planea segmentos con `SegmentPlanner`
9. muestra revisión/QA al usuario antes de exportar
10. exporta clips con `ClipExporter` aplicando trim + transformaciones
11. genera `BatchReport` con resumen, warnings y discrepancias

**Prueba aislada:** Parcial. Se puede validar con mocks, pero su valor real aparece al integrar todo.

---

## Arquitectura Del Frontend (Avalonia)

El frontend se divide en bloques de trabajo, no solo en pantallas. Cada bloque
recoge una decisión del usuario, muestra evidencia y entrega información al
siguiente; ninguno interpreta por sí mismo el video o el `.mat`.

| Bloque UI y vistas | Función en simple | Recibe | Entrega | Conexión con backend |
|--------------------|-------------------|--------|---------|----------------------|
| `SessionSetup` (`VideoLoadView`) | Cargar una carpeta y ayudar al usuario a confirmar qué sesiones se van a procesar. | Carpeta elegida y nombres de videos. | Lista de `SessionMetadata`, archivos no reconocidos y grupos de trabajo. | Puede conectarse ahora a `NomenclatureParser` y `SessionMetadataResolver`. |
| `CameraSetup` (`CropView`, `LightMarkerView`, `LightCalibrationView`) | Preparar cómo se verá y medirá un grupo de videos con el mismo encuadre. | Frame representativo, decisiones de crop/orientación y ROIs. | `CameraProfileDraft`: transformación, ROIs, referencias OFF/ON y umbrales aceptados. | Puede abrir frames con `VideoReader` y validar ROIs con `FrameAnalyzer`. La detección real espera `BrightnessAdapter`. |
| `ProcessingReview` (`SegmentTimelineView`, `HabituationView`, `BehavioralFindingsView`) | Mostrar lo que el backend propuso y permitir confirmar o corregir casos importantes. | Segmentos, eventos `.mat`, advertencias, hallazgos `InterEventCrossing`/`ShortSideChange` y duraciones de habituación. | Decisiones de revisión: aceptar, excluir, ajustar o marcar para revisión manual. | Se diseña ahora; se conecta después a `LightTimelineBuilder`, `MatParser` y `SegmentPlanner`. |
| `BatchExport` (`ExportView`) | Ejecutar el lote y mostrar qué se exportó o falló. | Clips confirmados, opciones de salida y progreso. | `BatchReport`, logs y acceso a la carpeta de salida. | Se diseña ahora; se conecta después a `ClipExporter` y `BatchOrchestrator`. |

### Estado De La UI

`CameraProfileDraft` y las decisiones de revisión son **estado de interfaz**:
existen mientras el usuario configura o revisa. Solo pasan a configuración
reutilizable cuando el usuario los confirma y el backend los guarda. Esto evita
que la pantalla se convierta en otra fuente de verdad distinta al Core.

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

Cuando exista `BrightnessAdapter`, esa segunda ruta se amplía así:

```text
frame real -> FrameAnalyzer -> BrightnessAdapter -> LightDetection -> LightSample
```

La UI debe mostrar el resultado, no repetir la detección. Timeline, asociación
con `.mat`, habituación calculada y exportación permanecen deshabilitados o en
modo de diseño hasta que sus módulos backend existan.

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
3. Revisa frames representativos y confirma o crea `CameraProfile` por cambio de encuadre
4. Para cada perfil: dibuja crop, elige orientación, marca las 3 ROIs y calibra OFF/ON por luz
5. Los parámetros y la asignación de perfiles se guardan en un archivo de configuración
6. Programa genera segmentos preliminares y los muestra en timeline
7. Usuario revisa/corrige segmentos críticos, emparejamiento `.mat` y excepciones de habituación
8. Usuario da clic en "Procesar todo"
9. Pipeline se ejecuta sobre todos los videos
10. Al terminar: carpeta de salida + reporte
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
         MatParser               -> independiente

Paso 3:  SegmentPlanner          -> depende de LightTimelineBuilder + opcional MatParser
         CameraProfile           -> reúne transformaciones, ROIs y calibración por grupo de sesiones
         VideoTransformConfig    -> requiere VideoReader y preview de transformaciones

Paso 4:  ClipExporter            -> depende de SegmentPlanner + VideoTransformConfig
         BatchOrchestrator       -> depende de todo lo anterior

Paso 5:  UI base                 -> carga, lista, preview, ROIs y calibración
                                  usa módulos Core ya implementados
         UI de timeline/export   -> espera LightTimelineBuilder, SegmentPlanner,
                                  ClipExporter y BatchOrchestrator
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
manifest_path: "/videos/exp_0126_dis/batch_manifest.yaml"
metadata_defaults:
  initials: "abs"
  sex: "m"
  treatment: "stx"
camera_profiles:
  - id: "center-camera"
    applies_from_session: "exp_0126_cs_d1r1"
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
  segment_padding_seconds: 0.5
  quality: "research_archive"
```

Los perfiles se evalúan en orden dentro del lote: cada uno aplica desde su
sesión inicial hasta la sesión inicial del perfil siguiente. La UI debe mostrar
esa asignación antes de procesar y nunca extender un perfil a otro protocolo sin
confirmación del usuario.

Esto permite reprocesar sin tener que configurar cada vez.
