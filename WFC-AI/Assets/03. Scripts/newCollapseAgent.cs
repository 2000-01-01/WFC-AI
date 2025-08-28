using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using System.Diagnostics;

public class newCollapseAgent : Agent
{
    public newWaveFunction wave;
    private Stopwatch stopwatch;
    private int lastTileIndex = -1;

    private Dictionary<Tile, int> _tileCounts = new Dictionary<Tile, int>();

    public override void OnEpisodeBegin()
    {
        wave.Initialize();
        stopwatch = new Stopwatch();
        stopwatch.Start();
        
        lastTileIndex = -1; 
        
        RequestDecision();
    }
    
    public override void CollectObservations(VectorSensor sensor)
    {
        int totalCells = wave.gridSize * wave.gridSize;
        if (totalCells == 0)
        {
            int spaceSize = 2 + (wave.tiles.Length * 2); 
            for(int i = 0; i < spaceSize; i++)
            {
                sensor.AddObservation(0f);
            }
            return;
        }

        sensor.AddObservation((float)wave.collapsedCount / totalCells);
        if (wave.uncollapsedCount > 0)
        {
            float avgEntropy = wave.totalEntropy / wave.uncollapsedCount;
            sensor.AddObservation(avgEntropy / wave.tiles.Length);
        }
        else
        {
            sensor.AddObservation(0f);
        }

        foreach (var tile in wave.tiles)
        {
            if (wave.uncollapsedCount > 0)
            {
                int availableCount = wave.GetAvailableCellCount(tile);
                sensor.AddObservation((float)availableCount / wave.uncollapsedCount);
            }
            else
            {
                sensor.AddObservation(0f);
            }
        }
        for (int i = 0; i < wave.tileUsage.Length; i++)
        {
            if (wave.collapsedCount > 0)
            {
                sensor.AddObservation(wave.tileUsage[i] / wave.collapsedCount);
            }
            else
            {
                sensor.AddObservation(0f);
            }
        }
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        for (int t = 0; t < wave.tiles.Length; t++)
        {
            if (wave.GetLowestEntropyCell(wave.tiles[t]) == null)
            {
                actionMask.SetActionEnabled(0, t, false);
            }
        }
    }


    public override void OnActionReceived(ActionBuffers actions)
    {
        int tileIndex = actions.DiscreteActions[0];

        if (tileIndex == lastTileIndex)
        {
            AddReward(-0.05f);
        }
        lastTileIndex = tileIndex;

        if (tileIndex < 0 || tileIndex >= wave.tiles.Length)
        {
            EndEpisode();
            return;
        }

        var tile = wave.tiles[tileIndex];
        int placeableCount = wave.GetPlaceableCellCount(tile);

        if (placeableCount > 6)
            AddReward(0.02f);
        else if (placeableCount <= 2)
            AddReward(-0.02f);

        var cell = wave.GetLowestEntropyCell(tile);

        if (cell == null)
        {
            AddReward(-0.3f);
            EndEpisode();
            return;
        }

        wave.CollapseCell(cell, tile);

        int similarNeighbors = 0;
        foreach (var pos in wave.GetNeighborsNonAlloc(cell.gridPosition))
        {
            if (wave.cellMap.TryGetValue(pos, out var neighbor) &&
                neighbor.IsCollapsed &&
                neighbor.collapsedTile == tile)
            {
                similarNeighbors++;
            }
        }

        if (similarNeighbors >= 2)
        {
            AddReward(similarNeighbors * 0.01f);
        }

        if (wave.IsComplete())
        {
            EvaluateFinalReward();
        }
        else
        {
            RequestDecision();
        }
    }

    private void EvaluateFinalReward()
    {
        stopwatch.Stop();
        UnityEngine.Debug.Log($"맵 생성 소요 시간: {stopwatch.ElapsedMilliseconds}ms");

        int totalCells = wave.cells.Count;
        int collapsedCount = wave.collapsedCount;

        if (totalCells == 0 || collapsedCount == 0)
        {
            SetReward(0f);
            EndEpisode();
            return;
        }

        _tileCounts.Clear();
        foreach (var cell in wave.cells)
        {
            if (cell.IsCollapsed)
            {
                if (_tileCounts.ContainsKey(cell.collapsedTile))
                {
                    _tileCounts[cell.collapsedTile]++;
                }
                else
                {
                    _tileCounts[cell.collapsedTile] = 1;
                }
            }
        }

        int mostCommonCount = 0;
        if (_tileCounts.Count > 0)
        {
            foreach (var count in _tileCounts.Values)
            {
                if (count > mostCommonCount)
                {
                    mostCommonCount = count;
                }
            }
        }

        if (mostCommonCount > totalCells * 0.5f)
        {
            UnityEngine.Debug.LogWarning("타일 과다");
            SetReward(0.0f);
            EndEpisode();
            return;
        }

        int isolatedCells = 0;
        float similarityScore = 0f;

        foreach (var c in wave.cells)
        {
            if (!c.IsCollapsed) continue;
            var neighbors = wave.GetNeighborsNonAlloc(c.gridPosition);
            int same = 0;
            int total = 0;
            foreach (var pos in neighbors)
            {
                if (wave.cellMap.TryGetValue(pos, out var n) && n.IsCollapsed)
                {
                    total++;
                    if (n.collapsedTile == c.collapsedTile)
                        same++;
                }
            }
            if (total > 0)
            {
                float localRatio = same / (float)total;
                similarityScore += localRatio;
                if (same <= 1) isolatedCells++;
            }
        }

        float avgSimilarity = similarityScore / collapsedCount;
        float isolationPenalty = isolatedCells / (float)collapsedCount;

        int distinctTileCount = _tileCounts.Count;

        float diversityBonus = (distinctTileCount >= 5)
            ? 0.5f
            : Mathf.Clamp01((distinctTileCount - 1) / 3f) * 0.2f;

        float dominantRatio = mostCommonCount / (float)collapsedCount;
        float dominancePenalty = Mathf.Clamp01((dominantRatio - 0.25f) * 3.0f);

        float finalReward = avgSimilarity - isolationPenalty + diversityBonus - dominancePenalty;
        finalReward = Mathf.Clamp(finalReward, 0f, 1f);

        SetReward(finalReward);
        EndEpisode();
    }

    private void Start()
    {
        wave.OnDomainWipeout = () =>
        {
            SetReward(-0.5f);
            EndEpisode();
        };
    }
}