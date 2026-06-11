# Convención Operativa De Términos

> 🔗 Volver al [índice de documentación](../README.md)

Este documento define cómo usaremos los términos principales del protocolo CMC dentro del Video Batch Processor. La meta es evitar ambigüedad al implementar parsers, segmentadores y exportadores.

## Términos Principales

| Término | Significado operativo |
|---------|-----------------------|
| **Sesión** | Video completo de un día/rata/fase. Puede incluir habituación inicial, eventos/ensayos, ITIs y habituación final. |
| **Clip de salida** | Fragmento de video exportado por el Video Batch Processor. Puede corresponder a un evento/ensayo, ITI o habituación, y se nombra con la nomenclatura de output del programa. |
| **Evento** | Registro conductual producido por una presión de palanca o timeout en el `.mat`. Una fila del `.mat` corresponde a un evento, no necesariamente a un cruce. |
| **Ensayo** | Periodo detectado en video por señales de luz/LED. Inicia con la señal relevante y termina cuando la señal se apaga, por palanqueo o timeout. |
| **Ensayo de cruce** | Ensayo en el que la rata debe desplazarse al lado opuesto para obtener comida. |
| **Primer ensayo seguro** | Regla contextual de sesión: el primer ensayo siempre es seguro/de comida. Sirve como referencia experimental o sanity check, pero no reemplaza la detección de luces ni la lectura del `.mat`. |
| **Evento del mismo lado** | Evento donde la luz se enciende del mismo lado donde ya está la rata. Puede haber palanqueo sin cruce real. Esto puede ocurrir en CS, CP y DIS. |
| **Cruce** | Desplazamiento válido de la rata hacia el lado opuesto. En el `.mat`, se interpreta principalmente con `Desplaz > 1`. |
| **No cruce** | Caso donde la rata presiona sin desplazamiento significativo (`Desplaz <= 1`) o no cruza antes del timeout. No confundir palanqueo sin cruce con timeout. |
| **Timeout** | Ensayo/evento donde la rata no cruza dentro del tiempo máximo. En el `.mat` suele aparecer como `Lado = -2`, latencia cercana al límite y `Desplaz` cercano al timeout. |
| **Latencia de palanqueo** | Tiempo desde el inicio del evento/ensayo hasta que la rata presiona la palanca. En el `.mat` corresponde a la columna `Latencia`. |
| **Latencia de cruce/desplazamiento** | Tiempo o medida asociada al desplazamiento real de la rata. En el `.mat` corresponde a `Desplaz`. |
| **ITI** | Intervalo entre ensayos/eventos. En video no hay luces ni LED relevantes encendidos. Puede ser corto en CS/DIS y largo en CP, por lo que debe poder conservarse como clip si se quiere segmentar toda la sesión. |
| **Habituación inicial** | Periodo al inicio de la sesión, sin luces, ruido ni descarga. En el protocolo original es de 5 min. |
| **Habituación final** | Periodo al final de la sesión, sin luces, ruido ni descarga. En videos reales puede variar por corte manual y no debe asumirse como exactamente 5 min. |
| **Seguro / no conflicto** | Ensayo con luz de comida sin LED de ruido blanco. |
| **Conflicto / peligroso** | Ensayo con luz de comida y LED de ruido blanco. En DIS, el LED puede encender antes que la luz de comida. |
| **Periodo de advertencia** | Delay intencional en ensayos de riesgo/conflicto: primero se enciende el LED de ruido blanco y suena el ruido; unos segundos después se prende la luz de comida. El clip de video puede conservar este periodo. |
| **Inicio MATLAB del evento** | Momento desde el que MATLAB empieza a contar la latencia del evento. En ensayos de riesgo/conflicto ocurre cuando se prende la luz de comida, no cuando se prende el LED de ruido. |

## Reglas Para Implementación

1. El video define los tiempos visuales de encendido/apagado de luces.
2. El `.mat` es la fuente de verdad para cruce, no cruce, timeout y latencias.
3. No asumir que una fila del `.mat` equivale automáticamente a un cruce.
4. No asumir que toda luz encendida implica que la rata debe cruzar; puede ser un evento del mismo lado.
5. No asumir que la habituación final dura exactamente 5 min.
6. Para segmentar clips, conservar también timeouts y eventos sin cruce si son relevantes para el análisis.
7. En caso de conflicto entre heurística visual y `.mat`, documentar la discrepancia y priorizar el `.mat` para etiquetas conductuales.
8. En nombres de clips, `eN` conserva el número del evento/ensayo del `.mat`; no significa necesariamente cruce exitoso.
9. En clips de ITI o habituación, usar `na` para campos de tipo de ensayo o resultado que no apliquen.
10. En ensayos de riesgo/conflicto, distinguir entre inicio visual del clip (LED/ruido) e inicio de latencia en MATLAB (luz de comida).
