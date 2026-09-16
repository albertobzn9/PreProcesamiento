# Términos Operativos

> Volver al [índice de documentación](../README.md).

Esta guía fija el vocabulario mínimo compartido por el protocolo, los datos
conductuales, el video y los clips exportados. No define reglas por fase ni
detalles técnicos: esos viven en sus documentos específicos.

## Las Tres Capas Del Programa

| Capa | Qué representa | Fuente principal |
|------|----------------|------------------|
| **Evento conductual** | Un registro de lo que MATLAB/CajaValentia guardó: lado, latencias, tipo de estímulo o timeout. Una fila del `.mat` o CSV equivale a un evento conductual. | `.mat` o CSV |
| **Evento visual** | Un cambio observable en el video, como el encendido o apagado de una luz. Tiene sus propios frames y tiempos. | Video |
| **Segmento o clip** | La parte de video que el programa decide exportar después de asociar los dos registros anteriores. Puede ser un evento, un ITI o habituación. | Video Batch Processor |

Un evento conductual y un evento visual describen el mismo proceso desde dos
registros distintos, pero no comparten automáticamente el mismo reloj. El
programa los asocia y calcula su desfase antes de recortar.

## Términos Básicos

| Término | Significado en este proyecto |
|---------|------------------------------|
| **Sesión** | Video completo de una rata, un día y una fase. Puede incluir habituación inicial, eventos, ITIs y habituación final. |
| **Evento** | Evento conductual: una fila de la fuente conductual. No equivale automáticamente a un cruce exitoso. |
| **Cruce** | Resultado conductual cuya regla depende de la fase. |
| **No cruce** | Evento conductual que no cumplió la regla de cruce de su fase. Es distinto de un timeout. |
| **Timeout** | Evento en que la conducta no se completó dentro del límite. En los MAT históricos suele registrarse como `Lado = -2`. |
| **ITI** | Intervalo entre dos eventos. Se conserva porque forma parte de la sesión, aunque su prioridad analítica cambia según la fase. |
| **Habituación inicial** | Parte sin señales relevantes antes del primer evento. |
| **Habituación final** | Parte sin señales relevantes después del último evento; su duración real puede variar porque el final es manual. |
| **Código de segmento** | Etiqueta de output: `eNN` para un evento, `itiNN` para el ITI posterior a ese evento, `habini` o `habfin`. La regla completa está en nomenclatura. |

## Tiempos

| Término | Significado |
|---------|-------------|
| **Tiempo absoluto** (`TiempoAbs`) | Segundos desde que MATLAB creó su reloj interno. No empieza necesariamente con el primer estímulo visible. |
| **Latencia de palanqueo** (`Latencia`) | Tiempo entre el inicio interno del evento en MATLAB y el palanqueo. |
| **Latencia de desplazamiento** (`Desplaz`) | Tiempo registrado por el sensor de desplazamiento.Cuanto tarda la rata en cruzar. Si es del mismo lado es menor a una, cuando es evento de cruce se termina cuando los láseres detectan que llego a la zona segura contraria. |
| **Desfase video-conducta** | Diferencia medida entre el reloj del video y el registro conductual de una sesión. No se reemplaza por un valor fijo universal. |

## Fuentes De Verdad

- El `.mat` o CSV conserva el resultado conductual, las latencias y el tipo de evento.
- El video conserva cuándo se ven realmente las luces y los límites visuales del clip.
- El Excel es un reporte para revisar; no es la fuente que el programa debe leer.
- Los archivos fuente no se renombran ni modifican.

## Dónde Está El Detalle

- [Protocolo CMC](../protocol/cmc-protocol.md): fases y lógica experimental.
- [Formato MAT histórico](mat-format.md): columnas y ejemplos reales.
- [Nomenclatura de archivos](naming-convention.md): nombres legacy, estándar del lab y de output.
- [Sincronización video-conducta](../project/sincronizacion-video-mat-cajavalentia.md): cómo se relacionan los tiempos de video y MATLAB.
- [Arquitectura](../project/architecture.md): módulos y funcionamiento técnico.
