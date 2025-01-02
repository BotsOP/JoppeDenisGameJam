using System.Runtime.CompilerServices;
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
    
    public NativeParallelHashMap<uint, int> amountObjectsInCell; //hoeveel objecten je in een cell hebt
    private NativeParallelMultiHashMap<uint, int> objects; //de indexen die wijzen naar de objecten in een cell
    [NativeDisableContainerSafetyRestriction]
    private NativeArray<Matrix4x4> enemyTransforms;
    private NativeArray<float2> precomputedBoundSizes;

    public NativeQuadTree(int maxObjects, int maxDepth, int objectsPerNode, float2 boundsSize, NativeArray<Matrix4x4> enemyTransforms)
    {
        this.maxDepth = maxDepth;
        this.objectsPerNode = objectsPerNode;
        
        amountObjectsInCell = new NativeParallelHashMap<uint, int>(maxObjects, Allocator.Persistent);
        objects = new NativeParallelMultiHashMap<uint, int>(maxObjects / objectsPerNode, Allocator.Persistent);
        precomputedBoundSizes = new NativeArray<float2>(maxDepth + 2, Allocator.Persistent);
        for (int i = 0; i < maxDepth + 2; i++)
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

    public void InsertPoint(int objectIndex, float2 position)
    {
        Insert(0, -1, objectIndex, position);
    }

    private void Insert(uint parentIndex, int depth, int objectIndex, float2 position)
    {
        uint quadChildIndex = GetQuadIndex(parentIndex, depth, position);
        uint localIndex = GetChildIndex(parentIndex, quadChildIndex);
        amountObjectsInCell.TryAdd(localIndex, 0);
        int amount = amountObjectsInCell[localIndex];
            
        depth++;
        if (amount >= objectsPerNode && depth != maxDepth)
        {
            if (amount != int.MaxValue)
            {
                Subdivide(localIndex, depth);
            }
            Insert(localIndex, depth, objectIndex, position);
            return;
        }
                
        amountObjectsInCell[localIndex]++;
        objects.Add(localIndex, objectIndex);
    }

    private void Subdivide(uint cellIndex, int depth)
    {
        amountObjectsInCell[cellIndex] = int.MaxValue;
        int objectCount = 0;
        NativeArray<int> objectsInCell = new NativeArray<int>(objectsPerNode, Allocator.Temp);
        foreach (var tempObj in objects.GetValuesForKey(cellIndex))
            objectsInCell[objectCount++] = tempObj;

        objects.Remove(cellIndex);

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
    private float2 GetCellBounds(uint cellId, int depth)
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
    private uint GetChildIndex(uint parentIndex, uint childIndex)
    {
        return (childIndex | 4) | (parentIndex << 3);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint GetQuadIndex(uint cellIndex, int depth, float2 point)
    {
        float2 centerCellPosition = GetCellBounds(cellIndex, depth);
        uint x = point.x > centerCellPosition.x ? (uint)1 : 0;
        return point.y > centerCellPosition.y ? x | 2 : x;
    }
    
    //Debug visual
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
}
