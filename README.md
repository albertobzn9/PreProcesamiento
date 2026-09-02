# Video Batch Processor

Aplicación de escritorio para preprocesamiento masivo de videos de la tarea de Conflicto Mediado por Cruces (CMC).

El objetivo del proyecto es convertir sesiones largas de video en clips cortos, consistentes y bien nombrados antes de usarlos en DeepLabCut, BORIS u otros análisis posteriores. La app busca normalizar crop, orientación, detección de luces, segmentación por eventos/ITIs/habituación y exportación por lote.

## Estado

El primer flujo completo de Cruces Seguros ya existe en backend; falta exponerlo
en la interfaz y validarlo con un lote real antes de ampliar fases.

- Producto activo: `VideoBatchProcessor.App`; `LightEventDetector` permanece
  como prototipo histórico.
- Backend: `VideoBatchProcessor.Core` ya incluye nomenclaturas, metadata,
  lectura de video, ROI/brillo, detección y calibración de luces, timeline,
  MAT/CSV y una puerta de sincronización video-conducta por sesión.
- Interfaz: HTML/CSS local dentro de Avalonia con carga recursiva, preview,
  giro, espejo, crop, ROIs circulares, calibración, rango de análisis y
  exportación XLSX de diagnóstico.
- Validación: 171 pruebas pasan. En una sesión real de Cruces Seguros (CS), el
  diagnóstico completo empató los 67 eventos MAT con video y planeó 1
  habituación inicial, 67 eventos, 66 ITIs y 1 habituación final. Cuatro
  señales visuales extra quedaron como avisos para revisión, no como eventos.
  El XLSX incluye perfil de cámara, comparación y segmentos planeados. Un
  `ClipExporter` ya genera un clip individual con FFmpeg y transformaciones.
  `BatchOrchestrator` ya coordina el lote CS: MAT del mismo stem,
  sincronización, diagnóstico XLSX y exportación de clips.
- Pendiente: conexión de exportación a la interfaz, perfiles reutilizables por
  grupo, validación de un lote CS real, validación CP/DIS y compatibilidad con
  el CSV actual de 10 columnas de CajaValentia.

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
- [Historial de cambios](CHANGELOG.md)
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

La aplicación activa se puede ejecutar con:

```bash
dotnet run --project src/VideoBatchProcessor.App/VideoBatchProcessor.App.csproj
```

El prototipo histórico se puede ejecutar con:

```bash
dotnet run --project src/LightEventDetector/LightEventDetector.csproj
```
