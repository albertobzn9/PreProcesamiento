# Video Batch Processor

Aplicación de escritorio para preprocesamiento masivo de videos de la tarea de Conflicto Mediado por Cruces (CMC).

El objetivo del proyecto es convertir sesiones largas de video en clips cortos, consistentes y bien nombrados antes de usarlos en DeepLabCut, BORIS u otros análisis posteriores. La app busca normalizar crop, orientación, detección de luces, segmentación por eventos/ITIs/habituación y exportación por lote.

## Estado

Proyecto en fase de definición técnica y prototipo.

- Producto completo: Video Batch Processor.
- Prototipo actual: `LightEventDetector`, una app Avalonia que permite abrir un video, marcar ROIs de luces, detectar eventos ON/OFF y exportar una línea de tiempo con CSV.
- Próximo objetivo técnico: convertir el prototipo en un pipeline por lotes con parsing de nomenclatura, crop/orientación, segmentación de clips y exportación.

## Stack

- C# / .NET
- Avalonia UI
- OpenCvSharp
- FFmpeg como dependencia prevista para procesamiento/exportación de video

## Estructura

```text
.
├── docs/                    # Documentación de producto, protocolo, referencia y arquitectura
├── src/LightEventDetector/   # Prototipo C# / Avalonia existente
└── VideoBatchProcessor.sln   # Solución C#
```

## Documentación

- [Índice de documentación](docs/README.md)
- [Requisitos de producto](docs/requirements/product-requirements.md)
- [Protocolo CMC](docs/protocol/cmc-protocol.md)
- [Formato .mat](docs/reference/mat-format.md)
- [Nomenclatura](docs/reference/naming-convention.md)
- [Convención operativa de términos](docs/reference/operational-terms.md)
- [Propuesta técnica](docs/architecture/technical-proposal.md)

## Build

```bash
dotnet build src/LightEventDetector/LightEventDetector.csproj
```
