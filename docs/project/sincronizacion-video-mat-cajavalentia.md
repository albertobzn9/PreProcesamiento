# Sincronizacion Video-MAT De CajaValentia

## Proposito

Definir como el programa debe medir y reportar el desfase entre las luces que
aparecen en el video y los tiempos que guarda MATLAB en el archivo `.mat`.

No se debe asumir que el video y MATLAB empiezan a contar desde el mismo
instante.

## Hecho Experimental Que Debe Verificarse

En videos de discriminacion se observo aproximadamente lo siguiente:

```text
LED de ruido blanco / amenaza ON
             ~4 s despues
luz de comida ON
```

En el codigo de CajaValentia, la secuencia de riesgo esta ordenada como:

```text
ruido blanco -> LED de amenaza -> parrilla -> luz de comida -> reloj R2
```

El codigo revisado contiene esperas de menos de un segundo en ese tramo. Por
eso, los ~4 s observados en video deben tratarse como una medicion fisica que
requiere confirmacion por sesion; no como un numero fijo que se reste a todos
los videos.

## Regla De Sincronizacion

Para vincular un evento del `.mat` con el video, el ancla principal es el
**encendido de la luz de comida**, no el encendido del LED de ruido blanco.

El LED de ruido blanco marca el inicio visual de la advertencia/amenaza. Debe
conservarse como una fase anterior del mismo evento peligroso, pero no debe
desplazar silenciosamente el tiempo de inicio usado para empatar con MATLAB.

## Datos Que Debe Conservar El Pipeline

Por cada evento detectado en video, guardar al menos:

| Campo | Significado |
|---|---|
| `noiseLedOnFrame` / `noiseLedOnSeconds` | Primer frame estable con LED de amenaza encendido. |
| `foodLightOnFrame` / `foodLightOnSeconds` | Primer frame estable con luz de comida encendida. |
| `foodLightOffFrame` / `foodLightOffSeconds` | Fin visual de la oportunidad de comida, si se detecta. |
| `warningToFoodSeconds` | `foodLightOnSeconds - noiseLedOnSeconds`. |
| `matchedMatEventIndex` | Fila/evento del `.mat` asociado, si existe. |
| `matStartEstimateSeconds` | Estimacion del inicio MATLAB, conservando tambien los valores raw usados. |
| `matToFoodOffsetSeconds` | `foodLightOnSeconds - matStartEstimateSeconds`. |
| `confidence` | Calidad del emparejamiento y motivo de cualquier advertencia. |

El `.mat` original no se modifica. El reporte debe conservar tanto los valores
raw como las estimaciones calculadas.

## Como Interpretar El `.mat`

En los resultados legacy, `Tiempo Absoluto` se escribe al registrar el
resultado y `Latencia` se mide desde el reloj `R2`. Una estimacion inicial del
inicio de evento puede ser:

```text
inicio MATLAB estimado = Tiempo Absoluto - Latencia
```

Esto es una hipotesis de trabajo, no una verdad universal: debe validarse con
casos de cruce y de no cruce antes de usarla para cortes automaticos.

En versiones nuevas, `TipoEvento` es la columna 9:

```text
0 = seguro
1 = riesgo con comida
2 = solo sonido
```

## Procedimiento Del Programa

1. Detectar transiciones de las tres luces: comida izquierda, comida derecha y
   LED de ruido blanco.
2. Formar candidatos de evento peligroso cuando el LED de ruido blanco precede
   a una luz de comida.
3. Medir `warningToFoodSeconds` para cada candidato. No fijar 4 s en el codigo.
4. Empatar los inicios de luz de comida con los eventos del `.mat`, respetando
   orden, lado, tipo de evento y tiempos relativos.
5. Estimar por sesion el desfase video-MAT con varios eventos, usando una
   medida robusta como la mediana y reportando los eventos que no coinciden.
6. Mantener dos referencias de clip cuando sea util:
   - inicio conductual/MATLAB: luz de comida;
   - inicio de advertencia: LED de ruido blanco.

## Criterios De Validacion

Antes de automatizar recortes de una sesion:

- Revisar manualmente varios eventos seguros y de riesgo.
- Confirmar si el desfase LED -> comida es estable dentro de la sesion.
- Confirmar si el inicio MATLAB estimado coincide con la luz de comida, no con
  el LED de amenaza.
- Reportar cualquier sesion donde el desfase cambie mucho entre eventos.

Si los ~4 s aparecen de forma estable en video pero no en el tiempo que mide
MATLAB, el reporte debe etiquetarlos como `warningPhaseSeconds`: una fase real
para el animal, anterior al inicio temporal de MATLAB.

Si ni la luz de comida ni el LED se empatan de forma consistente con el `.mat`,
la sesion debe marcarse para revision manual. Posibles causas: una copia vieja
de MATLAB, rutas de funciones distintas, latencia de tarjeta/electronica o
datos incompletos.

## Fuentes Tecnicas

La investigacion de esta secuencia esta en el repositorio canonico de
CajaValentia:

```text
/Users/ab/Documents/GitHub/CajaValentia
```

Archivos relevantes:

- `matlab/OA_ValentiaCuatroE.m`
- `matlab/Valentia/OA_Sonidos.m`
- `matlab/Valentia/valentia/OA_ValentiaEstimuloI.m`
- `matlab/Valentia/valentia/OA_ValentiaEstimuloD.m`
- `matlab/Valentia/valentia/OA_ValentiaElectrico.m`
