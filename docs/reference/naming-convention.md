# Nomenclatura De Archivos

> Volver al [índice de documentación](../README.md).

En este proyecto conviven tres formas de nombrar archivos. Las fuentes
conductuales y los videos completos se conservan tal como existen; solo los
clips creados por Video Batch Processor reciben la nomenclatura de output.

## Reglas Generales

- Todo va en minúsculas y se separa con guion bajo (`_`).
- Día y rata van juntos: `d1r3`, no `d1_r3`.
- Los archivos fuente nunca se renombran ni modifican.
- El programa identifica videos fuente por nomenclatura legacy o estándar del
  lab. Un clip de output nunca vuelve a procesarse como video fuente.

## 1. Legacy: Sesión Completa Histórica

Es la nomenclatura de los datos ya existentes en el laboratorio.

```text
exp_0126_dis_d1r3.mp4
exp_0126_dis_d1r3.mat
```

| Parte | Significado |
|-------|-------------|
| `exp` | Prefijo histórico. |
| `0126` | Mes y año de inicio (`MMYY`). |
| `dis` | Fase: `lc`, `cs`, `cm`, `cp`, `dis` o `pb`. |
| `d1r3` | Día y rata. |

Representa una sesión completa, no un evento individual.

## 2. Estándar Del Lab: Sesión Fuente Nueva

Es la nomenclatura acordada para sesiones futuras.

```text
[iniciales]_[fecha]_[fase]_[dia][rata]_[sexo]_[tratamiento].[extensión]

abs_2601_f5_d1r3_m_stx.mp4
```

| Parte | Significado |
|-------|-------------|
| `abs` | Iniciales del experimentador. |
| `2601` | Año y mes de inicio (`YYMM`). |
| `f1`–`f6` | Fase experimental. |
| `d1r3` | Día y rata. |
| `m` / `h` | Sexo. |
| `stx`, `veh`, `dzp`, `skf`, `meth` | Tratamiento. |

También representa una sesión completa. Antes de procesar el video no se puede
saber desde su nombre si un evento terminó en cruce, no cruce o timeout.

## 3. Output: Clips Del Video Batch Processor

```text
[iniciales]_[fecha]_[fase]_[dia][rata]_[sexo]_[segmento]_[tipo]_[resultado]_[tratamiento].mp4
```

Ejemplo:

```text
abs_2601_f5_d1r3_m_e02_p_cr_stx.mp4
```

| Campo | Qué indica |
|-------|------------|
| `segmento` | Qué parte de la sesión representa el clip. |
| `tipo` | `s` seguro, `p` peligroso/riesgo, `ruido` solo ruido o `na` cuando no aplica. |
| `resultado` | `cr` cruce, `nc` no cruce, `to` timeout o `na` cuando no aplica. |

### Orden De Eventos E ITIs

El orden se conserva directamente en el nombre para que una carpeta sea fácil
de leer sin depender primero del Excel.

| Código | Significado |
|--------|-------------|
| `e01`, `e02`, ... | Evento conductual 1, 2, ... de la sesión, en el orden de la fuente conductual ya emparejada con el video. El número siempre tiene dos dígitos para que Finder los ordene correctamente. |
| `iti01`, `iti02`, ... | ITI posterior a `e01`, `e02`, ... respectivamente. Los ITIs quedan como una serie aparte, pero mantienen el orden de sus eventos asociados. |
| `habini` | Habituación inicial. |
| `habfin` | Habituación final. |

`eNN` no afirma que ocurrió un cruce. El resultado vive en su propio campo.
Esta regla aplica a todas las fases que el programa llegue a procesar.

```text
abs_2605_f4_d1r2_m_e01_p_nc_stx.mp4    # primer evento de CP; no cruce
abs_2605_f4_d1r2_m_iti01_na_na_stx.mp4 # ITI posterior a e01
abs_2605_f4_d1r2_m_e02_p_cr_stx.mp4    # segundo evento de CP; cruce
abs_2605_f4_d1r2_m_e03_p_to_stx.mp4    # tercer evento de CP; timeout
```

En una sesión de discriminación, por ejemplo, los eventos podrán leerse como
`e01` a `e30`, mientras que `s` o `p` y `cr`, `nc` o `to` explicarán el tipo y
resultado de cada uno. La regla experimental que calcula esos campos depende
de la fase; la nomenclatura solo los conserva de manera legible.

### Regla Confirmada Para CP

En Cruces Peligrosos (`cp` / `f4`), cada fila conductual corresponde a `eNN`.
El tipo siempre es `p` y el resultado se obtiene así:

| Dato conductual | Resultado |
|-----------------|-----------|
| `Lado = -2` | `to` |
| `Lado` válido y `Desplaz > 1 s` | `cr` |
| `Lado` válido y `Desplaz <= 1 s` | `nc` |

## Relación Entre Las Tres Nomenclaturas

```text
Fuente legacy:       exp_0126_cp_d1r3.mp4
Fuente estándar:     abs_2601_f4_d1r3_m_stx.mp4
Clip de output:      abs_2601_f4_d1r3_m_e02_p_cr_stx.mp4
```

El CSV `clips_exportados.csv` acompaña cada carpeta de recortes y conserva la
relación completa entre clip, fila conductual, frames y tiempos del video
original.
