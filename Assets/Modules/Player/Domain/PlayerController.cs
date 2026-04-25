using UnityEngine;

namespace Player
{
    /// <summary>
    /// Управляет передвижением игрока: читает ввод через сгенерированный <see cref="InputSystem_Actions"/>,
    /// рассчитывает смещение через <see cref="PlayerMovementCalculator"/> и применяет его к
    /// <see cref="CharacterController"/>.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private PlayerConfig _config;

        private CharacterController _characterController;
        private PlayerMovementCalculator _calculator;
        private InputSystem_Actions _inputs;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _calculator = new PlayerMovementCalculator(_config);
            _inputs = new InputSystem_Actions();
        }

        private void OnEnable()
        {
            _inputs.Player.Enable();
        }

        private void OnDisable()
        {
            _inputs.Player.Disable();
        }

        private void OnDestroy()
        {
            _inputs?.Dispose();
        }

        private void Update()
        {
            var input = _inputs.Player.Move.ReadValue<Vector2>();
            var isSprinting = _inputs.Player.Sprint.IsPressed();
            var step = _calculator.Calculate(input, isSprinting, Time.deltaTime);

            _characterController.Move(step.Displacement);

            if (step.FacingDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(step.FacingDirection);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                _config.RotationSpeed * Time.deltaTime);
        }
    }
}
