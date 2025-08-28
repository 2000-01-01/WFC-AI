using System.Collections.Generic;
using UnityEngine;

public class Cell : MonoBehaviour
{
    public Tile collapsedTile { get; private set; }
    public Vector2Int gridPosition;
    public List<Tile> domain = new List<Tile>();
    public bool IsCollapsed => collapsed;
    public int RemainingOptionCount => domain.Count;

    private bool collapsed = false;
    private GameObject currentVisual;

    public void Initialize(Tile[] allTiles)
    {
        domain = new List<Tile>(allTiles);
        collapsed = false;
        UpdateVisual(null);
    }

    public void ForceCollapse(Tile tile)
    {
        collapsedTile = tile;
        domain = new List<Tile> { tile };
        collapsed = true;
        UpdateVisual(tile);
    }

    public void UpdateVisual(Tile tile)
    {
        if (currentVisual != null)
            Destroy(currentVisual);

        if (tile != null && tile.tilePrefab != null)
        {
            currentVisual = Instantiate(tile.tilePrefab, transform.position, Quaternion.identity, transform);
        }
    }

    public bool ReduceDomain(HashSet<Tile> allowedTiles)
    {
        int before = domain.Count;
        domain.RemoveAll(tile => !allowedTiles.Contains(tile));
        int after = domain.Count;
        return after < before;
    }
}