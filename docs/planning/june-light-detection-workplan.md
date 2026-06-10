# Cronograma De Junio - Light Detection

Este documento define el trabajo de junio para cerrar el módulo de detección de luces del Video Batch Processor.

El objetivo no es rehacer todo el programa. El objetivo es convertir el prototipo actual en un módulo pequeño, claro y verificable, siguiendo la arquitectura definida en [technical-proposal.md](../architecture/technical-proposal.md).

## Contexto

El prototipo actual en `src/LightEventDetector/` es valioso porque demuestra que se puede:

- Abrir un video.
- Marcar regiones de interés para las luces.
- Medir brillo en esas regiones.
- Mostrar si cada luz parece estar prendida o apagada.
- Generar archivos auxiliares como CSV/timeline para inspección visual.

Eso está bien como prototipo visual y herramienta de calibración. Sin embargo, no debe convertirse tal cual en la arquitectura final porque mezcla varias responsabilidades: UI, lectura de video, detección de luces, clasificación de eventos, procesamiento de video completo y generación de salidas.

Para el proyecto final, la regla es:

> El `technical-proposal.md` manda.

Si algo del prototipo encaja con la arquitectura, se rescata. Si algo es útil pero no corresponde al módulo actual, se conserva como herramienta de diagnóstico o prototipo. Si algo contradice la arquitectura, no se adopta como base.

## Responsabilidad Del Módulo

El módulo de trabajo de junio es:

```text
src/VideoBatchProcessor.Core/LightDetection/
```

Su responsabilidad es solamente detectar luces en un frame o muestra de video.

### Input

- Región de interés de la luz de comida izquierda.
- Región de interés de la luz de comida derecha.
- Región de interés del LED/ruido.
- Brillo promedio medido en cada región.
- Umbral de decisión para cada luz.
- Frame index y timestamp cuando aplique.

### Output

- `FoodLeft`: prendida/apagada.
- `FoodRight`: prendida/apagada.
- `NoiseLed`: prendido/apagado.
- Brillo medido de cada región.
- Umbral usado para cada región.

### Fuera De Alcance Por Ahora

Eric no debe implementar en este módulo:

- Lectura completa de video.
- Segmentación de ensayos.
- Clasificación de cruce/no cruce/timeout.
- Lectura de archivos `.mat`.
- Exportación de clips.
- Nomenclatura de archivos.
- Decisión de ITI o habituación.

Esas responsabilidades pertenecen a otros módulos del sistema.

## Explicación Para Eric

Lo que hiciste está bien como prototipo porque demuestra que la idea funciona: abrir un video, seleccionar regiones y ver si las luces se detectan como ON/OFF.

El ajuste que necesitamos ahora no es porque el trabajo esté mal, sino porque el proyecto completo va a crecer. Si una misma pieza abre videos, dibuja UI, clasifica eventos, genera reportes y detecta luces, después será difícil probarla, corregirla o integrarla con `.mat`.

La meta de junio es separar la parte esencial:

```text
LightDetector:
  Dado un frame/muestra y tres regiones, responde qué luces están ON/OFF.
```

Nada más. Si eso queda sólido, después otros módulos pueden usarlo para construir la línea de tiempo, planear segmentos y exportar clips.

## Criterio De Aceptación General

Cada avance debe poder revisarse sin depender de entender toda la app.

Para cada entrega debe existir una forma simple de comprobar:

- Qué comando correr.
- Qué salida esperar.
- Qué caso está probando.
- Qué limitaciones conocidas tiene.

## Semana 1 - Alineación Y Alcance

Fechas: miércoles 10 de junio a viernes 12 de junio de 2026.

Objetivo: entender la arquitectura y separar mentalmente prototipo vs módulo final.

### Miércoles 10 De Junio

- Leer la sección de `LightDetector` en `technical-proposal.md`.
- Revisar el prototipo actual en `src/LightEventDetector/`.
- Identificar qué partes corresponden al detector y qué partes son UI/debug/procesamiento.
- Explicar con palabras propias qué debe hacer `LightDetection` y qué no debe hacer.

Entregable:

- Lista breve: "se rescata", "se deja como prototipo", "no corresponde a este módulo".

### Jueves 11 De Junio

- Revisar `src/VideoBatchProcessor.Core/LightDetection/`.
- Entender los modelos actuales: `LightId`, `LightRoi`, `LightReading`, `LightSample`, `LightDetectionConfig`, `LightDetector`.
- Comparar esos nombres con los del prototipo: `Left`, `Center`, `Right`.
- Confirmar que la nomenclatura correcta del módulo es `FoodLeft`, `FoodRight`, `NoiseLed`.

Entregable:

- Explicación corta del flujo: ROI + brillo + umbral = ON/OFF.

### Viernes 12 De Junio

- Preparar ejemplos sintéticos simples para validar el detector sin video real.
- Documentar cómo se espera que responda el módulo en casos básicos.

Entregable revisable:

- Casos de prueba propuestos:
  - todo oscuro = todo OFF.
  - brillo alto en `FoodLeft` = `FoodLeft` ON.
  - brillo alto en `FoodRight` = `FoodRight` ON.
  - brillo alto en `NoiseLed` = `NoiseLed` ON.

## Semana 2 - Módulo LightDetection Cerrado

Fechas: lunes 15 de junio a viernes 19 de junio de 2026.

Objetivo: dejar el módulo `LightDetection` pequeño, probado y claro.

### Lunes 15 De Junio

- Implementar o ajustar pruebas unitarias para `LightRoi`.
- Validar que no acepte regiones inválidas: ancho cero, alto cero, coordenadas negativas o umbral negativo.

Entregable revisable:

- Pruebas de validacion de ROI.

### Martes 16 De Junio

- Implementar pruebas para `LightDetector`.
- Usar una fuente sintética de brillo, no video real.
- Confirmar que cada luz responde correctamente a su propio umbral.

Entregable revisable:

- Pruebas ON/OFF para `FoodLeft`, `FoodRight` y `NoiseLed`.

### Miércoles 17 De Junio

- Revisar el contrato de salida de `LightSample`.
- Confirmar que incluye frame index, timestamp, brillo, umbral y estado ON/OFF.
- Ajustar nombres si algo todavía puede confundirse con posición física en vez de significado experimental.

Entregable revisable:

- `LightSample` claro y explicado en comentarios o documentación breve.

### Jueves 18 De Junio

- Revisar que el módulo no dependa de Avalonia, OpenCV ni UI.
- Confirmar que el Core se puede compilar por separado.

Entregable revisable:

```bash
dotnet build src/VideoBatchProcessor.Core/VideoBatchProcessor.Core.csproj
```

Resultado esperado:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

### Viernes 19 De Junio

- Cierre de semana: limpiar nombres, comentarios y ejemplos.
- Escribir un README breve dentro o cerca del módulo si hace falta.

Entregable revisable:

- Comando único para correr build/pruebas del módulo.
- Nota corta de que queda dentro y fuera del alcance.

## Semana 3 - Integración Con El Prototipo Visual

Fechas: lunes 22 de junio a viernes 26 de junio de 2026.

Objetivo: usar el módulo limpio sin romper el prototipo visual.

### Lunes 22 De Junio

- Revisar como el prototipo actual construye ROIs desde la UI.
- Mapear esos valores a `LightDetectionConfig`.
- No cambiar todavia el comportamiento visual.

Entregable revisable:

- Mapa de equivalencia:
  - UI izquierda -> `FoodLeft`.
  - UI derecha -> `FoodRight`.
  - UI LED/centro -> `NoiseLed`.

### Martes 23 De Junio

- Crear adaptador entre OpenCV y `IFrameBrightnessSource`.
- La lógica de píxeles puede usar OpenCV, pero `LightDetector` no debe saber de OpenCV.

Entregable revisable:

- Adaptador pequeño que calcule brillo promedio por ROI.

### Miércoles 24 De Junio

- Conectar el prototipo visual al módulo limpio.
- Verificar que los indicadores visuales sigan mostrando ON/OFF.

Entregable revisable:

- Demo con un video real o frame real.

### Jueves 25 De Junio

- Probar casos difíciles:
  - reflejos.
  - luces parcialmente prendidas.
  - umbrales muy altos o bajos.
  - ROI mal colocada.
  - frame oscuro.

Entregable revisable:

- Lista de limites conocidos y recomendaciones para elegir umbrales.

### Viernes 26 De Junio

- Limpieza de integracion.
- Confirmar que el prototipo sigue siendo herramienta de calibración, no arquitectura final.

Entregable revisable:

- Build completo de la solución:

```bash
dotnet build VideoBatchProcessor.sln
```

Resultado esperado:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

## Semana 4 - Cierre De Junio

Fechas: lunes 29 de junio a martes 30 de junio de 2026.

Objetivo: dejar el trabajo entregable y entendible para continuar con otros módulos.

### Lunes 29 De Junio

- Preparar demo final del módulo de detección.
- Confirmar que el módulo funciona sin depender de `.mat`.
- Confirmar que no decide ensayos ni cruces.

Entregable revisable:

- Demo breve: configurar tres ROIs y mostrar ON/OFF.

### Martes 30 De Junio

- Escribir reporte breve de cierre:
  - Qué se hizo.
  - Cómo se prueba.
  - Qué limitaciones tiene.
  - Qué queda pendiente para julio si continúa.

Entregable revisable:

- Documento de cierre del módulo `LightDetection`.

## Reglas De Trabajo

- Seguir `technical-proposal.md`.
- Mantener `src/LightEventDetector/` como prototipo/calibración.
- Implementar lógica reusable en `src/VideoBatchProcessor.Core/`.
- No mezclar UI con lógica de detección.
- No agregar decisiones de protocolo dentro de `LightDetection`.
- No tocar `.mat` por ahora.
- No cambiar nombres públicos o arquitectura sin revisarlo antes.
- Cada entrega debe poder probarse con un comando o demo pequeña.

## Definición De Terminado Para Junio

El trabajo de junio se considera completo si:

- `LightDetection` detecta `FoodLeft`, `FoodRight` y `NoiseLed`.
- El módulo funciona con datos sintéticos y, si alcanza el tiempo, con el prototipo visual.
- El build de la solución pasa.
- Hay instrucciones claras para probarlo.
- Queda claro que el detector no segmenta ensayos ni interpreta conducta.
