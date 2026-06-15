# Documentation

Índice principal de documentación del Video Batch Processor.

<<<<<<< HEAD
## Requirements

- [Product Requirements](requirements/product-requirements.md): requisitos de producto, decisiones actuales y especificación de usuario fusionadas sin resumir.
=======
## Project

- [Product Requirements](project/product-requirements.md): requisitos de producto, decisiones actuales y especificación de usuario fusionadas sin resumir.
- [Architecture](project/architecture.md): arquitectura de módulos, flujo de uso, configuración y dependencias.
>>>>>>> 906fb64bcc4507d01341306ee4fe0c9f547ee5e2

## Protocol

- [CMC Protocol](protocol/cmc-protocol.md): explicación de la tarea CMC y guías de fases.
<<<<<<< HEAD
=======
- [Illescas-Huerta et al. (2021) - CMC excerpt](protocol/illescas-huerta-2021-cmc.md): recorte del artículo original con las secciones relevantes para la tarea CMC.
>>>>>>> 906fb64bcc4507d01341306ee4fe0c9f547ee5e2

## Reference

- [MAT Format](reference/mat-format.md): estructura de los archivos `.mat` generados por Caja Valentia.
- [Naming Convention](reference/naming-convention.md): tres nomenclaturas del proyecto: legacy, estándar del lab y output del Video Batch Processor.
- [Operational Terms](reference/operational-terms.md): convención operativa de términos para implementar parsers, segmentadores y exportadores.

<<<<<<< HEAD
## Literature

- [Illescas-Huerta et al. (2021) - CMC excerpt](literature/illescas-huerta-2021-cmc.md): recorte del artículo original con las secciones relevantes para la tarea CMC.

## Arquitectura

- [Technical Proposal](architecture/technical-proposal.md): propuesta técnica de módulos, flujo de uso, configuración y dependencias.

## Planning

- [June Light Detection Workplan](planning/june-light-detection-workplan.md): cronograma de junio para cerrar el módulo `LightDetection` y alinear el prototipo con la arquitectura.

## Development

- [LightDetection Backend Guide](development/light-detection-backend-guide.md): explicación de backend sin GUI, uso del módulo `LightDetection` y forma de invocar `LightDetector.Analyze`.
=======
## Development

- [LightDetection Backend Guide](development/light-detection-backend-guide.md): explicación de backend sin GUI, uso del módulo `LightDetection` y forma de invocar `LightDetector.Analyze`.
- [LightDetection Testing Guide](development/light-detection-testing-guide.md): explicación de pruebas automáticas para validar `LightDetection` sin GUI ni video real.
- [June Light Detection Workplan](development/june-light-detection-workplan.md): cronograma de junio para cerrar el módulo `LightDetection` y alinear el prototipo con la arquitectura.
>>>>>>> 906fb64bcc4507d01341306ee4fe0c9f547ee5e2

## Código

- [LightEventDetector](../src/LightEventDetector): prototipo actual en C# / Avalonia.
<<<<<<< HEAD
=======
- [VideoBatchProcessor.Core](../src/VideoBatchProcessor.Core): librería backend reusable del producto.
>>>>>>> 906fb64bcc4507d01341306ee4fe0c9f547ee5e2
