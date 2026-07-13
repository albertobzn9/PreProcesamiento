# Current Project Status

[← Volver al índice de documentación](../README.md)

**Fecha de revisión:** 12-07-2026

**Estado general:** El proyecto ya tiene una base conceptual sólida, módulos
backend útiles y probados, y una primera integración de interfaz que funciona.
Todavía no procesa ni exporta sesiones completas.

## Fuente Activa

El repositorio activo y canónico es:

```text
/Users/ab/Documents/GitHub/PreProcesamiento
```

Drive conserva antecedentes, ejemplos y archivo histórico. No debe funcionar
como una segunda copia activa del código o de los requisitos.

## Foto Del Proyecto

El producto final es una aplicación de escritorio para transformar sesiones CMC
largas en clips revisables y bien nombrados antes de DLC/BORIS. Para lograrlo,
combina tres fuentes de información:

1. El video: frames, crop, orientación y cambios de las tres luces.
2. Los datos conductuales: CSV V1 nuevo o MAT histórico.
3. Las decisiones del investigador: perfiles de cámara, ROIs, calibración y
   revisión de casos ambiguos.

La documentación ya expresa esa lógica con claridad. El trabajo pendiente es
convertirla gradualmente en los módulos que todavía faltan, sin duplicar reglas
en la interfaz.

## Estado Verificado

| Área | Estado | Evidencia |
|------|--------|-----------|
| Documentación conceptual | Sólida | Requisitos, arquitectura, protocolo, nomenclatura, MAT/CSV y sincronización están conectados entre sí. |
| Nomenclaturas | Implementadas y probadas | `NomenclatureParser` entiende legacy, estándar del lab y output. |
| Metadata de sesión | Implementada y probada | `SessionMetadataResolver` completa o señala datos faltantes. |
| Lectura de video | Implementada y probada | `VideoReader` usa OpenCvSharp para metadata y frames. |
| Medición de ROIs | Implementada y probada | `FrameAnalyzer` calcula brillo y valida ROIs. |
| Detección de luces | Implementada y probada con datos sintéticos | `LightDetector` compara brillo contra umbrales. |
| Fuente conductual CSV V1 | Implementada y probada | Resolver de rutas, validación estricta y lector de eventos/palanqueos. |
| MAT histórico | Parcial | Ya normaliza matrices N×8/N×9, pero falta el lector binario real del archivo `.mat`. |
| Interfaz de carga | Integrada | En macOS se verificó diseño HTML local, selector nativo y reconocimiento de nombres legacy. |
| Preview de video | Integrado y validado manualmente en macOS | `VideoReader` devuelve el primer frame JPEG y metadata reales a la interfaz. |
| CameraSetup inicial | Integrado y validado manualmente en macOS | Giro de 180°, espejo y recorte en modal actualizan el JPEG mostrado mediante `VideoTransformPreviewRenderer`; todavía no guarda perfiles. |

El estado actual compila y tiene **126 pruebas** aprobadas. La interfaz también
compila con Avalonia 12 y mantiene esas 126 pruebas.

## Interfaz Actual

La dirección validada para el frontend es:

```text
HTML/CSS local (diseño Stitch)
  dentro de NativeWebView de Avalonia
    conectado a C# / VideoBatchProcessor.Core
```

No convierte el producto en una página web ni mueve la lógica a JavaScript.
Avalonia y C# conservan selector de archivos, parser, video, datos conductuales
y procesamiento; HTML/CSS resuelve la presentación visual.

Usa el `NativeWebView` oficial y abierto de Avalonia 12. Ya confirma el primer
recorrido útil: HTML solicita archivos, macOS muestra el selector nativo, C#
analiza los nombres y HTML recibe la lista resultante.

La pantalla XAML anterior fue retirada. `MainWindow.axaml` permanece solo como
ventana anfitriona de `NativeWebView`; el diseño visible vive en
`src/VideoBatchProcessor.App/WebUi/index.html`. Así existe una única interfaz.

Al seleccionar una carpeta, la aplicación recorre sus subcarpetas. Omite por
defecto Luz-Comida (`f1`) y Condicionamiento al Miedo (`cm`/`f3`) porque no
pertenecen al flujo de cruces, e informa cuántas sesiones dejó fuera.

### Validación Manual Cerrada

El 12-07-2026 se verificó en macOS el primer flujo funcional de interfaz:

1. Elegir archivos individuales o una carpeta con subcarpetas.
2. Reconocer y etiquetar sesiones `Legacy` y `Estándar lab`.
3. Identificar un `Clip de output` como archivo generado, no como video fuente.
4. Marcar `Nombre no compatible` cuando un video no sigue una nomenclatura
   conocida.
5. Ofrecer `Quitar no compatibles (N)` cuando hay más de cinco; solo los retira
   del lote actual, nunca borra archivos físicos.
6. Abrir una sesión fuente compatible y confirmar que el panel **PREVIEW**
   muestra un frame útil, resolución, frames totales, FPS y duración coherentes.

Al seleccionar una sesión fuente compatible, el panel **PREVIEW** abre su primer
frame mediante `VideoReader` y muestra resolución, frames totales, FPS y
duración. Esta prueba se aprobó manualmente el 12-07-2026 en macOS.

El panel **CAMERA SETUP** ya permite girar 180°, espejar y abrir una herramienta
de recorte en modal. El video completo queda fijo dentro del área de trabajo;
el usuario mueve o redimensiona solo el marco de selección. La interfaz muestra
los límites `izquierda`, `arriba`, `derecha` y `abajo` de las dos esquinas del
recorte, aunque C# los conserva internamente como `x`, `y`, ancho y alto. Usa
una copia local de Cropper.js 1.6.2, de licencia MIT, para que el producto no
dependa de internet. La interfaz manda la decisión confirmada a C#;
`VideoTransformPreviewRenderer` aplica crop, giro de 180° y espejo al JPEG de
preview. Las coordenadas se guardan en píxeles del video fuente, no en píxeles
de la imagen reducida. Así la decisión podrá reutilizarse al procesar el video
completo. El flujo, incluidos límites numéricos, recorte completo, giro y
espejo, se aprobó manualmente el 12-07-2026 en macOS. Esta configuración vive
por ahora solo durante el lote abierto.

## Lo Que Falta

### Backend inmediato

1. `BrightnessAdapter`: conectar frames reales de `FrameAnalyzer` con
   `LightDetection` sin reescribir el cálculo de brillo.
2. `LightTimelineBuilder`: convertir lecturas por frame en transiciones estables.
3. Lector binario real de MAT histórico, manteniendo la normalización N×8/N×9
   ya existente.
4. `SegmentPlanner`: proponer eventos, ITIs, habituación y hallazgos conductuales.

### Backend posterior

1. Guardado y asignación de `CameraProfile` reutilizable para varias sesiones.
2. `ClipExporter` con FFmpeg/ffprobe internos.
3. `BatchOrchestrator`, reporte final y decisiones de revisión.

### Próxima integración de interfaz y Core

1. Marcar las tres ROIs y construir `LightCalibration` sobre el frame ya
   preparado, cuando `BrightnessAdapter` esté listo para probarlas.
2. Mostrar fuente conductual, avisos y campos faltantes por sesión cuando el
   flujo de revisión llegue a necesitarlos; no bloquea `CameraSetup`.

## Riesgos Y Decisiones Pendientes

- El resultado del módulo de luces todavía se prueba con brillo sintético; falta
  validarlo con videos reales y ROIs marcadas en una caja.
- No hay todavía segmentación, crop aplicado al archivo completo ni exportación
  de clips. La app no debe presentarse como procesador completo aún.
- El código de output para `SoundOnly` sigue pendiente de acuerdo del lab.
- El desfase video-conducta debe medirse por sesión; nunca usar un offset fijo.
- `docs/design/stitch_lab_interface_ux_redesign/` conserva el export original
  de Stitch como referencia trazable. `WebUi/index.html` es la implementación
  activa; no se deben editar ambas versiones como si fueran dos interfaces.

## Estado Git

- La rama activa es `codex/ui-stitch-fidelity` hasta que esta etapa se integre
  a la línea principal del repositorio.
- Esta interfaz ya pasó su prueba manual inicial. Repetirla al conectar un
  módulo nuevo, no por cambios puramente documentales.

## Próximo Orden Recomendado

1. En paralelo, completar `BrightnessAdapter` y probar luces con un video real.
2. Construir `LightCalibration` y validar las tres ROIs con `FrameAnalyzer`
   sobre el frame ya preparado por `CameraSetup`.
3. Con las ROIs y la calibración validadas, construir `LightTimelineBuilder`;
   no empezar exportación antes de tener transiciones visuales confiables.

## Lectura Para Retomar

1. [Product Requirements](product-requirements.md)
2. [Architecture](architecture.md)
3. [Current synchronization rules](sincronizacion-video-mat-cajavalentia.md)
4. [Eric Workplan](../development/eric-workplan.md)
