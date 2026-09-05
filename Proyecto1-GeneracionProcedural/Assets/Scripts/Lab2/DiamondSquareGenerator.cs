using System;
using UnityEngine;

public static class DiamondSquareGenerator
{
    // -------------------------------------------------------------------------
    // DIAMOND-SQUARE
    // -------------------------------------------------------------------------
    //
    // Diamond-Square es un algoritmo de generación procedural utilizado para
    // crear terrenos con características naturales y variaciones a diferentes
    // escalas.
    //
    // El algoritmo comienza con una cuadrícula grande:
    //
    //      A ------------------- B
    //      |                     |
    //      |                     |
    //      |                     |
    //      |                     |
    //      D ------------------- C
    //
    // Las cuatro esquinas poseen valores iniciales.
    //
    // Posteriormente la cuadrícula se subdivide repetidamente utilizando dos
    // pasos:
    //
    //      1. Diamond Step
    //      2. Square Step
    //
    // En cada iteración:
    //
    //      - Se calculan nuevos puntos.
    //      - Se reduce el tamaño de las regiones.
    //      - Se reduce la magnitud de la perturbación aleatoria.
    //
    //
    // La resolución generada debe cumplir:
    //
    //      resolution = 2^iterations + 1
    //
    // Ejemplos:
    //
    //      iterations = 5  -> 33
    //      iterations = 6  -> 65
    //      iterations = 7  -> 129
    //      iterations = 8  -> 257
    //
    // Esta relación garantiza que la cuadrícula pueda subdividirse
    // correctamente en cada iteración.
    public static float[,] GenerateHeightmap(
        int iterations,
        int seed,
        float roughness,
        float roughnessDecay)
    {
        // La resolución debe tener la forma:
        //
        //      2^iterations + 1
        //
        // Esto permite comenzar con un cuadrado grande y dividirlo
        // repetidamente por dos.
        int resolution = (1 << iterations) + 1;

        float[,] heights = new float[resolution, resolution];

        // Se utiliza System.Random para mantener resultados reproducibles
        // utilizando la misma seed.
        System.Random random = new System.Random(seed);

        // Las cuatro esquinas iniciales se generan utilizando valores aleatorios
        // entre 0 y 1.
        //
        //       (0,0) ---------------- (resolution-1,0)
        //         A                          B
        //
        //
        //
        //         D                          C
        //       (0,resolution-1) ------ (resolution-1,resolution-1)
        //
        heights[0, 0] = (float)random.NextDouble();
        heights[0, resolution - 1] = (float)random.NextDouble();
        heights[resolution - 1, 0] = (float)random.NextDouble();
        heights[resolution - 1, resolution - 1] =
            (float)random.NextDouble();

        // stepSize representa el tamaño de la región actual.
        //
        // Al inicio:
        //
        //      stepSize = resolution - 1
        //
        // Después de cada iteración:
        //
        //      stepSize = stepSize / 2
        //
        int stepSize = resolution - 1;

        // currentRoughness representa la magnitud actual de las perturbaciones.
        float currentRoughness = roughness;

        // El algoritmo continúa mientras existan regiones que puedan dividirse.
        while (stepSize > 1)
        {
            // TODO: PROCESO ITERATIVO
            //
            // 1. Ejecutar DiamondStep utilizando:
            //      heights, stepSize, currentRoughness y random.
            //
            // 2. Ejecutar SquareStep utilizando los mismos parámetros.
            //
            // Después de ambos pasos se cambia a una escala menor.

            DiamondStep(
                heights,
                stepSize,
                currentRoughness,
                random
            );

            SquareStep(
                heights,
                stepSize,
                currentRoughness,
                random
            );

            stepSize /= 2;
            currentRoughness *= roughnessDecay;
        }

        return heights;
    }

    // -------------------------------------------------------------------------
    // DIAMOND STEP
    // -------------------------------------------------------------------------
    //
    // El Diamond Step calcula el valor del centro de cada cuadrado.
    //
    // Ejemplo:
    //
    //      A ---------------- B
    //      |                  |
    //      |        X         |
    //      |      center      |
    //      |                  |
    //      D ---------------- C
    //
    // El valor del centro se calcula utilizando:
    //
    //      average =
    //          (A + B + C + D) / 4
    //
    // Luego se añade una perturbación aleatoria:
    //
    //      center = average + randomOffset
    //
    // La magnitud del desplazamiento depende de roughness.
    private static void DiamondStep(
        float[,] heights,
        int stepSize,
        float roughness,
        System.Random random)
    {
        int resolution = heights.GetLength(0);

        int halfStep = stepSize / 2;

        // TODO: DIAMOND STEP
        //
        // 1. Recorrer los centros de cada cuadrado.
        // 2. Obtener las cuatro esquinas utilizando halfStep.
        // 3. Calcular el promedio de las cuatro alturas.
        // 4. Añadir RandomOffset(random, roughness).
        // 5. Mantener la nueva altura dentro del rango [0,1].
        // 6. Guardar el resultado en el punto central.

        for (int y = halfStep; y < resolution; y += stepSize)
        {
            for (int x = halfStep; x < resolution; x += stepSize)
            {
                float topLeft = heights[y - halfStep, x - halfStep];
                float topRight = heights[y - halfStep, x + halfStep];
                float bottomLeft = heights[y + halfStep, x - halfStep];
                float bottomRight = heights[y + halfStep, x + halfStep];

                float average =
                    (topLeft + topRight + bottomLeft + bottomRight) / 4f;

                float offset = RandomOffset(random, roughness);

                heights[y, x] = Mathf.Clamp01(average + offset);
            }
        }
    }

    // -------------------------------------------------------------------------
    // SQUARE STEP
    // -------------------------------------------------------------------------
    //
    // Después de calcular los centros de los cuadrados, se deben calcular los
    // puntos restantes que se encuentran entre ellos.
    //
    // Cada nuevo punto utiliza los vecinos disponibles en:
    //
    //          arriba
    //             |
    //             |
    // izquierda -- X -- derecha
    //             |
    //             |
    //           abajo
    //
    // En posiciones interiores normalmente existen cuatro vecinos.
    //
    // Sin embargo, en los bordes algunos vecinos no existen.
    //
    // Por ejemplo:
    //
    //          borde superior
    //
    //              X
    //              |
    //      izquierda -- X -- derecha
    //              |
    //            abajo
    //
    // En ese caso solo deben utilizarse los vecinos válidos.
    //
    // El promedio debe calcularse utilizando la cantidad real de vecinos.
    private static void SquareStep(
        float[,] heights,
        int stepSize,
        float roughness,
        System.Random random)
    {
        int resolution = heights.GetLength(0);

        int halfStep = stepSize / 2;

        // TODO: SQUARE STEP
        //
        // 1. Recorrer las filas separadas por halfStep.
        // 2. Determinar si la fila comienza en x = 0 o x = halfStep.
        // 3. Recorrer los puntos de esa fila utilizando stepSize.
        // 4. Para cada punto:
        //      - Inicializar suma y contador.
        //      - Comprobar vecino izquierdo.
        //      - Comprobar vecino derecho.
        //      - Comprobar vecino superior.
        //      - Comprobar vecino inferior.
        // 5. Calcular el promedio utilizando solo los vecinos válidos.
        // 6. Añadir RandomOffset(random, roughness).
        // 7. Mantener el resultado dentro de [0,1].
        // 8. Guardar la nueva altura.

        for (int y = 0; y < resolution; y += halfStep)
        {
            int row = y / halfStep;

            int startX = (row % 2 == 0)
                ? halfStep
                : 0;

            for (int x = startX; x < resolution; x += stepSize)
            {
                float sum = 0f;
                int count = 0;

                // Vecino izquierdo
                if (x - halfStep >= 0)
                {
                    sum += heights[y, x - halfStep];
                    count++;
                }

                // Vecino derecho
                if (x + halfStep < resolution)
                {
                    sum += heights[y, x + halfStep];
                    count++;
                }

                // Vecino superior
                if (y - halfStep >= 0)
                {
                    sum += heights[y - halfStep, x];
                    count++;
                }

                // Vecino inferior
                if (y + halfStep < resolution)
                {
                    sum += heights[y + halfStep, x];
                    count++;
                }

                if (count > 0)
                {
                    float average = sum / count;

                    float offset = RandomOffset(random, roughness);

                    heights[y, x] = Mathf.Clamp01(
                        average + offset
                    );
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // RANDOM OFFSET
    // -------------------------------------------------------------------------
    //
    // Este método genera una perturbación aleatoria dentro del intervalo:
    //
    //      [-roughness, +roughness]
    //
    // Por ejemplo, si:
    //
    //      roughness = 0.4
    //
    // el resultado estará entre:
    //
    //      -0.4 y +0.4
    //
    // La perturbación permite evitar que el terreno sea completamente suave.
    //
    // Este método ya se encuentra implementado y puede utilizarse directamente.
    private static float RandomOffset(
        System.Random random,
        float roughness)
    {
        return ((float)random.NextDouble() * 2f - 1f) * roughness;
    }
}