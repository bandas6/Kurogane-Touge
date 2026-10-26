# Plan: horizonte, pueblo de la meta, clima y tráfico

Plan de trabajo para después de las fases 1 a 7. Cada bloque va en su propia rama, con un PR a `main`, igual que las fases anteriores.

## Por qué el final de la meta se ve negro

Son tres causas que se suman:

1. **La carretera termina en la meta** (`player.Index >= track.Data.N - 5` en `RaceManager`). Más allá no hay geometría.
2. **No hay cielo.** `KuroganeSceneSetup` deja `RenderSettings.skybox = null` y la cámara pinta un color sólido. El prototipo web tenía cielo degradado, estrellas, luna, montañas lejanas y luces de ciudad en el valle; eso no se portó.
3. **El plano lejano de la cámara se corta en la niebla.** `ChaseCamera.Awake` limita `farClipPlane` a `fogEndDistance + 30` (unos 270 m). Cualquier cosa más lejos no se dibuja, aunque su material ignore la niebla.

## Orden

| # | Rama | Resultado | Arregla el negro |
|---|---|---|---|
| A | `feat/cielo-horizonte` | Cielo, estrellas, luna, cordilleras y luces del valle | Sí, en toda la pista |
| B | `feat/pueblo-meta` | La carretera sigue tras la meta hasta un pueblo, con escena de llegada | Sí, en la meta |
| C | `feat/clima` | Hora del día, niebla, lluvia y agarre en mojado | — |
| D | `feat/trafico` | Coches en el pueblo y tráfico opcional en la montaña | — |

A es pequeño y elimina el negro en todas partes. B depende de A porque el pueblo usa las mismas luces lejanas. C y D son independientes entre sí.

## Reglas para todos los bloques

- Seguir `CLAUDE.md`: rama propia, Conventional Commits en español y nada de commits directos a `main`.
- **Horneado:** toda geometría nueva tiene que entrar en **Kurogane → Hornear pista** (`KuroganeBake`) y respetar el troceado (`chunkPoints`, `cellSize`). Los detalles pequeños se registran en `DetailCulling`.
- **Luces:** las luces fijas nuevas (faroles del pueblo, konbini) deben poder pasar por **Hornear luces**. Solo los faros de los coches quedan en tiempo real.
- **Opciones nuevas del menú:** se guardan en `RaceSettings` (PlayerPrefs) y se muestran en `RaceManager.Menu.cs` / `MenuView`.
- **Física:** la de `CarController` no se toca salvo lo indicado en C.
- **Filtro PS2:** comprobar cada bloque con el filtro (`PS2Filter`) encendido y apagado.
- **Rendimiento:** apuntar a 60 fps en Android de gama media. Medir con **Kurogane → Compilar → Android APK de desarrollo** y el Profiler antes de cerrar cada PR.
- **Derechos:** nada de logos, carteles o edificios propios de ninguna serie. Los nombres de tiendas son inventados. La referencia visual son lugares reales de Gunma, como Shibukawa e Ikaho al pie del Monte Haruna.

---

## A · Cielo y horizonte (`feat/cielo-horizonte`)

**Idea:** el fondo lejano no necesita perspectiva real. Va en un grupo que sigue a la cámara (como en el prototipo web), dentro de una esfera de radio menor que el plano lejano (unos 240 m). Usa materiales unlit sin niebla y se dibuja primero (cola de render `Background`). Así no hace falta ampliar el `farClipPlane` ni usar varias cámaras.

**Archivos nuevos:** `Scripts/Environment/SkyDome.cs`, y quizás `Shaders/SkyGradient.shader` si el unlit de URP con color por vértice no alcanza.

**Contenido:**
- Cúpula con degradado por vértice: horizonte azul violáceo y cenit casi negro.
- Unas 800 estrellas (puntos o quads pequeños).
- Luna con halo.
- Dos anillos de cordilleras low-poly, oscuras y sin niebla.
- **Luces del valle:** unas 1.800 luces de ciudad cálidas y algunas frías, bajo el nivel de la carretera, en la dirección del valle. Si el valle cae fuera de la esfera, se proyectan sobre ella.
- Bancos de bruma baja sobre el valle con sprites aditivos muy tenues.

**Integración:**
- `KuroganeSceneSetup` crea el `SkyDome`, y `ChaseCamera` (o el propio `SkyDome` en `LateUpdate`) lo mueve con la cámara.
- La cámara de garaje (`showroom`) también lo ve.
- El color de fondo de la cámara y el de la niebla deben coincidir con el horizonte de la cúpula, para que no se note la unión.

**Listo cuando:**
- Desde cualquier punto de la pista y en las tres vistas de cámara no queda ninguna zona negra lisa.
- Se ven el cielo, la luna y las luces del valle.
- El coste es menor de 0,5 ms en el Profiler de Android.

---

## B · Pueblo de la meta (`feat/pueblo-meta`)

### B1. La carretera sigue después de la meta
- `TrackSegment` gana un campo `afterGoal` (bool). `TrackDefinition.DefaultSegments()` añade unos 300 m de bajada suave al final: por ejemplo una curva de 25° de 60 m y rectas de 120 m y 120 m, con pendiente 0,03.
- `TrackData` expone `FinishIndex`, que es el último punto antes del primer tramo `afterGoal`. `Sectors[2]` y la pancarta de GOAL usan `FinishIndex`.
- `RaceManager` termina la carrera en `FinishIndex`, no en `N - 5`. `RivalAI` también usa `FinishIndex` para dar el tiempo de llegada y luego sigue rodando despacio.
- Los récords guardados siguen siendo válidos, porque el tramo cronometrado no cambia.

### B2. Llegada
- Al cruzar la meta, el jugador pasa a **piloto automático**: sigue la trazada como `RivalAI` hasta una velocidad de unos 40 km/h y frena frente a la tienda de tofu.
- **Cámara de llegada:** dos o tres planos fijos (lateral bajo, picado desde un poste, frontal) con cortes y el filtro PS2. Después aparece la pantalla de resultados, que ya existe.
- Se puede saltar con Confirmar.

### B3. Pueblo cercano (0–300 m) con geometría real
**Archivos nuevos:** `Scripts/City/CityBuilder.cs`, `Scripts/City/CityStyle.cs` (ScriptableObject con paleta, densidad y semilla) y `Scripts/City/CityProps.cs` (piezas).

- **Calles:** cuadrícula pequeña junto al final de la carretera: la calle principal y 2 o 3 transversales, con aceras y bordillos.
- **Casas:** de dos pisos con tejado de teja japonesa (kawara) a dos aguas o a cuatro, balcones con barandilla, aire acondicionado exterior y ventanas con luz cálida aleatoria.
- **Tiendas:** con persiana metálica bajada y algún cartel luminoso inventado.
- **Konbini iluminado:** fachada blanca, franjas de color propias, interior con luz fría y estanterías, y luz derramada sobre la acera.
- **Otros puntos de luz:** gasolinera con marquesina, grupos de máquinas expendedoras (ya existe `vending` en el prototipo web) y cabina de teléfono.
- **Postes eléctricos (電柱):** de hormigón cada unos 30 m, con travesaños, transformadores y varios cables que cuelgan en catenaria. Los cables se hacen con tiras finas generadas por código.
- **Señalización:** semáforo en ámbar intermitente, paso a nivel con barreras, luces rojas alternas y una vía que cruza.
- **Alrededores:** arrozales a los lados con un poco de agua reflejando el cielo, y una fila de cedros detrás.
- **La tienda de tofu (destino del reparto):** casa-tienda de madera con cortina noren y un nombre inventado, como 豆腐店 黒鉄 (el del coche).

### B4. Ciudad media y lejana
- **Media (300 m – 1,5 km):** bloques de edificios con ventanas emisivas en atlas, combinados en pocas mallas. Van fuera de la niebla de la carretera con un material de niebla más lejana o sin niebla.
- **Lejana:** silueta de la ciudad recortada en una tarjeta 2D dentro del `SkyDome`, más las luces del valle del bloque A.

**Listo cuando:**
- Al cruzar la meta el coche entra solo al pueblo y la cámara muestra la llegada.
- No hay negro detrás de la meta.
- El pueblo entra en el horneado y en el troceado de la pista.
- En Android se mantienen 60 fps al entrar al pueblo.

---

## C · Clima y hora del día (`feat/clima`)

**Archivos nuevos:** `Scripts/Environment/WeatherProfile.cs` (ScriptableObject), `Scripts/Environment/WeatherSystem.cs` y los perfiles en `Assets/Kurogane/Data/Clima/`.

**Perfil de clima:** color y distancias de niebla, colores del cielo para el `SkyDome`, luz ambiente, luna o sol, intensidad de faroles y faros, densidad de lluvia, nivel de mojado (0–1) y multiplicador de agarre.

| Perfil | Visual | Física |
|---|---|---|
| Despejado (medianoche) | Lo actual más el cielo de A | 1,0 |
| Amanecer | Paleta del prototipo web (niebla lila, horizonte naranja, faroles al 35 %) | 1,0 |
| Atardecer | Paleta nueva, cálida | 1,0 |
| Niebla | Niebla densa (fin a unos 90 m), bancos de niebla en las hondonadas y conos de luz de los faros visibles | 1,0 |
| Lluvia | Ver abajo | Agarre ×0,8, derrape ×0,85 |

**Lluvia:**
- **Partículas** alrededor de la cámara con el sistema de partículas normal, con un tope de 1.500 en Android, en gotas alargadas según la velocidad.
- **Gotas en pantalla:** un parámetro nuevo en el shader `PS2Filter` (distorsión con un mapa de gotas que se desliza), sin un pase de postproceso extra.
- **Asfalto mojado:** más oscuro y con más smoothness por propiedad global del shader o por cambio de material. Reflejos con reflection probes horneadas, sin SSR. Charcos con una máscara de ruido en la textura del asfalto.
- **Ruedas:** el agua que levantan sustituye al humo de derrape en mojado.
- **Vista de capó:** limpiaparabrisas animados.
- **Sonido:** lluvia como ruido filtrado en `Sound/`, más fuerte dentro del coche en la vista de capó y apagada dentro del túnel.
- **Opcional:** relámpagos lejanos que iluminan las cordilleras un instante.

**Física (único cambio en `CarController`):** una propiedad `GripScale`, que fija `WeatherSystem`, multiplica `spec.grip` y `spec.driftGrip`. El resto de la física no cambia. En lluvia, `RivalAI` escala también `VMax` por la raíz del agarre, para que el rival no vaya imposible de rápido.

**Menú:** una opción **Clima** (Despejado, Amanecer, Atardecer, Niebla, Lluvia, Al azar) guardada en `RaceSettings`.

**Listo cuando:**
- Cada perfil se puede elegir en el menú y se ve distinto.
- En lluvia el coche desliza más pero se puede controlar.
- La lluvia cuesta menos de 1,5 ms en Android.

---

## D · Tráfico (`feat/trafico`)

**Archivos nuevos:** `Scripts/Cars/TrafficCar.cs` y `Scripts/Cars/TrafficManager.cs`.

**Modelos nuevos en `CarModelBuilder` (solo decorativos):** camioneta kei, furgoneta de reparto, taxi y camión pequeño. Se combinan en una sola malla por coche, como ya hace la fase 6.

**Tráfico en la montaña:** opción **Tráfico**, apagada por defecto en Batalla.
- Coches que **suben** en sentido contrario por su carril. En Japón se circula por la izquierda: el que sube va por la izquierda de su sentido, que es la derecha (lateral positivo) para el jugador que baja.
- Siguen la línea central con un desplazamiento fijo de unos +2,6 m y una velocidad baja.
- Aparecen fuera de la vista y desaparecen detrás del jugador. Como máximo 2 a la vez.

**Contacto:** generalizar el contacto con el rival de `CarController` a una lista de obstáculos (rival y tráfico), con la misma respuesta: rebote, pérdida de agua del vaso y temblor de cámara.

**Pueblo:**
- Coches estacionados en las calles y la gasolinera.
- Dos o tres coches en movimiento por rutas fijas a velocidad baja, que se paran en el paso a nivel cuando baja la barrera.

**Horquillas:** los coches estacionados con luces de emergencia que ya existen pasan a usar estos modelos.

**Listo cuando:**
- Con Tráfico activado aparece algún coche subiendo en cada bajada y el contacto funciona.
- Con Tráfico apagado no aparece ninguno.
- El pueblo se ve con vida en la escena de llegada.

---

## Después (no planificado aún)

- Nieve, con agarre ×0,6 y marcas en la calzada.
- Una segunda montaña o el sentido de subida.
- Repetición de la bajada desde la escena de llegada.
