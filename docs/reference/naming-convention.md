# Nomenclatura De Archivos

> Volver a la [visión general](../protocol/cmc-protocol.md)

Este documento define las tres nomenclaturas que conviven en el proyecto:

1. **Legacy / sesión completa**: nombres actuales de los datos históricos que ya existen.
2. **Estándar del lab**: nomenclatura acordada por el laboratorio para las sesiones fuente futuras.
3. **Output del Video Batch Processor**: extensión usada por este programa para exportar clips pequeños sin perder ensayos sin cruce, ITIs ni habituación.

La regla principal es simple: los archivos fuente conductuales (`.mat` históricos o CSV V1 nuevos) **no se renombran ni se modifican**. El programa los lee, interpreta sus eventos y genera clips de video con la nomenclatura de output.

---

## 1. Nomenclatura Legacy / Sesión Completa

Es la nomenclatura usada por los archivos actuales del laboratorio.

```text
exp_0126_dis_d1r3.mat
exp_0126_dis_d1r3.mp4
```

Representa una **sesión completa**: un día, una rata y una fase. No representa un ensayo individual.

| Parte | Significado | Ejemplo |
|-------|-------------|---------|
| `exp` | Prefijo fijo de experimento | `exp` |
| `0126` | Mes y año de inicio (MMYY) | `0126` = enero 2026 |
| `dis` | Fase o etapa del protocolo | `cs`, `cm`, `cp`, `dis`, `pb` |
| `d1` | Día de la sesión | `d1`, `d5`, `d31` |
| `r3` | Número de rata | `r1`, `r2`, `r3` |
| `.mat` / `.mp4` | Tipo de archivo | latencias o video |

### Códigos Legacy De Etapa

| Código | Fase | Equivalente |
|--------|------|-------------|
| `cs` | Cruces Seguros | `f2` |
| `cm` | Condicionamiento al Miedo | `f3` |
| `cp` | Cruces Peligrosos | `f4` |
| `dis` | Discriminación | `f5` |
| `pb` | Prueba | día de prueba posterior |

> Nota: la fase Luz-Comida (`f1`) no suele aparecer en este formato como archivo `.mat` de cruces, porque la rata está confinada y no cruza.

### Uso En El Proyecto

- El `.mat` legacy es la fuente conductual histórica para eventos, latencias, cruces, no cruces y timeouts.
- El video legacy es el archivo completo que se va a recortar.
- Una fila del `.mat` corresponde a un evento experimental, no necesariamente a un cruce exitoso.
- El programa debe poder leer estos nombres porque son los datos reales actuales.

---

## 2. Nomenclatura Estándar Del Lab / Sesión Fuente

Es la nomenclatura acordada por el laboratorio para archivos futuros.

```text
[iniciales]_[fecha_de_inicio]_[fase]_[dia][rata]_[sexo]_[tratamiento].[extension]
```

Ejemplo:

```text
abs_2601_f5_d1r3_m_stx.mp4
```

| Parte | Significado | Ejemplo |
|-------|-------------|---------|
| `abs` | Iniciales del experimentador | nombre + dos apellidos |
| `2601` | Año y mes de inicio (YYMM) | `2601` = enero 2026 |
| `f5` | Fase del protocolo | `f1` a `f5` |
| `d1r3` | Día + rata, sin separador | día 1, rata 3 |
| `m` | Sexo | `m` = macho, `h` = hembra |
| `stx` | Tratamiento | sin tratamiento |
| `.mp4`, `.mat`, `.szv` | Extensión | según el archivo |

### Tratamientos

| Código | Significado |
|--------|-------------|
| `stx` | Sin tratamiento |
| `veh` | Vehículo |
| `dzp` | Diazepam |
| `skf` | SKF |
| `meth` | Metanfetamina |

### Contexto

Esta nomenclatura identifica una **sesión fuente completa**: una rata, día y fase.
No identifica todavía un ensayo individual, su tipo ni su resultado. Esos datos no
pueden conocerse desde el nombre del video antes de que Video Batch Processor lea
el video y, cuando exista, la fuente conductual asociada.

Aunque el estándar del lab contempla extensiones como `.mat`, `.mp4` y `.szv`, en este proyecto los `.mat` existentes y los CSV V1 nuevos se tratan como archivos de entrada. El Video Batch Processor no genera datos conductuales nuevos; genera clips de video.

Por ejemplo, el estándar del lab no distingue explícitamente:

- si el ensayo terminó en cruce o no cruce;
- si el evento fue timeout;
- si el clip corresponde a ITI;
- si el clip corresponde a habituación.

Por eso el programa necesita una tercera convención de salida.

---

## 3. Nomenclatura De Output Del Video Batch Processor

Es la nomenclatura específica de este programa para clips exportados.

```text
[iniciales]_[fecha]_[fase]_[dia][rata]_[sexo]_[segmento]_[tipoensayo]_[resultado]_[tratamiento].mp4
```

Ejemplo:

```text
abs_2601_f5_d1r3_m_cr1_p_cr_stx.mp4
```

Esta convención parte de los datos de sesión del estándar del lab y agrega los
campos que Video Batch Processor puede obtener solo después de analizar el
video: `segmento`, `tipoensayo` y `resultado`. Así permite saber qué parte del
video representa el clip y si el evento fue cruce, no cruce o timeout. El CSV
que acompaña cada corrida conserva siempre el número original de la fila
conductual para mantener trazabilidad.

En ensayos de riesgo/conflicto, el clip puede incluir el periodo de advertencia donde se prende el LED de ruido blanco antes de la luz de comida. Aun así, el identificador `eN` conserva la trazabilidad con el evento de la fuente conductual, cuya latencia empieza cuando se prende la luz de comida.

### Campos

| Parte | Significado | Ejemplo |
|-------|-------------|---------|
| `abs` | Iniciales del experimentador | `abs` |
| `2601` | Año y mes de inicio (YYMM) | enero 2026 |
| `f5` | Fase del protocolo | discriminación |
| `d1r3` | Día + rata | día 1, rata 3 |
| `m` | Sexo | macho |
| `cr1` | Segmento | primer evento clasificado como cruce en esa sesión |
| `p` | Tipo de ensayo | peligroso/riesgo |
| `cr` | Resultado | cruce |
| `stx` | Tratamiento | sin tratamiento |

### Segmentos

| Código | Significado |
|--------|-------------|
| `crN` | Evento con cruce. `N` cuenta solo cruces dentro de esa sesión. |
| `ncN` | Evento sin cruce. `N` cuenta solo no cruces dentro de esa sesión. |
| `eN` | Evento no clasificable (`na`) o timeout (`to`); conserva su número original de la fuente conductual. También se acepta al leer outputs históricos. |
| `itiN` | Intervalo entre eventos. Por ejemplo, `iti1` es el intervalo posterior a `e1`. |
| `hab` | Habituación cuando se exporta como un solo bloque. |
| `habini` | Habituación inicial, si se exporta separada. |
| `habfin` | Habituación final, si se exporta separada. |

### Tipo De Ensayo

| Código | Significado |
|--------|-------------|
| `s` | Seguro |
| `p` | Peligroso/riesgo |
| `na` | No aplica |

### Evento De Solo Ruido

CajaValentia registra este nuevo tipo experimental como `TipoEvento = 2`: tiene
LED y ruido aversivo, pero no luz de comida ni recompensa. Video Batch Processor
debe conservarlo como un evento distinto al segmentar y exportar.

El código corto que ocupará dentro del campo `tipoensayo` de la nomenclatura de
output todavía está pendiente de acuerdo. No se debe reutilizar `s`, `p` ni `na`
para ocultar esa diferencia experimental.

### Resultado

| Código | Significado |
|--------|-------------|
| `cr` | Cruce |
| `nc` | No cruce |
| `to` | Timeout |
| `rv` | Legacy only: accepted when reading old output, but no longer generated by this program |
| `na` | No aplica |

### Ejemplos DIS, Día 1, Rata 3

```text
abs_2601_f5_d1r3_m_cr1_p_cr_stx.mp4    # primer cruce peligroso/riesgo
abs_2601_f5_d1r3_m_cr2_s_cr_stx.mp4    # segundo cruce seguro
abs_2601_f5_d1r3_m_nc1_p_nc_stx.mp4    # primer no cruce peligroso/riesgo
abs_2601_f5_d1r3_m_nc2_s_nc_stx.mp4    # segundo no cruce seguro
abs_2601_f5_d1r3_m_e5_p_to_stx.mp4     # evento peligroso/riesgo con timeout
abs_2601_f5_d1r3_m_e6_s_rv_stx.mp4     # evento con excepción pendiente de revisión
abs_2601_f5_d1r3_m_iti1_na_na_stx.mp4  # ITI posterior al evento 1
abs_2601_f5_d1r3_m_hab_na_na_stx.mp4   # habituación
```

### Contadores Separados Y Trazabilidad

Para que la carpeta sea fácil de leer, los eventos de cruce y no cruce llevan
contadores independientes. Por ejemplo, una sesión puede tener `cr1` a `cr32`
y `nc1` a `nc34`, aunque haya 67 filas conductuales en total. El primer evento
queda `e1_*_na_*` porque no se puede comparar con un evento anterior.

La trazabilidad no se pierde: `clips_exportados.csv` guarda el campo `ensayo`,
que es el número original de la fila del `.mat` o CSV. Ese archivo es la fuente
para relacionar cada `crN` o `ncN` con el evento cronológico original.

---

## Relación Entre Las Tres Nomenclaturas

Un flujo típico queda así:

```text
Input legacy:
  exp_0126_dis_d1r3.mp4
  exp_0126_dis_d1r3.mat

Entrada con estándar del lab:
  abs_2601_f5_d1r3_m_stx.mp4
  abs_2601_f5_d1r3_m_stx.mat

Output del Video Batch Processor:
  abs_2601_f5_d1r3_m_cr1_p_cr_stx.mp4
  abs_2601_f5_d1r3_m_nc1_s_nc_stx.mp4
  abs_2601_f5_d1r3_m_iti1_na_na_stx.mp4
  abs_2601_f5_d1r3_m_hab_na_na_stx.mp4
```

La diferencia conceptual es:

| Nomenclatura | Qué representa | Para qué sirve |
|--------------|----------------|----------------|
| Legacy | Sesión completa existente | Leer datos actuales sin alterar fuentes |
| Estándar del lab | Sesión fuente nombrada con el acuerdo actual del laboratorio | Identificar la sesión antes de segmentar |
| Output del programa | Clip exportado, incluyendo cruce, no cruce, timeout, ITI o habituación | Recortar todo el video sin perder segmentos relevantes |

---

## Reglas Importantes

1. Todo en minúsculas.
2. Usar guion bajo `_` entre campos.
3. Día y rata van juntos: `d1r3`, no `d1_r3`.
4. No usar espacios en nombres de archivo.
5. No renombrar ni modificar los datos conductuales fuente.
6. El programa debe poder leer la nomenclatura legacy y la nomenclatura estándar del lab para sesiones fuente completas.
7. El programa debe exportar clips usando la nomenclatura de output del Video Batch Processor.
8. Para eventos clasificados, usar `crN` o `ncN` con contadores separados; usar
   `eN` solo cuando el resultado sea `na` o `to`. Consultar `clips_exportados.csv`
   para el número original de la fuente conductual.
9. Usar `na` cuando un campo no aplique, como en ITI o habituación.

---

## Para Implementación Futura

- `NomenclatureParser` debe detectar nombres legacy y nombres con estándar del lab de sesión fuente.
- `IBehavioralSessionReader` debe leer la fuente conductual sin asumir cambios de nombre.
- `SegmentPlanner` debe generar segmentos de tipo `eN`, `itiN`, `hab`, `habini` o `habfin`.
- `ClipExporter` debe construir nombres usando la nomenclatura de output del Video Batch Processor.
- La correspondencia entre video completo y fuente conductual se mantiene por el mismo `stem` de sesión cuando se trabaja con datos históricos o CSV V1.
