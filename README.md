# Kurogane Touge 黒鉄峠

Juego de carreras de montaña nocturnas con estética de PS2. Bajas el Monte Kurogane (2,3 km, 4 horquillas y 1 túnel) contra un rival, derrapando, metiendo las ruedas en la canaleta de las horquillas y sin derramar el vaso de agua del portavasos.

Es el port a Unity del prototipo web. El objetivo es publicarlo para **Android** y **PC (Windows)**.

## Requisitos

- **Unity 6 LTS** (`6000.0.x`). Si tienes una versión 6 más nueva, Unity Hub ofrece abrirlo con ella y actualiza los paquetes solo.
- Paquetes (se instalan al abrir el proyecto, ver `Packages/manifest.json`): URP, Input System y uGUI.
- **Git LFS**, para modelos, texturas y audio (`git lfs install` una vez por equipo).

## Abrir y jugar el prototipo

1. En Unity Hub: **Add → Add project from disk** y elige esta carpeta.
2. La primera vez Unity importa los paquetes. Si pregunta por el nuevo sistema de entrada, acepta: reinicia el editor y activa el Input System.
3. En el menú superior: **Kurogane → Crear escena de prueba**. Crea los datos en `Assets/Kurogane/Data` y la escena `Assets/Kurogane/Scenes/Prototipo.unity`.
4. Pulsa **Play**.

### URP (opcional en la Fase 1)

La escena funciona sin configurar nada. Para usar URP desde ya:

1. **Assets → Create → Rendering → URP Asset (with Universal Renderer)**.
2. Asígnalo en **Project Settings → Graphics → Default Render Pipeline** y en **Project Settings → Quality**.

Los materiales generados por código detectan solos si URP está activo.

## Controles

| Acción | Teclado | Mando |
|---|---|---|
| Gas | ↑ / W | Gatillo derecho |
| Freno | ↓ / S | Gatillo izquierdo |
| Volante | ← → / A D | Stick izquierdo |
| Freno de mano | Espacio | A / Cruz |
| Marchas (manual) | Q / E | LB / RB |
| Cámara | C | X / Cuadrado |
| Volver a la pista | R | Y / Triángulo |
| Pausa | Esc / P | Start |
| Otra bajada / cambiar coche (al terminar) | Enter / Tab | A / Select |

La caja manual se activa en el componente **RaceManager → Manual Gearbox**. El coche, el modo (Batalla o Contrarreloj) y la dificultad del rival también se eligen ahí.

## Estructura

```
Assets/Kurogane/
  Scripts/
    Track/    TrackDefinition (trazado editable), TrackData (cálculo de la pista y el terreno), TrackBuilder (mallas)
    Cars/     CarSpec, CarController (física arcade), RivalAI, CarModelBuilder, CarVisual
    Camera/   ChaseCamera
    Race/     RaceManager (cuenta atrás, puntos, vaso, sectores, récords, HUD provisional)
    Controls/ DriverInput (teclado y mando; táctil en la Fase 5)
    Core/     MaterialFactory, ProceduralTextures, MeshKit
  Editor/     Menú "Kurogane"
  Data/       Assets de pista y coches (los genera el menú)
  Scenes/     Escenas
```

La física, el rival y el cálculo de la pista son un port directo del prototipo web, con los mismos números. Convención de ejes: rumbo 0 = +Z, positivo = giro a la derecha, y el lateral positivo es la derecha.

## Hoja de ruta

| Fase | Contenido | Estado |
|---|---|---|
| 0 | Proyecto, repositorio, LFS, paquetes | ✅ |
| 1 | Pista generada, física del coche, cámara, rival, flujo de carrera, HUD provisional | 🧪 en prueba |
| 2 | Pulido del juego: menú de coche y opciones, caja manual en pantalla, nota final S–C | ⏳ |
| 3 | Arte: coches modelados en Blender, texturas a mano, público, señales, lightmaps, mallas horneadas | ⏳ |
| 4 | Filtro PS2 (estela entre cuadros, brillo, tramado, color de 16 bits) y audio (motor por coche, turbo, eurobeat) | ⏳ |
| 5 | Interfaz definitiva, menú garaje 3D, controles táctiles, opciones de PC | ⏳ |
| 6 | Optimización: 60 fps en Android de gama media, LOD, instancing | ⏳ |
| 7 | Compilaciones: AAB firmado para Google Play y Windows x64 | ⏳ |

## Antes de compilar para Android o PC

Los materiales de la Fase 1 se crean con `Shader.Find`. En una build solo existen los shaders que algo referencia. Hasta la Fase 3, que hornea materiales, añade `Universal Render Pipeline/Lit` y `Universal Render Pipeline/Unlit` en **Project Settings → Graphics → Always Included Shaders**.

## Nombres y derechos

Kurogane Touge es una obra original inspirada en el género *touge*. No usa nombres, personajes ni logos de ninguna serie. Los coches son genéricos: antes de publicarlo en tiendas, revisa que sus diseños, matrículas y rótulos no copien modelos o marcas reales.
