# Sincronización Video-Conducta De CajaValentia

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

### Fuente Conductual: MAT Histórico O CSV Nuevo

Los `.mat` históricos siguen siendo fuente legacy. Las sesiones nuevas de
`ValentiaE` exportan `stem.csv` (resultados) y `stem_palanqueos.csv`
(presiones individuales). Ambos formatos deben llegar al mismo modelo interno
de evento conductual. El contrato completo del CSV está en el
[handoff de backend](handoff-cajavalentia-csv-backend.md).

El CSV actual de CajaValentia conserva diez columnas de `Resultados`, incluida
`ensayo_cruce`. El backend de esta aplicación todavía acepta la versión previa
de nueve columnas; antes de automatizar sesiones nuevas debe incorporar un
lector versionado de diez columnas. Esta diferencia se controla en el
[contrato de captura de sesión](cajavalentia-session-capture-integration.md).
La equivalencia temporal de las primeras nueve columnas es:

```text
TiempoAbs  -> tiempo_absoluto_s
Latencia   -> latencia_s
Desplaz    -> desplazamiento_s
TipoEvento -> tipo_evento
```

La columna 9 ya es parte del contrato de datos nuevo, no una nota futura:

```text
tipo_evento = 0  seguro con comida
tipo_evento = 1  riesgo/conflicto con comida
tipo_evento = 2  solo sonido, LED marcador y parrilla; sin comida ni pellet
```

Para `tipo_evento=2`, el ancla visual es `NoiseLed`; no se debe esperar una luz
de comida. La fila se registra al terminar los 180 s. Por ello,
`tiempo_absoluto_s - latencia_s` conserva el inicio MATLAB estimado y
`desplazamiento_s` conserva por separado el primer cruce, si existió.

### CSV De Palanqueos

`stem_palanqueos.csv` no sustituye el CSV principal para empatar ensayos con
video. Es un registro complementario que permite revisar presiones dentro de
habituación, ITI (`sin_luz`) y ensayo:

```text
evento_sesion,tiempo_s,fase,ensayo,tipo_evento,lado,contador_lado_sesion,contador_hardware
```

Reglas para el backend:

- nunca descubrir `_palanqueos.csv` como archivo principal de sesión;
- conservar `NA` en `ensayo` como faltante fuera de ensayo, no como ensayo 0;
- usar `contador_lado_sesion` para orden por lado y conservar
  `contador_hardware` solo como evidencia cruda, pues puede volver de 15 a 0;
- no bloquear el segmentador inicial si el archivo hermano falta, pero reportar
  una advertencia revisable.

### Lo Que Significan Las Columnas

| Dato conductual (`.mat` / CSV) | Significado operativo |
|-----------------|-----------------------|
| `TiempoAbs` | Segundos desde que MATLAB crea su reloj interno `R0`. En `ValentiaE`, `R0` se crea antes de los mensajes modales y antes de la habituación inicial; no equivale automáticamente al primer estímulo visible en video. En un evento con palanqueo, es el momento en que MATLAB registra ese evento. |
| `Latencia` | Segundos que transcurrieron desde que MATLAB inició el evento hasta que la rata palanqueó. Es una duración, no un timestamp absoluto. |
| `Lado` / `lado` | **1 = izquierda**, **0 = derecha**, `-2` = timeout/no cruce. Esta codificación se validó físicamente con pruebas de izquierda y derecha el 11-jul-2026; no invertirla en el parser. |
| `Desplaz` | Latencia asociada al sensor de desplazamiento. Con cambio de `Lado`, `> 1 s` confirma un cruce completo. Si se repite con `> 1 s`, emitir `InterEventCrossing` para decisión del investigador. Si cambia de lado pero dura `<= 1 s`, emitir `ShortSideChange` y revisar video porque la rata pudo estar en medio de la caja. No equivale a un tiempo que este programa pueda medir desde video. |
| `TipoEvento` / `tipo_evento` | Novena columna en sesiones nuevas: `0` seguro, `1` riesgo con comida, `2` solo sonido. Preferirla sobre `Estim` / `estimulo` al clasificar el evento. |

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

Una segunda prueba, con habituación configurada en `0`, obtuvo:

```text
TiempoAbs = 7.854 s
Latencia  = 5.295 s
inicio MATLAB estimado = 2.559 s
```

Esto aísla una sobrecarga base de aproximadamente 2.6 s antes del primer
ensayo, debida a mensajes y preparación de MATLAB. La diferencia respecto a la
prueba con 30 s de habituación confirma que no se debe restar un offset fijo:
el programa debe estimar el inicio MATLAB para cada fila y compararlo con el
video de la sesión.

### Intervalo Interno Entre Ensayos (Medido En Caja)

La prueba física `prueba_crucessensor.mat` del 11-jul-2026 midió por primera
vez el intervalo interno real del flujo entre ensayos seguros. Para dos filas
con palanqueo consecutivas se calcula:

```text
inicio MATLAB del ensayo siguiente = TiempoAbs_siguiente - Latencia_siguiente
intervalo interno = inicio MATLAB del ensayo siguiente - TiempoAbs_anterior
```

Resultados de la prueba:

```text
ensayo 1 -> 2: (39.0212 - 5.6348) - 26.6318 = 6.7546 s
ensayo 2 -> 3: (48.3422 - 2.5351) - 39.0212 = 6.7859 s
```

Es decir, hubo aproximadamente `6.77 s` entre el registro MATLAB de una
respuesta y el inicio MATLAB del siguiente evento. Este tiempo permite que la
rata quede en el centro entre ensayos; no debe asumirse que permanece en el
extremo donde terminó el evento previo.

Esta medida pertenece a la rama experimental
`feature/sensor-validated-crosses` de CajaValentia: incluye apagado de
estímulos, preparación del siguiente ensayo y lecturas adicionales de sensores
para validar la posición inicial. No es todavía la duración visual exacta del
ITI: la luz de comida se ordena antes de que MATLAB inicie su reloj `R2`, y el
video sigue siendo la fuente para medir el encendido/apagado físico. Para el
preprocesamiento sirve como restricción de orden y control de calidad, no como
un offset universal que se reste a todas las sesiones históricas.

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
- sonido solo, usando `NoiseLed` como ancla y sin exigir luz de comida.

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
| `matInterTrialGapSeconds` | Entre dos eventos consecutivos: `(TiempoAbs_sig - Latencia_sig) - TiempoAbs_actual`. Sirve para documentar el intervalo interno MATLAB, no para sustituir una medición visual del ITI. |
| `matchedBehaviorEventIndex` | Fila del `.mat` o CSV asociada. |
| `behaviorSourceFormat` | `mat` histórico o `csv_v1` nuevo. |
| `eventType` | Seguro, riesgo con comida o sonido solo, preservado desde columna 9 cuando existe. |
| `matchConfidence` | Alta, media, baja o requiere revisión, con su motivo. |

El reporte por sesión debe incluir mediana, rango y eventos atípicos de cada
medida. No debe reducir todo a un solo delay global.

## Procedimiento Del Programa

1. Detectar en video las transiciones de `FoodLeft`, `FoodRight` y `NoiseLed`.
2. Leer de `.mat` histórico o CSV V1 `TiempoAbs`/`tiempo_absoluto_s`,
   `Latencia`/`latencia_s`, `Desplaz`/`desplazamiento_s`, lado, tipo y resultado.
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
7. Si existe CSV de palanqueos, asociarlo a la sesión como evidencia adicional
   sin usarlo para reemplazar las filas principales de eventos.

## Por Qué La Integración Futura Lo Resolverá Mejor

Hoy video y CajaValentia empiezan con relojes separados, por lo que el programa
debe reconstruir la relación a partir de los datos. En la integración futura,
CajaValentia pedirá a OBS confirmar que ya graba y solo entonces creará `R0`.
Un manifiesto de sesión conservará esa identidad, orden y rutas de archivos.
Esto elimina las adivinanzas de asociación y permite medir desde el origen las
diferencias residuales; no promete una sincronía de frame perfecta por sí sola.
El contrato completo está en
[CajaValentia Session Capture Integration](cajavalentia-session-capture-integration.md).

## Fuente Técnica

La secuencia conductual se investigó en el repositorio canónico de CajaValentia:

```text
/Users/ab/Documents/GitHub/CajaValentia
```

Archivos relevantes:

- `matlab/OA_ValentiaCuatroE.m`
- `matlab/cmc_escribir_csv_resultados.m`
- `matlab/cmc_escribir_csv_palanqueos.m`
- `matlab/Valentia/OA_Sonidos.m`
- `matlab/Valentia/valentia/OA_ValentiaEstimuloI.m`
- `matlab/Valentia/valentia/OA_ValentiaEstimuloD.m`
- `matlab/Valentia/valentia/OA_ValentiaElectrico.m`

Documento upstream de estado y pruebas:

- [Cambios reutilizables de Discriminacion a CP](/Users/ab/Documents/GitHub/CajaValentia/docs/architecture/06_cambios_reutilizables_discriminacion_a_cp.md)
