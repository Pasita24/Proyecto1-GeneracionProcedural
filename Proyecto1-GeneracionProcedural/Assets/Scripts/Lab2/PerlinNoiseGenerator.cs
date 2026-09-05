using System;
using UnityEngine;

public static class PerlinNoiseGenerator
{
    // Gradientes disponibles para la versión 2D utilizada en el laboratorio.
    //
    // Cada punto entero de la grilla tendrá asociado uno de estos vectores.
    // El gradiente NO representa directamente una altura. Representa una
    // dirección que será utilizada posteriormente para calcular la influencia
    // de ese punto sobre las posiciones cercanas.
    //
    // La selección determinista del gradiente ya se encuentra implementada.
    private static readonly Vector2[] gradients =
    {
        new Vector2(1f, 0f),
        new Vector2(-1f, 0f),
        new Vector2(0f, 1f),
        new Vector2(0f, -1f),

        new Vector2(1f, 1f).normalized,
        new Vector2(-1f, 1f).normalized,
        new Vector2(1f, -1f).normalized,
        new Vector2(-1f, -1f).normalized
    };

    // -------------------------------------------------------------------------
    // GENERACIÓN DEL HEIGHTMAP
    // -------------------------------------------------------------------------
    //
    // Esta parte se entrega implementada.
    //
    // Su función es recorrer todas las posiciones del heightmap y convertir
    // sus coordenadas a coordenadas dentro de la grilla de Gradient Noise.
    //
    // Primero las coordenadas x e y del heightmap se normalizan entre 0 y 1.
    //
    // Luego se multiplican por frequency:
    //
    //      sampleX = normalizedX * frequency
    //      sampleY = normalizedY * frequency
    //
    // Por ejemplo, si frequency = 4, el terreno recorrerá aproximadamente
    // cuatro celdas de ruido a lo largo de cada eje.
    //
    // Una frecuencia mayor produce variaciones más pequeñas y frecuentes.
    // Una frecuencia menor produce características de mayor escala.
    //
    // Finalmente se consulta GetNoiseValue() para obtener el valor correspondiente
    // a cada posición.
    public static float[,] GenerateHeightmap(
        int resolution,
        float frequency,
        int seed,
        HeightmapGenerator.InterpolationMode interpolationMode)
    {
        if (interpolationMode == HeightmapGenerator.InterpolationMode.None)
        {
            throw new ArgumentException(
                "Perlin Noise requiere interpolación Bilinear o Bicubic."
            );
        }

        float[,] heights = new float[resolution, resolution];

        frequency = Mathf.Max(0.001f, frequency);

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float normalizedX = x / (float)(resolution - 1);
                float normalizedY = y / (float)(resolution - 1);

                float sampleX = normalizedX * frequency;
                float sampleY = normalizedY * frequency;

                float noise = GetNoiseValue(
                    sampleX,
                    sampleY,
                    seed,
                    interpolationMode
                );

                // Gradient Noise puede producir valores positivos y negativos.
                //
                // Para utilizar el resultado como altura de Unity se transforma
                // aproximadamente desde [-1,1] hacia [0,1].
                heights[y, x] = Mathf.Clamp01((noise + 1f) * 0.5f);
            }
        }

        return heights;
    }

    // -------------------------------------------------------------------------
    // PERLIN / GRADIENT NOISE 2D
    // -------------------------------------------------------------------------
    //
    // A diferencia de Value Noise, los puntos de la grilla NO almacenan
    // directamente valores de altura.
    //
    // Cada punto de la grilla posee un VECTOR GRADIENTE:
    //
    //               ↗                 ←
    //               O-----------------O
    //               |                 |
    //               |       P         |
    //               |      (x,y)      |
    //               |                 |
    //               O-----------------O
    //               ↓                 ↘
    //
    // Para calcular el valor de ruido correspondiente a P se utilizan los
    // gradientes de las cuatro esquinas que rodean al punto.
    //
    //
    // -------------------------------------------------------------------------
    // 1. IDENTIFICAR LA CELDA
    // -------------------------------------------------------------------------
    //
    // Las coordenadas recibidas por este método pueden contener decimales.
    //
    // Ejemplo:
    //
    //      x = 1.35
    //      y = 2.60
    //
    // El punto se encuentra entre:
    //
    //      x = 1 y x = 2
    //      y = 2 y y = 3
    //
    // Por lo tanto, las cuatro esquinas de la celda son:
    //
    //      (1,2) -------- (2,2)
    //        |              |
    //        |      P       |
    //        | (1.35,2.60)  |
    //        |              |
    //      (1,3) -------- (2,3)
    //
    // Las coordenadas enteras inferiores pueden obtenerse utilizando el piso
    // de x e y. Las coordenadas superiores corresponden al entero siguiente.
    //
    //
    // -------------------------------------------------------------------------
    // 2. CALCULAR LA POSICIÓN LOCAL
    // -------------------------------------------------------------------------
    //
    // Luego se debe determinar dónde se encuentra P dentro de esa celda.
    //
    // Para el ejemplo:
    //
    //      localX = 1.35 - 1 = 0.35
    //      localY = 2.60 - 2 = 0.60
    //
    // Estos valores se encuentran entre 0 y 1:
    //
    //      localX = 0  -> borde izquierdo
    //      localX = 1  -> borde derecho
    //
    //      localY = 0  -> un borde horizontal
    //      localY = 1  -> el borde horizontal opuesto
    //
    // Estos valores serán utilizados tanto para construir los vectores de
    // desplazamiento como para calcular posteriormente los pesos de interpolación.
    //
    //
    // -------------------------------------------------------------------------
    // 3. OBTENER LOS CUATRO GRADIENTES
    // -------------------------------------------------------------------------
    //
    // Cada esquina debe consultar su vector mediante:
    //
    //      GetGradient(xEntero, yEntero, seed)
    //
    // Conceptualmente tendremos:
    //
    //      gradient00 -------- gradient10
    //          |                  |
    //          |        P         |
    //          |                  |
    //      gradient01 -------- gradient11
    //
    // GetGradient() ya está implementado.
    //
    // La misma coordenada y la misma seed siempre producirán el mismo gradiente.
    //
    //
    // -------------------------------------------------------------------------
    // 4. CALCULAR LOS VECTORES DE DESPLAZAMIENTO
    // -------------------------------------------------------------------------
    //
    // Para cada esquina debe construirse un vector que apunte:
    //
    //          DESDE LA ESQUINA
    //          HACIA EL PUNTO P
    //
    // Por ejemplo, para la esquina superior izquierda:
    //
    //      O ----------> P
    //      esquina       punto
    //
    // El desplazamiento corresponde conceptualmente a:
    //
    //      displacement = punto - esquina
    //
    // Como localX y localY representan la posición dentro de una celda
    // de tamaño 1, los desplazamientos pueden construirse utilizando estos
    // valores.
    //
    // Para el ejemplo localX = 0.35 y localY = 0.60:
    //
    // esquina 00:
    //      ( 0.35,  0.60)
    //
    // esquina 10:
    //      ( 0.35 - 1,  0.60)
    //
    // esquina 01:
    //      ( 0.35,  0.60 - 1)
    //
    // esquina 11:
    //      ( 0.35 - 1,  0.60 - 1)
    //
    //
    // -------------------------------------------------------------------------
    // 5. CALCULAR LOS PRODUCTOS PUNTO
    // -------------------------------------------------------------------------
    //
    // Cada gradiente se compara con su correspondiente desplazamiento mediante
    // el producto punto:
    //
    //      dot = gradient.x * displacement.x
    //          + gradient.y * displacement.y
    //
    // También puede utilizarse Vector2.Dot().
    //
    // El producto punto permite medir cuánto coincide la dirección del
    // gradiente con la dirección hacia el punto evaluado.
    //
    // Conceptualmente:
    //
    //      misma dirección       -> influencia positiva
    //      dirección opuesta     -> influencia negativa
    //      perpendicular         -> influencia cercana a cero
    //
    // Al finalizar este paso tendremos cuatro valores:
    //
    //          dot00 -------- dot10
    //             |             |
    //             |      P      |
    //             |             |
    //          dot01 -------- dot11
    //
    //
    // -------------------------------------------------------------------------
    // 6. CALCULAR LOS PESOS DE INTERPOLACIÓN
    // -------------------------------------------------------------------------
    //
    // Los cuatro productos punto deben combinarse utilizando la posición local
    // del punto dentro de la celda.
    //
    // Para X:
    //
    //      weightX = GetInterpolationWeight(localX, interpolationMode)
    //
    // Para Y:
    //
    //      weightY = GetInterpolationWeight(localY, interpolationMode)
    //
    // Esto reutiliza las interpolaciones implementadas previamente:
    //
    //      Bilinear -> utiliza directamente t.
    //      Bicubic  -> utiliza BicubicWeight(t).
    //
    //
    // -------------------------------------------------------------------------
    // 7. INTERPOLAR LAS CUATRO CONTRIBUCIONES
    // -------------------------------------------------------------------------
    //
    // El procedimiento es equivalente al utilizado previamente en Value Noise.
    //
    // Primero interpolar horizontalmente:
    //
    //      dot00 -------- dot10
    //             |
    //            top
    //
    //      dot01 -------- dot11
    //             |
    //           bottom
    //
    // utilizando weightX.
    //
    // Luego interpolar verticalmente:
    //
    //             top
    //              |
    //              |
    //           resultado
    //              |
    //              |
    //            bottom
    //
    // utilizando weightY.
    //
    // El resultado final corresponde al valor de Gradient Noise para la
    // posición (x,y).
    //
    //
    // RESUMEN:
    //
    // Coordenada (x,y)
    //        |
    //        v
    // Identificar celda
    //        |
    //        v
    // Calcular localX y localY
    //        |
    //        v
    // Obtener 4 gradientes
    //        |
    //        v
    // Crear 4 desplazamientos
    //        |
    //        v
    // Calcular 4 productos punto
    //        |
    //        v
    // Interpolar en X
    //        |
    //        v
    // Interpolar en Y
    //        |
    //        v
    // Valor final de Gradient Noise
    //
    public static float GetNoiseValue(
        float x,
        float y,
        int seed,
        HeightmapGenerator.InterpolationMode interpolationMode)
    {
        // TODO: PERLIN / GRADIENT NOISE 2D
        //
        // Para cada posición (x, y):
        //
        // 1. Identificar las cuatro esquinas enteras de la celda.
        // 2. Calcular localX y localY.
        // 3. Obtener el gradiente correspondiente a cada esquina.
        // 4. Crear los cuatro vectores de desplazamiento.
        // 5. Calcular el producto punto para cada esquina.
        // 6. Obtener weightX y weightY según el modo de interpolación.
        // 7. Interpolar horizontalmente los dos resultados superiores.
        // 8. Interpolar horizontalmente los dos resultados inferiores.
        // 9. Interpolar verticalmente ambos resultados.
        // 10. Retornar el valor obtenido.
        //
        // Puede reutilizar:
        //
        //      GetGradient(...)
        //      Vector2.Dot(...)
        //      HeightmapGenerator.GetInterpolationWeight(...)
        //      HeightmapGenerator.LinearInterpolation(...)

        // 1. Identificar la celda que contiene el punto.
        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);

        int x1 = x0 + 1;
        int y1 = y0 + 1;

        // 2. Calcular la posición local del punto dentro de la celda.
        float localX = x - x0;
        float localY = y - y0;

        // 3. Obtener los cuatro gradientes de las esquinas.
        Vector2 gradient00 = GetGradient(x0, y0, seed);
        Vector2 gradient10 = GetGradient(x1, y0, seed);
        Vector2 gradient01 = GetGradient(x0, y1, seed);
        Vector2 gradient11 = GetGradient(x1, y1, seed);

        // 4. Crear los cuatro vectores de desplazamiento.
        Vector2 displacement00 = new Vector2(localX, localY);
        Vector2 displacement10 = new Vector2(localX - 1f, localY);
        Vector2 displacement01 = new Vector2(localX, localY - 1f);
        Vector2 displacement11 = new Vector2(localX - 1f, localY - 1f);

        // 5. Calcular el producto punto para cada esquina.
        float dot00 = Vector2.Dot(gradient00, displacement00);
        float dot10 = Vector2.Dot(gradient10, displacement10);
        float dot01 = Vector2.Dot(gradient01, displacement01);
        float dot11 = Vector2.Dot(gradient11, displacement11);

        // 6. Obtener weightX y weightY según el modo de interpolación.
        float weightX = HeightmapGenerator.GetInterpolationWeight(
            localX,
            interpolationMode
        );

        float weightY = HeightmapGenerator.GetInterpolationWeight(
            localY,
            interpolationMode
        );

        // 7. Interpolar horizontalmente los dos resultados superiores.
        float top = HeightmapGenerator.LinearInterpolation(
            dot00,
            dot10,
            weightX
        );

        // 8. Interpolar horizontalmente los dos resultados inferiores.
        float bottom = HeightmapGenerator.LinearInterpolation(
            dot01,
            dot11,
            weightX
        );

        // 9. Interpolar verticalmente ambos resultados.
        float result = HeightmapGenerator.LinearInterpolation(
            top,
            bottom,
            weightY
        );

        // 10. Retornar el valor obtenido.
        return result;
    }

    // -------------------------------------------------------------------------
    // SELECCIÓN DETERMINISTA DE GRADIENTES
    // -------------------------------------------------------------------------
    //
    // Este método se entrega implementado.
    //
    // Su objetivo es asociar de manera determinista uno de los vectores
    // disponibles a cada coordenada entera de la grilla.
    //
    // Esto significa que:
    //
    //      GetGradient(2, 3, 123)
    //
    // devolverá siempre el mismo gradiente mientras la seed sea 123.
    //
    // Una seed diferente permite generar una distribución distinta de
    // gradientes y, por lo tanto, un terreno diferente.
    //
    // Para este laboratorio se utiliza un hash simple en lugar de implementar
    // una tabla de permutaciones.
    private static Vector2 GetGradient(int x, int y, int seed)
    {
        unchecked
        {
            uint hash = (uint)seed;

            hash ^= (uint)x * 374761393u;
            hash ^= (uint)y * 668265263u;

            hash = (hash ^ (hash >> 13)) * 1274126177u;

            int index = (int)(hash % (uint)gradients.Length);

            return gradients[index];
        }
    }
}