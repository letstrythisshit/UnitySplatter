Shader "UnitySplatter/GaussianSplat"
{
    Properties
    {
        _GlobalScale ("Global Scale", Float) = 1
        _OpacityMultiplier ("Opacity Multiplier", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "UnityCG.cginc"

            StructuredBuffer<float3> _SplatBuffer_Position;
            StructuredBuffer<float3> _SplatBuffer_Scale;
            StructuredBuffer<float4> _SplatBuffer_Rotation;
            StructuredBuffer<float4> _SplatBuffer_Color;

            float _GlobalScale;
            float _OpacityMultiplier;

            struct SplatData
            {
                float3 position;
                float3 scale;
                float4 rotation;
                float4 color;
            };

            StructuredBuffer<SplatData> _SplatBuffer;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
            };

            float3x3 QuaternionToMatrix(float4 q)
            {
                float3x3 m;
                float x2 = q.x + q.x;
                float y2 = q.y + q.y;
                float z2 = q.z + q.z;

                float xx = q.x * x2;
                float yy = q.y * y2;
                float zz = q.z * z2;
                float xy = q.x * y2;
                float xz = q.x * z2;
                float yz = q.y * z2;
                float wx = q.w * x2;
                float wy = q.w * y2;
                float wz = q.w * z2;

                m[0][0] = 1.0 - (yy + zz);
                m[0][1] = xy - wz;
                m[0][2] = xz + wy;
                m[1][0] = xy + wz;
                m[1][1] = 1.0 - (xx + zz);
                m[1][2] = yz - wx;
                m[2][0] = xz - wy;
                m[2][1] = yz + wx;
                m[2][2] = 1.0 - (xx + yy);

                return m;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                SplatData splat = _SplatBuffer[input.vertexID];
                float3x3 rot = QuaternionToMatrix(splat.rotation);
                float3 scale = splat.scale * _GlobalScale;
                float3 worldPos = splat.position;
                float4 positionCS = UnityObjectToClipPos(float4(worldPos, 1));

                output.positionCS = positionCS;
                output.color = float4(splat.color.rgb, splat.color.a * _OpacityMultiplier);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return half4(input.color.rgb, input.color.a);
            }
            ENDHLSL
        }
    }
}
