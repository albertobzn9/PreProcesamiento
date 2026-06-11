# Guía De Pruebas - LightDetection

Esta guía explica qué son las pruebas automáticas para el módulo `LightDetection`, por qué son importantes y qué debe implementar Eric para que el módulo quede revisable.

## Idea Principal

Una prueba automática es una forma de decirle al programa:

```text
Si te doy este input exacto, espero este output exacto.
```

Si después alguien cambia el código y rompe ese comportamiento, el comando de pruebas falla. Eso permite revisar avances sin depender de abrir la GUI, cargar un video o interpretar manualmente si "parece" que funcionó.

Para `LightDetection`, las pruebas deben ser pequeñas. El módulo no necesita abrir videos para probarse. Se puede simular el brillo de cada luz.

Ejemplo conceptual:

```text
FoodLeft brightness  = 220
FoodRight brightness = 80
NoiseLed brightness  = 40
Threshold            = 180
```

Resultado esperado:

```text
FoodLeft  = ON
FoodRight = OFF
NoiseLed  = OFF
```

## Qué Módulo Se Está Probando

Las pruebas de esta guía aplican a:

```text
src/VideoBatchProcessor.Core/LightDetection/
```

El objetivo es validar el módulo limpio del backend, no el prototipo visual en `src/LightEventDetector/`.

## Por Qué No Se Usa Video Real Al Inicio

Un video real mete muchas variables al mismo tiempo:

- OpenCV.
- Lectura de archivo.
- Formato del video.
- FPS.
- ROIs mal colocadas.
- Brillo variable.
- GUI.

Eso sirve para integración, pero no para probar la lógica mínima.

Primero se prueba la regla central:

```text
ROI + brillo + umbral = ON/OFF
```

Después se conecta con video real mediante un adaptador como `OpenCvFrameBrightnessSource`.

## Estructura Esperada De Pruebas

Cuando se implementen, las pruebas deberían vivir en un proyecto separado:

```text
tests/
└── VideoBatchProcessor.Core.Tests/
    ├── LightDetection/
    │   ├── LightRoiTests.cs
    │   ├── LightDetectionConfigTests.cs
    │   └── LightDetectorTests.cs
    └── VideoBatchProcessor.Core.Tests.csproj
```

Ese proyecto de pruebas debe referenciar:

```text
src/VideoBatchProcessor.Core/VideoBatchProcessor.Core.csproj
```

Así las pruebas validan la librería real.

## Comando Esperado

Cuando el proyecto de pruebas exista, el comando principal debe ser:

```bash
dotnet test VideoBatchProcessor.sln
```

Resultado esperado:

```text
Passed
```

Si una prueba falla, no significa que todo esté mal. Significa que hay un caso específico donde el comportamiento real no coincide con el comportamiento esperado.

## Casos Mínimos Que Deben Existir

### 1. `LightRoi`

Validar que una ROI correcta se pueda crear.

Validar que una ROI inválida falle:

- `x` negativo.
- `y` negativo.
- `width` menor o igual a cero.
- `height` menor o igual a cero.
- `threshold` negativo.

Esto protege al resto del sistema de configuraciones imposibles.

### 2. `LightDetectionConfig`

Validar que la configuración exige exactamente estas tres luces:

```text
FoodLeft
FoodRight
NoiseLed
```

Casos importantes:

- Si la ROI de `FoodLeft` viene marcada como `FoodRight`, debe fallar.
- Si la ROI de `FoodRight` viene marcada como `FoodLeft`, debe fallar.
- Si la ROI de `NoiseLed` viene marcada como otra luz, debe fallar.
- Si una ROI es `null`, debe fallar con un error claro.

### 3. `LightReading`

Validar la regla actual:

```text
IsOn = Brightness > Threshold
```

Casos importantes:

- Brillo menor que el umbral = OFF.
- Brillo mayor que el umbral = ON.
- Brillo exactamente igual al umbral = OFF.

Este último caso debe quedar explícito porque evita ambigüedad.

### 4. `LightDetector.Analyze`

Validar que el detector produce un `LightSample` correcto.

Casos mínimos:

- Todo oscuro = todo OFF.
- Brillo alto en `FoodLeft` = solo `FoodLeft` ON.
- Brillo alto en `FoodRight` = solo `FoodRight` ON.
- Brillo alto en `NoiseLed` = solo `NoiseLed` ON.
- Varias luces con brillo alto = varias luces ON.
- `frameIndex` negativo = error.
- `timeSeconds` negativo = error.
- `frame` null = error.

## Cómo Simular Brillo Sin Video

Para probar sin video se usa una fuente falsa de brillo.

Ejemplo conceptual:

```csharp
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

Con eso se puede decir:

```text
Para esta prueba, FoodLeft vale 220, FoodRight vale 80 y NoiseLed vale 40.
```

Y después confirmar que `LightDetector.Analyze(...)` responde lo esperado.

## Ejemplo De Prueba Conceptual

```csharp
var config = new LightDetectionConfig(
    new LightRoi(LightId.FoodLeft, x: 0, y: 0, width: 10, height: 10, threshold: 180),
    new LightRoi(LightId.FoodRight, x: 20, y: 0, width: 10, height: 10, threshold: 180),
    new LightRoi(LightId.NoiseLed, x: 40, y: 0, width: 10, height: 10, threshold: 180)
);

var detector = new LightDetector(config);

var frame = new FakeBrightnessSource(new Dictionary<LightId, double>
{
    [LightId.FoodLeft] = 220,
    [LightId.FoodRight] = 80,
    [LightId.NoiseLed] = 40
});

var sample = detector.Analyze(frame, frameIndex: 0, timeSeconds: 0);

// Esperado:
// sample.IsFoodLeftOn  == true
// sample.IsFoodRightOn == false
// sample.IsNoiseLedOn  == false
```

## Qué Debe Entregar Eric

Eric debe entregar pruebas que permitan responder estas preguntas:

- ¿La ROI rechaza valores inválidos?
- ¿La configuración exige las tres luces correctas?
- ¿La regla de umbral está clara?
- ¿El detector prende y apaga cada luz correctamente?
- ¿El módulo puede probarse sin GUI?

La entrega ideal debe incluir:

- Proyecto de pruebas agregado a la solución.
- Casos de prueba para `LightDetection`.
- Comando para correr todo.
- Salida esperada.
- Nota breve de cualquier límite conocido.

## Criterio De Terminado

Esta parte se considera terminada cuando:

```bash
dotnet test VideoBatchProcessor.sln
```

corre correctamente y valida los casos mínimos del módulo `LightDetection`.

Si las pruebas pasan, podemos decir con más seguridad que el módulo base de detección de luces está estable antes de conectarlo con video real.
