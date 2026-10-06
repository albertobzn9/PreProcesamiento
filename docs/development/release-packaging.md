# Release Packaging

[← Volver al índice de documentación](../README.md)

## Objetivo

Entregar Video Batch Processor como una aplicación que se abre normalmente y no requiere que el usuario instale .NET, FFmpeg o FFprobe.

La distribución se genera por separado para:

- `osx-arm64`: Macs con Apple Silicon.
- `osx-x64`: Macs Intel.
- `win-x64`: Windows de 64 bits.

## Estado

La aplicación tiene nombre y versión, elige el runtime nativo de OpenCV según
el paquete y busca primero FFmpeg dentro de su propia carpeta `tools/`. En
desarrollo conserva como respaldo el FFmpeg instalado en el sistema.

La versión interna `0.3.1` se distribuye para Mac Apple Silicon y Windows x64.
Mac se construye y prueba localmente; Windows se construye dentro de una máquina
virtual efímera de GitHub Actions. Mac Intel se omite en esta versión por
decisión del proyecto. Ambas entregas incluyen FFmpeg/FFprobe portables, aviso
de procedencia y una huella SHA-256. La app de Mac usa firma local; una entrega
pública fuera del laboratorio todavía requiere Developer ID y notarización.

## Crear La Aplicación De macOS

Colocar primero FFmpeg y FFprobe en `vendor/ffmpeg/osx-arm64/` o
`vendor/ffmpeg/osx-x64/`. Después ejecutar:

```bash
./scripts/package-macos.sh osx-arm64
```

El resultado queda en `artifacts/release/0.3.1/osx-arm64/` como `.app`,
`VideoBatchProcessor-<version>-osx-arm64.dmg`, un ZIP portátil y sus archivos
`.sha256`. El DMG muestra la aplicación y el acceso a Aplicaciones para que el
usuario la arrastre una vez. El ZIP conserva la misma app para abrirla sin instalar.
Sin una identidad de Apple, el script aplica una firma local apropiada solo
para pruebas. La entrega fuera del equipo de desarrollo requiere Developer ID,
Hardened Runtime y notarización.

## Crear La Aplicación De Windows

El workflow manual `Build Windows release` usa una máquina virtual Windows x64,
verifica la descarga fijada de FFmpeg, ejecuta las pruebas y llama:

```powershell
./scripts/package-windows.ps1 -Version 0.3.1
```

El resultado incluye `VideoBatchProcessor-<version>-win-x64-setup.exe` y un ZIP
portátil con la misma carpeta publicada. El instalador creado con Inno Setup
incluye `VideoBatchProcessor.exe`, sus dependencias y herramientas multimedia;
instala bajo el perfil del usuario, crea un acceso en Inicio, ofrece acceso de
escritorio opcional y deja un desinstalador. El ZIP se extrae completo y se abre
sin instalación. Ambas formas son adecuadas para validación interna.
La interfaz usa Microsoft WebView2: Windows 11 normalmente ya lo incluye;
Windows 10 puede requerir instalar el runtime Evergreen de Microsoft. La máquina
virtual lo instala antes de comprobar el arranque del paquete.

## Validación Obligatoria

En una computadora que no tenga el repositorio ni .NET instalado:

1. Abrir la aplicación con doble clic.
2. Cargar un MP4 y un MKV.
3. Verificar preview, crop, giro, espejo y ROIs.
4. Leer un MAT y procesar una sesión CS conocida.
5. Confirmar el XLSX y los clips exportados.
6. Cerrar y abrir nuevamente para comprobar el perfil guardado.

Para `0.3.1`, esta validación debe realizarse en Apple Silicon y Windows x64.
El workflow de Windows comprueba compilación, pruebas, estructura y arranque;
la prueba manual completa en una PC del laboratorio sigue siendo obligatoria.

## Logo E Iconos

El logo aprobado vive en `src/VideoBatchProcessor.App/Assets/Branding/product-logo.png`.
La variante con fondo claro para GitHub y los iconos nativos está en
`Assets/Branding/product-icon.png`, dentro del mismo proyecto. Se generaron
con la herramienta de imágenes integrada: un fotograma dividido en tres clips
con un símbolo de reproducción; la segunda variante añade fondo blanco para
mantener contraste. Se usan para el icono de la app en Aplicaciones/Dock,
el ejecutable Windows y la portada de GitHub. Dentro de la ventana se conservan
los escudos UNAM e IFC. El archivo de referencia de Stitch no se modifica.

Para regenerar `.icns` e `.ico` desde la imagen aprobada, en macOS:

```sh
node scripts/build-product-icons.mjs
```

El script solo convierte formatos y tamaños; no cambia el diseño.

## Decisión Sobre FFmpeg

FFmpeg publica código fuente y enlaza compilaciones de terceros; el proyecto
debe registrar exactamente cuál distribuye. La configuración actual usa
`libx264`, por lo que la elección tiene consecuencias GPL. Antes de un release
público se debe conservar junto al paquete la licencia, el origen y la
configuración de compilación correspondientes. Esto no cambia el procesamiento
actual: evita distribuir una dependencia sin trazabilidad.
