# Visión a futuro: el mapa de Gunma

> Nota de diseño, sin planificar aún. Se convierte en bloques con ramas cuando terminen los planes en curso (`plan-pistas-modo-libre.md`).

## La idea

La serie transcurre en la **prefectura de Gunma**, en la región de Kantō, al norte de Tokio, y sus montañas se basan en puertos reales de allí. Queremos que el mapa del juego se organice como Gunma: sus montañas, pueblos y ciudades en su posición relativa real, con eventos y modos propios de cada zona y de cada estación.

Gunma, sus lugares reales y sus fiestas se pueden usar libremente como inspiración. Lo que no se usa son los nombres, personajes, equipos o carteles inventados por la serie. Las pistas siguen llevando nombre propio, aunque su ubicación y su carácter sigan a la montaña real.

## Mapa: zonas en su sitio real, a escala comprimida

Un mapa 1:1 de Gunma no es viable en un móvil. La propuesta:

- **Mapa regional** estilizado de Gunma como menú del mundo. Cada zona aparece donde está en la realidad, y se viaja entre zonas con una ruta rápida (pantalla de carga con el filtro PS2).
- **Cada zona** es un mundo continuo como el del modo libre: un pueblo o ciudad al pie y su montaña.
- **Escala:** distancias comprimidas dentro de cada zona, pero los puntos de referencia en el orden y la orientación reales. Por ejemplo, el lago arriba del Haruna y el pueblo de onsen en la ladera.

| Zona (referencia real) | Pista del juego | Pueblo o ciudad al pie | Carácter |
|---|---|---|---|
| Monte Haruna, Shibukawa e Ikaho | **Monte Kurogane** (actual) | Pueblo de la tienda de tofu, escaleras de piedra del onsen y lago en la cima | Zona de inicio: repartos y primeras batallas |
| Monte Akagi, Maebashi | **Akane-tōge** | Maebashi (capital): avenidas y más tráfico | Rápida, niebla en la cima, praderas |
| Monte Myōgi, Tomioka | **Myōseki-tōge** | Tomioka (antigua fábrica de seda) | Técnica, agujas de roca |
| Paso de Usui, Yokokawa y Annaka | **Usuba-tōge** | Yokokawa: estación de tren antigua y viaducto de ladrillo | Muchísimas curvas, sin faroles |
| Kusatsu y ruta 292 | Pista nueva de alta montaña | Kusatsu: campo de vapor del onsen en la plaza | La carretera nacional más alta de Japón, muros de nieve en primavera |
| Tsumagoi | Caminos rurales | Campos de repollo, granjas | Rally ligero en tierra o caminos estrechos |
| Takasaki | Ciudad (circuito urbano o autopista) | Takasaki: muñecos daruma | Centro de eventos y concesionario |

**Irozaka** se inspira en Irohazaka, que está en **Tochigi**, fuera de Gunma. Tiene dos encajes posibles: zona "de excursión" fuera del mapa principal, o cambiarla por un puerto de Gunma.

## Eventos y modos por zona

- **Haruna (inicio):** repartos de tofu de madrugada al hotel del lago, desafíos en el aparcamiento de la cima y una carrera nocturna de bienvenida.
- **Akagi:** contrarreloj en grupo con salida en ola, batallas a alta velocidad y prueba de niebla.
- **Myōgi:** pruebas de precisión entre conos o franjas sin tocar el guardarraíl, y batallas cortas por tramos.
- **Usui:** "184 curvas" (bajada completa sin chocar) y carrera contra el último tren de la noche que cruza el viaducto.
- **Kusatsu:** bajada entre muros de nieve y entrega de pedidos al onsen sin derramar el vaso con el piso resbaladizo.
- **Tsumagoi:** repartos por caminos de granja al amanecer.
- **Takasaki:** concesionario y taller (si se añade la progresión con dinero) y punto de reunión de los eventos especiales.

## Estaciones, clima y fiestas reales

El clima de Gunma da reglas de juego propias:

- **Karakkaze (空っ風):** el viento seco y frío del invierno, famoso en Gunma. Añade ráfagas laterales que empujan el coche en los tramos abiertos.
- **Tormentas de verano:** Gunma es conocida por sus tormentas eléctricas. Lluvia fuerte de repente, relámpagos y calzada que se moja en pocos segundos.
- **Nieve en invierno** en Kusatsu y Akagi, con agarre reducido: el perfil de nieve que quedó pendiente.
- **Temporada de lluvias** (junio): más días de lluvia en el ciclo del clima.
- **Otoño:** arces rojos en Myōgi y en las laderas, con paleta especial.

**Calendario de fiestas** como eventos con decorado temporal y carreras especiales, basado en fiestas reales:
- Mercado de daruma de Takasaki (enero).
- Fuegos artificiales de verano.
- Fiestas de los pueblos en julio y agosto, con farolillos en las calles y tráfico cortado.

## Qué necesita del código (cuando toque)

- Un `RegionMap` (escena o menú) con las zonas y su posición, y viaje entre zonas con carga de escenas por partes.
- `EnvironmentTheme` y `CityStyle` por zona: ya existen desde los bloques E y H1.
- `WeatherProfile` con viento lateral (karakkaze), y un único cambio en `CarController`: una fuerza lateral que hay que probar en el juego.
- Calendario del juego: fecha y estación que eligen la paleta, el clima probable y las fiestas activas.
- Eventos como datos (`EventDefinition`, ScriptableObject): zona, requisitos, reglas, recompensa y fechas.

## Orden sugerido para más adelante

1. Mapa regional con las zonas que ya existan (Haruna y Usui) y el viaje entre ellas.
2. Eventos por zona como datos, empezando por Haruna y Usui.
3. Estaciones y calendario: karakkaze, nieve y tormentas.
4. Zonas nuevas: Akagi, Myōgi, Kusatsu y Tsumagoi, una por rama.
5. Fiestas y eventos temporales.
