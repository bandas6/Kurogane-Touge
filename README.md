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
| Moverse por los menús | ↑ ↓ ← → / W A S D, Enter, Esc | Cruceta, A, Start |

Al pulsar Play aparece el **garaje**: la cámara gira alrededor del coche (arrástrala con el ratón o el dedo) y a la izquierda está el menú. La página principal elige coche, modo (Batalla o Contrarreloj), dificultad del rival y caja (automática o manual). **Opciones** abre la segunda página: unidades de velocidad, filtro PS2, volúmenes de sonido y música, controles táctiles, un contador de FPS y, en PC, resolución, pantalla completa o ventana, VSync y calidad. Lo elegido se guarda entre sesiones. Para saltarte el menú, desactiva **RaceManager → Show Menu On Start** y se usan los valores del inspector.

Toda la interfaz es uGUI generada por código (`Kurogane.Hud`) y funciona con teclado, mando, ratón y pantalla táctil.

### Controles táctiles

Salen solos en móviles y pantallas táctiles (opción **Táctil**: Auto, Sí o No). Volante ◀ ▶ abajo a la izquierda; gas, freno y freno de mano abajo a la derecha; arriba, volver a la pista, pausa y cámara. Con la caja manual, las marchas se cambian con los botones ▼ / ▲ de la caja del HUD, que con los controles táctiles sube a la esquina superior derecha. En Android el juego se fija a 60 fps.

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

### Filtro PS2 y sonido

- **Filtro PS2** (componente `PS2Filter` en la cámara): la escena se dibuja a 448 líneas, como la PS2 en NTSC, y se le aplica estela entre cuadros, brillo, tramado Bayer y color de 16 bits (5-6-5). Todo se ajusta en el inspector y se apaga desde el menú. Funciona con URP y con el pipeline integrado.
- **Motores por código** (`EngineSynth` + `CarAudio` en cada coche): cada coche tiene su tipo de motor (Panda 4 cilindros, Coupé rotativo, Sedán 6 cilindros), con silbido y descarga del turbo, petardeo al soltar el gas, chirrido de ruedas en el derrape y golpes contra el muro. El rival suena según la distancia y el lado.
- **Música**: el juego no trae pistas. Pon las tuyas (con licencia) en `Assets/Kurogane/Audio/Musica` y vuelve a ejecutar **Kurogane → Crear escena de prueba**.

Si la escena se creó antes de la Fase 4, ejecuta otra vez **Kurogane → Crear escena de prueba** para añadir el filtro, el sonido y la música.

### Rendimiento

- La pista se construye en tramos de 80 m, y los árboles, postes y faroles se agrupan en celdas de 60 m, para que Unity descarte lo que no está en cámara.
- La cámara no dibuja más allá del final de la niebla (unos 270 m).
- Postes, catadióptricos, ojos de gato y las luces de faroles y túnel se apagan por distancia (`DetailCulling` en la pista; distancias en **TrackBuilder → Rendimiento**).
- Cada coche fusiona sus piezas en una malla por material, y al cambiar de coche se liberan sus mallas y materiales.
- El HUD solo rehace un texto cuando cambia el número que muestra.
- En Android: 60 fps fijos, *frame pacing* activado, sin HDR en el perfil de URP para móvil y filtro PS2 con texturas de 8 bits.
- **Opciones → Ver FPS** muestra los fps y los milisegundos por cuadro. Para medir en un móvil, haz una build de desarrollo y usa también el Profiler de Unity conectado al dispositivo.

## Estructura

```
Assets/Kurogane/
  Scripts/
    Track/    TrackDefinition (trazado editable), TrackData (cálculo de la pista y el terreno), TrackBuilder (mallas)
    Cars/     CarSpec, CarController (física arcade), RivalAI, CarModelBuilder, CarVisual
    Camera/   ChaseCamera (persecución y cámara de garaje)
    Race/     RaceManager (cuenta atrás, puntos, vaso, sectores, récords),
              RaceManager.Menu (menú, opciones, pausa, nota final), RaceSettings (opciones guardadas)
    Hud/      HudView (HUD y caja de cambios), MenuView (menús, pausa, resultados), TouchControls, UiKit
    Controls/ DriverInput (teclado, mando y táctil)
    Core/     MaterialFactory, ProceduralTextures, MeshKit, DetailCulling
    Retro/    PS2Filter (imagen a baja resolución, estela, brillo, tramado, 16 bits)
    Sound/    EngineSynth (motor por código), CarAudio, MusicPlayer
  Shaders/    PS2Filter.shader
  Audio/      Musica/ (pistas propias, ver LEEME.txt)
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
| 2 | Pulido del juego: menú de coche y opciones, caja manual en pantalla, nota final S–C | ✅ |
| 3 | Arte: coches modelados en Blender, texturas a mano, público, señales, lightmaps, mallas horneadas | ⏳ |
| 4 | Filtro PS2 (estela entre cuadros, brillo, tramado, color de 16 bits) y audio (motor por coche, turbo, eurobeat) | ✅ (la música la pones tú) |
| 5 | Interfaz definitiva, menú garaje 3D, controles táctiles, opciones de PC | ✅ |
| 6 | Optimización: 60 fps en Android de gama media, LOD, instancing | 🧪 en prueba (falta medir en un Android real) |
| 7 | Compilaciones: AAB firmado para Google Play y Windows x64 | ⏳ |

## Antes de compilar para Android o PC

Los materiales de la Fase 1 se crean con `Shader.Find`. En una build solo existen los shaders que algo referencia. Hasta la Fase 3, que hornea materiales, añade `Universal Render Pipeline/Lit` y `Universal Render Pipeline/Unlit` en **Project Settings → Graphics → Always Included Shaders**.

## Nombres y derechos

Kurogane Touge es una obra original inspirada en el género *touge*. No usa nombres, personajes ni logos de ninguna serie. Los coches son genéricos: antes de publicarlo en tiendas, revisa que sus diseños, matrículas y rótulos no copien modelos o marcas reales.
