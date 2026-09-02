# Handoff: CajaValentia CSV Para El Backend

Estado: contrato aplicado parcialmente en Video Batch Processor el 12-jul-2026.
El resolver de fuentes, el lector CSV V1 y la normalización MAT N×8/N×9 ya están
implementados y probados. Falta conectar un lector binario real de `.mat` y
validar el primer CSV de una sesión real con la caja. Este documento no autoriza
cambios en CajaValentia por sí solo.

## Objetivo

CajaValentia esta migrando su exportacion final de sesiones nuevas a CSV. El
backend de Video Batch Processor debe poder leer estos CSV sin perder la
capacidad de procesar los `.mat` historicos. La meta es que ambos formatos
produzcan el mismo modelo interno de eventos conductuales.

## Situacion Actual Verificada

- Los datos historicos permanecen en `.mat` y son inmutables.
- El backend resuelve una fuente conductual por prioridad: override explícito,
  `stem.csv` V1, `stem.mat` legacy o ausencia. Conserva `SourceMatPath` y
  `MatPath` como aliases de compatibilidad cuando la fuente sigue siendo MAT.
- CajaValentia candidata (`ValentiaE`) exporta ahora dos CSV al guardar. Los
  `.mat` dentro de su carpeta `Valentia/` siguen existiendo, pero son estado
  temporal de MATLAB y no son entregables de sesion.
- La exportacion CSV de CajaValentia paso pruebas sin hardware. Antes de
  considerarla fuente de produccion debe existir al menos una sesion real
  validada con la caja.

## Referencias Upstream: CajaValentia

Repositorio canonico:

```text
/Users/ab/Documents/GitHub/CajaValentia
```

Lectura recomendada antes de cambiar backend:

- `docs/architecture/06_cambios_reutilizables_discriminacion_a_cp.md`: estado, reglas conductuales, pruebas y pendientes físicos.
- `matlab/cmc_escribir_csv_resultados.m`: encabezado y formato exacto de `stem.csv`.
- `matlab/cmc_escribir_csv_palanqueos.m`: encabezado y reglas `NA` de `stem_palanqueos.csv`.
- `matlab/OA_ValentiaCuatroE.m`: semántica de resultados, sonido solo, habituación y guardado.

Estas rutas pertenecen al checkout local del repositorio upstream indicado
arriba; se dejan como texto para no publicar enlaces absolutos que GitHub no
puede resolver desde este repositorio.

No modificar CajaValentia desde este repositorio. Si el contrato CSV cambia,
actualizar primero los documentos upstream y despues este handoff.

## Contrato De Archivos Nuevo: CSV V1

Para una sesion llamada `stem`, CajaValentia crea:

```text
stem.csv
stem_palanqueos.csv
```

El archivo principal es solo `stem.csv`. El sufijo `_palanqueos.csv` nunca se
debe descubrir como archivo conductual principal.

### `stem.csv`: Eventos/Resultados

> **Estado histórico del handoff:** esta sección documenta el contrato V1 de
> nueve columnas que el lector actual implementó. CajaValentia validó después
> un CSV actual de diez columnas con `ensayo_cruce`. Antes de integrar sesiones
> automáticas, usar el contrato versionado de
> [captura de sesión](cajavalentia-session-capture-integration.md); no tratar
> este encabezado V1 como la exportación actual de CajaValentia.

Una fila por resultado registrado, en orden cronologico de MATLAB. Encabezado
exacto:

```text
ensayo,lado,estimulo,latencia_s,tiempo_absoluto_s,palancas_izq,palancas_der,desplazamiento_s,tipo_evento
```

| CSV | Matriz legacy `Resultados` | Tipo esperado | Nota para backend |
|---|---:|---|---|
| `ensayo` | 1 | entero | Identificador de evento de la sesion. Preservar orden de fila; no asumir que sirve como indice perfecto si una sesion fue interrumpida. |
| `lado` | 2 | entero | `1` izquierda, `0` derecha, `-2` timeout/no cruce. |
| `estimulo` | 3 | entero | Registro electrico legacy. No usarlo como fuente primaria del tipo de evento. |
| `latencia_s` | 4 | decimal con punto | Duracion MATLAB desde inicio de evento hasta respuesta; en timeout llega al limite. |
| `tiempo_absoluto_s` | 5 | decimal con punto | Reloj MATLAB desde antes de mensajes modales y habituacion inicial; no comparte reloj garantizado con el video. |
| `palancas_izq` | 6 | entero | Contador acumulado de sesion; para cada presion individual usar el CSV hermano. |
| `palancas_der` | 7 | entero | Igual que anterior. |
| `desplazamiento_s` | 8 | decimal con punto | Medicion de desplazamiento/cruce. Conservar raw; no sustituir por una inferencia visual. |
| `tipo_evento` | 9 | entero | `0` seguro, `1` riesgo/conflicto con comida, `2` sonido solo. |

Reglas conductuales que el parser debe preservar, no reinterpretar:

- `tipo_evento=2` es sonido + LED marcador + parrilla, sin luz de comida ni
  pellet. Dura 180 s aunque la rata cruce.
- `lado=-2` es un timeout/no cruce registrado.
- Para clasificar el lote, comparar solo `lado` con el evento anterior válido:
  mismo lado = no cruce y cambio de lado = cruce. `desplazamiento_s` se entrega
  raw para trazabilidad, sin bloquear ni cambiar esa clasificación.
- Para clasificar tipo, preferir `tipo_evento` sobre `estimulo`.

### `stem_palanqueos.csv`: Presiones Individuales

Encabezado exacto:

```text
evento_sesion,tiempo_s,fase,ensayo,tipo_evento,lado,contador_lado_sesion,contador_hardware
```

Este archivo no reemplaza `stem.csv`. Es opcional para la primera version del
segmentador, pero debe poder asociarse a la sesion despues. Detalles relevantes:

- `evento_sesion` es consecutivo para toda la sesion.
- `contador_lado_sesion` es consecutivo por lado.
- `contador_hardware` es crudo, separado por lado y puede volver de 15 a 0.
- `ensayo=NA` fuera de ensayo (`habituacion_inicial`, `sin_luz`,
  `habituacion_final`).
- `tipo_evento` aqui es texto: `seguro`, `riesgo`, `sonido_solo` o `ninguno`.

## Cambios Recomendados En El Backend

### 1. Generalizar La Fuente Conductual

No eliminar nombres ni soporte MAT de golpe. Introducir una abstraccion con un
nombre neutral, por ejemplo `BehavioralSessionReader` o
`IBehavioralSessionReader`, que produzca el actual/futuro `SessionData` y sus
eventos. Implementaciones:

- `MatBehavioralSessionReader`: N x 8 y N x 9 historicos.
- `CsvBehavioralSessionReader`: CSV V1 nuevo.

El modelo del Core se llama `BehavioralEvent`; el contrato ya no asume que el
origen siempre es MAT. Los aliases de ruta MAT se mantienen temporalmente para
no romper manifests o UI previos.

### 2. Resolver El Archivo Correcto Por Sesion

Cambiar la resolucion de `SessionMetadataResolver` de "solo `stem.mat`" a:

1. Un override explicito del manifest gana siempre.
2. Si existe `stem.csv` con el encabezado CSV V1, usarlo como fuente nueva.
3. Si no, usar `stem.mat` como fuente historica.
4. Si no existe ninguno, dejar la fuente conductual como ausente y permitir que
   la UI lo muestre o el usuario lo seleccione.

La migración gradual ya usa `SourceBehavioralPath`, `SourcePressesPath` y
`SourceBehavioralKind`. `SourceMatPath` y `MatPath` permanecen como aliases
legacy y solo representan una ruta real cuando la fuente resuelta es MAT.

### 3. Parser CSV V1

Requisitos minimos:

- Delimitador coma, punto decimal e identificadores ASCII.
- Validar exactamente las nueve columnas principales antes de parsear.
- Leer decimales con cultura invariante; nunca usar la configuracion regional
  de Windows para interpretar `.`.
- Conservar el orden de filas y la precision exportada.
- Reportar un error claro para encabezado faltante, columna ausente, valor no
  numerico o archivo `_palanqueos.csv` usado por error como principal.
- No inventar una fila cuando el CSV principal tiene solo encabezado.

El parser de palanqueos puede ser un segundo modulo o una extension opcional de
`SessionData`. No bloquear la lectura de `stem.csv` si falta el archivo
hermano, pero dejar una advertencia revisable.

### 4. Sincronizacion Con Video

La semantica temporal no cambia al pasar a CSV:

```text
inicio MATLAB estimado = tiempo_absoluto_s - latencia_s
```

Esto sigue siendo una estimacion para cotejar con la luz de comida observada en
video. No tratarlo como reloj compartido. En riesgo, el LED de ruido puede
aparecer antes de la luz de comida; en sonido solo no debe exigirse luz de
comida.

El backend debe conservar dos hallazgos ya medidos, pero no convertirlos en
constantes universales:

- Con habituacion 30 s, el primer inicio MATLAB estimado aparecio en 35.911 s;
  con habituacion 0 s, en 2.559 s. Mensajes modales y preparacion de GUI hacen
  que no exista un offset global fijo.
- Entre respuestas consecutivas de una prueba con sensores hubo 6.7546 y
  6.7859 s antes del inicio MATLAB del siguiente evento. Ese intervalo interno
  permite que la rata quede en el centro entre ensayos; sirve como control de
  orden/calidad, no como ITI visual ni correccion que se reste a todos los
  datos.

La guia completa y vigente esta en
`docs/project/sincronizacion-video-mat-cajavalentia.md`. El CSV de palanqueos
puede usarse despues como evidencia temporal adicional: sus fases distinguen
habituacion inicial, `sin_luz` (ITI), ensayo y habituacion final. No debe
reemplazar las filas principales ni forzar una sincronizacion exacta.

## Pruebas Que Debe Crear El Otro Proyecto

1. Fixture MAT N x 8 historico: comportamiento actual sin cambios.
2. Fixture MAT N x 9: conserva `TipoEvento=2`.
3. Fixture CSV V1 con tipos `0`, `1`, `2` y un `lado=-2`.
4. Fixture CSV malformado: encabezado incorrecto o nueve columnas incompletas.
5. Resolucion de archivos: CSV nuevo, MAT legacy, ambos presentes, override del
   manifest y ningun archivo presente.
6. CSV de palanqueos con `NA`, vuelta de contador 15 a 0 y `sonido_solo`.
7. Prueba de sincronizacion: comprobar que el calculo temporal usa
   `tiempo_absoluto_s - latencia_s` sin modificar los valores raw.

La primera muestra real de CajaValentia debe agregarse como fixture privado o
anonimizado despues de la validacion con caja. No reescribir datos historicos.

## Fuera De Alcance De Este Handoff

- Cambiar reglas experimentales o decidir automaticamente excepciones de
  conducta.
- Convertir todos los MAT historicos a CSV.
- Introducir Parquet, base de datos o metadata adicional antes de que CSV V1
  este validado con una sesion real.
- Modificar el codigo de CajaValentia desde este proyecto.

## Criterio De Integracion Terminada

El backend acepta una sesion legacy `.mat` y una sesion nueva CSV V1, produce
el mismo modelo interno de eventos, conserva `tipo_evento=2` y permite la
sincronizacion revisable con video en ambos casos.
