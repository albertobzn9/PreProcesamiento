# Video Batch Processor — Propuesta de Solución

## Stack tecnológico

| Capa | Tecnología |
|------|-----------|
| UI | Avalonia UI (cross-platform: Windows + macOS) |
| Backend | C# (.NET 10, target actual del repo) |
| Video | FFmpeg (via FFmpeg.AutoGen o proceso externo) |
| Imágenes | SkiaSharp (via Avalonia) |
| Archivos .mat | Librería para leer MATLAB .mat (CSV export o librería .NET) |

**¿Por qué este stack?**
- C#/Avalonia es cross-platform nativo (no electron)
- FFmpeg es el estándar de la industria para procesamiento de video
- SkiaSharp ya viene con Avalonia para análisis de píxeles en frames

---

## Arquitectura general

```
┌─────────────────────────────────────────────────┐
│                   UI (Avalonia)                  │
│  ┌──────────┐ ┌──────────┐ ┌─────────────────┐  │
│  │ LoadView │ │CropView  │ │ LightMarkerView │  │
│  └──────────┘ └──────────┘ └─────────────────┘  │
│  ┌──────────┐ ┌──────────┐ ┌─────────────────┐  │
│  │Timeline  │ │Habituat. │ │ ExportView      │  │
│  └──────────┘ └──────────┘ └─────────────────┘  │
└──────────────────────┬──────────────────────────┘
                       │ llama a
┌──────────────────────▼──────────────────────────┐
│             Core Library (Backend)               │
│  ┌──────────┐ ┌──────────┐ ┌─────────────────┐  │
│  │Nomenclat │ │Metadata  │ │ VideoReader     │  │
│  │Parser    │ │Resolver  │ │                 │  │
│  └──────────┘ └──────────┘ └─────────────────┘  │
│  ┌──────────┐ ┌──────────┐ ┌─────────────────┐  │
│  │Frame     │ │Light     │ │ LightTimeline   │  │
│  │Analysis  │ │Detection │ │ Builder         │  │
│  └──────────┘ └──────────┘ └─────────────────┘  │
│  ┌──────────┐ ┌──────────┐ ┌─────────────────┐  │
│  │MatParser │ │Segment   │ │ ClipExporter    │  │
│  │          │ │Planner   │ │ + FFmpeg        │  │
│  └──────────┘ └──────────┘ └─────────────────┘  │
│  ┌──────────┐ ┌──────────────────────────────┐  │
│  │Transform │ │ BatchOrchestrator            │  │
│  │Config    │ │                              │  │
│  └──────────┘ └──────────────────────────────┘  │
└──────────────────────────────────────────────────┘
```

---

## Modelo De Datos Conceptual

Antes de programar módulos concretos conviene separar los datos principales. Esta capa no cambia el comportamiento; solo define contratos claros para que UI, parser, detección y exportación hablen el mismo idioma.

| Modelo | Qué representa | Campos clave |
|--------|----------------|--------------|
| `SessionMetadata` | Identidad de una sesión completa. | `scheme`, `initials`, `dateCode`, `phaseCode`, `day`, `rat`, `sex`, `treatment`, `sourceVideoPath`, `sourceMatPath`. |
| `BatchManifest` | Archivo/configuración que completa datos que no vienen en nombres legacy. | `metadataDefaults`, overrides por archivo, ruta de `.mat`, treatment, sex, initials. |
| `LightSample` | Estado de luces en un frame o tiempo específico. | `frameIndex`, `timeSeconds`, `foodLeft`, `foodRight`, `noiseLed`, brillo por ROI. |
| `LightTransition` | Cambio estable de una luz entre OFF/ON. | `lightId`, `from`, `to`, `frameIndex`, `timeSeconds`, confianza. |
| `MatEvent` | Fila/evento del `.mat`. | `eventIndex`, `side`, `stim`, `leverLatency`, `absoluteTime`, `leftLeverPresses`, `rightLeverPresses`, `crossingLatency`, `result`. |
| `VideoSegment` | Pedazo lógico que se puede revisar o exportar. | `segmentCode`, `startFrame`, `endFrame`, `warningStart`, `foodLightStart`, `matEventIndex`, `trialType`, `result`. |
| `ExportClip` | Instrucción final para generar un archivo de video. | `inputVideoPath`, `outputPath`, `segment`, `transformConfig`, `namingMetadata`. |
| `BatchReport` | Evidencia de lo procesado. | clips exportados, warnings, discrepancias `.mat` vs video, errores, configuración usada. |

La distinción importante es esta: `MatEvent` describe lo que MATLAB registró; `VideoSegment` describe lo que se va a cortar del video; `ExportClip` describe el archivo final que se escribirá en disco.

## Convención De Nombres Técnicos

Las explicaciones del proyecto pueden estar en español, pero los nombres de módulos, clases, métodos y campos internos se mantienen principalmente en inglés para que el código sea consistente.

| Concepto | Nombre técnico | Uso |
|----------|----------------|-----|
| Módulo de detección de luces | `LightDetection` | Carpeta/módulo dentro de `VideoBatchProcessor.Core`. |
| Clase que decide ON/OFF | `LightDetector` | Clase principal del módulo `LightDetection`. |
| Método principal del detector | `Analyze(...)` | Analiza una muestra/frame y regresa un `LightSample`. |
| Luz de comida izquierda | `FoodLeft` | ROI/lectura de la luz de comida izquierda. |
| Luz de comida derecha | `FoodRight` | ROI/lectura de la luz de comida derecha. |
| LED de ruido blanco | `NoiseLed` | ROI/lectura del LED asociado al ruido blanco/conflicto. |
| Adaptador de brillo por ROI | `IFrameBrightnessSource` | Contrato para obtener brillo promedio sin acoplar el detector a OpenCV o UI. |

---

## Módulos del Backend

### 1. NomenclatureParser
**Responsabilidad:** Extraer metadata del nombre del archivo.

```
Input legacy:  "exp_0126_dis_d9r4.mp4"
Output:        { Scheme="LegacySession", DateCode="0126", PhaseCode="dis",
                 Day=9, Rat=4 }
```

También parsea la nomenclatura estándar del lab y la nomenclatura de output del Video Batch Processor:

```
Input lab:     "abs_2601_f5_d9r4_m_e1_p_stx.mp4"
Output:        { Scheme="LabStandard", Initials="abs", DateCode="2601",
                 PhaseCode="f5", Day=9, Rat=4, Sex="m",
                 SegmentCode="e1", TrialTypeCode="p", Treatment="stx" }

Input output:  "abs_2601_f5_d9r4_m_e1_p_cr_stx.mp4"
Output:        { Scheme="VideoBatchOutput", ..., SegmentCode="e1",
                 TrialTypeCode="p", ResultCode="cr", Treatment="stx" }
```

**Independiente:** Sí — solo trabaja con strings, sin dependencias de video.

---

### 2. SessionMetadataResolver
**Responsabilidad:** Completar la metadata de la sesión combinando nombre de archivo, manifest/configuración y rutas reales.

```
Resolve(parsedName, batchManifest, videoPath) → SessionMetadata
```

El `NomenclatureParser` solo debe parsear lo que existe en el nombre. El `SessionMetadataResolver` completa lo que no existe en nombres legacy, por ejemplo sexo, tratamiento, iniciales o ruta del `.mat`.

**Independiente:** Sí — se prueba con nombres y manifests sintéticos.

---

### 3. VideoReader
**Responsabilidad:** Abrir un archivo de video y extraer frames + metadatos.

```
Open(path) → VideoHandle
GetMetadata() → { Fps, Width, Height, Duration, Codec }
GetFrame(index) → Bitmap
GetFrameAtTime(seconds) → Bitmap
GetTotalFrames() → int
```

**Nota:** No hace procesamiento, solo lectura. Internamente usa FFmpeg para decodificar.

**Independiente:** Sí — con un solo video de prueba se puede validar.

---

### 4. FrameAnalysis
**Responsabilidad:** Operaciones sobre frames individuales.

```
ExtractROI(frame, Rect) → Bitmap          // Recorta región de interés
Rotate(frame, degrees) → Bitmap           // 0, 90, 180, 270
Flip(frame, axis) → Bitmap                // Horizontal o vertical
GetMeanBrightness(frame, LightRoi) → double
```

Este módulo/adaptador es el que sabe trabajar con píxeles reales. Por eso puede depender de OpenCV, FFmpeg o SkiaSharp según la implementación concreta.

**Independiente:** Sí — opera sobre cualquier imagen, no requiere video completo.

---

### 5. LightDetection
**Responsabilidad:** Decidir el estado ON/OFF de las tres luces de interés usando brillo promedio y umbral.

```
LightDetectionConfig = {
  FoodLeft:  LightRoi,
  FoodRight: LightRoi,
  NoiseLed:  LightRoi,
}

LightDetector.Analyze(frameBrightnessSource, frameIndex, timeSeconds) → LightSample
  LightSample = {
    FrameIndex:    int,
    TimeSeconds:   float,
    FoodLeft:      LightReading,
    FoodRight:     LightReading,
    NoiseLed:      LightReading,
  }
```

`LightReading` conserva el brillo medido, el umbral usado y el estado booleano (`IsOn`). El brillo puede venir de un adaptador como `IFrameBrightnessSource`, que permite probar el detector con datos sintéticos o conectarlo después a OpenCV.

**Independiente:** Sí — con fuentes sintéticas de brillo se prueba sin abrir video ni UI.

---

### 6. LightTimelineBuilder
**Responsabilidad:** Convertir detecciones frame-by-frame en una línea de tiempo estable de encendidos y apagados.

```
Build(samples[], detectionConfig) → LightTimeline

LightTimeline = {
  Samples:     LightSample[],
  Transitions: LightTransition[],
  Warnings:    LightTransition[]   // NoiseLed
  FoodLights:  LightTransition[]   // FoodLeft/FoodRight
}
```

Este módulo suaviza ruido, aplica umbrales mínimos de duración y evita que un frame brillante aislado se interprete como evento real.

**Independiente:** Sí — con secuencias sintéticas de `LightSample`.

---

### 7. SegmentPlanner
**Responsabilidad:** Tomar la línea de tiempo de luces, las reglas del protocolo y el `.mat` para construir segmentos revisables/exportables.

```
Input:  LightTimeline + MatEvent[]? + SessionMetadata + SegmentConfig
Output: VideoSegment[]

VideoSegment = {
  StartFrame:         int,
  EndFrame:           int,
  Side:               enum { Left, Right, None },
  SegmentCode:        string,  // e1, e2, iti1, hab, habini, habfin
  TrialType:          enum { Safe, Conflict, ITI, Habituation },
  Result:             enum { Crossing, NoCrossing, Timeout, NotApplicable },
  MatEventIndex:      int?,    // evento/fila correspondiente del .mat
  LeverLatencyMat:    float?,  // columna Latencia del .mat
  CrossingLatencyMat: float?,  // columna Desplaz del .mat
  WarningStart:       int?,    // frame donde prende NoiseLed, si aplica
  FoodLightStart:     int?,    // frame donde prende FoodLeft/FoodRight
  DurationSeconds:    float,
}
```

**Lógica de detección:**
1. Usa `LightTimeline.Transitions` para ubicar cambios estables de luces
2. Cuando una luz de comida pasa de OFF a ON → inicio MATLAB del evento
3. Cuando se apaga la luz de comida → fin del evento
4. Si el LED de ruido se enciende antes de la luz de comida → periodo de advertencia de riesgo/conflicto
5. LED de ruido asociado al evento → tipo `Conflicto`
6. Sin LED de ruido asociado al evento → tipo `Seguro`
7. Entre ensayos/eventos sin luces relevantes → `ITI`
8. Al inicio/fin del video sin luces → `Habituacion`
9. El primer ensayo de la sesión siempre es seguro/de comida; usarlo como referencia contextual, no como sustituto de la detección
10. Para determinar cruce/no cruce/timeout: primero puede usar heurística visual, y si hay `.mat` disponible, prioriza la información real del `.mat`

**Nota de timing:** en ensayos de riesgo/conflicto, el clip puede empezar en `WarningStart` para conservar el LED/ruido previo. La latencia del `.mat` empieza en `FoodLightStart`.

**Independiente:** Sí — con `LightTimeline` y `MatEvent` sintéticos se prueba sin necesidad de video.

---

### 8. MatParser
**Responsabilidad:** Leer el archivo `.mat` de una sesión y extraer datos por evento.

```
Read(matPath) → SessionData

SessionData = {
  Eventos: MatEvent[] donde
    MatEvent = {
      EventIndex:         int,    // columna Ensayo del .mat
      LeverLatency:       float,  // columna Latencia: palanqueo desde luz de comida
      CrossingLatency:    float,  // columna Desplaz: cruce/desplazamiento
      Result:             enum,   // cruce, no cruce o timeout
      Side:               int,    // columna Lado: 0=izq, 1=der, -2=timeout
      Stim:               int,    // columna Estim: 1=descarga activa/conflicto
    }
}
```

El .mat normalmente tiene una variable `Resultados` (array N×8), pero algunos archivos pueden usar como nombre de variable el identificador de la sesión. El `MatParser` debe buscar la primera variable no interna que sea una matriz numérica con 8 columnas. Las columnas son:

| Col | Nombre | Significado |
|-----|--------|-------------|
| 0 | Ensayo | Número de evento |
| 1 | Lado | 0=izq, 1=der, -2=no cruzó/timeout |
| 2 | EstimElectrico | 1=descarga (conflicto) |
| 3 | Latencia | Latencia de palanqueo desde luz de comida (~límite de fase=timeout) |
| 4 | TiempoAbs | Timestamp desde inicio de sesión |
| 5 | PalancasIzq | Presiones acumuladas palanca izquierda |
| 6 | PalancasDer | Presiones acumuladas palanca derecha |
| 7 | Desplazamiento | >1 = cruce válido, <=1 = palanqueo sin cruce, ~límite de fase = timeout |

**Estrategia de parseo:** Los .mat pueden exportarse a CSV con scripts existentes en Python, o leerse directamente en C# con una librería como `MathNet.Numerics` o `MATLAB File Format` (solo lectura). Alternativa: pre-procesar los .mat a JSON/CSV y que este módulo lea el formato intermedio.

**Independiente:** Sí — con un .mat de prueba se valida.

---

### 9. VideoTransformConfig / VideoTransformPreview
**Responsabilidad:** Guardar y previsualizar transformaciones de video sin obligar a generar un video intermedio.

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

**Independiente:** Sí — con frames o videos cortos de prueba.

---

### 10. ClipExporter
**Responsabilidad:** Tomar un plan de exportación y generar cada clip como video individual.

```
Export(exportClips[], ffmpegConfig) → List<FileInfo>
```

Cada `ExportClip` incluye video de entrada, segmento, configuración de transformaciones y nombre final. Usa FFmpeg con `-ss`/`-to` y filtros de crop/rotación/flip en un solo paso por clip. Nombra cada archivo según la nomenclatura de output del Video Batch Processor.

**Independiente:** Sí — con un video de prueba + segmentos sintéticos.

---

### 11. BatchOrchestrator
**Responsabilidad:** Orquestar todo el pipeline para una carpeta de videos.

```
Run(config) → BatchReport
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
1. Escanea la carpeta y encuentra videos candidatos
2. Parsea nombres con `NomenclatureParser`
3. Completa metadata con `SessionMetadataResolver` y `BatchManifest`
4. Lee metadata del video con `VideoReader`
5. Obtiene brillo por ROI con `FrameAnalysis` y detecta luces con `LightDetection`
6. Construye transiciones estables con `LightTimelineBuilder`
7. Si hay `.mat`, lo lee con `MatParser`
8. Planea segmentos con `SegmentPlanner`
9. Muestra revisión/QA al usuario antes de exportar
10. Exporta clips con `ClipExporter` aplicando trim + transformaciones
11. Genera `BatchReport` con resumen, warnings y discrepancias

---

## Módulos de la UI (Avalonia)

| Vista | Propósito |
|-------|-----------|
| `VideoLoadView` | Seleccionar carpeta de entrada. Muestra lista de videos detectados con su metadata (fase, día, rata). |
| `CropView` | Muestra el primer frame. Usuario dibuja un rectángulo con el mouse sobre la caja conductual. Preview del resultado. |
| `LightMarkerView` | Misma imagen. Usuario marca las ROIs `FoodLeft`, `FoodRight` y `NoiseLed`. Se puede ajustar el umbral de brillo. |
| `SegmentTimelineView` | Línea de tiempo con barras por tipo: seguro, conflicto, ITI y habituación. Permite revisar luces, corregir inicios/finales, confirmar habituación/ITIs y validar el emparejamiento con `.mat` antes de exportar. |
| `HabituationView` | Muestra duración de habituación inicial y final. Input del usuario: "recortar a X minutos". Alerta si dura menos de lo esperado. |
| `ExportView` | Barra de progreso, logs en tiempo real, resumen final: "35 clips exportados de 4 videos". Botón para abrir la carpeta de salida. |

---

## Flujo de uso completo

```
1. Usuario abre el programa
2. Selecciona carpeta con videos → se listan automáticamente
3. Primer video se despliega
   a. Usuario dibuja crop → se aplica preview
   b. Usuario marca 3 luces → se muestra detección en tiempo real
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
Paso 1:  NomenclatureParser      → independiente
         SessionMetadataResolver → independiente
         FrameAnalysis           → independiente
         VideoReader             → independiente

Paso 2:  LightDetection          → depende de FrameAnalysis solo cuando se conecta a frames reales
         LightTimelineBuilder    → depende de LightDetection
         MatParser               → independiente

Paso 3:  SegmentPlanner          → depende de LightTimelineBuilder + opcional MatParser
         VideoTransformConfig    → depende de FrameAnalysis para preview

Paso 4:  ClipExporter            → depende de SegmentPlanner + VideoTransformConfig
         BatchOrchestrator       → depende de todo lo anterior

Paso 5:  UI (todas las vistas)   → depende de BatchOrchestrator
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
