# Documentation

Índice principal de documentación del Video Batch Processor.

## Project

- [Product Requirements](project/product-requirements.md): requisitos de producto, decisiones actuales y especificación de usuario fusionadas sin resumir.
- [Architecture](project/architecture.md): arquitectura de módulos, flujo de uso, configuración y dependencias.
- [Architecture Review Log](project/architecture-review-log.md): decisiones aplicadas y pendientes de diseño retirados del documento principal.
- [CajaValentia Video-MAT Synchronization](project/sincronizacion-video-mat-cajavalentia.md): especificación para medir, estimar y reportar el desfase entre señales visuales y eventos MATLAB.
- [Future Integration Context](project/future-integration-context.md): horizonte compartido con CajaValentia y reglas de sesión que este proyecto debe respetar desde ahora.

## Protocol

- [CMC Protocol](protocol/cmc-protocol.md): explicación de la tarea CMC y guías de fases.
- [Illescas-Huerta et al. (2021) - CMC excerpt](protocol/illescas-huerta-2021-cmc.md): recorte del artículo original con las secciones relevantes para la tarea CMC.

## Reference

- [MAT Format](reference/mat-format.md): estructura de los archivos `.mat` generados por Caja Valentia.
- [Naming Convention](reference/naming-convention.md): tres nomenclaturas del proyecto: legacy, estándar del lab y output del Video Batch Processor.
- [Operational Terms And Decision Rules](reference/operational-terms.md): vocabulario y reglas compartidas para protocolo, parser, segmentación y exportación.

## Development

- [Eric Workplan](development/eric-workplan.md): estado verificado, reglas de trabajo y bloque activo a partir del 13 de julio de 2026.
- [MAT And Video Synchronization Guide](development/mat-video-synchronization-guide.md): por qué el `.mat` acompaña al video y cómo se asociarán sus eventos sin forzar una sincronía perfecta.

## Código

- [LightEventDetector](../src/LightEventDetector): prototipo actual en C# / Avalonia.
- [VideoBatchProcessor.Core](../src/VideoBatchProcessor.Core): librería backend reusable del producto.
