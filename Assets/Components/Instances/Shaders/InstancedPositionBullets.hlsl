#ifndef SHADER_GRAPH_SUPPORT_H
#define SHADER_GRAPH_SUPPORT_H

#include "Matrix.hlsl"

struct Bullet
{
    float damage;
    float speed;
    int penetrations;
    
    float2 position;
    float2 dir;
};

int amountEnemies;
StructuredBuffer<Bullet> bulletDataBuffer;

float GetAngleBetweenVectors(float2 vectorA, float2 vectorB)
{
    // Calculate the dot product and the magnitudes of the vectors
    float dotProduct = dot(vectorA, vectorB);
    float magnitudeA = length(vectorA);
    float magnitudeB = length(vectorB);

    // Prevent division by zero
    if (magnitudeA == 0.0 || magnitudeB == 0.0)
        return 0.0;

    // Calculate the cosine of the angle
    float cosTheta = clamp(dotProduct / (magnitudeA * magnitudeB), -1.0, 1.0);

    // Calculate the angle in radians and convert to degrees
    return degrees(acos(cosTheta));
}


float4 EulerToQuaternion(float3 euler)
{
    float3 halfAngles = 0.5f * euler;
    float3 sinAngles = sin(halfAngles);
    float3 cosAngles = cos(halfAngles);

    float4 q;
    q.x = sinAngles.x * cosAngles.y * cosAngles.z - cosAngles.x * sinAngles.y * sinAngles.z;
    q.y = cosAngles.x * sinAngles.y * cosAngles.z + sinAngles.x * cosAngles.y * sinAngles.z;
    q.z = cosAngles.x * cosAngles.y * sinAngles.z - sinAngles.x * sinAngles.y * cosAngles.z;
    q.w = cosAngles.x * cosAngles.y * cosAngles.z + sinAngles.x * sinAngles.y * sinAngles.z;

    return q;
}

inline void SetUnityMatrices(uint instanceID, inout float4x4 objectToWorld, inout float4x4 worldToObject)
{
// #if UNITY_ANY_INSTANCING_ENABLED
    float4x4 worldMatrix;
    float angle = GetAngleBetweenVectors(bulletDataBuffer[instanceID].dir, float2(0, 1));
    worldMatrix = compose(float3(bulletDataBuffer[instanceID].position, -1), EulerToQuaternion(float3(0, 0, angle)), float3(0.1, 0.1, 0.1));

    // if(instanceID < amountEnemies)
    // {
    //     worldMatrix = compose(float3(bulletDataBuffer[instanceID].position, -1), EulerToQuaternion(float3(0, 0, angle)), float3(0.1, 0.1, 0.1));
    // }
    // else
    // {
    //     worldMatrix = compose(float3(bulletDataBuffer[instanceID].position, 1), float4(0, 0, 0, 1), float3(0.1, 0.1, 0.1));
    // }

    objectToWorld = worldMatrix;
    
    float3x3 w2oRotation;
    w2oRotation[0] = objectToWorld[1].yzx * objectToWorld[2].zxy - objectToWorld[1].zxy * objectToWorld[2].yzx;
    w2oRotation[1] = objectToWorld[0].zxy * objectToWorld[2].yzx - objectToWorld[0].yzx * objectToWorld[2].zxy;
    w2oRotation[2] = objectToWorld[0].yzx * objectToWorld[1].zxy - objectToWorld[0].zxy * objectToWorld[1].yzx;
    
    float det = dot(objectToWorld[0].xyz, w2oRotation[0]);
    w2oRotation = transpose(w2oRotation);
    w2oRotation *= rcp(det);
    float3 w2oPosition = mul(w2oRotation, -objectToWorld._14_24_34);
    
    worldToObject._11_21_31_41 = float4(w2oRotation._11_21_31, 0.0f);
    worldToObject._12_22_32_42 = float4(w2oRotation._12_22_32, 0.0f);
    worldToObject._13_23_33_43 = float4(w2oRotation._13_23_33, 0.0f);
    worldToObject._14_24_34_44 = float4(w2oPosition, 1.0f);
// #endif
}

void passthroughVec3_float(in float3 In, out float3 Out)
{
    Out = In;
}

void setup()
{
#if UNITY_ANY_INSTANCING_ENABLED
    SetUnityMatrices(unity_InstanceID, unity_ObjectToWorld, unity_WorldToObject);
#endif
}




#endif