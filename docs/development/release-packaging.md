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

La versión interna `0.2.0` se distribuye para Mac Apple Silicon y Windows x64.
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

El resultado queda en `artifacts/release/0.2.0/osx-arm64/` como `.app`, `.zip` y
archivo `.sha256`.
Sin una identidad de Apple, el script aplica una firma local apropiada solo
para pruebas. La entrega fuera del equipo de desarrollo requiere Developer ID,
Hardened Runtime y notarización.

## Crear La Aplicación De Windows

El workflow manual `Build Windows release` usa una máquina virtual Windows x64,
verifica la descarga fijada de FFmpeg, ejecuta las pruebas y llama:

```powershell
./scripts/package-windows.ps1 -Version 0.2.0
```

El resultado contiene `VideoBatchProcessor.exe`, todas sus dependencias, las
herramientas multimedia y su archivo `.sha256` dentro del artefacto del workflow.
Esta forma es adecuada para validación interna. Después de probarla manualmente
en una computadora del laboratorio se puede crear un instalador MSIX firmado.
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

Para `0.2.0`, esta validación debe realizarse en Apple Silicon y Windows x64.
El workflow de Windows comprueba compilación, pruebas, estructura y arranque;
la prueba manual completa en una PC del laboratorio sigue siendo obligatoria.

## Decisión Sobre FFmpeg

FFmpeg publica código fuente y enlaza compilaciones de terceros; el proyecto
debe registrar exactamente cuál distribuye. La configuración actual usa
`libx264`, por lo que la elección tiene consecuencias GPL. Antes de un release
público se debe conservar junto al paquete la licencia, el origen y la
configuración de compilación correspondientes. Esto no cambia el procesamiento
actual: evita distribuir una dependencia sin trazabilidad.
