using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Tile", menuName = "WFC/Tile")]
public class Tile : ScriptableObject
{
    public string tileName;
    public GameObject tilePrefab;

    [Tooltip("이웃에 올 수 있는 타일들")]
    public List<Tile> compatibleNeighbors;

    public List<Tile> GetCompatibleNeighbors()
    {
        return new List<Tile>(compatibleNeighbors);
    }
}