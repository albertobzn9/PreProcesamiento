# Formato .mat — 8 Columnas Históricas Y Extensión `TipoEvento`

> 🔗 Volver a [visión general](../protocol/cmc-protocol.md)

Para distinguir evento, ensayo, cruce, no cruce, timeout y tipos de latencia, ver también la [convención operativa de términos](operational-terms.md). Para la asociación de tiempos entre el `.mat` y el video, ver [Sincronización video-MAT de CajaValentia](../project/sincronizacion-video-mat-cajavalentia.md).

## ¿Qué es un .mat?

Los archivos históricos contienen una tabla numérica de **N filas × 8 columnas**. Las sesiones nuevas que usen el evento de solo ruido contienen **N filas × 9 columnas**: la novena columna se llama `TipoEvento`.

En muchos archivos la variable se llama `Resultados`, pero algunos `.mat` usan como nombre de variable el identificador de la sesión (por ejemplo `exp_0126_cs_d1r1`). El parser debe buscar una matriz numérica no interna con 8 o 9 columnas; no depender únicamente del nombre `Resultados`.

Cada fila NO es un ensayo de cruce exitoso — es un **evento de palanqueo o timeout**. Como la rata puede presionar varias veces en un mismo lado (hasta 3 veces del mismo lado, en CS, CP y DIS), hay más filas/eventos que cruces. Por lo tanto tenemos eventos con cruce, eventos del mismo lado sin cruce y timeouts.

Las sesiones históricas de **CS** (Cruces Seguros), **CP** (Cruces Peligrosos) y **DIS** (Discriminación) usan las ocho columnas base. Las versiones nuevas de CP o DIS con evento de solo ruido agregan `TipoEvento` como novena columna.

> Nota operativa: los Excel son vistas auxiliares útiles para revisar los datos, pero el formato que debe analizar el programa es el `.mat`. El parser no debe depender de hojas, fechas o bloques visuales del Excel.

> Nota sobre timing: en ensayos de riesgo/conflicto, el LED de ruido blanco se prende antes que la luz de comida. MATLAB inicia su evento en la lógica asociada a la luz de comida, no al LED. El encendido observado de esa luz puede tener un desfase respecto a esa referencia; el programa debe medirlo, como se explica en la guía de sincronización. El video puede conservar el periodo previo como contexto visual.

---

## Las Columnas

Los índices de esta tabla empiezan en `0`, como los leería el parser en C#. Por eso la novena columna física de MATLAB aparece como índice `8`.

| # | Nombre | Valores | Qué significa |
|---|--------|---------|---------------|
| 0 | **Ensayo** | 1, 2, 3... | Contador de eventos/ensayos donde la rata presionó la palanca durante la sesión (NO es el número de ensayo de cruce) |
| 1 | **Lado** | `0`, `1`, `-2` | **0** = lado izquierdo registrado para el evento · **1** = lado derecho registrado para el evento · **-2** = la rata no cruzó (timeout) |
| 2 | **Estim** | `0`, `1` | **0** = sin descarga · **1** = descarga activa. No distingue por sí sola un evento de riesgo con comida de un evento de solo ruido. |
| 3 | **Latencia** | float (s) | Latencia de palanqueo: duración desde que MATLAB inició el evento hasta que la rata presionó la palanca. No es un timestamp absoluto y no debe confundirse con `Desplaz`. Si alcanza el límite de fase, corresponde a timeout. |
| 4 | **TiempoAbs** | float (s) | Segundos desde que MATLAB crea su reloj interno, antes de mensajes modales y de la habituación inicial. En un evento con palanqueo, registra el momento MATLAB de ese evento. Junto con `Latencia` permite calcular `inicio MATLAB estimado = TiempoAbs - Latencia` para compararlo con el video. |
| 5 | **PalancasIzq** | int (acumulado) | Presiones acumuladas en la palanca izquierda hasta este evento |
| 6 | **PalancasDer** | int (acumulado) | Presiones acumuladas en la palanca derecha hasta este evento |
| 7 | **Desplaz** | float | Latencia asociada al sensor de desplazamiento. Se mide con sensores infrarrojos esparcidos linealmente en toda la caja. Cambio de `Lado` + `Desplaz > 1 s` es el cruce automático normal. Los dos patrones excepcionales se reportan para decisión humana. **~180** = timeout. |
| 8 | **TipoEvento** | `0`, `1`, `2` | Solo existe en archivos `N×9`. **0** = seguro con comida · **1** = riesgo/conflicto con comida · **2** = solo ruido, LED y parrilla, sin luz de comida ni recompensa. |

Los archivos históricos `N×8` no cambian. Para ellos, el parser conserva la interpretación existente: `Estim=0` representa seguro y `Estim=1` representa conflicto con comida, porque esos datos fueron producidos antes de que existiera el evento de solo ruido.

### Regla Para Clasificar Cruce

El parser debe conservar `Lado` y `Desplaz` raw; el resultado conductual no se
debe deducir de una sola columna.

| Comparación con el último `Lado` conocido (`0` o `1`) | `Desplaz` | Interpretación |
|--------------------------------------------------------|------------|----------------|
| Cambia `0 -> 1` o `1 -> 0` | `> 1 s` | Cruce completo con cambio de lado. |
| Se mantiene igual | `<= 1 s` | Palanqueo del mismo lado, sin cruce. |
| Se mantiene igual | `> 1 s` | Hallazgo `InterEventCrossing`: la rata pudo cruzar durante el ITI y el evento siguiente volvió a registrar el mismo lado. Reportar para decisión del investigador; no clasificarlo automáticamente como cruce contado. |
| Cambia `0 -> 1` o `1 -> 0` | `<= 1 s` | Hallazgo `ShortSideChange`: puede ocurrir si la rata ya estaba en medio de la caja. Reportar para decisión del investigador; no clasificarlo automáticamente como cruce contado. |
| `Lado = -2` o valor cercano al límite de fase | — | Timeout. |

Un timeout o un `Lado` anterior desconocido rompe la secuencia de lados. El
siguiente evento no debe clasificarse por comparación con un lado viejo. La
latencia de palanqueo tampoco sustituye estas reglas.

---

## Cómo leerlo: casos prácticos

### Caso 1: Ensayo seguro, la rata cruzó
```
Ensayo=3, Lado=1, Estim=0, Latencia=6.06, TiempoAbs=334.8, PalI=1, PalD=2, Desplaz=4.25
```
🟢 **Seguro.** Si el último `Lado` conocido era 0, el cambio a 1 junto con Desplaz=4.25 > 1 confirma un cruce completo. Palanqueó en 6 s.

### Caso 2: Ensayo conflicto, la rata NO cruzó
```
Ensayo=7, Lado=-2, Estim=1, Latencia=180.60, TiempoAbs=551.6, PalI=1, PalD=5, Desplaz=180.0
```
🔴 **Conflicto.** No cruzó (Lado=-2). Timeout de 180s. Desplaz=180 (marca de timeout).

### Caso 3: Palanqueo sin cruce (mismo lado que ensayo anterior)
```
Ensayo=2, Lado=0, Estim=0, Latencia=1.85, TiempoAbs=323.0, PalI=0, PalD=2, Desplaz=0.15
```
🟢 **Seguro.** Luz izquierda (Lado=0), pero Desplaz=0.15 → solo presionó palanca, no cruzó. La rata ya estaba en ese lado.

## Ejemplos Reales Por Fase

Los siguientes ejemplos vienen directamente de archivos `.mat` reales. Los Excel pueden servir para revisar visualmente los datos, pero estos ejemplos están anclados al formato que debe leer el programa.

### CS: `exp_0126_cs_d1r1.mat`

Variable interna: `exp_0126_cs_d1r1`; shape `(57, 8)`.

| Caso | Ensayo | Lado | Estim | Latencia | TiempoAbs | PalI | PalD | Desplaz | Interpretación |
|------|--------|------|-------|----------|-----------|------|------|---------|----------------|
| Seguro con cruce | 1 | 0 | 0 | 28.8554 | 377.2654 | 0 | 1 | 18.1489 | Ensayo seguro con desplazamiento válido. |
| Seguro sin cruce | 2 | 0 | 0 | 10.7573 | 393.6353 | 0 | 2 | 0.1486 | Palanqueo del mismo lado sin desplazamiento significativo. |

Lectura rápida:

- Todos los eventos tienen `Estim=0`.
- Cambio de `Lado` + `Desplaz > 1` indica cruce con cambio de lado.
- Lado igual + `Desplaz > 1` genera hallazgo `InterEventCrossing` para revisión del investigador.
- Lado igual + `Desplaz <= 1` indica palanqueo sin cruce.
- En este ejemplo no hay `Lado=-2`, pero pueden existir timeouts raros en CS, sobre todo al inicio del entrenamiento.

### CP: `exp_0126_cp_d1r1.mat`

Variable interna: `Resultados`; shape `(10, 8)`.

| Caso | Ensayo | Lado | Estim | Latencia | TiempoAbs | PalI | PalD | Desplaz | Interpretación |
|------|--------|------|-------|----------|-----------|------|------|---------|----------------|
| Conflicto con cruce | 2 | 1 | 1 | 6.0452 | 282.3598 | 1 | 1 | 3.5488 | Ensayo de conflicto con desplazamiento válido. |
| Conflicto sin cruce | 4 | 1 | 1 | 29.2779 | 937.3838 | 13 | 2 | 0.2927 | Palanqueo sin desplazamiento significativo. |
| Conflicto timeout | 5 | -2 | 1 | 30.5815 | 1087.9733 | 13 | 2 | 30.0 | Timeout en CP día 1, donde el límite es ~30 s. |

Lectura rápida:

- Todos los eventos tienen `Estim=1`.
- `Lado=-2` marca timeout.
- En CP, el valor de timeout esperado cambia por día: ~30, 30, 60, 90 o 120 s.

### DIS: `exp_0126_dis_d2r1.mat`

Variable interna: `Resultados`; shape `(71, 8)`. Sirve para ver los cuatro casos que el parser debe distinguir en una misma sesión de discriminación.

| Caso | Ensayo | Lado | Estim | Latencia | TiempoAbs | PalI | PalD | Desplaz | Interpretación |
|------|--------|------|-------|----------|-----------|------|------|---------|----------------|
| Seguro sin cruce | 1 | 1 | 0 | 11.5168 | 317.6651 | 1 | 0 | 0.29 | La rata presionó la palanca sin desplazamiento significativo. |
| Seguro con cruce | 2 | 0 | 0 | 20.6967 | 344.0998 | 2 | 1 | 12.8591 | Ensayo seguro con desplazamiento válido. |
| Conflicto timeout | 9 | -2 | 1 | 180.6699 | 642.6691 | 7 | 4 | 180.0 | Ensayo de conflicto donde la rata no cruzó. |
| Conflicto con cruce | 53 | 1 | 1 | 37.7153 | 1540.0 | 32 | 33 | 35.6512 | Ensayo de conflicto con desplazamiento válido. |

Lectura rápida:

- `Estim=0` indica seguro; `Estim=1` indica conflicto.
- `Lado=-2` marca timeout/no cruce.
- Cambio de `Lado` + `Desplaz > 1` indica cruce con cambio de lado.
- Lado igual + `Desplaz > 1` genera hallazgo `InterEventCrossing` para revisión del investigador.
- Lado igual + `Desplaz <= 1` indica palanqueo sin cruce significativo.
- Cambio de `Lado` + `Desplaz <= 1` requiere revisar video: la rata pudo estar en medio de la caja.
- `Latencia` y `Desplaz` pueden diferir porque representan mediciones distintas: palanqueo vs cruce/desplazamiento.

---

## Diferencias entre fases

| Fase | Estim | Timeouts | Latencia típica de cruce | Lado -2 |
|------|-------|----------|-----------------|---------|
| **CS** | Siempre 0 | ❌ No | ~5–20s | ❌ No aparece |
| **CP** | Siempre 1 | ✅ Sí (al límite del día) | Variable de 30s hasta 120s dependiente del día | ✅ Aparece |
| **DIS** | Mezcla 0 y 1 | ✅ Sí (~180s) | Seguro: ~20s · Conflicto: ~80s o 180s | ✅ Aparece |

---

## Relación Con La Nomenclatura

Los `.mat` actuales se tratan como archivos fuente legacy. No se renombran, no se modifican y no se generan nuevos `.mat` desde el Video Batch Processor.

En datos históricos, el nombre del `.mat` suele coincidir con el nombre del video de la sesión, cambiando únicamente la extensión:

```text
exp_0126_dis_d9r4.mat
exp_0126_dis_d9r4.mp4
```

La relación completa entre nomenclatura legacy, estándar del lab y output del programa se define en [Nomenclatura de archivos](naming-convention.md).

---

## Para el Video Batch Processor

El `.mat` es la **fuente de verdad** para saber si la rata cruzó o no en cada evento. El módulo `LightDetection`, en particular `LightDetector`, detecta los estados visuales de `FoodLeft`, `FoodRight` y `NoiseLed`, y el `MatParser` lee el `.mat` para obtener `TiempoAbs`, latencia de palanqueo, desplazamiento y resultado. Combinando ambas fuentes se pueden medir el desfase al inicio y la cola visual después del palanqueo sin inventar un reloj compartido.

Cuando exista `TipoEvento`, el `MatParser` debe conservarlo en cada `MatEvent` y usarlo antes que `Estim` para clasificar el evento. Para `TipoEvento=2`, `SegmentPlanner` debe buscar el LED/ruido sin exigir una luz de comida y crear un segmento de solo ruido. El resultado de cruce/no cruce/timeout no debe inferirse automáticamente con las reglas de un ensayo con comida.
