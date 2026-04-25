using UnityEngine;

namespace Floor
{
    /// <summary>
    /// Чистые расчёты проекции мировой XZ-точки на пиксельную сетку текстуры пола.
    /// </summary>
    public static class FloorProjection
    {
        /// <summary>
        /// Проектирует мировую точку на пиксельные координаты текстуры пола.
        /// Левый-нижний и правый-верхний углы пола соответствуют
        /// <paramref name="floorCenterXZ"/> минус и плюс <paramref name="floorWorldSize"/> / 2.
        /// </summary>
        /// <param name="worldXZ">Мировая XZ-точка для проекции.</param>
        /// <param name="floorCenterXZ">Мировая XZ-позиция центра пола.</param>
        /// <param name="floorWorldSize">Размер пола в мировых единицах по X и Z.</param>
        /// <param name="textureSize">Разрешение текстуры пола в пикселях.</param>
        /// <returns>Целочисленные пиксельные координаты на текстуре. Может выходить за её пределы.</returns>
        public static Vector2Int ProjectWorldToTexel(
            Vector2 worldXZ,
            Vector2 floorCenterXZ,
            Vector2 floorWorldSize,
            Vector2Int textureSize)
        {
            var local = worldXZ - floorCenterXZ;
            var u = local.x / floorWorldSize.x + 0.5f;
            var v = local.y / floorWorldSize.y + 0.5f;
            var x = Mathf.FloorToInt(u * textureSize.x);
            var y = Mathf.FloorToInt(v * textureSize.y);
            return new Vector2Int(x, y);
        }

        /// <summary>
        /// Проверяет, что пиксель находится внутри текстуры заданного размера.
        /// </summary>
        /// <param name="texel">Координаты пикселя.</param>
        /// <param name="textureSize">Размер текстуры.</param>
        /// <returns>True, если пиксель лежит в прямоугольнике <c>[0..textureSize)</c>.</returns>
        public static bool IsInside(Vector2Int texel, Vector2Int textureSize)
        {
            return texel.x >= 0 && texel.x < textureSize.x
                && texel.y >= 0 && texel.y < textureSize.y;
        }
    }
}
