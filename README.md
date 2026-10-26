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
3. Abre `Assets/Kurogane/Scenes/Inicio.unity` (**Kurogane → Abrir escena Inicio**; si no existe, la crea).
4. Pulsa **Play**: en el garaje eliges pista, sentido, coche y opciones, y **Empezar** carga la pista con una pantalla de carga.

También se puede pulsar Play directamente en la escena de una pista (`Pista_<id>.unity`): sale el menú de siempre en la salida, útil para probar.

### Pistas

Cada pista es un asset `TrackDefinition` (nombre, nombre en japonés, descripción, ambiente y lista de tramos) con su escena `Pista_<id>.unity`. La lista del menú está en `Assets/Kurogane/Resources/Pistas.asset`.

- **Tramos**: recta, curva, horquilla y ese (dos curvas encadenadas). Cada tramo puede llevar su propio medio ancho y su pendiente, ir en túnel o ser un puente (sin terreno debajo y con barandilla a los dos lados).
- **Ambiente** (`EnvironmentTheme`): vegetación (cedros, hoja ancha o arces de otoño), muros (hormigón, roca o piedra), densidad de faroles (0 = solo los faros), color del guardarraíl, cordilleras y ciudad lejana del horizonte, pueblo del pie y clima de la opción **Según la pista**.
- **Subida**: la misma carretera al revés. Sale de la pancarta de meta de bajada, encima del pueblo, y llega unos 140 m antes de la cima, donde está la pancarta de meta de subida (solo se ve subiendo). El rival acelera menos y puede frenar más tarde. Los récords van por pista, sentido, modo y coche; los de la bajada del Monte Kurogane conservan su archivo de antes.
- **Kurogane → Pistas** abre la herramienta: mapa desde arriba, perfil de altitud, ficha y avisos si la pista se cruza consigo misma o si dos tramos pasan a menos de 4 m entre bordes. **Crear escena y hornear** crea `Pista_<id>.unity`, la añade al catálogo y a la lista de compilación, y hornea la geometría y las luces.

Para crear una pista: **Assets → Create → Kurogane → Track Definition** (o **Nueva pista** en la herramienta), rellena los tramos, revísala en la ventana y pulsa **Crear escena y hornear**.

Cada pista ajusta también el perfil del rival (aceleración lateral en curva y velocidad máxima) y su tiempo de referencia de la nota, y puede llevar decorado propio (`landmarks`, por ahora el viaducto de ladrillo).

| Pista | Carácter | Datos |
|---|---|---|
| **Monte Kurogane (黒鉄峠)** | Rápida y variada: horquillas con canaleta, túnel y el pueblo de la tienda de tofu | 2,3 km · 14 curvas · 4 horquillas · 1 túnel · referencia 120 s (subida 117 s) |
| **Usuba-tōge (薄葉峠)** | Carretera vieja muy estrecha (medio ancho 3,5 m) sin faroles: curvas cortas sin respiro entre bosque cerrado de hoja ancha y cedros, muros de piedra y un viaducto de ladrillo de cuatro arcos al otro lado del valle. Al pie, una aldea con su casa de té. Clima de la pista: niebla | 2,9 km · 53 curvas · 4 horquillas · 177 m de desnivel · referencia 187 s (subida 183 s) |

Los tiempos de referencia salen del rival en Normal con el Coupé: la proporción entre su tiempo en cada pista y en el Monte Kurogane.

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
- **Música**: el juego no trae pistas. Pon las tuyas (con licencia) en `Assets/Kurogane/Audio/Musica` y vuelve a crear las escenas (**Kurogane → Crear escena Inicio** y **Crear escena y hornear** en **Kurogane → Pistas**).

Las escenas creadas por el menú ya traen el filtro, el sonido y la música.

### Cielo y horizonte

`SkyDome` (objeto **Cielo**) dibuja el fondo lejano: cúpula con degradado, unas 800 estrellas, luna con halo, dos anillos de cordilleras, unas 1.800 luces de ciudad en el valle (hacia donde baja la carretera) y bruma baja. Va en un grupo que sigue a la cámara, dentro de una esfera menor que su plano lejano, con un shader sin niebla que se dibuja antes que todo lo demás (`Shaders/Sky.shader`). El horizonte usa el color de la niebla, igual que el fondo de la cámara, para que no se note la unión. Se hornea junto con la pista; las escenas antiguas sin cielo reciben uno al arrancar.

### Tráfico

- **Opción Tráfico** del menú: Auto (apagado en Batalla, encendido en Contrarreloj), Sí o No.
- **Montaña:** coches que suben en sentido contrario por su carril (se circula por la izquierda: para el jugador que baja es la derecha, a +2,6 m), a velocidad baja y frenando en las horquillas. Aparecen a 300-400 m por delante, fuera de la vista, y desaparecen detrás del jugador; como mucho dos a la vez (`TrafficManager`, `TrafficCar`).
- **Contacto:** el jugador choca con el tráfico igual que con el rival (`ICarObstacle`): rebote, agua del vaso y temblor de cámara. El rival no choca con el tráfico.
- **Pueblo:** coches aparcados en las transversales, el konbini y la gasolinera; tres coches lentos por la calle mayor que se paran en el paso a nivel. Cada 50 s el paso se cierra 14 s (luces rojas alternas y barreras), salvo mientras el jugador cruza el pueblo en la llegada (`CrossingController`).
- **Horquillas:** un coche aparcado con los intermitentes de emergencia junto al público.
- **Modelos** (`TrafficModels`): camioneta kei, furgoneta de reparto, taxi y camión pequeño, una sola malla por coche y materiales compartidos.

### Clima y hora del día

Opción **Clima** del menú: Despejado, Amanecer, Atardecer, Niebla, Lluvia o Al azar (cambia en cada bajada). Cada perfil (`WeatherProfile`, assets en `Data/Clima` que se pueden retocar) fija niebla, cielo, luz ambiente, luna o sol, faroles, faros, lluvia, asfalto mojado y agarre; `WeatherSystem` lo aplica.

| Perfil | Visual | Física |
|---|---|---|
| Despejado | Medianoche con el cielo estrellado | Normal |
| Amanecer | Niebla lila, horizonte naranja, faroles al 35 % | Normal |
| Atardecer | Paleta cálida, sol bajo | Normal |
| Niebla | Niebla densa (fin a 90 m), conos de luz de los faros | Normal |
| Lluvia | Lluvia alrededor de la cámara, gotas en pantalla (con el filtro PS2), asfalto mojado con charcos, salpicaduras, limpiaparabrisas en la vista de capó, sonido de lluvia y relámpagos | Agarre ×0,8, derrape ×0,85 |

El único cambio de física es `CarController.GripScale` / `DriftGripScale`, que fija el clima; el rival escala su velocidad en curva por la raíz del agarre. Los reflejos del asfalto mojado salen de sondas de reflexión que se crean con **Hornear luces**. Los colores y el mojado se aplican con `MaterialPropertyBlock`, así que no modifican los materiales horneados.

### Pueblo de la meta y llegada

- **La carretera sigue tras la meta** unos 300 m hasta un pueblo (tramos `afterGoal` de `TrackDefinition`; los trazados guardados sin ellos los reciben solos). La meta, los sectores, la pancarta GOAL y la llegada del rival se calculan sobre el tramo cronometrado (`TrackData.FinishIndex` y `TimedLength`), que no cambia: **los récords guardados siguen valiendo**.
- **Llegada:** al cruzar la meta el coche sigue solo (`CarController.Autopilot`, sin tocar la física) por el carril izquierdo a unos 40 km/h y frena frente a la tienda de tofu, mientras la cámara corta entre tres planos fijos (lateral bajo, picado desde un poste y frontal). Luego salen los resultados. **Confirmar** la salta. El rival sigue despacio y aparca a la derecha.
- **Pueblo** (`Scripts/City`: `CityBuilder`, `CityStyle`, `CityProps`): calle mayor con aceras, dos transversales, paso a nivel, casas de dos pisos con tejado de kawara, balcones y aire acondicionado, tiendas con persiana y rótulo, konbini iluminado, gasolinera, máquinas expendedoras, cabina, postes eléctricos con cables y farolas, semáforo en ámbar intermitente, arrozales, cedros y la tienda de tofu 豆腐店 黒鉄. Casi todo usa un único material con el atlas del pueblo, se combina por celdas y entra en **Hornear pista** y **Hornear luces**. Los nombres de las tiendas son inventados y se cambian en el asset `Data/PuebloMeta.asset`.
- **Ciudad del valle:** bloques con ventanas encendidas entre 300 m y 1,5 km, sin niebla; la cámara alarga su plano lejano solo al acercarse a la meta. Más lejos, una silueta de ciudad dentro del `SkyDome`.

### Ambiente y horneado

- **Señales** (por código, en pixel art): chevrones en el exterior de cada horquilla, aviso de curva cerrada 70 m antes, límite de 40 en la salida y al salir del túnel, postes kilométricos cada 500 m y rótulo sobre la boca del túnel. Brillan un poco, como el retrorreflectante con los faros.
- **Público** en las explanadas de las horquillas y junto a la salida, con flashes de cámara (`CrowdFlashes`). Cantidades en **TrackBuilder → Ambiente**.
- **Kurogane → Hornear pista** guarda la geometría en la escena y sus mallas, texturas y materiales en `Assets/Kurogane/Baked`. Al pulsar Play ya no se genera nada y los materiales entran en la build sin depender de `Shader.Find`.
- **Kurogane → Hornear luces (lightmaps)** (después de hornear la pista) pasa los faroles y las luces del túnel a lightmaps y añade sondas de luz para los coches. En ejecución esas luces ya no cuestan nada; los faros de los coches y la luna siguen en tiempo real. Tarda unos minutos.
- **Kurogane → Quitar horneado** vuelve a la pista generada al arrancar. Si cambias el trazado o los ajustes de `TrackBuilder`, vuelve a hornear.
- Lo horneado se sube con Git LFS (ver `.gitattributes`).

### Rendimiento

- La pista se construye en tramos de 80 m, y los árboles, postes y faroles se agrupan en celdas de 60 m, para que Unity descarte lo que no está en cámara.
- La cámara no dibuja más allá del final de la niebla (unos 270 m).
- Postes, catadióptricos, ojos de gato y las luces de faroles y túnel se apagan por distancia (`DetailCulling` en la pista; distancias en **TrackBuilder → Rendimiento**).
- Cada coche fusiona sus piezas en una malla por material, y al cambiar de coche se liberan sus mallas y materiales.
- El HUD solo rehace un texto cuando cambia el número que muestra.
- En Android: 60 fps fijos, *frame pacing* activado, sin HDR en el perfil de URP para móvil y filtro PS2 con texturas de 8 bits.
- **Opciones → Ver FPS** muestra los fps y los milisegundos por cuadro. Para medir en un móvil, haz una build de desarrollo y usa también el Profiler de Unity conectado al dispositivo.
- El cielo se dibuja después de lo opaco con prueba de profundidad (solo rellena lo que nada tapa). Cerca del pueblo la cámara alarga el plano lejano, pero con `layerCullDistances` solo llegan lejos la ciudad del valle (capa 30) y el cielo (capa 31).
- En el perfil de URP para móvil: sin sombras y con las luces adicionales por vértice. Con el filtro PS2 apagado, en móvil se sigue dibujando a 540 líneas (sin efectos).

#### Benchmark en Android

Con el APK de desarrollo (**Kurogane → Compilar → Android APK de desarrollo**) instalado, el juego recorre solo ocho escenas de 20 s y escribe los resultados en el log:

```powershell
adb shell am start -n com.bandas6.kuroganetouge/com.unity3d.player.UnityPlayerGameActivity -e unity "-kuroganeBench"
adb logcat -s Unity | Select-String KuroganeBench
```

Arranca en Inicio y carga la primera pista del catálogo, de bajada. Para otra pista, `-kuroganeBench=Usuba` o el id de la pista escrito en `benchmark.txt` (en `persistentDataPath`). Mientras mide no se guardan récords.

Medido en un Redmi Note 11 (Snapdragon 680, Adreno 610, Vulkan, 2400×1080), con el filtro PS2:

| Escena | Monte Kurogane | Usuba |
|---|---|---|
| Montaña | 59,9 fps · GPU 13,0 ms | 59,9 fps · GPU 12,9 ms |
| Montaña sin cielo | 59,9 · 12,6 ms (el cielo cuesta 0,4 ms) | 59,9 · 12,6 ms |
| Montaña sin filtro PS2 | 59,9 · 12,8 ms | 59,9 · 12,8 ms |
| Lluvia | 59,9 · 13,4 ms | 59,9 · 13,1 ms |
| Lluvia sin partículas | 59,9 · 12,9 ms (la lluvia cuesta 0,45 ms) | 59,9 · — |
| Niebla | 59,9 · 12,6 ms | 59,8 · 13,0 ms |
| Tráfico | 59,9 · 13,5 ms | 59,9 · 13,9 ms |
| Llegada al pueblo | 59,9 · 13,2 ms | 58,7 · 13,3 ms |

En el Monte Kurogane ningún cuadro pasó de 16,8 ms. En Usuba hubo un cuadro suelto de 50 ms en la niebla y otro de 67 ms en la llegada; el p95 sigue en 16,8 ms. (En una escena de Usuba el contador de GPU devolvió un valor inválido.)

## Estructura

```
Assets/Kurogane/
  Scripts/
    Track/    TrackDefinition (trazado y ficha), EnvironmentTheme (ambiente), TrackCatalog (lista de pistas), TrackData (pista
              y terreno, también de subida), TrackBuilder (mallas, señales y público), TrackPreview (mapa, perfil y avisos), CrowdFlashes
    Cars/     CarSpec, CarController (física arcade), RivalAI, CarModelBuilder, CarVisual,
              TrafficModels, TrafficCar, TrafficManager, ICarObstacle
    Camera/   ChaseCamera (persecución y cámara de garaje)
    Race/     RaceManager (cuenta atrás, puntos, vaso, sectores, récords),
              RaceManager.Menu (menú, opciones, pausa, nota final), RaceManager.Arrival (llegada al pueblo),
              RaceManager.Tracks (pista y sentido), RaceSettings (opciones guardadas), GameSession y SceneLoader
              (cambio de escena con pantalla de carga), Garage (garaje de la escena Inicio)
    Hud/      HudView (HUD y caja de cambios), MenuView (menús, pausa, resultados), TouchControls, UiKit
    Controls/ DriverInput (teclado, mando y táctil)
    Core/     MaterialFactory, ProceduralTextures, MeshKit, DetailCulling
    Environment/ SkyDome (cielo, luna, cordilleras y luces del valle), WeatherProfile, WeatherSystem,
              RainFx, RainAudio, WheelSpray (namespace Kurogane.Atmosphere)
    City/     CityBuilder (pueblo de la meta y ciudad del valle), CityStyle, Blinker, CrossingController
    Retro/    PS2Filter (imagen a baja resolución, estela, brillo, tramado, 16 bits)
    Sound/    EngineSynth (motor por código), CarAudio, MusicPlayer
  Shaders/    PS2Filter.shader, Sky.shader
  Audio/      Musica/ (pistas propias, ver LEEME.txt)
  Editor/     Menú "Kurogane": escenas (KuroganeSceneSetup), herramienta de pistas (KuroganeTrackTools),
              hornear (KuroganeBake) y compilar (KuroganeBuild)
  Baked/      Pista horneada e iluminación (los genera el menú)
  Data/       Assets de pista y coches (los genera el menú)
  Scenes/     Inicio (menú y garaje) y Pista_<id> (una por pista)
  Resources/  Pistas (catálogo de pistas del menú)
```

La física, el rival y el cálculo de la pista son un port directo del prototipo web, con los mismos números. Convención de ejes: rumbo 0 = +Z, positivo = giro a la derecha, y el lateral positivo es la derecha.

## Hoja de ruta

| Fase | Contenido | Estado |
|---|---|---|
| 0 | Proyecto, repositorio, LFS, paquetes | ✅ |
| 1 | Pista generada, física del coche, cámara, rival, flujo de carrera, HUD provisional | ✅ |
| 2 | Pulido del juego: menú de coche y opciones, caja manual en pantalla, nota final S–C | ✅ |
| 3 | Arte: coches modelados en Blender, texturas a mano, público, señales, lightmaps, mallas horneadas | 🧪 en prueba: público, señales, lightmaps y horneado hechos por código; faltan los coches de Blender y las texturas a mano |
| 4 | Filtro PS2 (estela entre cuadros, brillo, tramado, color de 16 bits) y audio (motor por coche, turbo, eurobeat) | ✅ (la música la pones tú) |
| 5 | Interfaz definitiva, menú garaje 3D, controles táctiles, opciones de PC | ✅ |
| 6 | Optimización: 60 fps en Android de gama media, LOD, instancing | ✅ (falta medir en un Android real) |
| 7 | Compilaciones: AAB firmado para Google Play y Windows x64 | ✅ |
| E | Varias pistas: escena Inicio, una escena por pista, subida, ambiente por pista y herramienta de pistas (`docs/plan-pistas-modo-libre.md`) | 🧪 en revisión |
| F | Pistas nuevas: Usuba (hecha); Akane, Myōseki e Irozaka | 🧪 Usuba en revisión |

## Compilar

Menú **Kurogane → Compilar** (las compilaciones van a `Builds/`, que no se sube al repositorio):

| Opción | Resultado |
|---|---|
| Windows x64 | `Builds/Windows/Kurogane Touge.exe` (Mono) |
| Android AAB (Google Play) | `Builds/Android/KuroganeTouge-<versión>-<código>.aab`, firmado con tu keystore |
| Android APK de desarrollo (Profiler) | APK con firma de depuración que se instala en el móvil conectado por USB y se conecta al Profiler |

Cada opción deja configurado el proyecto: nombre, versión, identificador `com.bandas6.kuroganetouge`, solo horizontal, y en Android IL2CPP, ARM64 y Android 8.0 (API 26) como mínimo. También añade a **Always Included Shaders** los shaders que el juego busca por nombre (`Shader.Find`), que si no faltarían en la build. El AAB sube solo el código de versión de Android en cada compilación.

Antes de la primera subida a Google Play, revisa el identificador y la versión en `Assets/Kurogane/Editor/KuroganeBuild.cs`: el identificador no se puede cambiar después.

**Módulos de Unity**: la compilación para Windows (Mono) viene con el editor de Windows. Para Android, en Unity Hub → Installs → tu versión → Add modules, instala *Android Build Support* con *OpenJDK* y *Android SDK & NDK Tools*.

### Firma de Android

Google Play exige un AAB firmado con tu clave de subida. La clave y sus contraseñas nunca van al repositorio (`*.keystore` y `*.jks` están en `.gitignore`).

1. Crea la clave una sola vez con el `keytool` del JDK que trae Unity, y guárdala **fuera** de la carpeta del proyecto:

   ```powershell
   & "C:\Program Files\Unity\Hub\Editor\<versión>\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe" `
     -genkeypair -v -keystore "$HOME\claves\kurogane.keystore" -alias kurogane -keyalg RSA -keysize 2048 -validity 10000
   ```

2. Haz una copia de seguridad de ese archivo y de sus contraseñas. Con *Play App Signing* Google guarda la clave final, pero sin la de subida no podrás publicar actualizaciones hasta pedir que la restablezcan.
3. Antes de abrir Unity, define las variables de entorno en la misma consola (o como variables de usuario de Windows):

   ```powershell
   $env:KUROGANE_KEYSTORE = "$HOME\claves\kurogane.keystore"
   $env:KUROGANE_KEYSTORE_PASS = "<contraseña del keystore>"
   $env:KUROGANE_KEY_ALIAS = "kurogane"
   $env:KUROGANE_KEY_PASS = "<contraseña de la clave>"   # si no la defines, se usa la del keystore
   ```

4. **Kurogane → Compilar → Android AAB (Google Play)**. Al terminar, la ruta del keystore se borra de ProjectSettings para que no se suba al repositorio.

Sin abrir el editor (con Unity cerrado):

```powershell
& "<ruta de Unity>\Editor\Unity.exe" -batchmode -quit -projectPath . -logFile Builds\android.log `
  -executeMethod Kurogane.EditorTools.KuroganeBuild.BuildAndroidRelease
```

(`BuildWindows` y `BuildAndroidDevelopment` funcionan igual.)

## Nombres y derechos

Kurogane Touge es una obra original inspirada en el género *touge*. No usa nombres, personajes ni logos de ninguna serie. Los coches son genéricos: antes de publicarlo en tiendas, revisa que sus diseños, matrículas y rótulos no copien modelos o marcas reales.
