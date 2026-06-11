# Video Batch Processor

Aplicación de escritorio para preprocesamiento masivo de videos de la tarea de Conflicto Mediado por Cruces (CMC).

El objetivo del proyecto es convertir sesiones largas de video en clips cortos, consistentes y bien nombrados antes de usarlos en DeepLabCut, BORIS u otros análisis posteriores. La app busca normalizar crop, orientación, detección de luces, segmentación por eventos/ITIs/habituación y exportación por lote.

## Estado

Proyecto en fase de definición técnica y prototipo.

- Producto completo: Video Batch Processor.
- Prototipo actual: `LightEventDetector`, una app Avalonia que permite abrir un video, marcar ROIs de luces, detectar eventos ON/OFF y exportar una línea de tiempo con CSV.
- Backend actual: `VideoBatchProcessor.Core`, librería donde vive la lógica reusable del producto.
- Módulo en desarrollo: `LightDetection`, responsable de decidir si `FoodLeft`, `FoodRight` y `NoiseLed` están prendidos o apagados a partir de brillo por ROI.
- Próximo objetivo técnico: cerrar `LightDetection` con pruebas automáticas y después conectarlo al prototipo visual mediante un adaptador de frames.

## Stack

- C# / .NET
- Avalonia UI
- OpenCvSharp
- FFmpeg como dependencia prevista para procesamiento/exportación de video

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
│   └── VideoBatchProcessor.Core/      # Librería backend reusable del producto
└── VideoBatchProcessor.sln            # Solución C#
```

## Documentación

- [Índice de documentación](docs/README.md)
- [Requisitos de producto](docs/project/product-requirements.md)
- [Protocolo CMC](docs/protocol/cmc-protocol.md)
- [Formato .mat](docs/reference/mat-format.md)
- [Nomenclatura](docs/reference/naming-convention.md)
- [Convención operativa de términos](docs/reference/operational-terms.md)
- [Arquitectura](docs/project/architecture.md)
- [Guía backend de LightDetection](docs/development/light-detection-backend-guide.md)
- [Guía de pruebas de LightDetection](docs/development/light-detection-testing-guide.md)
- [Cronograma de junio para LightDetection](docs/development/june-light-detection-workplan.md)

## Build

```bash
dotnet build VideoBatchProcessor.sln
```

El prototipo visual se puede ejecutar con:

```bash
dotnet run --project src/LightEventDetector/LightEventDetector.csproj
```
