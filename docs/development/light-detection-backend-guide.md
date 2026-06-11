# Guía Backend - LightDetection

Esta guía explica qué se hizo en `LightDetection`, por qué está separado de la GUI y cómo se usa desde código.

El objetivo es que Eric pueda trabajar el backend sin depender de la ventana visual de Avalonia.

## Idea Principal

El módulo `LightDetection` vive en:

```text
src/VideoBatchProcessor.Core/LightDetection/
```

Este módulo no abre videos, no dibuja ventanas y no sabe nada de Avalonia. Su trabajo es más pequeño:

> Dado el brillo promedio de tres regiones, decidir si la luz de comida izquierda, la luz de comida derecha y el LED/ruido están prendidos o apagados.

Esto es intencional. En backend queremos piezas pequeñas que se puedan probar sin abrir una interfaz gráfica.

## Qué Se Hizo

Se creó un proyecto nuevo:

```text
src/VideoBatchProcessor.Core/
```

Este proyecto representa la lógica real del producto, separada del prototipo visual.

Dentro de ese proyecto se creó:

```text
LightDetection/
├── LightId.cs
├── LightRoi.cs
├── LightDetectionConfig.cs
├── IFrameBrightnessSource.cs
├── LightReading.cs
├── LightSample.cs
└── LightDetector.cs
```

## Qué Hace Cada Archivo

### `LightId.cs`

Define cuáles luces existen para este módulo:

```text
FoodLeft
FoodRight
NoiseLed
```

Usamos estos nombres porque describen el significado experimental, no solo la posición visual.

### `LightRoi.cs`

Representa una región de interés.

Una ROI tiene:

- Qué luz representa.
- Coordenada `x`.
- Coordenada `y`.
- Ancho.
- Alto.
- Umbral.

Ejemplo conceptual:

```text
FoodLeft está en x=100, y=80, width=30, height=30, threshold=180
```

### `LightDetectionConfig.cs`

Agrupa las tres ROIs necesarias:

```text
FoodLeft ROI
FoodRight ROI
NoiseLed ROI
```

El detector necesita esta configuración para saber dónde mirar y qué umbral usar.

### `IFrameBrightnessSource.cs`

Esta es la pieza más importante para entender backend sin GUI.

`LightDetector` no necesita saber si el frame viene de:

- OpenCV.
- Una imagen sintética.
- Un video real.
- Una prueba unitaria.

Solo necesita una cosa:

```text
Dame el brillo promedio de esta ROI.
```

Por eso existe esta interfaz:

```csharp
public interface IFrameBrightnessSource
{
    double GetMeanBrightness(LightRoi roi);
}
```

Una interfaz es un contrato. Dice qué operación debe existir, pero no dice cómo implementarla.

Más adelante, puede existir una clase como:

```text
OpenCvFrameBrightnessSource
```

que use OpenCV para leer píxeles reales. Pero `LightDetector` no tiene que saber eso.

### `LightReading.cs`

Representa la lectura de una sola luz:

```text
Light
Brightness
Threshold
IsOn
```

La regla actual es simple:

```text
IsOn = Brightness > Threshold
```

### `LightSample.cs`

Representa el resultado completo de analizar un frame:

```text
FrameIndex
TimeSeconds
FoodLeft reading
FoodRight reading
NoiseLed reading
```

También tiene accesos rápidos:

```text
IsFoodLeftOn
IsFoodRightOn
IsNoiseLedOn
```

### `LightDetector.cs`

Es la clase principal del módulo.

El método principal es:

```csharp
Analyze(IFrameBrightnessSource frame, int frameIndex, double timeSeconds)
```

Ese método:

1. Recibe una fuente de brillo.
2. Lee el brillo de `FoodLeft`, `FoodRight` y `NoiseLed`.
3. Compara cada brillo contra su umbral.
4. Regresa un `LightSample`.

## Entonces, Cómo Se Corre La Función

Hay un detalle importante: `VideoBatchProcessor.Core` es una librería, no una aplicación.

Eso significa que no se corre así:

```bash
dotnet run src/VideoBatchProcessor.Core
```

Una librería no tiene ventana ni `Main`. Una librería se usa desde otro lugar:

- una prueba unitaria,
- una app de consola,
- la app visual,
- otro módulo del sistema.

Hoy lo que sí se puede correr es el build:

```bash
dotnet build src/VideoBatchProcessor.Core/VideoBatchProcessor.Core.csproj
```

Eso confirma que el módulo compila.

Para “correr la función” hay que llamarla desde código. Por ejemplo, desde una prueba o desde una app de consola.

## Ejemplo Mínimo Sin GUI

Este ejemplo no abre video. Solo simula brillos.

```csharp
using VideoBatchProcessor.Core.LightDetection;

var config = new LightDetectionConfig(
    new LightRoi(LightId.FoodLeft, x: 10, y: 10, width: 20, height: 20, threshold: 180),
    new LightRoi(LightId.FoodRight, x: 40, y: 10, width: 20, height: 20, threshold: 180),
    new LightRoi(LightId.NoiseLed, x: 70, y: 10, width: 20, height: 20, threshold: 180)
);

var detector = new LightDetector(config);

var frame = new FakeBrightnessSource(new Dictionary<LightId, double>
{
    [LightId.FoodLeft] = 220,  // ON
    [LightId.FoodRight] = 80,  // OFF
    [LightId.NoiseLed] = 40    // OFF
});

var sample = detector.Analyze(frame, frameIndex: 0, timeSeconds: 0.0);

Console.WriteLine($"FoodLeft:  {sample.IsFoodLeftOn}");
Console.WriteLine($"FoodRight: {sample.IsFoodRightOn}");
Console.WriteLine($"NoiseLed:  {sample.IsNoiseLedOn}");

public sealed class FakeBrightnessSource : IFrameBrightnessSource
{
    private readonly IReadOnlyDictionary<LightId, double> _brightness;

    public FakeBrightnessSource(IReadOnlyDictionary<LightId, double> brightness)
    {
        _brightness = brightness;
    }

    public double GetMeanBrightness(LightRoi roi)
    {
        return _brightness[roi.Light];
    }
}
```

Salida esperada:

```text
FoodLeft:  True
FoodRight: False
NoiseLed:  False
```

Esto es backend puro: no hay GUI, no hay video real, no hay OpenCV. Solo se prueba la lógica.

## Cómo Se Conecta Después Con Video Real

Para video real se necesita un adaptador.

El adaptador toma un frame real y cumple el contrato `IFrameBrightnessSource`.

Conceptualmente:

```text
OpenCV frame
  -> OpenCvFrameBrightnessSource
    -> GetMeanBrightness(roi)
      -> LightDetector.Analyze(...)
        -> LightSample
```

Así se mantiene separada la responsabilidad:

- OpenCV sabe leer píxeles.
- `LightDetector` sabe decidir ON/OFF.
- La UI sabe mostrar resultados.

## Qué Debe Hacer Eric Ahora

Primero no necesita tocar la GUI.

El orden recomendado es:

1. Entender `LightDetector.Analyze`.
2. Entender `IFrameBrightnessSource`.
3. Hacer pruebas con una fuente falsa de brillo.
4. Confirmar que los tres casos básicos funcionan:
   - todo oscuro = todo OFF.
   - brillo alto en `FoodLeft` = `FoodLeft` ON.
   - brillo alto en `FoodRight` = `FoodRight` ON.
   - brillo alto en `NoiseLed` = `NoiseLed` ON.
5. Después crear el adaptador para OpenCV.
6. Solo al final conectar eso al prototipo visual.

## Por Qué El Prototipo Actual No Es Suficiente

El prototipo en `src/LightEventDetector/` está bien porque demuestra que la idea funciona.

El problema es que mezcla varias cosas:

- UI.
- OpenCV.
- Lectura de video.
- Detección de luces.
- Clasificación de eventos.
- Generación de CSV/timeline.

Eso sirve para explorar, pero no para construir el sistema completo.

La arquitectura final necesita módulos pequeños:

```text
LightDetector
  -> solo detecta luces

LightTimelineBuilder
  -> convierte muchos frames en una línea de tiempo estable

SegmentPlanner
  -> decide ensayos, ITI y habituación usando luces + .mat
```

Por eso lo que hizo Eric no se tira. Se usa como prototipo y como referencia, pero el módulo final debe vivir limpio en `VideoBatchProcessor.Core`.

## Regla De Trabajo

Si una clase empieza a abrir videos, dibujar ventanas, detectar luces y clasificar eventos al mismo tiempo, ya está haciendo demasiado.

Para junio, el foco es:

```text
LightDetection = brillo + umbral + ON/OFF
```

Nada más.

Para validar este módulo sin GUI, ver también [Guía De Pruebas - LightDetection](light-detection-testing-guide.md).
