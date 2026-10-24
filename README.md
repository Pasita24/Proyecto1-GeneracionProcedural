# Lights Out 3D: Generación Procedural de Puzzles

Proyecto de Unidad II (PCG) desarrollado en Unity.

**Integrantes:** Benjamín Paz, Jose Hernandez
**Versión ejecutable (itch.io):** [https://pasita24.itch.io/proyecto2-pcg]

## Descripción

Juego de puzzle basado en **Lights Out**, llevado a una red de nodos conectados en **3D** con temática de virus. Cada nodo es una esfera verde (Infectado) o un cubo rojo (Limpio). Al pulsar un nodo se invierte su estado y el de todos sus vecinos. El objetivo es dejar toda la red desinfectada.

El sistema genera puzzles que siempre tienen solución y calcula **en cuántos pasos mínimos** se pueden resolver. Ese resultado se usa dentro del juego: fija el tiempo objetivo, las recompensas y las pistas.

## Puzzle y representación

**Puzzle:** Lights Out (biblioteca de Ludii).

Como pulsar dos veces un mismo nodo se cancela y el orden de los clics no importa, una solución es simplemente un conjunto de nodos a pulsar (cada uno 0 o 1 vez). Minimizar los pasos equivale a minimizar la cantidad de clics.

| Elemento | Representación |
|---|---|
| Estado del tablero | `bool[] board`, un valor por nodo (`true` = infectado) |
| Topología de la red | `List<int>[] neighbors`, lista de vecinos por nodo |
| Solución | `bool[] clicks`, un valor por nodo (`true` = pulsar) |

**Por qué:** la red es irregular y 3D, por lo que una grilla de filas y columnas no calza. Con listas de vecinos, pulsar un nodo solo recorre sus conexiones, y la misma estructura sirve para el estado, la evaluación, la técnica evolutiva y la búsqueda local.

**Alternativas consideradas:** matriz 2D (Lights Out clásico), lista ordenada de clics (genera soluciones equivalentes repetidas) y matriz de adyacencia (más memoria).

## Técnicas PCG implementadas

### Generación de instancias

- **Forma de la red, Random Walk 3D:** la red crece desde un nodo inicial añadiendo nodos en posiciones libres contiguas en 6 direcciones (±X, ±Y, ±Z). Dos nodos son vecinos si están a distancia 1. El parámetro de profundidad controla qué tan 3D es la red.
- **Estado inicial, Backward from Goal State:** se parte del estado ganador (todo limpio) y se aplican clics aleatorios hacia atrás. Así el puzzle siempre tiene solución, y la cantidad de clics fija la dificultad máxima. Esos clics son una solución válida, pero no necesariamente la más corta.

### Técnica evolutiva: Evolution Strategy (μ+λ)

- **Individuo:** un vector de clics más un parámetro `sigma` propio que indica cuánto muta (se auto-adapta).
- **Variación:** se invierten `sigma` clics al azar (con cruce opcional entre padres).
- **Selección:** padres e hijos compiten juntos y sobreviven los μ mejores. Las soluciones válidas siempre ganan a las inválidas, y entre válidas gana la de menos clics.
- La población se siembra con la solución del generador, así que el resultado nunca es peor que ella.

### Búsqueda local: Simulated Annealing

- **Vecino:** la misma solución con un solo clic invertido.
- **Aceptación:** se acepta si mejora, o con probabilidad `exp(-Δ/T)` si empeora. La temperatura baja con enfriamiento geométrico.
- **Memoria:** se conserva el mejor estado visto durante el enfriamiento.
- Cada hijo de Evolution Strategy pasa por un Simulated Annealing corto antes de competir (combinación de tipo memético): ES explora y SA refina.

### Función de evaluación

```
costo = (nodos infectados × peso) + cantidad de clics
```

Empuja hacia soluciones que apagan todo y que son cortas. Guía las decisiones de SA, ordena la selección de ES y su resultado se usa en el juego (temporizador, recompensas y pistas).

### Pistas desde el estado actual

El juego mantiene un plan (nodos que faltan pulsar). Al pulsar un nodo se invierte ese nodo en el plan, que sigue siendo válido, y en segundo plano se busca un camino más corto desde el nuevo estado.

> El número de pasos mostrado es el mejor encontrado por Evolution Strategy + Simulated Annealing (heurística), no un mínimo demostrado matemáticamente.

## Modos de juego

- **Fácil / Medio / Difícil:** cambian cantidad de nodos, pasos del generador y profundidad 3D. Medio y Difícil se desbloquean resolviendo puzzles de la dificultad anterior.
- **Temporizador:** tiempo objetivo según los pasos mínimos. Es un bonus, no una derrota.
- **Monedas y pistas:** cada puzzle resuelto da monedas (base, bonus de eficiencia y de tiempo). Se pueden gastar en una pista de un nodo o en el plan completo. Las pistas se desbloquean junto con Medio.
- **Vista previa:** al pasar el mouse sobre un nodo se resaltan él y sus vecinos.
- **Modo desarrollador:** panel con sliders y botones para generar, reiniciar, resolver, ver la solución animada y activar o desactivar pistas.

## Principales parámetros configurables

Se modifican desde el panel de desarrollador del juego (sliders) y también desde el Inspector.

| Grupo | Parámetros |
|---|---|
| Puzzle | Cantidad de nodos, pasos de dificultad, profundidad 3D, semilla fija |
| Evolution Strategy | Población (μ), hijos (λ), generaciones máximas, límite de estancamiento, sigma inicial, probabilidad de cruce |
| Simulated Annealing | Temperatura inicial, tasa de enfriamiento, iteraciones, vecinos por paso |
| Evaluación | Peso de cada nodo infectado |
| Juego (por dificultad) | Tiempos, recompensas, costo de pistas, requisitos de desbloqueo |

## Instrucciones básicas de ejecución

**Versión jugable:** descargar y ejecutar el build desde la página de itch.io indicada arriba.

**Desde el código fuente:**

1. Clonar el repositorio.
2. Abrir la carpeta con Unity (versión: [VERSIÓN DE UNITY]).
3. Abrir la escena principal ([NOMBRE DE LA ESCENA]) y pulsar Play.

**Controles:**

- Clic izquierdo: pulsar un nodo.
- Clic derecho + arrastrar: girar la cámara alrededor del puzzle.
- Rueda del mouse: acercar o alejar.

## Estructura del código

| Script | Función |
|---|---|
| `LightsOutLogic3D` | Reglas del juego (pulsar nodo, contar, simular clics) |
| `BackwardFromGoalState3D` | Generación de la red 3D y del estado inicial |
| `EvolutionStrategySolver` | Evolution Strategy con Simulated Annealing interno |
| `LightsOutController3D` | Control del puzzle en escena, pistas y vista previa |
| `LightsOutNodeClick3D` | Detección del mouse sobre los nodos |
| `GameManager` | Menú, dificultades, temporizador, monedas, pistas y música |
| `DifficultyConfig`, `PlayerProgress` | Configuración de dificultades y progreso guardado |
| `SettingsPanelToggle`, `SliderValueLabel` | Panel de configuración y etiquetas de sliders |
