# FFmpeg Packaging Input

Esta carpeta es una entrada local para crear paquetes de distribución. Los
binarios no se guardan en Git.

Estructura esperada:

```text
vendor/ffmpeg/
├── osx-arm64/
│   ├── ffmpeg
│   └── ffprobe
├── osx-x64/
│   ├── ffmpeg
│   └── ffprobe
└── win-x64/
    ├── ffmpeg.exe
    └── ffprobe.exe
```

Cada par debe ser portable para su plataforma y debe incluirse únicamente
después de registrar su versión, configuración de compilación, licencia y
origen. El empaquetado falla si estos archivos no existen: así se evita crear
una aplicación que funcione solo en la computadora del desarrollador.

El exportador actual solicita `libx264`. Por ello, antes de distribuir FFmpeg,
se debe decidir y documentar expresamente cómo se cumplirán las condiciones de
la GPL de la compilación elegida. No copiar aquí los ejecutables instalados por
Homebrew: normalmente dependen de bibliotecas externas y no son portables.
