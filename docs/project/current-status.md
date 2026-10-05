# Current Project Status

[← Volver al índice de documentación](../README.md)

**Fecha de revisión:** 05-10-2026

**Versión `0.3.0`:** logo propio, aviso de MAT/CSV
ausente, selección manual de una tabla existente y opción explícita de seguir
solo con luces. La salida aproximada conserva `na` para resultado desconocido
y no inventa sincronización ni filas conductuales. Pruebas automáticas de
segmentación, Excel y consentimiento de interfaz. Una prueba integral sin MAT
analizó un video sintético y exportó cinco MP4 con FFmpeg, comprobando que se
abren y que su número de frames coincide. La tabla se detecta al cargar o
seleccionar el video y muestra palomita solo si se pudo leer (no confirma aún
sincronización). El avance muestra frames y los errores no desaparecen.
El usuario confirmó que el paso 1 funciona después de los ajustes; quedó
guardado en el commit `0acaeae`. El paso 2 agrega ancho ajustable de Video Setup,
con límites, memoria local y doble clic para restablecer; quedó guardado en
`8b97620`. El paso 3 permite pegar ocho columnas y N filas desde Excel, validar,
previsualizar y asignarlas a un video sin importar archivos Excel. Pruebas de
parser, flujo de sincronización/exportación e interfaz. Una tabla pegada se
emparejó con tres eventos sintéticos y produjo siete recortes reales verificados;
la prueba real del usuario con `exp_0526_dis_d10r2` también se completó.
Se incluye en los paquetes `0.3.0` para Windows x64 y Mac Apple Silicon;
la app instalada no se reemplaza automáticamente al publicar.
Ver [formato y trazabilidad](../reference/mat-format.md#tabla-pegada-desde-excel).

### Validación De Tabla Pegada (04-10-2026)

Comparación recursiva de la salida anterior con MAT y la nueva con tabla pegada:
ambas contienen 121 clips (60 eventos, 59 ITIs y dos habituaciones), los mismos
nombres y resultados (26 cruces, 30 no cruces y cuatro timeouts), y 60/60 eventos
empatados. En 117 clips los límites son idénticos; `e04`, `e13`, `iti04` e `iti12`
varían un frame. El encuadre, los ROIs y los umbrales también cambiaron entre
ejecuciones; por tanto, no es una comparación con configuración idéntica.

La tabla pegada coincide con el MAT al redondearlo a un decimal, pero pierde
precisión temporal. Conviene mostrar todos los decimales disponibles en Excel
antes de copiar. La señal visual adicional alrededor de 17:55 sigue como aviso,
sin crear otro ensayo.

**Pendiente conocido:** en ambas ejecuciones la habituación final contiene
5762 frames, frente a 5764 declarados en el índice. Se confirmó contando los
frames decodificados. Es una discrepancia previa de dos frames (unos 0.067 s),
no una regresión del pegado; falta investigar y corregir su causa.

**Estado general:** El proyecto ya tiene una base conceptual sólida, módulos
backend útiles y probados, y una primera integración de interfaz que funciona.
Ya coordina y exporta sesiones completas de CS y CP desde la interfaz,
emparejando cada video con MAT/CSV por contenido. CP d2r1 fue validado por el
investigador. DIS con comida está implementado y la corrida real
`exp_0526_dis_d10r2` terminó con 60/60 eventos empatados y 121/121 recortes.
Se verificaron duraciones y frames de 85 recortes durante la exportación;
la revisión visual de todos los límites sigue siendo responsabilidad del usuario.
DIS con MAT/CSV está incluido desde `0.2.0`; el pegado de tablas, el panel
ajustable y la opción sin tabla se incluyen en `0.3.0`.

La detección adicional de comida derecha en 17:55.533–17:55.900 no tenía fila
MAT. Se conserva como aviso y dentro del clip `e34`, sin crear un ensayo extra.

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
| Emparejamiento automático por lote | Integrado; primera corrida DIS real completada | `Procesar sesiones` ejecuta `BatchSessionPairingAnalyzer` antes de recortar. Lee cada entrada una vez, compara todas las combinaciones y solo confirma parejas fuertes. Detecta MAT duplicados, nombres cruzados, ambigüedades y archivos sin pareja en `emparejamiento_sesiones.csv`. MP4 tiene prioridad y MKV funciona como respaldo. El nombre nunca decide la pareja. Una discrepancia se detiene y la interfaz permite elegir otra tabla para comprobarla u omitir el video. La barra pondera el avance por frames de video para no aparentar un bloqueo después de leer muchos MAT pequeños. |
| SegmentPlanner inicial | CS y CP validados; primera corrida DIS real completada | Parte exclusivamente de eventos MAT/CSV ya empatados; crea habituación, eventos, ITIs y final cuando el rango es completo. En CP y DIS usa `Lado=-2` para timeout y clasifica los demás eventos con `Desplaz > 1 s` como cruce o `<= 1 s` como no cruce. Los ITIs mantienen el número del evento anterior (`iti01`, `iti02`, etc.). Falta `SoundOnly` y ampliar la validación DIS. |
| Procesamiento por lote CS/CP/DIS | DIS probado con una sesión real | Cada evento conserva su orden como `e01`, `e02`, `e03`...; en DIS el tipo alterna entre `s` y `p` según la tabla, con resultado `cr`, `nc` o `to`. `Procesar sesiones` toma la cámara y calibración confirmadas de una sesión de referencia y puede procesar uno o muchos videos. Primero confirma cada MAT/CSV por contenido, reutiliza ese escaneo para sincronizar y exportar, y bloquea parejas dudosas. La prueba rápida crea cinco clips de eventos/ITIs sin habituación. Cada carpeta termina en `_recortes` y conserva diagnóstico, `clips_exportados.csv` y un checkpoint de progreso; los eventos reciben cinco frames de contexto antes y después. |
| Interfaz de carga | Integrada | En macOS se verificó diseño HTML local, selector nativo y reconocimiento de nombres legacy. |
| Preview de video | Integrado y validado manualmente en macOS | `VideoReader` devuelve el primer frame JPEG y metadata reales a la interfaz. |
| CameraSetup inicial | Integrado y validado manualmente en macOS | Giro de 180°, espejo y recorte en modal actualizan el JPEG mostrado mediante `VideoTransformPreviewRenderer`. Junto con las ROIs se guarda un perfil local, asociado a la ruta del video, en coordenadas del video fuente; al reabrirlo se convierte al frame preparado que usa el escáner. |
| Marcado de ROIs | Integrado y validado manualmente en macOS | Un modal marca círculos para `FoodLeft`, `FoodRight` y `NoiseLed`, con zoom de trackpad/rueda y desplazamiento por modo Mano, una pulsación de Espacio o botón central. Al crear un círculo pasa automáticamente a la siguiente luz pendiente; C# los valida con `FrameAnalyzer` en coordenadas reales del video preparado y los conserva en el perfil local del video. |
| LightCalibration inicial | Integrado y validado manualmente en macOS | Una barra navega por frame sobre el video preparado. Para `CS` guarda un frame OFF y otro con comida ON, sin exigir LED; para `CP`/`DIS` usa comida + `NoiseLed` ON. Mide el frame completo con `BrightnessAdapter`, propone umbrales, cierra al guardar y muestra confirmaciones visuales. Al reabrir conserva los frames elegidos y, si cambian las ROIs, vuelve a medir esos mismos frames antes de actualizar los umbrales. La otra luz de comida usa por ahora una referencia compartida provisional. |

El backend tiene **256 pruebas** aprobadas. Incluyen la regla compartida
CP/DIS, umbral exacto de 1 s, timeout y una sesión DIS sintética que verifica
sincronización, límites de comida/LED, orden y cobertura continua, además de
tabla pegada y exportación real con FFmpeg. La interfaz tiene 20 pruebas,
ejecutadas en WebKit y Chrome (40 ejecuciones), incluida la carga del logo.

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
por frames. Conserva las mismas transformaciones/ROIs usadas al calibrar, pero
convierte las tres ROIs a coordenadas fuente una sola vez para no reconstruir
innecesariamente cada frame completo. Muestra
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

1. Corregir la discrepancia de dos frames entre el índice y el clip de
   habituación final detectada en d10r2; no regenerar datos del usuario sin aviso.
2. Inventariar MAT/CSV en toda la carpeta seleccionada, incluso en subcarpetas
   sin videos. Actualmente se buscan junto a los videos y el aviso de tabla
   ausente puede aparecer antes del emparejamiento por contenido si el nombre
   no coincide. Hasta corregirlo, colocar cada tabla junto a su video con el
   mismo nombre base, o seleccionarla manualmente.
3. Validar con dos videos CP reales que el inventario empareje correctamente el
   video mal nombrado `r1d5` con su tabla y exporte cinco clips sin habituación.
4. Revisar el falso positivo breve de habituación y, solo con más sesiones,
   decidir si corresponde una regla de calidad adicional.
5. Conectar la importación de `Perfil de camara` a la UI y validar dimensiones,
   transformación y condiciones equivalentes antes de reutilizarlo en un lote.

### Backend posterior

1. Asignación explícita de un perfil de cámara reutilizable a varias sesiones.
2. Validar manualmente el paquete Windows `0.3.0` en una PC del laboratorio.
   Mac Apple Silicon y la máquina virtual Windows ya generan paquetes autónomos
   con FFmpeg/ffprobe trazables; Mac Intel se omite en esta versión. La firma
   pública/notarización queda para una entrega fuera del laboratorio.
3. Ampliar la validación DIS a otras sesiones antes de publicar una nueva app.
4. Lector del manifiesto de captura de CajaValentia y compatibilidad del CSV
   actual de 10 columnas antes de habilitar procesamiento automático.

### Validación CP Anterior

1. Cargar `exp_0526_cp_d5r1.mp4` y `exp_0526_cp_r1d5.mp4` desde la carpeta de prueba.
   `r1d5` puede corregirse con el botón **Corregir** si se conoce su nombre real,
   o conservarse para probar el emparejamiento por contenido.
2. Configurar cámara y luces usando `d5r1` como referencia y ejecutar la prueba
   rápida de cinco clips.
3. Confirmar que `r1d5.mp4` se asocie con `exp_0526_cp_d5r2.mat`, que ninguna
   habituación se exporte y que los diez clips resultantes tengan límites
   correctos en el video original.

## Riesgos Y Decisiones Pendientes

- La ruta de calibración ya mide frames reales, conserva sus referencias al
  reabrirse y pasó su flujo manual inicial.
  La referencia compartida entre las dos luces de comida se mantiene provisional
  hasta probarla con el lado opuesto en otra sesión.
- El resolver por contenido ya alimenta al orquestador, pero todavía debe pasar
  la prueba CP real antes de considerarlo validado para lotes completos.
- El código de output para `SoundOnly` sigue pendiente de acuerdo del lab.
- El desfase video-conducta debe medirse por sesión; nunca usar un offset fijo.
- La integración futura con CajaValentia ya tiene contrato: OBS confirma la
  grabación antes de crear `R0`, y un manifiesto une video, CSV y sesión. Falta
  implementarla y validarla fuera de esta aplicación.
- `docs/design/stitch_lab_interface_ux_redesign/` conserva el export original
  de Stitch como referencia trazable. `WebUi/index.html` es la implementación
  activa; no se deben editar ambas versiones como si fueran dos interfaces.

## Estado Git

- `main` reúne los tres pasos de `codex/optional-behavioral-source`.
- La entrega `v0.3.0` conserva `v0.2.0` como versión anterior recuperable.
  Publicar los paquetes no reemplaza automáticamente la app instalada.
- Esta interfaz ya pasó su prueba manual inicial. Repetirla al conectar un
  módulo nuevo, no por cambios puramente documentales.

## Próximo Orden Recomendado

1. Elegir un video DIS real y su MAT, confirmar ROIs y calibrar comida + LED.
2. Ejecutar la prueba de cinco clips y revisar el diagnóstico; comprobar que
   haya ejemplos seguros y peligrosos antes de dar por validada la mezcla.
3. Corroborar tiempos en el original, `eNN`/`itiNN`, tipo `s`/`p` y resultado
   por desplazamiento, incluidos timeouts si existen.
4. Repetir la validación en otra sesión; d10r2 ya completó el procesamiento.

## Lectura Para Retomar

1. [Product Requirements](product-requirements.md)
2. [Architecture](architecture.md)
3. [Current synchronization rules](sincronizacion-video-mat-cajavalentia.md)
4. [Light Module Handoff](../development/eric-workplan.md)
