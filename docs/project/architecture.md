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
│  │FrameAnalysis │ │LightDetection      │ │LightTimeline│ │
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
| `LightSample` | Estado de las tres luces en un frame o tiempo específico. | `frameIndex`, `timeSeconds`, `foodLeft`, `foodRight`, `noiseLed`, brillo por ROI. |
| `LightTransition` | Cambio estable de una luz entre OFF y ON o entre ON y OFF. | `lightId`, `from`, `to`, `frameIndex`, `timeSeconds`, confianza. |
| `MatEvent` | Un evento/fila leído desde el `.mat`. | `eventIndex`, `side`, `stim`, `eventType`, `leverLatency`, `absoluteTime`, `leftLeverPresses`, `rightLeverPresses`, `crossingLatency`, `result`. |
| `VideoSegment` | Parte lógica de una sesión que el programa propone revisar o exportar como clip. | `segmentCode`, `startFrame`, `endFrame`, `warningStart`, `foodLightStart`, `matEventIndex`, `trialType`, `result`. |
| `ExportClip` | Instrucción final para generar un archivo de salida. | `inputVideoPath`, `outputPath`, `segment`, `transformConfig`, `namingMetadata`. |
| `BatchReport` | Evidencia de lo procesado. | clips exportados, warnings, discrepancias `.mat` vs video, errores, configuración usada. |

La distinción importante es esta:
- `MatEvent` describe lo que MATLAB registró
- `VideoSegment` describe lo que se va a cortar del video
- `ExportClip` describe el archivo final que se escribirá en disco

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
VideoReader -> FrameAnalysis -> LightDetection -> LightTimelineBuilder -> SegmentPlanner -> ClipExporter
MatParser --------------------------------------------------------------^
VideoTransformConfig -----------------------------------------------> ClipExporter
BatchOrchestrator -> coordina todo
```
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

### 4. FrameAnalysis

**Función en simple:** Hace operaciones visuales básicas sobre un frame: recortar, rotar, espejear y medir brillo en regiones concretas.

**Recibe:** un frame y una instrucción concreta, por ejemplo una región de recorte, una rotación o una ROI de luz.

**Entrega:** un frame transformado o una medición visual, como el brillo promedio de una ROI.

**Depende de:** frames reales obtenidos por `VideoReader` o por cualquier otra fuente de imagen.

```
ExtractROI(frame, Rect) -> Bitmap          // Recorta región de interés
Rotate(frame, degrees) -> Bitmap           // 0, 90, 180, 270
Flip(frame, axis) -> Bitmap                // Horizontal o vertical
GetMeanBrightness(frame, LightRoi) -> double
```
La selección de crop y de luces viene de la UI. Este módulo no decide esas regiones; este módulo las aplica sobre los píxeles reales.

Aquí vive la lógica base para:
- quitar visualmente lo que sobra fuera de la caja conductual
- orientar el frame correctamente
- medir las regiones de las tres luces que el usuario marcó

**Prueba aislada:** Sí. Opera sobre cualquier imagen, no requiere video completo.

---

### 5. LightDetection

**Función en simple:** Decide si cada una de las tres luces está prendida o apagada en un momento dado.

**Recibe:** brillo medido en las ROIs de `FoodLeft`, `FoodRight` y `NoiseLed`, junto con umbrales y referencia de frame/tiempo.

**Entrega:** un `LightSample`, es decir, una lectura de estado de luces para ese frame.

**Depende de:** una fuente de brillo por ROI, normalmente conectada a `FrameAnalysis`.

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
  Result:             enum { Crossing, NoCrossing, Timeout, NotApplicable },
  MatEventIndex:      int?,    // evento/fila correspondiente del .mat
  LeverLatencyMat:    float?,  // columna Latencia del .mat
  CrossingLatencyMat: float?,  // columna Desplaz del .mat
  WarningStart:       int?,    // frame donde prende NoiseLed, si aplica
  FoodLightStart:     int?,    // frame donde prende FoodLeft/FoodRight
  DurationSeconds:    float,
}
```

Decisiones principales que toma este módulo:
1. usa las transiciones de luces para ubicar los límites de cada evento
2. determina si el evento es seguro, conflicto con comida o solo ruido
3. detecta huecos entre eventos para marcar `ITI`
4. detecta zonas sin eventos al inicio o final para marcar habituación
5. si hay `.mat`, empareja el evento visual con el evento conductual correspondiente
6. produce una lista para revisión y luego exportación

**Lógica de segmentación:**
1. Cuando una luz de comida pasa de OFF a ON -> inicio MATLAB del evento
2. Cuando se apaga la luz de comida -> fin del evento
3. Si el LED de ruido se enciende antes de la luz de comida -> periodo de advertencia de riesgo/conflicto
4. Si un `MatEvent` tiene `EventType=SoundOnly` (`TipoEvento=2`), LED sin luz de comida -> tipo `SoundOnly`
5. LED de ruido asociado a un evento con comida -> tipo `Conflict`
6. Luz de comida sin LED de ruido asociado -> tipo `Safe`
7. Entre ensayos/eventos sin luces relevantes -> `ITI`
8. Al inicio/fin del video sin luces -> `Habituation`
9. El primer ensayo de la sesión siempre es seguro/de comida; usarlo como referencia contextual, no como sustituto de la detección
10. Para determinar cruce/no cruce/timeout: primero puede usar heurística visual, y si hay `.mat` disponible, prioriza la información real del `.mat`

**Timing:** en ensayos de riesgo/conflicto, el clip puede empezar en `WarningStart` para conservar el LED/ruido previo. La latencia del `.mat` empieza en `FoodLightStart`. En un evento `SoundOnly`, `WarningStart` es el inicio relevante y `FoodLightStart` queda vacío.

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
      LeverLatency:     float,  // columna Latencia: palanqueo desde luz de comida
      CrossingLatency:  float,  // columna Desplaz: cruce/desplazamiento
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
| 3 | Latencia | Latencia de palanqueo desde luz de comida (~límite de fase=timeout) |
| 4 | TiempoAbs | Timestamp desde inicio de sesión |
| 5 | PalancasIzq | Presiones acumuladas palanca izquierda |
| 6 | PalancasDer | Presiones acumuladas palanca derecha |
| 7 | Desplazamiento | >1 = cruce válido, <=1 = palanqueo sin cruce, ~límite de fase = timeout |
| 8 | TipoEvento | Solo en N×9: 0=seguro con comida, 1=conflicto con comida, 2=solo ruido |

**Estrategia de parseo:** Los `.mat` pueden exportarse a CSV con scripts existentes en Python, o leerse directamente en C# con una librería como `MathNet.Numerics` o `MATLAB File Format` (solo lectura). Alternativa: pre-procesar los `.mat` a JSON/CSV y que este módulo lea el formato intermedio.

**Prueba aislada:** Sí. Con un `.mat` de prueba se valida.

---

### 9. VideoTransformConfig / VideoTransformPreview

**Función en simple:** Guardar y previsualizar cómo se debe transformar el video sin generar todavía el clip final.

**Recibe:** decisiones del usuario sobre crop, rotación, flip y calidad.

**Entrega:** una configuración reutilizable para preview y exportación.

**Depende de:** `FrameAnalysis` para previews y de `ClipExporter` en la etapa de salida.

```
TransformConfig = {
  CropRect,
  Rotation,
  Flip,
  OutputQuality,
}
```

La UI necesita mostrar crop/rotación/flip como preview, pero el flujo normal de exportación debe aplicar `trim + crop + rotate + flip` en un solo comando FFmpeg por clip cuando sea posible. Así se evita recodificar primero el video completo y después volver a recodificar cada clip.

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
    CropRect,
    Rotation,
    Flip,
    LightMarkers: Circle[3],
    ManifestPath,
    HabituationConfig: { MaxInitial, MaxFinal },
    UseMatParser: bool,
  }
```

Flujo:
1. escanea la carpeta y encuentra videos candidatos
2. parsea nombres con `NomenclatureParser`
3. completa metadata con `SessionMetadataResolver` y `BatchManifest`
4. lee metadata del video con `VideoReader`
5. obtiene brillo por ROI con `FrameAnalysis` y detecta luces con `LightDetection`
6. construye transiciones estables con `LightTimelineBuilder`
7. si hay `.mat`, lo lee con `MatParser`
8. planea segmentos con `SegmentPlanner`
9. muestra revisión/QA al usuario antes de exportar
10. exporta clips con `ClipExporter` aplicando trim + transformaciones
11. genera `BatchReport` con resumen, warnings y discrepancias

**Prueba aislada:** Parcial. Se puede validar con mocks, pero su valor real aparece al integrar todo.

---

## Módulos de la UI (Avalonia)

| Vista | Propósito |
|-------|-----------|
| `VideoLoadView` | Seleccionar carpeta de entrada. Muestra lista de videos detectados con su metadata (`SessionMetadata`: fase, día, rata, etc.). |
| `CropView` | Muestra el primer frame. Usuario dibuja un rectángulo con el mouse sobre la caja conductual. Preview del resultado. |
| `LightMarkerView` | Misma imagen. Usuario marca las ROIs `FoodLeft`, `FoodRight` y `NoiseLed`. Se puede ajustar el umbral de brillo. |
| `SegmentTimelineView` | Línea de tiempo con segmentos por tipo: `Safe`, `Conflict`, `ITI` y `Habituation`. Permite revisar luces, corregir inicios/finales, confirmar habituación/ITIs y validar el emparejamiento con `.mat` antes de exportar. |
| `HabituationView` | Muestra duración de habituación inicial y final. Input del usuario: "recortar a X minutos". Alerta si dura menos de lo esperado. |
| `ExportView` | Barra de progreso, logs en tiempo real, resumen final: "35 clips exportados de 4 videos". Botón para abrir la carpeta de salida. |

---

## Flujo de uso completo

```
1. Usuario abre el programa
2. Selecciona carpeta con videos -> se listan automáticamente
3. Primer video se despliega
   a. Usuario dibuja crop -> se aplica preview
   b. Usuario marca 3 luces -> se muestra detección en tiempo real
   c. Usuario configura habituación
4. Los parámetros se guardan en un archivo de configuración
5. Programa genera segmentos preliminares y los muestra en timeline
6. Usuario revisa/corrige segmentos críticos si hace falta
7. Usuario da clic en "Procesar todo"
8. Pipeline se ejecuta sobre todos los videos
9. Al terminar: carpeta de salida + reporte
```

---

## Orden Lógico De Implementación

```
Paso 1:  NomenclatureParser      -> independiente
         SessionMetadataResolver -> independiente
         FrameAnalysis           -> independiente
         VideoReader             -> independiente

Paso 2:  LightDetection          -> depende de FrameAnalysis solo cuando se conecta a frames reales
         LightTimelineBuilder    -> depende de LightDetection
         MatParser               -> independiente

Paso 3:  SegmentPlanner          -> depende de LightTimelineBuilder + opcional MatParser
         VideoTransformConfig    -> depende de FrameAnalysis para preview

Paso 4:  ClipExporter            -> depende de SegmentPlanner + VideoTransformConfig
         BatchOrchestrator       -> depende de todo lo anterior

Paso 5:  UI (todas las vistas)   -> depende de BatchOrchestrator
         Pruebas con datos reales
```

Cada módulo tiene pruebas unitarias y se puede entregar funcional antes de continuar con el siguiente.

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
crop:
  x: 50
  y: 30
  width: 900
  height: 500
rotation: 0
flip: none
lights:
  food_left:  { x: 120, y: 80, radius: 10 }
  food_right: { x: 780, y: 80, radius: 10 }
  noise_led:  { x: 450, y: 120, radius: 8 }
detection:
  threshold_mode: auto
  min_on_frames: 3
  min_off_frames: 3
habituation:
  max_initial_seconds: 300
  max_final_seconds: 300
use_mat_parser: true
export:
  include_events: true
  include_itis: true
  include_habituation: true
  include_warning_period: true
  segment_padding_seconds: 0.5
  quality: "research_archive"
```

Esto permite reprocesar sin tener que configurar cada vez.
