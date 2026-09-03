# Current Project Status

[← Volver al índice de documentación](../README.md)

**Fecha de revisión:** 02-09-2026

**Estado general:** El proyecto ya tiene una base conceptual sólida, módulos
backend útiles y probados, y una primera integración de interfaz que funciona.
Ya puede coordinar y exportar sesiones completas de Cruces Seguros desde Core;
falta exponer ese recorrido en la interfaz y validarlo con un lote real.

## Fuente Activa

El repositorio activo y canónico es:

```text
/Users/ab/Documents/GitHub/PreProcesamiento
```

Drive conserva antecedentes, ejemplos y archivo histórico. No debe funcionar
como una segunda copia activa del código o de los requisitos.

## Responsabilidad Actual

Desde el 13-07-2026, AB continúa el backend, frontend, pruebas e integración.
La contribución de Eric terminó con el módulo de luces; su antiguo plan se
conserva solo como [handoff histórico](../development/eric-workplan.md).

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
| Fuente conductual CSV V1 | Implementada para contrato histórico de 9 columnas | Resolver de rutas, validación estricta y lector de eventos/palanqueos. Antes de automatizar CajaValentia falta aceptar su CSV actual de 10 columnas con `ensayo_cruce`. |
| MAT histórico | Implementado para MAT Level-5 N×8/N×9 | `MatV5MatrixReader` lee la matriz binaria sin MATLAB ni modificar el archivo; se comprobó con `exp_0526_cs_d4r4.mat` (67×8). MAT HDF5/v7.3 todavía no está cubierto. |
| Sincronización video-conducta por sesión | Implementada y probada | `BehavioralVideoSynchronizer` estima desfase por sesión y devuelve listo/aviso/bloqueo sin cambiar archivos. `BatchOrchestrator` ya la aplica antes de exportar cada sesión CS. |
| SegmentPlanner inicial | Implementado, probado y validado con CS completo | Parte exclusivamente de eventos MAT/CSV ya empatados; crea habituación, eventos, ITIs y final cuando el rango es completo. En `exp_0526_cs_d4r4` empató 67/67 eventos y planeó 135 segmentos. Para cada evento posterior al primero, mismo lado es no cruce y cambio de lado es cruce; `Desplaz` se conserva raw sin bloquear el lote. Los ITIs mantienen el número del evento anterior (`iti1`, `iti2`, etc.). Falta `SoundOnly`, UI de resumen y validación CP/DIS. |
| Procesamiento por lote CS | Integrado en interfaz; pendiente de validación real | `Process Batch` toma la cámara y calibración confirmadas de una sesión de referencia, usa la carpeta cargada como salida y llama a `BatchOrchestrator`. Primero ofrece una prueba de límites con pocos clips por sesión; después permite exportar todo. El lote toma solo videos CS fuente, empareja el MAT exacto, escanea luces, sincroniza, guarda un Excel diagnóstico y exporta clips con FFmpeg. Cada carpeta de salida incluye `clips_exportados.csv`: distingue el rango lógico del evento del rango físico exportado. Los eventos reciben cinco frames de contexto antes y después; habituación e ITIs no. Bloquea sesiones sin MAT, metadata suficiente o sincronización válida; no sobrescribe una salida existente. |
| Interfaz de carga | Integrada | En macOS se verificó diseño HTML local, selector nativo y reconocimiento de nombres legacy. |
| Preview de video | Integrado y validado manualmente en macOS | `VideoReader` devuelve el primer frame JPEG y metadata reales a la interfaz. |
| CameraSetup inicial | Integrado y validado manualmente en macOS | Giro de 180°, espejo y recorte en modal actualizan el JPEG mostrado mediante `VideoTransformPreviewRenderer`. Junto con las ROIs se guarda un perfil local, asociado a la ruta del video, en coordenadas del video fuente; al reabrirlo se convierte al frame preparado que usa el escáner. |
| Marcado de ROIs | Integrado y validado manualmente en macOS | Un modal marca círculos para `FoodLeft`, `FoodRight` y `NoiseLed`, con zoom de trackpad/rueda y desplazamiento por modo Mano, una pulsación de Espacio o botón central. Al crear un círculo pasa automáticamente a la siguiente luz pendiente; C# los valida con `FrameAnalyzer` en coordenadas reales del video preparado y los conserva en el perfil local del video. |
| LightCalibration inicial | Integrado y validado manualmente en macOS | Una barra navega por frame sobre el video preparado. Para `CS` guarda un frame OFF y otro con comida ON, sin exigir LED; para `CP`/`DIS` usa comida + `NoiseLed` ON. Mide el frame completo con `BrightnessAdapter`, propone umbrales, cierra al guardar y muestra confirmaciones visuales. Al reabrir conserva los frames elegidos y, si cambian las ROIs, vuelve a medir esos mismos frames antes de actualizar los umbrales. La otra luz de comida usa por ahora una referencia compartida provisional. |

El estado actual compila y tiene **176 pruebas** aprobadas. La interfaz también
compila con Avalonia 12 y mantiene esas 176 pruebas.

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
espejo, se aprobó manualmente el 12-07-2026 en macOS. Cuando se guardan las
ROIs, la transformación y las tres regiones se conservan en un perfil local
asociado a la ruta del video; al reabrirlo se recuperan, aunque la calibración
OFF/ON debe confirmarse de nuevo.

`LightMarker` y `LightCalibration` se aprobaron manualmente el 13-07-2026 en
macOS. El investigador puede marcar las tres luces con círculos, verificar esas
ROIs sobre un frame ON/OFF, guardar la calibración y volver a abrirla sin perder
los frames elegidos. Si ajusta una ROI, el programa relee los mismos frames con
la nueva región y actualiza los umbrales; no le pide repetir la búsqueda visual.

`LightTimelineBuilder` y `LightTimelineScanner` ya están conectados a una
tarjeta pequeña de interfaz. Después de guardar la calibración, **Analizar
luces** puede recorrer el video completo o un intervalo elegido por tiempo o
por frames. Aplica la misma transformación/ROIs usadas al calibrar, muestra
progreso real por frames procesados y presenta los primeros cambios ON/OFF
estables. Después del análisis puede exportar un Excel de diagnóstico con los
intervalos visuales ON→OFF, cambios raw, coordenadas de las tres ROIs, filas
MAT/CSV, comparación diagnóstica por lado y patrón temporal, y `Segmentos
planeados` antes de cortar videos. Cada segmento muestra su duración total y,
para eventos, la clasificación de cruce/no cruce junto con la comparación de
lado que la sustenta. El mismo libro incluye `Perfil de camara`,
una hoja estructurada con versión, dimensiones fuente, crop, giro, espejo, ROIs
fuente y umbrales para una importación futura.
Estima un
desfase por sesión, deja visibles las señales visuales sin MAT compatible y no
declara sincronización final. `BehavioralVideoSynchronizer` reutiliza esa misma
comparación como puerta de calidad: devuelve listo, aviso o bloqueo antes de
que exista segmentación. Si se analizó solo parte del video, compara únicamente
las filas conductuales de ese intervalo. En `exp_0526_cs_d4r4`, los primeros
diez minutos encontraron 28 eventos MAT, los 28 en video y un falso positivo
breve durante habituación; la prueba real inicial ya quedó aprobada.

La validación completa de `SegmentPlanner` con `exp_0526_cs_d4r4` recorrió
0:00 hasta 19:11.499. Empató los 67 eventos MAT con una señal visual y planeó
135 segmentos: 1 habituación inicial, 67 eventos, 66 ITIs y 1 habituación
final. No hubo filas MAT sin evidencia visual. Cuatro señales visuales no
tenían fila conductual compatible y quedaron como avisos, no como clips. El
libro `exp_0526_cs_d4r4_diagnostico_luces_completo.xlsx` permite revisar el
orden y los límites antes de habilitar la exportación. La etiqueta de cada
evento se obtiene automáticamente con el `Lado` del evento anterior; el primer
evento queda como `No aplica` y `Desplaz` permanece disponible como evidencia
raw, sin generar trabajo manual.

## Lo Que Falta

### Backend inmediato

1. Construir un coordinador por lote que resuelva cada fuente, escanee cada
   video y aplique `BehavioralVideoSynchronizer` antes de permitir segmentar.
2. Revisar el falso positivo breve de habituación y, solo con más sesiones,
   decidir si corresponde una regla de calidad adicional.
3. Conectar `ClipExporter` a la interfaz y al coordinador de lote para CS.
4. Conectar la importación de `Perfil de camara` a la UI y validar dimensiones,
   transformación y condiciones equivalentes antes de reutilizarlo en un lote.

### Backend posterior

1. Asignación explícita de un perfil de cámara reutilizable a varias sesiones.
2. Empaquetado administrado de FFmpeg/ffprobe para macOS y Windows.
3. `BatchOrchestrator` y reporte final por sesión.
4. Lector del manifiesto de captura de CajaValentia y compatibilidad del CSV
   actual de 10 columnas antes de habilitar procesamiento automático.

### Próxima validación focalizada

1. Probar la calibración con una sesión donde se encienda el lado de comida
   opuesto al ya usado. Eso decide con evidencia si la referencia compartida
   provisional es suficiente o se requieren referencias directas separadas.
2. Ejecutar **Analizar luces** con una sesión CP o DIS conocida y comparar los
   primeros encendidos/apagados mostrados con el video. Confirmar que crop,
   giro y espejo no alteran las ROIs medidas.

## Riesgos Y Decisiones Pendientes

- La ruta de calibración ya mide frames reales, conserva sus referencias al
  reabrirse y pasó su flujo manual inicial.
  La referencia compartida entre las dos luces de comida se mantiene provisional
  hasta probarla con el lado opuesto en otra sesión.
- Ya existe planeación lógica de segmentos CS, pero todavía no hay crop aplicado
  al archivo completo ni exportación de clips. La app no debe presentarse como
  procesador completo aún.
- El código de output para `SoundOnly` sigue pendiente de acuerdo del lab.
- El desfase video-conducta debe medirse por sesión; nunca usar un offset fijo.
- La integración futura con CajaValentia ya tiene contrato: OBS confirma la
  grabación antes de crear `R0`, y un manifiesto une video, CSV y sesión. Falta
  implementarla y validarla fuera de esta aplicación.
- `docs/design/stitch_lab_interface_ux_redesign/` conserva el export original
  de Stitch como referencia trazable. `WebUi/index.html` es la implementación
  activa; no se deben editar ambas versiones como si fueran dos interfaces.

## Estado Git

- La rama activa es `codex/ui-stitch-fidelity` hasta que esta etapa se integre
  a la línea principal del repositorio.
- Esta interfaz ya pasó su prueba manual inicial. Repetirla al conectar un
  módulo nuevo, no por cambios puramente documentales.

## Próximo Orden Recomendado

1. Hacer una prueba focalizada del lado de comida opuesto.
2. Ejecutar y revisar la timeline con una sesión CMC real conocida.
3. Construir el coordinador de sincronización por lote y validar que detecte
   fuentes ausentes, duplicadas o posiblemente cruzadas sin corregirlas solo.
4. Revisar la sesión CS completa ya planeada y después validar CP/DIS, revisando
   límites de LED, ITIs y habituación final antes de exportar clips.

## Lectura Para Retomar

1. [Product Requirements](product-requirements.md)
2. [Architecture](architecture.md)
3. [Current synchronization rules](sincronizacion-video-mat-cajavalentia.md)
4. [Light Module Handoff](../development/eric-workplan.md)
