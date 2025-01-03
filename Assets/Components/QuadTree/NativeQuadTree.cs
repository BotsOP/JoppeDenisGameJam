using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

public struct NativeQuadTree
{
    private const int MAX_ALLOWED_DEPTH = 12;
    private readonly int maxDepth;
    private readonly int objectsPerNode;
    
    public NativeParallelHashMap<uint, int> amountObjectsInCell; //hoeveel objecten je in een cell hebt
    private NativeParallelMultiHashMap<uint, int> objects; //de indexen die wijzen naar de objecten in een cell
    [NativeDisableContainerSafetyRestriction]
    private readonly NativeArray<Matrix4x4> enemyTransforms;
    private NativeArray<float2> precomputedBoundSizes;
    
    public void Dispose()
    {
        amountObjectsInCell.Dispose();
        objects.Dispose();
        precomputedBoundSizes.Dispose();
    }

    public void Clear()
    {
        amountObjectsInCell.Clear();
        objects.Clear();
    }

    public NativeQuadTree(int maxObjects, int maxDepth, int objectsPerNode, float2 boundsSize, NativeArray<Matrix4x4> enemyTransforms)
    {
        if (maxDepth > MAX_ALLOWED_DEPTH)
        {
            Debug.LogError($"QuadTree: max depth should not be larger then {MAX_ALLOWED_DEPTH}");
        }
        this.maxDepth = maxDepth;
        this.objectsPerNode = objectsPerNode;
        this.enemyTransforms = enemyTransforms;
        
        amountObjectsInCell = new NativeParallelHashMap<uint, int>(maxObjects, Allocator.Persistent);
        objects = new NativeParallelMultiHashMap<uint, int>(maxObjects / objectsPerNode, Allocator.Persistent);
        precomputedBoundSizes = new NativeArray<float2>(maxDepth + 2, Allocator.Persistent);
        for (int i = 0; i < maxDepth + 2; i++)
        {
            float pow = math.pow(2, 1 + i);
            precomputedBoundSizes[i] = new float2(boundsSize.x / pow, boundsSize.y / pow);
        }
    }

    public NativeList<int> Query(NativeList<int> results, NativeList<uint> debugResults, float4 bounds)
    {
        QueryChild(results, debugResults, bounds, 0, -1);
        return results;
    }
    private void QueryChild(NativeList<int> results, NativeList<uint> debugResults, float4 bounds, uint cellIndex, int depth)
    {
        depth++;
        if(depth > maxDepth)
            return;
        
        for (uint i = 0; i < 4; i++)
        {
            uint localCellIndex = GetChildIndex(cellIndex, i);
            float4 cellBounds = GetCellBounds(localCellIndex, depth);
            uint howMuchAreBoundsOverlapping = HowMuchAreBoundsOverlapping(cellBounds, bounds);
            
            if (amountObjectsInCell.TryGetValue(localCellIndex, out int amount) && howMuchAreBoundsOverlapping > 0)
            {
                if (amount == int.MaxValue)
                {
                    if (howMuchAreBoundsOverlapping == 3)
                    {
                        GetAllChildIndexes(results, debugResults, localCellIndex, depth);
                        continue;
                    }
                    QueryChild(results, debugResults, bounds, localCellIndex, depth);
                }

                debugResults.Add(localCellIndex);
                foreach (int tempObj in objects.GetValuesForKey(localCellIndex))
                    results.Add(tempObj);
            }
        }
    }

    private void GetAllChildIndexes(NativeList<int> results, NativeList<uint> debugResults, uint cellIndex, int depth)
    {
        depth++;
        if(depth > maxDepth)
            return;
        
        for (uint i = 0; i < 4; i++)
        {
            uint localCellIndex = GetChildIndex(cellIndex, i);
            
            if (amountObjectsInCell.TryGetValue(localCellIndex, out int amount))
            {
                if (amount == int.MaxValue)
                {
                    GetAllChildIndexes(results, debugResults, localCellIndex, depth);
                    continue;
                }
                
                debugResults.Add(localCellIndex);
                foreach (int tempObj in objects.GetValuesForKey(localCellIndex))
                    results.Add(tempObj);
            }
        }
    }
    
    public void Insert(int objectIndex, float2 position)
    {
        uint cellIndex = 0;
        int depth = -1;
        for (int i = 0; i <= maxDepth; i++)
        {
            uint quadChildIndex = GetQuadIndex(cellIndex, depth, position);
            cellIndex = GetChildIndex(cellIndex, quadChildIndex);
            amountObjectsInCell.TryAdd(cellIndex, 0);
            int amount = amountObjectsInCell[cellIndex];
            
            depth++;
            if (amount >= objectsPerNode && depth != maxDepth)
            {
                if (amount != int.MaxValue)
                {
                    Subdivide(cellIndex, depth, amount);
                }
                continue;
            }
            
            amountObjectsInCell[cellIndex]++;
            objects.Add(cellIndex, objectIndex);
            break;
        }
    }

    private void Subdivide(uint cellIndex, int depth, int amount)
    {
        int objectCount = 0;
        Span<int> objectsInCell = stackalloc int[amount];
        // NativeArray<int> objectsInCell = new NativeArray<int>(objectsPerNode, Allocator.Temp);
        foreach (var tempObj in objects.GetValuesForKey(cellIndex))
            objectsInCell[objectCount++] = tempObj;

        objects.Remove(cellIndex);
        amountObjectsInCell[cellIndex] = int.MaxValue;

        for (int j = 0; j < objectCount; j++)
        {
            uint quadChildIndex = GetQuadIndex(cellIndex, depth, new float2(enemyTransforms[objectsInCell[j]].m03, 
                                                                            enemyTransforms[objectsInCell[j]].m13));
            uint localIndex = GetChildIndex(cellIndex, quadChildIndex);

            objects.Add(localIndex, objectsInCell[j]);
            amountObjectsInCell.TryAdd(localIndex, 0);
            amountObjectsInCell[localIndex]++;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float2 GetCellPosition(uint cellId, int depth)
    {
        float2 center = float2.zero;
        for (int i = depth; i >= 0 ; i--)
        {
            uint localCell = cellId >> i * 3;
            int iFlipped = depth - i;
            center.x += -precomputedBoundSizes[iFlipped + 1].x + precomputedBoundSizes[iFlipped].x * (int)(localCell & 1);
            center.y += -precomputedBoundSizes[iFlipped + 1].y + precomputedBoundSizes[iFlipped].y * (int)((localCell & 2) >> 1);
        }
        return center;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float4 GetCellBounds(uint cellId, int depth)
    {
        float2 center = float2.zero;
        for (int i = depth; i >= 0 ; i--)
        {
            uint localCell = cellId >> i * 3;
            int iFlipped = depth - i;
            center.x += -precomputedBoundSizes[iFlipped + 1].x + precomputedBoundSizes[iFlipped].x * (int)(localCell & 1);
            center.y += -precomputedBoundSizes[iFlipped + 1].y + precomputedBoundSizes[iFlipped].y * (int)((localCell & 2) >> 1);
        }
        return new float4(center, precomputedBoundSizes[depth].x, precomputedBoundSizes[depth].y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint GetChildIndex(uint parentIndex, uint childIndex)
    {
        return (childIndex | 4) | (parentIndex << 3);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint GetQuadIndex(uint cellIndex, int depth, float2 point)
    {
        float2 centerCellPosition = GetCellPosition(cellIndex, depth);
        uint x = point.x > centerCellPosition.x ? (uint)1 : 0;
        return point.y > centerCellPosition.y ? x | 2 : x;
    }
    
    // 0 = not overlapping
    // 1 = overlapping
    // 2 = BoxA is inside BoxB
    private uint HowMuchAreBoundsOverlapping(float4 boxA, float4 boxB)
    {
        float2 halfSizeA = new float2(boxA.z * 0.5f, boxA.w * 0.5f);
        float2 halfSizeB = new float2(boxB.z * 0.5f, boxB.w * 0.5f);

        float2 minA = boxA.xy - halfSizeA;
        float2 maxA = boxA.xy + halfSizeA;
        float2 minB = boxB.xy - halfSizeB;
        float2 maxB = boxB.xy + halfSizeB;

        uint amountAxisInsideBoundsB = minA.x > minB.x && minA.x < maxB.x && minA.y > minB.y && minA.y < maxB.y ? (uint)1 : 0;
        amountAxisInsideBoundsB     = minB.x > minA.x && minB.x < maxA.x && minB.y > minA.y && minB.y < maxA.y ? amountAxisInsideBoundsB | 4 : amountAxisInsideBoundsB;
        amountAxisInsideBoundsB     = minB.x > minA.x && minB.x < maxA.x && minB.y > minA.y && minB.y < maxA.y ? amountAxisInsideBoundsB | 8 : amountAxisInsideBoundsB;
        return                        maxA.x < maxB.x && maxA.x > minB.x && maxA.y < maxB.y && maxA.y > minB.y ? amountAxisInsideBoundsB | 2 : amountAxisInsideBoundsB;
    }
    
    #region Visual

    public float4 GetCellBounds(uint cellId)
    {
        float2 center = float2.zero;
        int depth = GetDepth(cellId);
        for (int i = depth; i >= 0 ; i--)
        {
            uint localCell = cellId >> i * 3;
            int iFlipped = depth - i;
            center.x += -precomputedBoundSizes[iFlipped + 1].x + precomputedBoundSizes[iFlipped].x * (int)(localCell & 1);
            center.y += -precomputedBoundSizes[iFlipped + 1].y + precomputedBoundSizes[iFlipped].y * (int)((localCell & 2) >> 1);
        }
        return new float4(center, precomputedBoundSizes[depth].x, precomputedBoundSizes[depth].y);
    }
    
    

    private readonly static uint[] depthMasks = {
        4,
        32,
        256,
        2048,
        16384,
        131072,
        1048576,
        8388608,
        67108864,
        536870912,
    };
    private int GetDepth(uint cellId)
    {
        for (int i = 9; i >= 0; i--)
        {
            if ((cellId & depthMasks[i]) > 0)
            {
                //1100
                // 4
                // >>
                //0000
                return i;
            }
        }
        return 10;
    }

    #endregion
    
}
