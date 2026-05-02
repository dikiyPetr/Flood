namespace Floor
{
    /// <summary>
    /// Состояние клетки сетки арены.
    /// </summary>
    public enum CellState : byte
    {
        Empty = 0,
        Territory = 1,
        Line = 2,
        Obstacle = 3,
    }
}
