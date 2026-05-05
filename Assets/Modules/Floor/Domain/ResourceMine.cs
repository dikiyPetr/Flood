using UnityEngine;

namespace Floor
{
    /// <summary>
    /// Шахта ресурса (GDD §3.6). Опрашивает состояние своей клетки в <see cref="ArenaState.Grid"/>;
    /// в <see cref="CellState.Territory"/> регистрирует свой rate (`AmountPerTick / IntervalSeconds`,
    /// ед/сек) в <see cref="ResourceBankAccumulator"/> — фактическим докидыванием в банк занимается
    /// аккумулятор. В <see cref="CellState.Empty"/>/<see cref="CellState.Line"/> rate снят, шахта
    /// копит во внутренний float-сток с капом. На переходе в Territory — мгновенный
    /// <see cref="ResourceBankAccumulator.AddBurst"/> на целую часть стока (быстрый feedback на
    /// замыкание петли). С банком напрямую не общается.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResourceMine : MonoBehaviour
    {
        [SerializeField] private ResourceMineConfig _config;
        [SerializeField] private ResourceBankAccumulator _accumulator;
        [SerializeField] private ArenaState _arena;

        private Vector2Int _cell;
        private float _ratePerSecond;
        private float _voidStock;
        private CellState _lastState;
        private bool _ready;

        // Start, не Awake: ArenaState.Awake создаёт грид; порядок Awake между MB не гарантирован,
        // а на момент Start все Awake уже отработали.
        private void Start()
        {
            var bank = _accumulator.Bank;
            if (bank == null) { Debug.LogError($"[Mine] {name}: accumulator has no bank"); return; }
            if (_config.Resource != bank.Resource)
            {
                Debug.LogError($"[Mine] {name}: resource mismatch — config={_config.Resource}, bank={bank.Resource}");
                return;
            }

            _ratePerSecond = _config.AmountPerTick / _config.IntervalSeconds;

            var pos = transform.position;
            var worldXZ = new Vector2(pos.x, pos.z);
            var floor = _arena.Floor;
            _cell = _arena.Grid.WorldToCell(worldXZ, floor.FloorCenterXZ, floor.WorldSize);
            _lastState = _arena.Grid.IsInside(_cell) ? _arena.Grid.Get(_cell) : CellState.Empty;
            _ready = true;

            // Если стартанули уже в зоне — сразу регистрируем rate, иначе первый кадр
            // упустит секцию «вошли в Territory» (lastState == current с самого старта).
            if (_lastState == CellState.Territory)
            {
                _accumulator.SetRate(this, _ratePerSecond);
            }
        }

        private void OnDisable()
        {
            // Teardown-гард: Unity-overridden == возвращает true для Destroyed-объекта,
            // если accumulator был разрушен раньше шахты при выгрузке сцены.
            if (_accumulator != null) _accumulator.SetRate(this, 0f);
        }

        private void Update()
        {
            if (!_ready) return;
            if (!_arena.Grid.IsInside(_cell)) return;

            var current = _arena.Grid.Get(_cell);

            if (current == CellState.Territory)
            {
                if (_lastState != CellState.Territory)
                {
                    var flush = Mathf.FloorToInt(_voidStock);
                    if (flush > 0) _accumulator.AddBurst(flush);
                    _voidStock = 0f;
                    _accumulator.SetRate(this, _ratePerSecond);
                }
            }
            else
            {
                if (_lastState == CellState.Territory)
                {
                    _accumulator.SetRate(this, 0f);
                }
                _voidStock = Mathf.Min(_voidStock + _ratePerSecond * Time.deltaTime, _config.VoidStockCap);
            }

            _lastState = current;
        }
    }
}
