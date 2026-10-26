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
| Otra bajada / cambiar coche / menú (al terminar) | Enter / Tab / Esc | A / Select / Start |
| Moverse por los menús | ↑ ↓ ← → / W A S D, Enter | Cruceta, A |

Al pulsar Play aparece el **menú de coche y opciones**: coche, modo (Batalla o Contrarreloj), dificultad del rival, caja (automática o manual) y unidades de velocidad. Lo elegido se guarda entre sesiones. Los menús también funcionan con el ratón, y en la caja manual los botones ▼ / ▲ de la pantalla cambian de marcha. Para saltarte el menú, desactiva **RaceManager → Show Menu On Start** y se usan los valores del inspector.

### Nota final

Al llegar a meta la bajada recibe una nota sobre 100:

| Parte | Puntos | Cómo se gana |
|---|---|---|
| Tiempo | 0–40 | 30 puntos bajando en el tiempo de referencia (**RaceManager → Par Time**, 120 s); cada segundo menos suma 1 |
| Agua | 0–25 | Lo que queda en el vaso |
| Estilo | 0–20 | Puntos de drift (15 al llegar a **Drift Par**) y hasta 5 canaletas |
| Limpieza | 0–15 | 15 menos 3 por golpe |
| Batalla | ±10 | Ganar o perder contra el rival |

**S** desde 90, **A** desde 75, **B** desde 60 y **C** por debajo. El tiempo de referencia y el par de drift son provisionales: ajústalos en el inspector cuando tengas tiempos reales.

## Estructura

```
Assets/Kurogane/
  Scripts/
    Track/    TrackDefinition (trazado editable), TrackData (cálculo de la pista y el terreno), TrackBuilder (mallas)
    Cars/     CarSpec, CarController (física arcade), RivalAI, CarModelBuilder, CarVisual
    Camera/   ChaseCamera
    Race/     RaceManager (cuenta atrás, puntos, vaso, sectores, récords, HUD provisional),
              RaceManager.Menu (menú, pausa, caja en pantalla, nota final), RaceSettings (opciones guardadas)
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
| 1 | Pista generada, física del coche, cámara, rival, flujo de carrera, HUD provisional | ✅ |
| 2 | Pulido del juego: menú de coche y opciones, caja manual en pantalla, nota final S–C | 🧪 en prueba |
| 3 | Arte: coches modelados en Blender, texturas a mano, público, señales, lightmaps, mallas horneadas | ⏳ |
| 4 | Filtro PS2 (estela entre cuadros, brillo, tramado, color de 16 bits) y audio (motor por coche, turbo, eurobeat) | ⏳ |
| 5 | Interfaz definitiva, menú garaje 3D, controles táctiles, opciones de PC | ⏳ |
| 6 | Optimización: 60 fps en Android de gama media, LOD, instancing | ⏳ |
| 7 | Compilaciones: AAB firmado para Google Play y Windows x64 | ⏳ |

## Antes de compilar para Android o PC

Los materiales de la Fase 1 se crean con `Shader.Find`. En una build solo existen los shaders que algo referencia. Hasta la Fase 3, que hornea materiales, añade `Universal Render Pipeline/Lit` y `Universal Render Pipeline/Unlit` en **Project Settings → Graphics → Always Included Shaders**.

## Nombres y derechos

Kurogane Touge es una obra original inspirada en el género *touge*. No usa nombres, personajes ni logos de ninguna serie. Los coches son genéricos: antes de publicarlo en tiendas, revisa que sus diseños, matrículas y rótulos no copien modelos o marcas reales.
