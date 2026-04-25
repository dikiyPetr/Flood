using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace Player.Tests
{
    [TestFixture]
    public sealed class PlayerMovementCalculatorTest
    {
        private const float MoveSpeed = 5f;
        private const float SprintMultiplier = 2f;
        private const float RotationSpeed = 720f;
        private const float DeltaTime = 0.02f;
        private const float Tolerance = 1e-4f;

        private static PlayerMovementCalculator CreateSut()
        {
            var config = ScriptableObject.CreateInstance<PlayerConfig>();
            config.SetForTests(MoveSpeed, SprintMultiplier, RotationSpeed);
            return new PlayerMovementCalculator(config);
        }

        [Test]
        public void Calculate_ZeroInput_DisplacementIsZero()
        {
            var sut = CreateSut();

            var actual = sut.Calculate(Vector2.zero, isSprinting: false, DeltaTime);

            Assert.That(actual.Displacement, Is.EqualTo(Vector3.zero).Using(Vector3EqualityComparer.Instance));
        }

        [Test]
        public void Calculate_ZeroInput_FacingIsZero()
        {
            var sut = CreateSut();

            var actual = sut.Calculate(Vector2.zero, isSprinting: false, DeltaTime);

            Assert.That(actual.FacingDirection, Is.EqualTo(Vector3.zero).Using(Vector3EqualityComparer.Instance));
        }

        [TestCase(0f, 1f, 0f, 0f, 1f, TestName = "Forward → +Z")]
        [TestCase(0f, -1f, 0f, 0f, -1f, TestName = "Backward → -Z")]
        [TestCase(1f, 0f, 1f, 0f, 0f, TestName = "Right → +X")]
        [TestCase(-1f, 0f, -1f, 0f, 0f, TestName = "Left → -X")]
        public void Calculate_UnitAxisInput_DisplacementIsAlongAxis(
            float inputX, float inputY,
            float dirX, float dirY, float dirZ)
        {
            var sut = CreateSut();
            var expected = new Vector3(dirX, dirY, dirZ) * (MoveSpeed * DeltaTime);

            var actual = sut.Calculate(new Vector2(inputX, inputY), isSprinting: false, DeltaTime);

            Assert.That(actual.Displacement, Is.EqualTo(expected).Using(Vector3EqualityComparer.Instance));
        }

        [TestCase(0f, 1f, 0f, 0f, 1f, TestName = "Forward facing")]
        [TestCase(0f, -1f, 0f, 0f, -1f, TestName = "Backward facing")]
        [TestCase(1f, 0f, 1f, 0f, 0f, TestName = "Right facing")]
        [TestCase(-1f, 0f, -1f, 0f, 0f, TestName = "Left facing")]
        public void Calculate_UnitAxisInput_FacingMatchesAxis(
            float inputX, float inputY,
            float facingX, float facingY, float facingZ)
        {
            var sut = CreateSut();
            var expected = new Vector3(facingX, facingY, facingZ);

            var actual = sut.Calculate(new Vector2(inputX, inputY), isSprinting: false, DeltaTime);

            Assert.That(actual.FacingDirection, Is.EqualTo(expected).Using(Vector3EqualityComparer.Instance));
        }

        [Test]
        public void Calculate_DiagonalUnitInput_DisplacementMagnitudeEqualsMoveSpeedDt()
        {
            var sut = CreateSut();
            var diagonal = new Vector2(0.7071068f, 0.7071068f);

            var actual = sut.Calculate(diagonal, isSprinting: false, DeltaTime);

            Assert.That(actual.Displacement.magnitude, Is.EqualTo(MoveSpeed * DeltaTime).Within(Tolerance));
        }

        [Test]
        public void Calculate_OverdrivenInput_DisplacementMagnitudeClampedToMoveSpeedDt()
        {
            var sut = CreateSut();

            var actual = sut.Calculate(new Vector2(2f, 2f), isSprinting: false, DeltaTime);

            Assert.That(actual.Displacement.magnitude, Is.EqualTo(MoveSpeed * DeltaTime).Within(Tolerance));
        }

        [Test]
        public void Calculate_OverdrivenInput_FacingMagnitudeIsOne()
        {
            var sut = CreateSut();

            var actual = sut.Calculate(new Vector2(2f, 2f), isSprinting: false, DeltaTime);

            Assert.That(actual.FacingDirection.magnitude, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Calculate_SprintEnabled_AppliesSprintMultiplier()
        {
            var sut = CreateSut();
            var expected = MoveSpeed * SprintMultiplier * DeltaTime;

            var actual = sut.Calculate(new Vector2(1f, 0f), isSprinting: true, DeltaTime);

            Assert.That(actual.Displacement.x, Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void Calculate_ZeroDeltaTime_DisplacementIsZero()
        {
            var sut = CreateSut();

            var actual = sut.Calculate(new Vector2(1f, 1f), isSprinting: false, deltaTime: 0f);

            Assert.That(actual.Displacement, Is.EqualTo(Vector3.zero).Using(Vector3EqualityComparer.Instance));
        }
    }
}
