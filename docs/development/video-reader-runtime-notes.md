# VideoReader Runtime Notes

Este documento resume el estado actual del modulo `VideoReader` y los ajustes hechos alrededor de su integracion en el repo.

## Lo que ya existe

- Eric agrego `VideoReader` en `src/VideoBatchProcessor.Core/VideoReader/`.
- El modulo ya expone:
  - apertura segura con `TryOpen(...)`
  - lectura secuencial con `MoveNext()` / `MoveNextAsync()`
  - lectura aleatoria por frame o timestamp
  - metadata de video en `VideoMetadata`
- Tambien agrego pruebas en `tests/VideoBatchProcessor.Tests/VideoReaderTests.cs`.

## Que ajuste se hizo

- Se agrego `tests/VideoBatchProcessor.Tests` a `VideoBatchProcessor.sln`.

Esto corrige un problema de infraestructura: antes, `dotnet test VideoBatchProcessor.sln` no ejecutaba el proyecto de pruebas porque la solucion no lo incluia.

## Resultado actual

- `NomenclatureParser` y `SessionMetadataResolver` pasan sus pruebas.
- `VideoReader` compila.
- El problema en esta Mac ARM no era la logica de `VideoReader`, sino la carga de librerias nativas de OpenCV al correr `dotnet test`.
- Con el runtime ARM64 correcto y un `DYLD_FALLBACK_LIBRARY_PATH` apuntando a la carpeta nativa de `OpenCvSharp`, la suite local vuelve a pasar.

## Problema observado en macOS ARM

Al correr pruebas de `VideoReader`, aparece `DllNotFoundException` para `OpenCvSharpExtern`.

La evidencia revisada apunta a tres puntos:

1. El proyecto estaba referenciando `OpenCvSharp4.runtime.osx.10.15-x64` incluso en macOS ARM.
2. El runtime correcto para Apple Silicon es `OpenCvSharp4.runtime.osx_arm64`.
3. Aun con ese runtime, `libOpenCvSharpExtern.dylib` busca algunas dependencias via `@executable_path/../libs`, y bajo `dotnet test` esa ruta no siempre coincide con donde NuGet deja las `.dylib`.

En otras palabras: el modulo estaba bien encaminado, pero hacia falta cerrar el entorno local de pruebas en macOS ARM.

## Ajuste aplicado del lado local

Se hicieron dos ajustes locales:

1. cambiar el runtime ARM64 del proyecto a `OpenCvSharp4.runtime.osx_arm64`;
2. agregar `scripts/test-macos.sh`, que:
   - construye el proyecto de pruebas,
   - detecta si la Mac es `arm64` o `x86_64`,
   - ubica la carpeta nativa correcta del runtime de OpenCV,
   - exporta `DYLD_FALLBACK_LIBRARY_PATH`,
   - y corre `dotnet test`.

Con ese wrapper, las pruebas locales que dependen de OpenCV vuelven a correr en esta Mac y el mismo comando se puede usar en una Mac Intel.

## Que no se cambio

- No se cambio la logica interna de `VideoReader`.
- No se reescribieron pruebas de Eric.
- No se cambio la decision arquitectonica de mantener `VideoReader` como modulo backend separado.

## Siguiente revision recomendada

Antes de seguir construyendo modulos arriba de `VideoReader`, conviene cerrar una de estas rutas:

1. Mantener `scripts/test-macos.sh` como comando local de referencia para macOS, tanto Apple Silicon como Intel.
2. Si se integra el trabajo remoto de Eric, volver a aplicar este mismo ajuste al snapshot ya fusionado.
3. Si OpenCV vuelve a fallar aun con el wrapper, entonces si conviene evaluar si `VideoReader` debe aislar mas su dependencia nativa.
