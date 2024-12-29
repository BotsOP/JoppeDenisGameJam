using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

[BurstCompile]
public class EnemyManager : MonoBehaviour
{
    [SerializeField] private float speed;
    [SerializeField] private int startAmountEnemies = 100;
    [SerializeField] private Mesh mesh;
    [SerializeField] private Material material;
    
    private RenderParams renderParams;
    private List<Matrix4x4> matrices = new List<Matrix4x4>();
    private List<Enemy> enemies = new List<Enemy>();
    
    private ComputeBuffer enemyDataBuffer;

    private void OnDisable()
    {
        enemyDataBuffer?.Dispose();
    }

    private void Awake()
    {
        renderParams = new RenderParams(material);
        enemyDataBuffer = new ComputeBuffer(startAmountEnemies, sizeof(int));
        
        for (int i = 0; i < startAmountEnemies; i++)
        {
            Vector3 pos = GetRandomPosition(10, Random.Range(0, Mathf.PI * 2));
            AddEnemy(pos);
        }
    }

    private void Update()
    {
        Graphics.RenderMeshInstanced(renderParams, mesh, 0, matrices);

        for (int i = 0; i < matrices.Count; i++)
        {
            MoveEnemy(i);
        }
    }

    private void MoveEnemy(int index)
    {
        Vector3 dirToCenter = -matrices[index].GetPosition().normalized * speed;
        Matrix4x4 matrix = matrices[index];
        matrix.m03 += dirToCenter.x;
        matrix.m13 += dirToCenter.y;
        matrices[index] = matrix;
    }

    private void AddEnemy(Vector3 position)
    {
        Matrix4x4 matrix = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);
        matrix.m23 = -1;
        matrices.Add(matrix);
        enemies.Add(new Enemy(10));
    }

    private Vector3 GetRandomPosition(float radius, float angle)
    {
        return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
    }
}

public struct Enemy
{
    public float health;
    public Enemy(float health)
    {
        this.health = health;
    }
}