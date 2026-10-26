# Plan: nuevas pistas y modo libre por el pueblo

Este plan va después de los bloques A–D de `plan-ambiente.md`, que ya están en `main`. Igual que antes, cada bloque va en su propia rama con un PR a `main`, sigue `CLAUDE.md` y espera el visto bueno entre uno y otro.

## Punto de partida (estado de `main`)

- **Una sola pista.** `TrackDefinition` es una lista de tramos que se generan cuesta abajo. `TrackData` calcula la línea central, el terreno, `FinishIndex` y `TownStart`. La escena `Prototipo.unity` hornea esa pista concreta.
- **La física está atada a la pista.** `CarController` busca el punto central más cercano (`Track.Nearest`) y limita el lateral contra el borde de la calzada. Funciona en una carretera, pero no en una red de calles con cruces.
- **El pueblo (`CityBuilder`) se coloca a lo largo de la calle mayor**, que es el tramo `afterGoal` de la pista. Las calles transversales son decorado: no se puede entrar en ellas.

Todo lo que sigue sale de esas tres limitaciones.

## Derechos y referencias

Las pistas de la serie están basadas en puertos de montaña reales de Gunma y Tochigi. Los lugares reales se pueden usar como referencia de trazado, paisaje y ambiente: fotos, Street View, mapas de altitud. Lo que no se usa son los nombres inventados por la serie, sus personajes, carteles o tiendas.

Cada pista lleva un **nombre propio inventado**. Algunos detalles son del mundo real y sí se pueden recrear a nuestra manera: los carteles con kana numerados en cada horquilla de Irohazaka y el viaducto de ladrillo de Usui (Megane-bashi).

---

## E · Varias pistas (`feat/multipista`)

Es la base para todo lo demás. No añade pistas nuevas todavía: convierte el Monte Kurogane en "una pista de la lista".

**Datos de cada pista (`TrackDefinition`):**
- Nombre, nombre en japonés, descripción, longitud y número de curvas para el menú, todo calculado.
- `EnvironmentTheme` (ScriptableObject nuevo): vegetación (cedros, hoja ancha, arces de otoño), tipo de roca o muro, densidad de faroles (de 0, sin luz, a mucha), color del guardarraíl, cordilleras del `SkyDome`, `CityStyle` del pueblo del pie y clima por defecto.
- **Por tramo:** ancho propio (`halfWidth` opcional) y pendiente propia, opcional, para tener pistas estrechas, anchas o mixtas.
- Tramos nuevos si los pide alguna pista: puente (sin terreno debajo, con barandilla) y curva en S como atajo de dos curvas.

**Sentido de subida:**
- `TrackData` puede construir la pista al revés: misma geometría, empezando por el pueblo y terminando arriba.
- Hace falta otro perfil de velocidad para el rival, porque subiendo frena menos y acelera menos.
- Récords separados para bajada y subida.

**Escenas:**
- Una escena por pista (`Pista_<nombre>.unity`), cada una con su horneado de geometría y luces.
- Una escena `Inicio` con el menú y el garaje que carga la pista elegida con `SceneManager`, mostrando una pantalla de carga con el filtro PS2.

**Menú:**
- Selector de pista con un mapa dibujado desde la línea central y un perfil de altitud.
- Récord por pista, sentido y coche.

**Herramienta de editor (`KuroganeTrackTools`):**
- Ventana con la vista desde arriba y el perfil de altitud de la `TrackDefinition` seleccionada.
- Avisos si la pista se cruza consigo misma o si dos tramos quedan demasiado cerca: es la comprobación de distancia mínima que usamos al diseñar el Monte Kurogane.
- Botón para crear la escena de la pista y hornearla.

**Listo cuando:**
- El Monte Kurogane funciona igual que hoy pero cargado desde `Inicio`.
- Se puede jugar de subida.
- Crear una pista nueva consiste en hacer un asset, revisarlo en la ventana y pulsar el botón.

---

## F · Pistas nuevas (una rama por pista: `feat/pista-<nombre>`)

Las cuatro propuestas cubren estilos muy distintos. Los nombres son provisionales.

| Pista | Referencia real | Carácter | Longitud | Ambiente |
|---|---|---|---|---|
| **Akane-tōge (茜峠)** | Monte Akagi | Rápida: rectas largas, curvas amplias y pocas horquillas abiertas. Calzada ancha. | ~3,5 km | Cima con niebla frecuente, praderas altas, pocos árboles arriba y bosque abajo |
| **Myōseki-tōge (妙石峠)** | Monte Myōgi | Técnica: eses encadenadas, calzada estrecha, curvas ciegas entre paredes de roca | ~2,6 km | Agujas de roca recortadas contra el cielo, muros de piedra y un mirador |
| **Usuba-tōge (薄葉峠)** | Paso de Usui (carretera vieja) | Muchísimas curvas cortas sin respiro y calzada muy estrecha | ~3 km | Sin faroles (solo faros), bosque cerrado y viaducto de ladrillo de cuatro arcos al lado |
| **Irozaka (色坂)** | Irohazaka (Nikkō) | Bajada de sentido único con más de 20 horquillas seguidas, cada una con su cartel kana | ~4 km | Arces de otoño, cascada y mirador al lago |

**Recomendación:** empezar por **Usuba**, que cambia por completo la forma de manejar: sin luces y con curvas sin parar. La sigue **Akane**, que es lo contrario: velocidad.

**Cada pista incluye:**
- Su lista de tramos, validada con la herramienta de E.
- Su `EnvironmentTheme` y su pueblo o poblado al pie (`CityStyle` propio: aldea, onsen u hotel).
- Decorado propio: agujas de roca, viaducto, cascada o lago.
- El perfil de velocidad del rival ajustado.
- Récords y nota S–C propios.

**Listo cuando:** se puede elegir la pista en el menú, bajarla y subirla contra el rival, cumple los 60 fps en Android y tiene su horneado.

---

## G · Física fuera de la pista (`feat/fisica-mundo`)

Es necesario para el modo libre. El manejo no cambia: solo cambia de dónde salen el suelo y las paredes.

- **`ICarSurface` (interfaz nueva)** con dos implementaciones:
  - **`TrackSurface`:** la de ahora, con línea central, límites laterales y canaleta. Se usa en carrera, porque es exacta y no depende de colisionadores.
  - **`WorldSurface`:** la altura del suelo sale de un rayo hacia abajo contra las mallas de calles y terreno (capa `Suelo`). La inclinación del coche sale de la normal del suelo. Las paredes, casas, guardarraíles y coches aparcados son colisionadores (capa `Obstaculo`): el coche se separa con `Physics.ComputePenetration` y la velocidad rebota igual que hoy contra el borde de la calzada.
- **`CarController`:** solo cambia la parte de "límites de la calzada", que pasa a llamar a la superficie activa. Las fuerzas, el agarre, el derrape y la caja de cambios no cambian. Hay que comprobar que en el Monte Kurogane se sienta idéntico con las dos superficies.
- **Canaleta en el mundo:** volumen disparador en las horquillas, para que el truco funcione también en modo libre.
- **Coste:** un rayo y una consulta de solapes por paso. Medir en Android a 120 Hz y, si hace falta, bajar la consulta de solapes a 60 Hz.

**Listo cuando:** con `WorldSurface` se puede bajar el Monte Kurogane sin atravesar nada y chocar con las casas del pueblo.

---

## H · Pueblo abierto y modo libre

### H1. Pueblo con calles transitables (`feat/pueblo-abierto`)

**Red de calles (`RoadGraph`):** nodos (cruces) y aristas (calles con ancho y aceras) en vez de una sola calle mayor.
- `CityBuilder` genera la red con una semilla en unos 600 × 600 m.
- Calle mayor, cuadrícula de calles secundarias y una avenida con más carriles.
- Un río con puente, la vía del tren con el paso a nivel actual y la estación.
- La salida hacia la montaña conecta con el tramo `afterGoal` de la pista.

**Lugares con función:**
- **Tienda de tofu:** base y garaje para cambiar de coche.
- **Gasolinera:** repara el vaso y recarga el agua.
- **Konbini** y **mirador sobre el río**.
- **Aparcamiento al pie de la montaña:** el punto de encuentro con los rivales.

**Rendimiento:** troceado del pueblo como la pista (`cellSize`), LOD de edificios, occlusion culling horneado y ventanas en atlas.

### H2. Tráfico del pueblo con normas (`feat/trafico-pueblo`)

- El `TrafficCar` actual pasa a seguir la red de calles por la izquierda.
- Semáforos en los cruces principales; fuera de las horas de mucho tráfico quedan en ámbar intermitente, como en el pueblo actual.
- Los coches se paran en el paso a nivel cerrado.
- Pocos peatones y bicicletas a medianoche, todos decorativos.

### H3. Modo libre (`feat/modo-libre`)

**Menú:** nuevo modo **Libre**. Empiezas en la tienda de tofu con el coche elegido.

**Mundo continuo:** desde el pueblo se sube por la carretera al Monte Kurogane y se puede bajar libremente. El paso de una zona a otra se carga por partes, sin pantalla de carga.

**Actividades:**
- **Reparto de tofu:** recoges el pedido en la tienda y lo llevas a un hotel de la montaña o a una casa del pueblo antes de un tiempo límite, sin derramar el vaso. Paga más cuanto más agua quede.
- **Desafíos en los aparcamientos de la montaña:** te acercas a un coche con las luces de emergencia y empieza una batalla de bajada desde ahí.
- **Contrarreloj libre:** pasas por la línea de salida y se cronometra sola.
- **Puntos de drift** en zonas marcadas del pueblo.

**Interfaz:** mapa del pueblo y la montaña en pausa con viaje rápido a los lugares descubiertos, minimapa en el HUD e indicadores de misiones.

**Tiempo:** ciclo opcional de hora y clima usando `WeatherSystem`, con transiciones lentas entre perfiles.

**Guardado:** dinero o puntos, repartos hechos y desafíos ganados, en JSON en `persistentDataPath`, igual que los récords.

**Listo cuando:** se puede salir de la tienda, recorrer el pueblo, subir y bajar la montaña, hacer un reparto y una batalla sin volver al menú, y todo se mantiene a 60 fps en Android.

---

## Orden recomendado

```
E multipista ──► F Usuba ──► F Akane ──► F Myōseki ──► F Irozaka
      │
      └──► G física mundo ──► H1 pueblo abierto ──► H2 tráfico ──► H3 modo libre
```

Hay dos líneas que pueden avanzar a la vez después de E: pistas y modo libre. Si hay que elegir una, conviene **E → F Usuba → G → H1 → H3**: así hay pronto una pista nueva y el modo libre básico. Después se completa el resto.

## Decisiones pendientes

1. ¿Qué pistas y en qué orden? La propuesta es Usuba, Akane, Myōseki e Irozaka.
2. ¿El modo libre incluye solo el pueblo o también la montaña, como mundo continuo? La propuesta es mundo continuo.
3. ¿Reparto con dinero para comprar coches o mejoras? Sin decidir: sería un bloque nuevo de progresión.
