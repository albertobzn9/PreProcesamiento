# Sincronización Video-MAT De CajaValentia

> Volver a la [arquitectura](architecture.md).

## Idea Central

Este proyecto no sigue el cuerpo de la rata en video. Por eso no intenta medir
visualmente el tiempo de desplazamiento. Lo que sí puede comparar es:

- lo que las luces muestran en el video;
- el tiempo de palanqueo que registró MATLAB;
- y la diferencia entre ambos relojes.

La meta no es fingir que video y MATLAB están sincronizados. Es medir sus
diferencias, reportarlas y después comparar si cambian entre tipos de evento.

## Los Dos Registros

```text
VIDEO:   luz de comida ON -------------------------- luz de comida OFF
MATLAB:        inicia su reloj ------ palanqueo registrado
```

En el video se ven con precisión los límites visuales de la luz. En MATLAB se
registra el palanqueo y su latencia. Ambas líneas deberían estar cerca, pero no
se debe asumir que empiezan o terminan exactamente al mismo tiempo.

### Lo Que Significan Las Columnas

| Dato del `.mat` | Significado operativo |
|-----------------|-----------------------|
| `TiempoAbs` | Segundos desde que MATLAB crea su reloj interno `R0`. En `ValentiaE`, `R0` se crea antes de los mensajes modales y antes de la habituación inicial; no equivale automáticamente al primer estímulo visible en video. En un evento con palanqueo, es el momento en que MATLAB registra ese evento. |
| `Latencia` | Segundos que transcurrieron desde que MATLAB inició el evento hasta que la rata palanqueó. Es una duración, no un timestamp absoluto. |
| `Desplaz` | Latencia asociada al sensor de desplazamiento. Con cambio de `Lado`, `> 1 s` confirma un cruce completo. Si se repite con `> 1 s`, emitir `InterEventCrossing` para decisión del investigador. Si cambia de lado pero dura `<= 1 s`, emitir `ShortSideChange` y revisar video porque la rata pudo estar en medio de la caja. No equivale a un tiempo que este programa pueda medir desde video. |

Para un evento con palanqueo, el programa puede obtener una estimación del
inicio MATLAB:

```text
inicio MATLAB estimado = TiempoAbs - Latencia
```

Esta operación conserva los datos raw y calcula una referencia para comparar;
no modifica el `.mat`.

### Hallazgo Confirmado En Caja (2026-07-11)

La prueba supervisada `prueba1107.mat` confirmó esta semántica en la caja real.
Con habituación configurada en 30 s, la primera fila tuvo:

```text
TiempoAbs = 77.914 s
Latencia  = 42.003 s
inicio MATLAB estimado = 35.911 s
```

El primer ensayo empezó aproximadamente 35.9 s después de crear `R0`: 30 s
nominales de habituación más tiempo de mensajes modales, lecturas de sensores y
sobrecarga de la GUI. Esto es un desfase interno de MATLAB, no todavía una
medición del desfase video-MAT. Cuando exista el video/Excel, se comparará el
inicio MATLAB estimado de varios eventos con el encendido visual de la luz para
obtener el desfase real de esa sesión.

En un evento `SoundOnly` (`TipoEvento=2`), la fila se registra al terminar los
180 s: `TiempoAbs - Latencia` sigue siendo el inicio MATLAB estimado porque la
latencia almacenada es la duración completa del evento. El primer cruce, si lo
hubo, se conserva por separado en `Desplaz`.

## Qué Diferencias Debemos Medir

### 1. Desfase Al Inicio

Compara cuándo MATLAB estima que comenzó el evento con cuándo aparece la luz de
comida en video.

```text
desfaseInicio = videoFoodLightOn - inicioMATLABEstimado
```

Por ejemplo, si el primer evento MATLAB empieza alrededor del segundo 300, pero
la luz aparece en el video en el segundo 305, el desfase inicial es cercano a
5 s. Ese retraso puede ser intencional o técnico; por ahora solo se mide.

### 2. Cola Visual Después Del Palanqueo

Idealmente la luz se apagaría cuando la rata palanquea. En la práctica puede
seguir prendida después de que MATLAB ya terminó de contar la latencia.

```text
colaDespuesDelPalanqueo = videoFoodLightOff - TiempoAbs
```

Un valor positivo significa que la luz siguió visible después del palanqueo
registrado por MATLAB.

### 3. Periodo De Advertencia En Riesgo

En eventos de riesgo/conflicto existe además un periodo visual anterior: el LED
de ruido se enciende antes que la luz de comida.

```text
advertenciaRiesgo = videoFoodLightOn - videoNoiseLedOn
```

Esta medida describe la advertencia para el animal. No debe mezclarse con el
desfase video-MAT del inicio o del final del evento.

## Qué Se Va A Comparar

El reporte debe resumir las tres medidas anteriores por sesión y por grupos de
eventos:

- seguro;
- riesgo/conflicto;
- mismo lado;
- lado contrario;
- no cruce y timeout, cuando aplique.

La latencia de palanqueo puede dar una pista útil: en los datos del laboratorio,
los eventos de mismo lado suelen ser cortos (por ejemplo ~3-8 s), mientras que
un cruce puede tardar más de 10 s. Es una señal para revisar patrones, no una
regla fija para clasificar el resultado. Cambio de `Lado` con `Desplaz > 1 s`
confirma un cruce. Los dos patrones excepcionales se reportan para decisión del
investigador: `InterEventCrossing` cuando el lado se repite con
`Desplaz > 1 s`, y `ShortSideChange` cuando el lado cambia con
`Desplaz <= 1 s`. El programa muestra la evidencia, pero no decide si esos
eventos cuentan como cruces en un análisis concreto.

## Qué Debe Guardar El Programa

Por cada evento con comida asociado al `.mat`, conservar:

| Campo | Significado |
|-------|-------------|
| `noiseLedOnSeconds` | Inicio visual del LED de ruido, si existe. |
| `foodLightOnSeconds` | Inicio visual de la luz de comida. |
| `foodLightOffSeconds` | Fin visual de la luz de comida. |
| `matPressSeconds` | `TiempoAbs` raw del evento. |
| `matEventStartEstimateSeconds` | `TiempoAbs - Latencia`. |
| `videoMatStartGapSeconds` | Diferencia entre inicio visual e inicio MATLAB estimado. |
| `postPressLightTailSeconds` | Tiempo que la luz siguió encendida tras el palanqueo MATLAB. |
| `warningToFoodSeconds` | Periodo LED de ruido -> luz de comida en riesgo. |
| `matchedMatEventIndex` | Fila del `.mat` asociada. |
| `matchConfidence` | Alta, media, baja o requiere revisión, con su motivo. |

El reporte por sesión debe incluir mediana, rango y eventos atípicos de cada
medida. No debe reducir todo a un solo delay global.

## Procedimiento Del Programa

1. Detectar en video las transiciones de `FoodLeft`, `FoodRight` y `NoiseLed`.
2. Leer del `.mat` `TiempoAbs`, `Latencia`, `Desplaz`, lado, tipo y resultado.
3. Empatar eventos respetando orden, lado, tipo y tiempos relativos.
4. Calcular desfase inicial, cola posterior y, en riesgo, advertencia previa.
5. Resumir las diferencias por sesión y por tipo de evento usando una medida
   robusta como la mediana.
6. Marcar para revisión manual los eventos o sesiones que no coincidan de forma
   consistente.

## Reglas Importantes

1. No fijar en código un delay universal de 4 s ni de cualquier otro valor.
2. No usar la duración visual de la luz como sustituto directo de `Latencia`.
3. Clasificar automáticamente como cruce solo cambio de `Lado` +
   `Desplaz > 1 s`. Emitir `InterEventCrossing` o `ShortSideChange` para
   decisión del investigador en los dos patrones excepcionales; no sustituir
   estas reglas con latencia de palanqueo corta/larga.
4. Conservar por separado el periodo de advertencia, el desfase inicial y la
   cola posterior al palanqueo.
5. Los clips pueden iniciar en el LED de ruido para conservar contexto, aunque
   la comparación con MATLAB use la luz de comida como referencia visual.
6. Si no hay una asociación consistente, exportar la advertencia para revisión
   en vez de forzar una coincidencia falsa.

## Por Qué La Integración Futura Lo Resolverá Mejor

Hoy video y CajaValentia empiezan con relojes separados, por lo que el programa
debe reconstruir la relación a partir de los datos. En una integración futura,
la captura de video y la tarea conductual podrán recibir la misma identidad de
sesión y timestamps compartidos. Entonces estas diferencias se podrán medir
desde el origen, sin hacer esta reconstrucción posterior.

## Fuente Técnica

La secuencia conductual se investigó en el repositorio canónico de CajaValentia:

```text
/Users/ab/Documents/GitHub/CajaValentia
```

Archivos relevantes:

- `matlab/OA_ValentiaCuatroE.m`
- `matlab/Valentia/OA_Sonidos.m`
- `matlab/Valentia/valentia/OA_ValentiaEstimuloI.m`
- `matlab/Valentia/valentia/OA_ValentiaEstimuloD.m`
- `matlab/Valentia/valentia/OA_ValentiaElectrico.m`
