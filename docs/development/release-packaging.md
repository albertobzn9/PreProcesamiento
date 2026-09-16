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

El paquete `osx-arm64` se generó y verificó localmente para la validación
interna de CS/CP. Todavía no existe un release público: falta probar el paquete
en Mac Intel y Windows, registrar el origen/licencia de los binarios que se
distribuirán y firmar/notarizar cualquier entrega fuera del laboratorio.

## Crear La Aplicación De macOS

Colocar primero FFmpeg y FFprobe en `vendor/ffmpeg/osx-arm64/` o
`vendor/ffmpeg/osx-x64/`. Después ejecutar:

```bash
./scripts/package-macos.sh osx-arm64
```

El resultado queda en `artifacts/release/0.1.0/osx-arm64/` como `.app` y `.zip`.
Sin una identidad de Apple, el script aplica una firma local apropiada solo
para pruebas. La entrega fuera del equipo de desarrollo requiere Developer ID,
Hardened Runtime y notarización.

## Crear La Aplicación De Windows

En una computadora Windows, colocar `ffmpeg.exe` y `ffprobe.exe` en
`vendor/ffmpeg/win-x64/` y ejecutar PowerShell:

```powershell
./scripts/package-windows.ps1
```

El resultado contiene `VideoBatchProcessor.exe` y todas sus dependencias en un
ZIP. Esta primera forma es adecuada para validación interna. Después de probarla
se puede crear un instalador MSIX firmado.

## Validación Obligatoria

En una computadora que no tenga el repositorio ni .NET instalado:

1. Abrir la aplicación con doble clic.
2. Cargar un MP4 y un MKV.
3. Verificar preview, crop, giro, espejo y ROIs.
4. Leer un MAT y procesar una sesión CS conocida.
5. Confirmar el XLSX y los clips exportados.
6. Cerrar y abrir nuevamente para comprobar el perfil guardado.

Esta validación debe realizarse en Apple Silicon, Mac Intel y Windows x64. Un paquete compilado pero no ejecutado en su sistema destino no se considera
aprobado.

## Decisión Sobre FFmpeg

FFmpeg publica código fuente y enlaza compilaciones de terceros; el proyecto
debe registrar exactamente cuál distribuye. La configuración actual usa
`libx264`, por lo que la elección tiene consecuencias GPL. Antes de un release
público se debe conservar junto al paquete la licencia, el origen y la
configuración de compilación correspondientes. Esto no cambia el procesamiento
actual: evita distribuir una dependencia sin trazabilidad.
