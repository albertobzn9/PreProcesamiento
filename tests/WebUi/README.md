# Pruebas de calibración visual

Verifican que imagen y círculos compartan posición y escala, sin modificar las
ROIs guardadas. Usan imágenes sintéticas, no videos del laboratorio. Cubren
distintos tamaños de ventana, proporciones, recorte, reapertura y referencias
OFF/ON. No sustituyen las pruebas de transformación de imagen del backend.

Requieren Node.js y Playwright. En macOS/Linux, desde la raíz del repo:

```sh
npm install --prefix /tmp/vbp-ui-tests playwright@1.62.1
/tmp/vbp-ui-tests/node_modules/.bin/playwright install webkit chromium
NODE_PATH=/tmp/vbp-ui-tests/node_modules node --test tests/WebUi/calibration-layout.test.cjs
NODE_PATH=/tmp/vbp-ui-tests/node_modules UI_BROWSER=chromium node --test tests/WebUi/calibration-layout.test.cjs
```

WebKit es la familia de motor usada por la app en macOS. Para usar Chrome ya
instalado, agregar `UI_CHANNEL=chrome` a la segunda ejecución.
