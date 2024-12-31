using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

public struct NativeQuadTree
{
    private readonly int maxDepth;
    private readonly int objectsPerNode;
    private readonly float2 boundsSize;
    
    public NativeParallelHashMap<uint, int> amountObjectsInCell; //hoeveel objecten je in een cell hebt
    private NativeParallelMultiHashMap<uint, int> objects; //de indexen die wijzen naar de objecten in een cell
    [NativeDisableContainerSafetyRestriction]
    private NativeArray<Matrix4x4> enemyTransforms;
    private NativeArray<float2> precomputedBoundSizes;

    public NativeQuadTree(int maxObjects, int maxDepth, int objectsPerNode, float2 boundsSize, NativeArray<Matrix4x4> enemyTransforms)
    {
        this.maxDepth = maxDepth;
        this.objectsPerNode = objectsPerNode;
        this.boundsSize = boundsSize;
        
        amountObjectsInCell = new NativeParallelHashMap<uint, int>(maxObjects, Allocator.Persistent);
        objects = new NativeParallelMultiHashMap<uint, int>(maxObjects / objectsPerNode, Allocator.Persistent);
        precomputedBoundSizes = new NativeArray<float2>(maxDepth + 1, Allocator.Persistent);
        for (int i = 0; i < maxDepth + 1; i++)
        {
            float pow = math.pow(2, 1 + i);
            precomputedBoundSizes[i] = new float2(boundsSize.x / pow, boundsSize.y / pow);
        }
        this.enemyTransforms = enemyTransforms;
    }

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

    private readonly static ProfilerMarker insert = new ProfilerMarker("Quadtree.Insert");
    public bool Insert(int objectIndex, float2 position)
    {
        insert.Begin();
        int depth = 0;

        for (uint i = 0; i < 4; i++)
        {
            uint localIndex = GetChildIndex(0, i);
            if (!amountObjectsInCell.TryGetValue(localIndex, out int amount))
            {
                amountObjectsInCell.Add(localIndex, 0);
            }
            
            float4 bounds = GetCellBounds(localIndex, depth);
            if (IsPointInsideBounds(bounds, position))
            {
                if (amount >= objectsPerNode)
                {
                    bool child1 = !amountObjectsInCell.TryGetValue(GetChildIndex(localIndex, 0), out _);
                    bool child2 = !amountObjectsInCell.TryGetValue(GetChildIndex(localIndex, 1), out _);
                    bool child3 = !amountObjectsInCell.TryGetValue(GetChildIndex(localIndex, 2), out _);
                    bool child4 = !amountObjectsInCell.TryGetValue(GetChildIndex(localIndex, 3), out _);
                    if (child1 &&
                        child2 &&
                        child3 &&
                        child4)
                    {
                        Subdivide(localIndex, depth);
                    }
                    
                    return InsertNext(localIndex, depth, objectIndex, position);
                }
                
                amountObjectsInCell[localIndex]++;
                objects.Add(localIndex, objectIndex);
                insert.End();
                return true;
            }
        }

        insert.End();
        return false;
    }

    private bool InsertNext(uint parentIndex, int depth, int objectIndex, float2 position)
    {
        depth++;
        for (uint i = 0; i < 4; i++)
        {
            uint localIndex = GetChildIndex(parentIndex, i);
            if (!amountObjectsInCell.TryGetValue(localIndex, out int amount))
            {
                amountObjectsInCell.Add(localIndex, 0);
            }
            
            float4 bounds = GetCellBounds(localIndex, depth);
            if (IsPointInsideBounds(bounds, position))
            {
                if (amount >= objectsPerNode)
                {
                    bool child1 = !amountObjectsInCell.TryGetValue(GetChildIndex(localIndex, 0), out _);
                    bool child2 = !amountObjectsInCell.TryGetValue(GetChildIndex(localIndex, 1), out _);
                    bool child3 = !amountObjectsInCell.TryGetValue(GetChildIndex(localIndex, 2), out _);
                    bool child4 = !amountObjectsInCell.TryGetValue(GetChildIndex(localIndex, 3), out _);
                    if (child1 &&
                        child2 &&
                        child3 &&
                        child4)
                    {
                        Subdivide(localIndex, depth);
                    }
                    return InsertNext(localIndex, depth, objectIndex, position);
                }
                
                amountObjectsInCell[localIndex]++;
                objects.Add(localIndex, objectIndex);
                insert.End();
                return true;
            }
        }
        
        insert.End();
        return false;
    }

    private readonly static ProfilerMarker subdivide = new ProfilerMarker("Quadtree.Subdivide");
    private void Subdivide(uint cellIndex, int depth)
    {
        subdivide.Begin();
        depth++;

        amountObjectsInCell[cellIndex] = 9999;
        int objectCount = 0;
        NativeArray<int> objectsInCell = new NativeArray<int>(objectsPerNode, Allocator.Temp);
        foreach (var tempObj in objects.GetValuesForKey(cellIndex))
            objectsInCell[objectCount++] = tempObj;

        objects.Remove(cellIndex);

        for (uint i = 0; i < 4; i++)
        {
            uint localIndex = GetChildIndex(cellIndex, i);
            float4 bounds = GetCellBounds(localIndex, depth);
            for (int j = 0; j < objectCount; j++)
            {
                if (!IsPointInsideBounds(bounds, new float2(enemyTransforms[objectsInCell[j]].m03, enemyTransforms[objectsInCell[j]].m13)))
                    continue;

                objects.Add(localIndex, objectsInCell[j]);
                
                // if (!amountObjectsInCell.TryGetValue(localIndex, out _))
                // {
                //     amountObjectsInCell.TryAdd(localIndex, 1);
                //     continue;
                // }
                amountObjectsInCell.TryAdd(localIndex, 0);
                amountObjectsInCell[localIndex]++;
            }
        }
        subdivide.End();
    }

    
    private readonly static ProfilerMarker cellbounds = new ProfilerMarker("Quadtree.Cellbounds");
    private float4 GetCellBounds(uint cellId, int depth)
    {
        cellbounds.Begin();
        float2 center = float2.zero;
        for (int i = depth; i >= 0 ; i--)
        {
            uint localCell = cellId >> i * 3;
            int iFlipped = depth - i;
            center.x += -precomputedBoundSizes[iFlipped + 1].x + precomputedBoundSizes[iFlipped].x * (int)(localCell & 1);
            center.y += -precomputedBoundSizes[iFlipped + 1].y + precomputedBoundSizes[iFlipped].y * (int)((localCell & 2) >> 1);
        }
        cellbounds.End();
        return new float4(center, precomputedBoundSizes[depth].x, precomputedBoundSizes[depth].y);
    }
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
                return i;
            }
        }
        return 10;
    }

    private readonly static ProfilerMarker getChildIndex = new ProfilerMarker("Quadtree.ChildIndex");
    private uint GetChildIndex(uint parentIndex, uint childIndex)
    {
        getChildIndex.Begin();
        parentIndex <<= 3;
        childIndex |= 4;
        uint final = childIndex | parentIndex;
        getChildIndex.End();
        return final;
    }
    
    private readonly static ProfilerMarker isPointInsideBounds = new ProfilerMarker("Quadtree.ChildIndex");
    private bool IsPointInsideBounds(float4 bounds, float2 point)
    {
        isPointInsideBounds.Begin();
        float2 halfSize = new float2(bounds.z * 0.5f, bounds.w * 0.5f);
        float2 min = bounds.xy - halfSize;
        float2 max = bounds.xy + halfSize;

        // Check if the point is within bounds
        bool pointInsideBounds = math.all(point >= min & point <= max);
        isPointInsideBounds.End();
        return pointInsideBounds;
    }
}
