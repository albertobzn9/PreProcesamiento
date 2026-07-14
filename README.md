# Video Batch Processor

Aplicación de escritorio para preprocesamiento masivo de videos de la tarea de Conflicto Mediado por Cruces (CMC).

El objetivo del proyecto es convertir sesiones largas de video en clips cortos, consistentes y bien nombrados antes de usarlos en DeepLabCut, BORIS u otros análisis posteriores. La app busca normalizar crop, orientación, detección de luces, segmentación por eventos/ITIs/habituación y exportación por lote.

## Estado

Proyecto en fase de backend base + primera integración de interfaz.

- Producto completo: Video Batch Processor.
- Prototipo actual: `LightEventDetector`, una app Avalonia que permite abrir un video, marcar ROIs de luces, detectar eventos ON/OFF y exportar una línea de tiempo con CSV.
- Backend actual: `VideoBatchProcessor.Core`, librería donde vive la lógica reusable del producto.
- Módulos backend ya implementados: `NomenclatureParser`, `SessionMetadataResolver`, `VideoReader`, `FrameAnalyzer`, `BrightnessAdapter`, `LightDetection`, `LightCalibration` y la base de `BehavioralData` para CSV V1/MAT histórico.
- Interfaz: HTML/CSS local dentro de Avalonia ya integrado y validado manualmente en macOS; cubre carga recursiva, filtros de fase, nomenclaturas, preview real, `CameraSetup`, marcado de las tres ROIs y una primera calibración ON/OFF por frame.
- Validación actual: hay 139 pruebas pasando. El flujo de calibración ya pasó su prueba manual con video real; falta decidir si ambas luces de comida comparten referencia, guardar `CameraProfile`, construir `LightTimelineBuilder` y leer MAT binario real antes de integrar procesamiento completo.

## Stack

- C# / .NET
- Avalonia UI
- HTML/CSS local dentro del WebView oficial de Avalonia para la interfaz final
- OpenCvSharp para lectura de video y análisis de frames
- FFmpeg como dependencia prevista para exportación de clips

## Estructura

```text
.
├── docs/
│   ├── project/                       # Producto y arquitectura
│   ├── protocol/                      # Protocolo CMC y literatura base
│   ├── reference/                     # Formatos, nomenclatura y términos
│   └── development/                   # Guías de trabajo e implementación
├── src/
│   ├── LightEventDetector/            # Prototipo C# / Avalonia para calibración visual
│   ├── VideoBatchProcessor.Core/      # Librería backend reusable del producto
│   └── VideoBatchProcessor.App/       # Aplicación de escritorio e interfaz
└── VideoBatchProcessor.sln            # Solución C#
```

## Documentación

- [Índice de documentación](docs/README.md)
- [Requisitos de producto](docs/project/product-requirements.md)
- [Estado actual del proyecto](docs/project/current-status.md)
- [Protocolo CMC](docs/protocol/cmc-protocol.md)
- [Formato MAT histórico](docs/reference/mat-format.md)
- [Nomenclatura](docs/reference/naming-convention.md)
- [Términos operativos y reglas de decisión](docs/reference/operational-terms.md)
- [Arquitectura](docs/project/architecture.md)
- [Sincronización video-conducta con CajaValentia](docs/project/sincronizacion-video-mat-cajavalentia.md)
- [Contexto de integración futura](docs/project/future-integration-context.md)
- [Handoff del módulo de luces](docs/development/eric-workplan.md)

## Archivo Paralelo En Drive

La fuente histórica de requisitos, guías y ejemplos del producto permanece en:

```text
/Users/ab/Library/CloudStorage/GoogleDrive-jasjabs19@gmail.com/My Drive/workspace/03_lab/01_Proyecto_Grande_App/02_video-batch-processor
```

Esa carpeta es de referencia y archivo; la documentación y el código activos
viven en este repositorio. No copiar árboles completos ni datos de sesión: si
aparece una corrección útil, incorporarla aquí con un cambio pequeño y probado.

## Build

```bash
dotnet build VideoBatchProcessor.sln
```

## Tests

```bash
dotnet test VideoBatchProcessor.sln
```

En macOS, usar este wrapper para pruebas con OpenCV:

```bash
./scripts/test-macos.sh
```

Ese script detecta si la Mac es Apple Silicon o Intel y resuelve la ruta nativa que OpenCvSharp necesita.

El prototipo visual se puede ejecutar con:

```bash
dotnet run --project src/LightEventDetector/LightEventDetector.csproj
```
