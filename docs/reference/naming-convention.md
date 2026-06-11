# Nomenclatura De Archivos

> Volver a la [visión general](../protocol/cmc-protocol.md)

Este documento define las tres nomenclaturas que conviven en el proyecto:

1. **Legacy / sesión completa**: nombres actuales de los datos históricos que ya existen.
2. **Estándar del lab**: nomenclatura acordada por el laboratorio para archivos futuros, pensada principalmente para ensayos de cruce.
3. **Output del Video Batch Processor**: extensión usada por este programa para exportar clips pequeños sin perder ensayos sin cruce, ITIs ni habituación.

La regla principal es simple: los archivos fuente, especialmente los `.mat`, **no se renombran ni se modifican**. El programa los lee, interpreta sus eventos y genera clips de video con la nomenclatura de output.

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

- El `.mat` legacy es fuente de verdad para eventos, latencias, cruces, no cruces y timeouts.
- El video legacy es el archivo completo que se va a recortar.
- Una fila del `.mat` corresponde a un evento experimental, no necesariamente a un cruce exitoso.
- El programa debe poder leer estos nombres porque son los datos reales actuales.

---

## 2. Nomenclatura Estándar Del Lab / Ensayos De Cruce

Es la nomenclatura acordada por el laboratorio para archivos futuros.

```text
[iniciales]_[fecha]_[fase]_[dia][rata]_[sexo]_[ensayo]_[tipoensayo]_[tratamiento].[extension]
```

Ejemplo:

```text
abs_2601_f5_d1r3_m_e1_p_stx.mp4
```

| Parte | Significado | Ejemplo |
|-------|-------------|---------|
| `abs` | Iniciales del experimentador | nombre + dos apellidos |
| `2601` | Año y mes de inicio (YYMM) | `2601` = enero 2026 |
| `f5` | Fase del protocolo | `f1` a `f5` |
| `d1r3` | Día + rata, sin separador | día 1, rata 3 |
| `m` | Sexo | `m` = macho, `h` = hembra |
| `e1` | Número de ensayo | `e1`, `e2`, `e30` |
| `p` | Tipo de ensayo | `s` = seguro, `p` = peligroso/riesgo |
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

Esta nomenclatura es el estándar general del laboratorio. Para el Video Batch Processor funciona como base, pero no alcanza por sí sola para describir todo lo que queremos exportar, porque está pensada principalmente para ensayos de cruce.

Aunque el estándar del lab contempla extensiones como `.mat`, `.mp4` y `.szv`, en este proyecto los `.mat` existentes se tratan como archivos de entrada. El Video Batch Processor no genera `.mat` nuevos; genera clips de video.

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
abs_2601_f5_d1r3_m_e1_p_cr_stx.mp4
```

Esta convención extiende el estándar del lab con un campo adicional: `resultado`. Ese campo permite saber si el evento fue cruce, no cruce o timeout.

En ensayos de riesgo/conflicto, el clip puede incluir el periodo de advertencia donde se prende el LED de ruido blanco antes de la luz de comida. Aun así, el identificador `eN` conserva la trazabilidad con el evento del `.mat`, cuya latencia empieza cuando se prende la luz de comida.

### Campos

| Parte | Significado | Ejemplo |
|-------|-------------|---------|
| `abs` | Iniciales del experimentador | `abs` |
| `2601` | Año y mes de inicio (YYMM) | enero 2026 |
| `f5` | Fase del protocolo | discriminación |
| `d1r3` | Día + rata | día 1, rata 3 |
| `m` | Sexo | macho |
| `e1` | Segmento | evento/ensayo 1 del `.mat` |
| `p` | Tipo de ensayo | peligroso/riesgo |
| `cr` | Resultado | cruce |
| `stx` | Tratamiento | sin tratamiento |

### Segmentos

| Código | Significado |
|--------|-------------|
| `eN` | Evento/ensayo registrado en el `.mat`. Se conserva el número para mantener trazabilidad con la fila correspondiente. |
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

### Resultado

| Código | Significado |
|--------|-------------|
| `cr` | Cruce |
| `nc` | No cruce |
| `to` | Timeout |
| `na` | No aplica |

### Ejemplos DIS, Día 1, Rata 3

```text
abs_2601_f5_d1r3_m_e1_p_cr_stx.mp4     # evento peligroso/riesgo con cruce
abs_2601_f5_d1r3_m_e2_s_cr_stx.mp4     # evento seguro con cruce
abs_2601_f5_d1r3_m_e3_p_nc_stx.mp4     # evento peligroso/riesgo sin cruce
abs_2601_f5_d1r3_m_e4_s_nc_stx.mp4     # evento seguro sin cruce
abs_2601_f5_d1r3_m_e5_p_to_stx.mp4     # evento peligroso/riesgo con timeout
abs_2601_f5_d1r3_m_iti1_na_na_stx.mp4  # ITI posterior al evento 1
abs_2601_f5_d1r3_m_hab_na_na_stx.mp4   # habituación
```

### Por Qué `eN` No Significa Solo Cruce

En esta convención, `eN` significa **evento/ensayo registrado en el `.mat`**, no "cruce exitoso".

Esto conserva trazabilidad directa:

```text
fila/evento 12 del .mat -> e12 -> clip e12
```

El resultado conductual se codifica aparte:

```text
e12_p_cr  -> evento 12, peligroso, con cruce
e13_s_nc  -> evento 13, seguro, sin cruce
e14_p_to  -> evento 14, peligroso, timeout
```

---

## Relación Entre Las Tres Nomenclaturas

Un flujo típico queda así:

```text
Input legacy:
  exp_0126_dis_d1r3.mp4
  exp_0126_dis_d1r3.mat

Estándar del lab:
  abs_2601_f5_d1r3_m_e1_p_stx.mp4

Output del Video Batch Processor:
  abs_2601_f5_d1r3_m_e1_p_cr_stx.mp4
  abs_2601_f5_d1r3_m_e2_s_nc_stx.mp4
  abs_2601_f5_d1r3_m_iti1_na_na_stx.mp4
  abs_2601_f5_d1r3_m_hab_na_na_stx.mp4
```

La diferencia conceptual es:

| Nomenclatura | Qué representa | Para qué sirve |
|--------------|----------------|----------------|
| Legacy | Sesión completa existente | Leer datos actuales sin alterar fuentes |
| Estándar del lab | Ensayo de cruce nombrado de forma común en el laboratorio | Mantener compatibilidad conceptual con el acuerdo del lab |
| Output del programa | Clip exportado, incluyendo cruce, no cruce, timeout, ITI o habituación | Recortar todo el video sin perder segmentos relevantes |

---

## Reglas Importantes

1. Todo en minúsculas.
2. Usar guion bajo `_` entre campos.
3. Día y rata van juntos: `d1r3`, no `d1_r3`.
4. No usar espacios en nombres de archivo.
5. No renombrar ni modificar los `.mat` fuente.
6. El programa debe poder leer la nomenclatura legacy y la nomenclatura estándar del lab.
7. El programa debe exportar clips usando la nomenclatura de output del Video Batch Processor.
8. Para eventos del `.mat`, conservar `eN` aunque no haya cruce.
9. Usar `na` cuando un campo no aplique, como en ITI o habituación.

---

## Para Implementación Futura

- `NomenclatureParser` debe detectar nombres legacy y nombres con estándar del lab.
- `MatParser` debe leer el `.mat` fuente sin asumir cambios de nombre.
- `SegmentPlanner` debe generar segmentos de tipo `eN`, `itiN`, `hab`, `habini` o `habfin`.
- `ClipExporter` debe construir nombres usando la nomenclatura de output del Video Batch Processor.
- La correspondencia entre video completo y `.mat` se mantiene por el nombre legacy de sesión cuando se trabaja con datos históricos.
