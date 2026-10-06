<p align="center">
  <img src="src/VideoBatchProcessor.App/Assets/Branding/product-icon.png" alt="Video Batch Processor" width="160" height="160">
</p>

# Video Batch Processor

Aplicación de escritorio para preprocesamiento masivo de videos de la tarea de Conflicto Mediado por Cruces (CMC).

El objetivo del proyecto es convertir sesiones largas de video en clips cortos, consistentes y bien nombrados antes de usarlos en DeepLabCut, BORIS u otros análisis posteriores. La app busca normalizar crop, orientación, detección de luces, segmentación por eventos/ITIs/habituación y exportación por lote.

## Descargar La App

Instaladores públicos en [Releases](https://github.com/albertobzn9/PreProcesamiento/releases/tag/v0.3.1),
sin necesidad de clonar el repositorio ni instalar .NET:

- [Windows x64: descargar instalador](https://github.com/albertobzn9/PreProcesamiento/releases/download/v0.3.1/VideoBatchProcessor-0.3.1-win-x64-setup.exe)
  (recomendado) o [ZIP portátil](https://github.com/albertobzn9/PreProcesamiento/releases/download/v0.3.1/VideoBatchProcessor-0.3.1-win-x64.zip).
  Abrir el archivo, seguir el asistente y después iniciar la app desde el menú
  Inicio. Con el ZIP, extraer **todo** el contenido y abrir
  `VideoBatchProcessor.exe` sin moverlo fuera de su carpeta. Si falta WebView2, instalar
  el [runtime Evergreen de Microsoft](https://developer.microsoft.com/microsoft-edge/webview2/).
- [Mac Apple Silicon: descargar instalador](https://github.com/albertobzn9/PreProcesamiento/releases/download/v0.3.1/VideoBatchProcessor-0.3.1-osx-arm64.dmg)
  (recomendado) o [ZIP portátil](https://github.com/albertobzn9/PreProcesamiento/releases/download/v0.3.1/VideoBatchProcessor-0.3.1-osx-arm64.zip).
  Abrir el DMG y arrastrar la app a Aplicaciones; con el ZIP, extraer la app y
  abrirla desde cualquier ubicación. No es un paquete para Mac Intel.

La versión `0.3.1` es una **versión de prueba para el laboratorio**: incluye
FFmpeg/FFprobe, pero no firma pública de Windows ni notarización de Apple.
Los sistemas pueden mostrar advertencias de seguridad. La validación completa
en una PC Windows del laboratorio sigue pendiente.

## Estado

La interfaz ya procesa y exporta sesiones CS y CP con validación real.
DIS con comida también completó una sesión real: 60 eventos empatados y
121 recortes en `exp_0526_dis_d10r2`. La versión `0.3.1` conserva tabla pegada
desde Excel (ocho columnas), aviso si falta MAT/CSV, procesamiento aproximado
sin tabla con consentimiento y panel Video Setup ajustable.

- Producto activo: `VideoBatchProcessor.App`; `LightEventDetector` permanece
  como prototipo histórico.
- Backend: `VideoBatchProcessor.Core` ya incluye nomenclaturas, metadata,
  lectura de video, ROI/brillo, detección y calibración de luces, timeline,
  MAT/CSV y una puerta de sincronización video-conducta por sesión.
- Interfaz: HTML/CSS local dentro de Avalonia con carga recursiva, preview,
  giro, espejo, crop, ROIs circulares, calibración, rango de análisis y
  exportación XLSX de diagnóstico.
- Validación: 256 pruebas de backend pasan. En una sesión real de Cruces Seguros (CS), el
  diagnóstico completo empató los 67 eventos MAT con video y planeó 1
  habituación inicial, 67 eventos, 66 ITIs y 1 habituación final. Cuatro
  señales visuales extra quedaron como avisos para revisión, no como eventos.
  El XLSX incluye perfil de cámara, comparación y segmentos planeados. Un
  `ClipExporter` ya genera un clip individual con FFmpeg y transformaciones.
  `BatchOrchestrator` coordina CS/CP/DIS con emparejamiento MAT/CSV por contenido,
  sincronización, diagnóstico XLSX y exportación de clips desde la interfaz.
- Pendiente: perfiles reutilizables por
  grupo, más validaciones DIS, eventos de solo sonido y compatibilidad con
  el CSV actual de 10 columnas de CajaValentia.
- Limitaciones conocidas: por ahora colocar cada MAT junto a su video y con
  el mismo nombre base, o seleccionarlo manualmente. La búsqueda automática
  de tablas en subcarpetas separadas está pendiente. En d10r2 el reporte de
  habituación final declara dos frames más que el clip real; la discrepancia
  también existe en la ejecución anterior con MAT.

## Stack

- C# / .NET
- Avalonia UI
- HTML/CSS local dentro del WebView oficial de Avalonia para la interfaz final
- OpenCvSharp para lectura de video y análisis de frames
- FFmpeg incluido en los paquetes para exportación de clips

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

La preparación de paquetes `.app` y Windows se describe en la
[guía de empaquetado](docs/development/release-packaging.md). Los paquetes son
autónomos respecto de .NET y deben incluir FFmpeg/FFprobe portables antes de
distribuirse.

El prototipo histórico se puede ejecutar con:

```bash
dotnet run --project src/LightEventDetector/LightEventDetector.csproj
```
