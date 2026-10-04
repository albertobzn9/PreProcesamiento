# Pruebas de calibración visual

Verifican que imagen y círculos compartan posición y escala, sin modificar las
ROIs guardadas. Usan imágenes sintéticas, no videos del laboratorio. Cubren
distintos tamaños de ventana, proporciones, recorte, reapertura y referencias
OFF/ON. No sustituyen las pruebas de transformación de imagen del backend.

Requieren Node.js y Playwright. En macOS/Linux, desde la raíz del repo:

```sh
npm install --prefix /tmp/vbp-ui-tests playwright@1.62.1
/tmp/vbp-ui-tests/node_modules/.bin/playwright install webkit chromium
NODE_PATH=/tmp/vbp-ui-tests/node_modules node --test tests/WebUi/*.test.cjs
NODE_PATH=/tmp/vbp-ui-tests/node_modules UI_BROWSER=chromium node --test tests/WebUi/*.test.cjs
```

WebKit es la familia de motor usada por la app en macOS. Para usar Chrome ya
instalado, agregar `UI_CHANNEL=chrome` a la segunda ejecución.

`missing-source.test.cjs` verifica que el aviso no aparezca al iniciar, que
cancelar no procese y que seguir sin tabla requiera una decisión explícita.
También comprueba la palomita de tabla leída y la persistencia de los errores.

`video-setup-panel.test.cjs` cubre arrastre, límites, teclado, restauración,
persistencia y ventanas pequeñas; verifica que cámara y calibración no cambien.

La prueba integral de backend con video sintético y exportación real se activa
indicando la ruta de FFmpeg (no usa videos ni tablas del investigador):

```sh
VBP_TEST_FFMPEG=/opt/homebrew/bin/ffmpeg dotnet test tests/VideoBatchProcessor.Tests/VideoBatchProcessor.Tests.csproj --filter VideoOnlyExportIntegrationTests
```
