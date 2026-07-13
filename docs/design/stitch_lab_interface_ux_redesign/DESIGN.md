---
name: Precision Laboratory
colors:
  surface: '#f7f9fb'
  surface-dim: '#d8dadc'
  surface-bright: '#f7f9fb'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#f2f4f6'
  surface-container: '#eceef0'
  surface-container-high: '#e6e8ea'
  surface-container-highest: '#e0e3e5'
  on-surface: '#191c1e'
  on-surface-variant: '#45464d'
  inverse-surface: '#2d3133'
  inverse-on-surface: '#eff1f3'
  outline: '#76777d'
  outline-variant: '#c6c6cd'
  surface-tint: '#565e74'
  primary: '#000000'
  on-primary: '#ffffff'
  primary-container: '#131b2e'
  on-primary-container: '#7c839b'
  inverse-primary: '#bec6e0'
  secondary: '#00668a'
  on-secondary: '#ffffff'
  secondary-container: '#40c2fd'
  on-secondary-container: '#004d6a'
  tertiary: '#000000'
  on-tertiary: '#ffffff'
  tertiary-container: '#002109'
  on-tertiary-container: '#009844'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#dae2fd'
  primary-fixed-dim: '#bec6e0'
  on-primary-fixed: '#131b2e'
  on-primary-fixed-variant: '#3f465c'
  secondary-fixed: '#c4e7ff'
  secondary-fixed-dim: '#7bd0ff'
  on-secondary-fixed: '#001e2c'
  on-secondary-fixed-variant: '#004c69'
  tertiary-fixed: '#6bff8f'
  tertiary-fixed-dim: '#4ae176'
  on-tertiary-fixed: '#002109'
  on-tertiary-fixed-variant: '#005321'
  background: '#f7f9fb'
  on-background: '#191c1e'
  surface-variant: '#e0e3e5'
  border-subtle: '#E2E8F0'
  text-main: '#1E293B'
  text-muted: '#64748B'
  surface-card: '#FFFFFF'
  input-bg: '#F1F5F9'
typography:
  headline-lg:
    fontFamily: Inter
    fontSize: 24px
    fontWeight: '600'
    lineHeight: 32px
    letterSpacing: -0.02em
  headline-md:
    fontFamily: Inter
    fontSize: 18px
    fontWeight: '600'
    lineHeight: 24px
  body-md:
    fontFamily: Inter
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
  body-sm:
    fontFamily: Inter
    fontSize: 13px
    fontWeight: '400'
    lineHeight: 18px
  label-bold:
    fontFamily: Inter
    fontSize: 12px
    fontWeight: '600'
    lineHeight: 16px
    letterSpacing: 0.05em
  label-mono:
    fontFamily: Inter
    fontSize: 12px
    fontWeight: '500'
    lineHeight: 16px
  data-display:
    fontFamily: Inter
    fontSize: 11px
    fontWeight: '700'
    lineHeight: 14px
rounded:
  sm: 0.125rem
  DEFAULT: 0.25rem
  md: 0.375rem
  lg: 0.5rem
  xl: 0.75rem
  full: 9999px
spacing:
  unit: 4px
  container-padding: 24px
  element-gap: 16px
  tight-gap: 8px
  margin-sm: 12px
---

## Brand & Style

The design system is centered on the **"Scientific Modern"** aesthetic, specifically tailored for high-precision laboratory desktop environments. The personality is clinical, efficient, and rigorously organized, prioritizing the heavy data density required for scientific video processing.

The style is **Minimalist**, leaning into a **Corporate / Modern** framework with subtle **Tonal Layers**. It avoids unnecessary ornamentation to reduce cognitive load during long research sessions. Every pixel must serve a functional purpose, evoking a sense of reliability and technical excellence. The UI should feel like a high-end instrument—solid, responsive, and precise.

## Colors

The palette is anchored by a deep **Slate/Navy (#0F172A)** for primary actions and structural identity, establishing an atmosphere of authority. A **Subtle Scientific Blue (#38BDF8)** is utilized for high-precision highlights, focus states, and active interactive elements.

**Success Green (#22C55E)** is reserved strictly for 'ready' states and completed batch processes, ensuring clear visual confirmation. The background uses a **Neutral Light Gray (#F8FAFC)** to minimize eye strain and provide a clean canvas for content. The contrast ratio between text and background must strictly adhere to accessibility standards to ensure legibility in various laboratory lighting conditions.

## Typography

The design system utilizes **Inter** for its exceptional legibility and high x-height, which is critical for reading numeric parameters and file paths.

The type hierarchy is designed for **high data density**. Headlines use tighter letter spacing for a compact, professional look. Labels for inputs and coordinates (`label-bold`) are intentionally small and occasionally capitalized to differentiate metadata from user data. For technical outputs like FFmpeg logs or coordinate displays (W, H, X, Y), use the `label-mono` or `data-display` style to maintain alignment and readability of numeric strings.

## Layout & Spacing

The layout follows a **Fixed Grid** philosophy suitable for a desktop application window, typically targeting a minimum resolution of 1280x800. The interface is divided into a three-column system or a sidebar-main configuration depending on the workflow stage.

A 4px baseline grid ensures a rhythmic spacing system. Elements are grouped in logical containers with 16px internal padding. High-density areas, such as the numeric input fields for trimming, use a 8px gap (`tight-gap`) to keep related controls visually unified. Large margins (24px) are used at the edges of the primary window to prevent a cluttered appearance.

## Elevation & Depth

This design system uses **Tonal Layers** rather than heavy shadows to convey hierarchy.

The base background is the most recessed layer. **White surfaces (#FFFFFF)** with a **1px solid border (#E2E8F0)** represent the primary interaction cards. A single, very subtle ambient shadow (2px blur, 4% opacity) may be used for the active "Process" panel to give it a slight lift. Modals or "Set Crop Area" views use a semi-transparent backdrop blur (Glassmorphism) to keep the context of the main application visible while focusing the user on the specific task.

## Shapes

The shape language is **Soft (0.25rem)**, striking a balance between clinical precision and modern software friendliness. Buttons, input fields, and cards share this uniform corner radius. For specialized containers like the "Dropzone" or the "Set Crop Area" modal, a larger radius (`rounded-lg`) of 0.5rem may be used to differentiate the work area from the configuration panels.

## Components

### Buttons
- **Primary:** Deep Navy (#0F172A) background with White text. High contrast, bold.
- **Secondary/Toggle:** Light Gray (#F1F5F9) background. When active (e.g., Asymmetric Trim ON), the border or background transitions to the Scientific Blue (#38BDF8).

### Input Fields
- Numeric inputs for minutes and coordinates should be compact with a light background (#F1F5F9). Labels are placed above the input in `label-bold`.

### Dropzone
- The file upload area features a dashed border (2px) in Slate. When files are hovered over, the background shifts to a very light tint of the Scientific Blue. Use a large icon and `headline-md` for the "Drag & Drop" instruction.

### Cards & Sections
- Settings are grouped into white cards with clear headers. Each section (Trim, Crop, Rotation) is visually separated to allow the user to scan the configuration quickly.

### Progress Indicators
- **Global:** A thick horizontal bar at the bottom of the UI.
- **Per-file:** A small pill-shaped indicator next to file names: "Pending" (Gray), "Processing" (Blue), "Ready" (Green).

### Specialized Crop Tool
- A high-contrast overlay (Scientific Blue) for the bounding box selection. Coordinates are displayed in real-time in a floating tooltip using `label-mono`.