using NUnit.Framework;
using UnityEngine;

namespace Floor.Tests
{
    [TestFixture]
    public sealed class FloorProjectionTest
    {
        private static readonly Vector2 FloorSize = new Vector2(20f, 20f);
        private static readonly Vector2 FloorCenterAtOrigin = Vector2.zero;
        private static readonly Vector2Int Resolution = new Vector2Int(100, 100);

        [TestCase(0f, 0f, 50, 50, TestName = "Center → middle texel")]
        [TestCase(-10f, -10f, 0, 0, TestName = "Bottom-left corner → (0,0)")]
        [TestCase(-5f, 5f, 25, 75, TestName = "Quadrant point projects proportionally")]
        [TestCase(5f, -5f, 75, 25, TestName = "Opposite quadrant projects proportionally")]
        public void ProjectWorldToTexel_PointInsideFloor_ReturnsExpectedTexel(
            float worldX, float worldZ,
            int expectedX, int expectedY)
        {
            var actual = FloorProjection.ProjectWorldToTexel(
                new Vector2(worldX, worldZ),
                FloorCenterAtOrigin,
                FloorSize,
                Resolution);

            Assert.That(actual, Is.EqualTo(new Vector2Int(expectedX, expectedY)));
        }

        [Test]
        public void ProjectWorldToTexel_OffsetFloorCenter_ProjectsRelativeToCenter()
        {
            var center = new Vector2(100f, 200f);

            var actual = FloorProjection.ProjectWorldToTexel(
                center,
                center,
                FloorSize,
                Resolution);

            Assert.That(actual, Is.EqualTo(new Vector2Int(50, 50)));
        }

        [Test]
        public void ProjectWorldToTexel_PointFarOutsideFloor_ReturnsTexelOutsideTextureBounds()
        {
            var actual = FloorProjection.ProjectWorldToTexel(
                new Vector2(50f, -50f),
                FloorCenterAtOrigin,
                FloorSize,
                Resolution);

            Assert.That(FloorProjection.IsInside(actual, Resolution), Is.False);
        }

        [TestCase(0, 0, true, TestName = "Origin is inside")]
        [TestCase(99, 99, true, TestName = "Last texel is inside")]
        [TestCase(-1, 0, false, TestName = "Negative X is outside")]
        [TestCase(0, -1, false, TestName = "Negative Y is outside")]
        [TestCase(100, 50, false, TestName = "X equal to size is outside")]
        [TestCase(50, 100, false, TestName = "Y equal to size is outside")]
        public void IsInside_BoundaryAndOutsidePoints_ReturnsExpected(int x, int y, bool expected)
        {
            var actual = FloorProjection.IsInside(new Vector2Int(x, y), Resolution);

            Assert.That(actual, Is.EqualTo(expected));
        }
    }
}
