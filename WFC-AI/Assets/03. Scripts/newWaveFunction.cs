using System.Collections.Generic;
using UnityEngine;
using Priority_Queue;

public class newWaveFunction : MonoBehaviour
{
    [SerializeField] private float hexWidth = 1f;
    [SerializeField] private float hexHeight = 1f;

    public int gridSize = 100;
    public Tile[] tiles;
    public Cell cellPrefab;

    public List<Cell> cells = new List<Cell>();
    public Dictionary<Vector2Int, Cell> cellMap = new Dictionary<Vector2Int, Cell>();

    private SimplePriorityQueue<Cell, int> cellQueue;
    public System.Action OnDomainWipeout;

    public int collapsedCount { get; private set; }
    public int uncollapsedCount { get; private set; }
    public float totalEntropy { get; private set; }
    public float[] tileUsage;

    private List<Vector2Int> _neighborCache = new List<Vector2Int>(6);
    private HashSet<Tile> _allowedNeighborCache = new HashSet<Tile>();

    public void Initialize()
    {
        ClearCells();

        cellQueue = new SimplePriorityQueue<Cell, int>();

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                Vector3 worldPos = HexToWorld(pos);
                Cell cell = Instantiate(cellPrefab, worldPos, Quaternion.identity, transform);
                cell.Initialize(tiles);
                cell.gridPosition = pos;

                cells.Add(cell);
                cellMap[pos] = cell;

                cellQueue.Enqueue(cell, cell.RemainingOptionCount);
            }
        }

        collapsedCount = 0;
        uncollapsedCount = cells.Count;

        totalEntropy = 0f;
        foreach (var cell in cells)
        {
            totalEntropy += cell.RemainingOptionCount;
        }

        tileUsage = new float[tiles.Length];
        System.Array.Clear(tileUsage, 0, tileUsage.Length);
    }

    public void ClearCells()
    {
        foreach (var cell in cells)
        {
            if (cell != null) Destroy(cell.gameObject);
        }
        cells.Clear();
        cellMap.Clear();
        cellQueue?.Clear();
    }

    public void CollapseCell(Cell cell, Tile tile)
    {
        if (!cellQueue.Contains(cell)) return;

        totalEntropy -= cell.RemainingOptionCount;
        collapsedCount++;
        uncollapsedCount--;

        for (int i = 0; i < tiles.Length; i++)
        {
            if (tiles[i] == tile)
            {
                tileUsage[i]++;
                break;
            }
        }
        
        cell.ForceCollapse(tile);
        cellQueue.Remove(cell);
        PropagateConstraints(cell);
    }
    
    public Cell GetLowestEntropyCell(Tile tile)
    {
        int minEntropy = int.MaxValue;
        List<Cell> candidates = new List<Cell>();

        foreach (var cell in cellQueue)
        {
            if (cell.domain.Contains(tile))
            {
                int currentEntropy = cell.RemainingOptionCount;
                if (currentEntropy < minEntropy)
                {
                    minEntropy = currentEntropy;
                    candidates.Clear();
                    candidates.Add(cell);
                }
                else if (currentEntropy == minEntropy)
                {
                    candidates.Add(cell);
                }
            }
        }

        if (candidates.Count > 0)
        {
            int randomIndex = Random.Range(0, candidates.Count);
            return candidates[randomIndex];
        }

        return null;
    }

    public bool IsComplete()
    {
        return cellQueue.Count == 0;
    }


    public void PropagateConstraints(Cell startCell)
    {
        Queue<Cell> agenda = new Queue<Cell>();
        agenda.Enqueue(startCell);

        while (agenda.Count > 0)
        {
            Cell current = agenda.Dequeue();
            if (!current.IsCollapsed || current.collapsedTile == null) continue;

            _allowedNeighborCache.Clear();
            foreach(var tile in current.collapsedTile.compatibleNeighbors)
            {
                _allowedNeighborCache.Add(tile);
            }

            foreach (var neighborPos in GetNeighborsNonAlloc(current.gridPosition))
            {
                if (!cellMap.TryGetValue(neighborPos, out var neighbor) || neighbor.IsCollapsed) continue;

                bool reduced = neighbor.ReduceDomain(_allowedNeighborCache);

                if (reduced)
                {
                    if (cellQueue.Contains(neighbor))
                    {
                        cellQueue.UpdatePriority(neighbor, neighbor.RemainingOptionCount);
                    }
                    agenda.Enqueue(neighbor);
                }

                if (neighbor.RemainingOptionCount == 0)
                {
                    Debug.LogWarning($"Wipeout at {neighbor.gridPosition} due to {current.gridPosition} with {current.collapsedTile.name}");
                    OnDomainWipeout?.Invoke();
                    return;
                }
            }
        }
    }

    public int GetAvailableCellCount(Tile tile)
    {
        int count = 0;
        foreach (var cell in cellQueue)
        {
            if (cell.domain.Contains(tile)) count++;
        }
        return count;
    }

    public int GetPlaceableCellCount(Tile tile)
    {
        int count = 0;
        foreach (var cell in cellQueue)
        {
            if (cell.domain.Contains(tile))
            {
                count++;
            }
        }
        return count;
    }

    private Vector3 HexToWorld(Vector2Int hex)
    {
        float z = hex.x * hexWidth * 0.866f;
        float x = hex.y * hexHeight + (hex.x % 2 == 0 ? 0 : hexHeight * 0.5f);
        return new Vector3(x, 0, z);
    }

    public List<Vector2Int> GetNeighborsNonAlloc(Vector2Int pos)
    {
        _neighborCache.Clear();
        Vector2Int[] offsets = (pos.x % 2 == 0) ? evenQ_Even : evenQ_Odd;
        foreach (var offset in offsets)
        {
            _neighborCache.Add(pos + offset);
        }
        return _neighborCache;
    }

    private static readonly Vector2Int[] evenQ_Even = { new Vector2Int(0, -1), new Vector2Int(+1, -1), new Vector2Int(+1, 0), new Vector2Int(0, +1), new Vector2Int(-1, 0), new Vector2Int(-1, -1) };
    private static readonly Vector2Int[] evenQ_Odd = { new Vector2Int(0, -1), new Vector2Int(+1, 0), new Vector2Int(+1, +1), new Vector2Int(0, +1), new Vector2Int(-1, +1), new Vector2Int(-1, 0) };

    private void OnDrawGizmos()
    {
        if (gridSize > 20 || cellMap == null || cells == null || cells.Count == 0) return;
        
        Gizmos.color = Color.cyan;
        foreach (var cell in cells)
        {
            if (cell == null) continue;
            Vector3 from = cell.transform.position;
            foreach (var neighborPos in GetNeighborsNonAlloc(cell.gridPosition))
            {
                if (cellMap.TryGetValue(neighborPos, out var neighbor) && neighbor != null)
                {
                    Gizmos.DrawLine(from, neighbor.transform.position);
                }
            }
        }
    }
}