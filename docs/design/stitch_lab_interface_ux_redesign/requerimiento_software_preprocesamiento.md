# 📋 Requerimientos: Video Batch PreProcessor (DeepLabCut)

**Fecha:** 01-05-2026
**Estado:** Definición Técnica
**Stack:** C# / Avalonia UI / FFmpeg
**Ref:** [[Guia Estándar de Desarrollo de Apps]]

---

## 🎯 Objetivo del Proyecto
Desarrollar una aplicación de escritorio nativa (Windows/macOS) para automatizar el pre-procesamiento masivo de videos de laboratorio.
*   **Meta:** Normalizar videos (recorte, rotación y cropping) en un solo paso antes de ingresarlos a **DeepLabCut**.
*   **Prioridad:** Mantener la integridad de los frames (sin pérdida visual) y ofrecer una UX "drag-and-drop" para usuarios no técnicos.

## 🖥️ Interfaz de Usuario (UI/UX)

### Estética General
*   **Estilo:** Minimalista, "Scientific Modern".
*   **Tipografía:** Inter o Segoe UI Variable.
*   **Feedback:** Barra de progreso global y estado por archivo (Pendiente -> Procesando -> Listo).

### Controles Principales
1.  **Lista de Archivos (Dropzone):**
    *   Área central grande para arrastrar y soltar (Drag & Drop) los 10+ videos.
    *   Soporte para `Ctrl+A` y `Delete`.
2.  **Sección de Recorte (Trim):**
    *   **Input Numérico:** Caja para ingresar minutos (ej. "4").
    * La idea es que el programa sepa cuanto dura cada video y pueda recortar los primeros y últimos minutos.
	    * Acortar el video para optimizar el procesamiento de los videos en DeepLabCut y SimBA.
    *   **Toggle "Asymmetric Trim":**
        *   *OFF:* El valor se aplica a inicio y fin.
        *   *ON:* Despliega dos inputs separados: "Start Cut" y "End Cut".
3.  **Sección de Cropping:**
    *   **Botón "Set Crop Area":** Abre un modal o vista previa del primer video cargado.
    *   **Interacción:** Permite dibujar un rectángulo sobre el frame.
    *   **Output Visual:** Muestra las coordenadas seleccionadas (W, H, X, Y) en la UI principal una vez confirmadas.
4.  **Sección de Rotación:**
    *   Botón Toggle: "Rotate 180°" o "Mirror / Flip".
5.  **Acción:** Botón primario grande "Process Batch".

## ⚙️ Lógica Técnica (Backend)

### 1. Motor de Procesamiento
*   **Core:** FFmpeg (Ejecutado internamente).
*   **Gestión de Dependencias:** `ffmpeg` (y `ffprobe`) deben estar incrustados como **Embedded Resources** dentro del ejecutable y extraerse a una carpeta temporal en tiempo de ejecución. *El usuario no instala nada.*

### 2. Algoritmo de Recorte
Para cada video en la lista:
1.  **Sondeo:** Usar `ffprobe` para obtener `Total_Duration`.
2.  **Cálculo del Final:**
    *   `Cut_End_Point = Total_Duration - Minutes_To_Remove_From_End`
3.  **Construcción del Comando:**
    *   Input: `-ss [Start_Minutes]`
    *   Input: `-to [Cut_End_Point]`

### 3. Parámetros de Calidad (DeepLabCut Standard)
El comando de exportación debe garantizar la estabilidad de los frames para el tracking:
*   **Codec:** H.264 (`libx264`).
*   **Calidad:** `-crf 18` (Visually Lossless).
*   **Filtros (`-vf`):** Cadena combinada de crop y rotación.
    *   Ejemplo: `"crop=w:h:x:y,transpose=2,transpose=2"` (para 180°).

## 📦 Arquitectura de Entrega

Siguiendo la [[Guia Estándar de Desarrollo de Apps]]:
1.  **Framework:** **Avalonia UI (.NET 8)**.
2.  **Compilación:** **Native AOT** (Ahead-of-Time) para minimizar peso y acelerar arranque.
3.  **Salida:** Single-File Executable (Un solo `.exe` o `.app` autocontenido).
4.  **Peso Objetivo:** < 150 MB.

---
*Generado para facilitar el desarrollo con asistencia de IA.*
