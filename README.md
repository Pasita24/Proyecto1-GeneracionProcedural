# Proyecto de Unidad I: Generación Procedural de Entornos

## Descripción del Proyecto
Este proyecto es un sistema de Generación Procedural de Contenido (PCG) desarrollado en Unity. El sistema integra múltiples algoritmos para construir un ecosistema completo y dependiente: esculpe el terreno, planifica una aldea, traza caminos orgánicos entre las estructuras y puebla el entorno con vegetación tridimensional. Está diseñado para ser altamente personalizable, permitiendo generar desde aldeas boscosas tradicionales hasta campamentos hostiles modificando parámetros desde el Inspector.

## Equipo
* **Integrantes:** Benjamin Paz, Jose Hernandez
* **Profesor:** Claudio Rubio

## Técnicas PCG Utilizadas
* **Fractal / Ruido (TerrainGenerator):** Algoritmos como Perlin Noise o Diamond-Square para generar la topografía base y las elevaciones del mapa.
* **Técnica Constructiva (BSPGenerator):** Partición Binaria del Espacio (BSP) para subdividir el terreno en lotes habitables y distribuir prefabs de casas evitando solapamientos.
* **L-Systems (LSystemTreeGenerator):** Sistema de gramáticas y Turtle Graphics para interpretar axiomas y hacer crecer vegetación 3D (árboles y arbustos) de forma procedural.
* **Random Walk Dirigido (PathGenerator):** Algoritmo de caminata aleatoria que extrae las coordenadas generadas por el BSP para trazar caminos que conecten lógicamente las estructuras.

## Instrucciones Básicas de Ejecución
1. Abre la escena principal en Unity.
2. Selecciona el objeto (o los objetos) que contienen los scripts generadores.
3. Asegúrate de asignar el terreno base y los prefabs requeridos (casas, ramas, hojas, baldosas) en el Inspector.
4. Ejecuta los generadores haciendo **clic derecho** sobre el nombre de cada script en el Inspector (mediante el *Context Menu*).
5. **Orden recomendado de generación:** 
   * Generar Terreno 
   * Generar Aldea (BSP) 
   * Generar Caminos 
   * Generar Bosque (L-System)

## Principales Parámetros Configurables
* **TerrainGenerator:** 
  * `Resolution` / `Frequency`: Controla el nivel de detalle y la agresividad del terreno (colinas suaves vs. montañas).
  * `Seed`: Semilla para regenerar topografías exactas.
* **BSPGenerator:** 
  * `Min Leaf Size` / `Max Leaf Size`: Define el tamaño de las parcelas, determinando la densidad y separación de las construcciones.
* **PathGenerator:** 
  * `Probabilidad Directa`: (0 a 100) Define qué tan recto y lógico es el camino hacia la siguiente casa frente a un comportamiento errático y orgánico.
* **LSystemTreeGenerator:** 
  * `Axiom` & `Rules`: Define la estructura base del árbol.
  * `Iterations`: Controla la profundidad del fractal (se recomienda un máximo de 4 para mantener el rendimiento).
  * `Angle`: Altera la apertura de las ramas, cambiando la especie visual del árbol.

## Consideraciones Técnicas y Limitaciones Conocidas
* **Escala de Prefabs:** Los modelos 3D inyectados deben tener su escala corregida manualmente o agrupados en un objeto padre vacío; de lo contrario, las colisiones podrían fallar.
* **Clipping de Terreno:** Los objetos instanciados detectan la altura (eje Y) pero no la inclinación del suelo, por lo que en montañas muy empinadas las bases pueden atravesar la malla del terreno.
* **Rendimiento L-System:** Debido a la naturaleza de reemplazo del algoritmo, se generan cilindros solapados en los troncos base. Evitar iteraciones mayores a 4 para prevenir caídas de *framerate*.
