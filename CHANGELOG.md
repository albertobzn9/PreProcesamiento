# Changelog

Todos los cambios relevantes de Video Batch Processor se registran aquí. El
formato sigue las categorías de [Keep a Changelog](https://keepachangelog.com/)
sin inventar versiones de producto antes de la primera liberación.

Para el estado operativo actual, ver [Current Project Status](docs/project/current-status.md).

## [Unreleased] - 2026-07-16

### Added

- `BatchOrchestrator` inicial para Cruces Seguros: recorre subcarpetas, toma
  solo sesiones fuente CS, exige el MAT correspondiente, sincroniza video-MAT,
  genera el XLSX diagnóstico y exporta habituación, eventos e ITIs con FFmpeg.
- Reporte por sesión y clip para distinguir resultados exportados, con avisos,
  bloqueados, fallidos u omitidos, sin detener el lote completo.

- Lector binario `MatV5MatrixReader` para matrices MAT Level-5 históricas
  N×8/N×9, sin requerir MATLAB ni modificar el archivo fuente.
- Exportación XLSX de diagnóstico de luces: resumen de video, intervalos ON→OFF,
  cambios raw, ROIs, fuente conductual y comparación con MAT/CSV.
- `LightTimelineDiagnosticComparer`: estima el desfase por sesión usando lado y
  patrón temporal; conserva las anomalías visuales en lugar de desplazar toda
  la comparación por orden.
- `BehavioralVideoSynchronizer`: reutiliza esa comparación como puerta de
  calidad por sesión, con estados listo/aviso/bloqueado antes de segmentar.
- `SegmentPlanner` inicial: crea segmentos solo desde filas conductuales
  empatadas, conserva ITIs y habituación cubierta, y deja excepciones de cruce
  en revisión.
- Perfil local por video para conservar transformación de cámara y las tres
  ROIs al reabrir la misma sesión, ahora en coordenadas del video fuente.
- Hoja `Perfil de camara` dentro del XLSX diagnóstico, con formato versionado,
  dimensiones, transformación, ROIs fuente y umbrales reutilizables.
- Hoja `Segmentos planeados` dentro del XLSX diagnóstico: muestra límites,
  duración total, evidencia visual, datos MAT mapeados, clasificación de
  cruce/no cruce y comparación de lados antes de cortar cualquier video.
- La clasificación de eventos del lote usa únicamente el `Lado` previo:
  mismo lado = no cruce, cambio de lado = cruce. El primer evento queda como
  no aplicable y `Desplaz` se conserva raw sin crear revisión manual.
- `ClipExporter` inicial para CS: exporta un segmento individual con FFmpeg,
  límites de frame exactos, crop/giro/espejo y archivo temporal seguro.
- Pruebas para lectura MAT, intervalos de luz, Excel diagnóstico, perfil de
  cámara y emparejamiento diagnóstico.
- Pruebas para descubrimiento recursivo de candidatos del lote CS y para
  conservar el identificador correcto del ITI (`itiN`).

### Changed

- La interfaz de timeline muestra duración total y frames totales; sus campos
  de tiempo y frame se actualizan entre sí sin perder el rango exacto.
- La app acepta explícitamente video MKV además de MP4, AVI, MOV y M4V.
- En Cruces Seguros, el reporte presenta el LED como desactivado en vez de
  imprimir el valor técnico interno usado para deshabilitarlo.
- La documentación de arquitectura y estado ahora describe el lector MAT,
  perfiles locales, la comparación diagnóstica y el sincronizador actual.
- La comparación diagnóstica limita las filas conductuales al intervalo de
  video analizado cuando se revisa solo una parte de la sesión.

### Validated

- Con `exp_0526_cs_d4r4`, los primeros diez minutos recuperaron los 28 eventos
  MAT presentes en el intervalo y aislaron una señal visual breve de habituación
  como anomalía revisable.
- El desfase visual-MAT de esa sesión se estimó en aproximadamente `+2.409 s`;
  se conserva como evidencia por sesión, no como constante universal.
- El `SegmentPlanner` se ejecutó sobre la sesión CS completa: los 67 eventos
  MAT tuvieron señal visual compatible; produjo 1 habituación inicial, 67
  eventos, 66 ITIs y 1 habituación final. Cuatro señales visuales extra y las
  excepciones de lado quedaron como avisos, no como clips inventados.
- `dotnet build VideoBatchProcessor.sln` y `dotnet test VideoBatchProcessor.sln`
  pasan con 166 pruebas.

## Earlier Milestones

### Interface And Light Workflow (2026-07-12 to 2026-07-13)

- Se reemplazó la vista XAML experimental por HTML/CSS local dentro de
  `NativeWebView` de Avalonia; C# conserva selector nativo, lógica y acceso a
  video.
- Se integraron carga recursiva, reconocimiento de nomenclaturas, preview,
  giro de 180°, espejo, crop, marcado circular de ROIs, zoom/pan y calibración
  OFF/ON.
- Se añadió escaneo de timeline con progreso e intervalo elegible por tiempo o
  frames.

### Backend Foundation

- Se implementaron `NomenclatureParser`, `SessionMetadataResolver`,
  `VideoReader`, `FrameAnalyzer`, `BrightnessAdapter`, `LightDetection`,
  `LightCalibration`, resolución de fuentes conductuales y CSV V1 histórico.
- Se definieron la arquitectura modular, las tres nomenclaturas, el formato
  MAT, reglas de sincronización video-conducta y el futuro límite de integración
  con CajaValentia/OBS.

### Repository And Documentation Foundation

- Se reorganizó el repositorio como solución .NET con `src/`, `tests/` y
  `docs/`, separando documentos de producto, protocolo, referencia, diseño y
  desarrollo.
- Se consolidaron los documentos históricos sin perder su contenido y se
  estableció este repositorio como fuente activa frente al archivo histórico de
  Drive.
