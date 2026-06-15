# Video Batch Processor

Aplicación de escritorio para preprocesamiento masivo de videos de la tarea de Conflicto Mediado por Cruces (CMC).

El objetivo del proyecto es convertir sesiones largas de video en clips cortos, consistentes y bien nombrados antes de usarlos en DeepLabCut, BORIS u otros análisis posteriores. La app busca normalizar crop, orientación, detección de luces, segmentación por eventos/ITIs/habituación y exportación por lote.

## Estado

Proyecto en fase de definición técnica y prototipo.

- Producto completo: Video Batch Processor.
- Prototipo actual: `LightEventDetector`, una app Avalonia que permite abrir un video, marcar ROIs de luces, detectar eventos ON/OFF y exportar una línea de tiempo con CSV.
<<<<<<< HEAD
- Próximo objetivo técnico: convertir el prototipo en un pipeline por lotes con parsing de nomenclatura, crop/orientación, segmentación de clips y exportación.
=======
- Backend actual: `VideoBatchProcessor.Core`, librería donde vive la lógica reusable del producto.
- Módulo en desarrollo: `LightDetection`, responsable de decidir si `FoodLeft`, `FoodRight` y `NoiseLed` están prendidos o apagados a partir de brillo por ROI.
- Próximo objetivo técnico: cerrar `LightDetection` con pruebas automáticas y después conectarlo al prototipo visual mediante un adaptador de frames.
>>>>>>> 906fb64bcc4507d01341306ee4fe0c9f547ee5e2

## Stack

- C# / .NET
- Avalonia UI
- OpenCvSharp
- FFmpeg como dependencia prevista para procesamiento/exportación de video

## Estructura

```text
.
<<<<<<< HEAD
├── docs/                    # Documentación de producto, protocolo, referencia y arquitectura
├── src/LightEventDetector/   # Prototipo C# / Avalonia existente
└── VideoBatchProcessor.sln   # Solución C#
=======
├── docs/
│   ├── project/                       # Producto y arquitectura
│   ├── protocol/                      # Protocolo CMC y literatura base
│   ├── reference/                     # Formatos, nomenclatura y términos
│   └── development/                   # Guías de trabajo e implementación
├── src/
│   ├── LightEventDetector/            # Prototipo C# / Avalonia para calibración visual
│   └── VideoBatchProcessor.Core/      # Librería backend reusable del producto
└── VideoBatchProcessor.sln            # Solución C#
>>>>>>> 906fb64bcc4507d01341306ee4fe0c9f547ee5e2
```

## Documentación

- [Índice de documentación](docs/README.md)
<<<<<<< HEAD
- [Requisitos de producto](docs/requirements/product-requirements.md)
=======
- [Requisitos de producto](docs/project/product-requirements.md)
>>>>>>> 906fb64bcc4507d01341306ee4fe0c9f547ee5e2
- [Protocolo CMC](docs/protocol/cmc-protocol.md)
- [Formato .mat](docs/reference/mat-format.md)
- [Nomenclatura](docs/reference/naming-convention.md)
- [Convención operativa de términos](docs/reference/operational-terms.md)
<<<<<<< HEAD
- [Propuesta técnica](docs/architecture/technical-proposal.md)
=======
- [Arquitectura](docs/project/architecture.md)
- [Guía backend de LightDetection](docs/development/light-detection-backend-guide.md)
- [Guía de pruebas de LightDetection](docs/development/light-detection-testing-guide.md)
- [Cronograma de junio para LightDetection](docs/development/june-light-detection-workplan.md)
>>>>>>> 906fb64bcc4507d01341306ee4fe0c9f547ee5e2

## Build

```bash
<<<<<<< HEAD
dotnet build src/LightEventDetector/LightEventDetector.csproj
=======
dotnet build VideoBatchProcessor.sln
```

El prototipo visual se puede ejecutar con:

```bash
dotnet run --project src/LightEventDetector/LightEventDetector.csproj
>>>>>>> 906fb64bcc4507d01341306ee4fe0c9f547ee5e2
```
