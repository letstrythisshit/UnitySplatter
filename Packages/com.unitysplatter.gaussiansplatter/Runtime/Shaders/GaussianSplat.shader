Shader "UnitySplatter/GaussianSplat"
{
    Properties
    {
        _OpacityMultiplier ("Opacity Multiplier", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"

            StructuredBuffer<float3> _SplatPositions;
            StructuredBuffer<float3> _SplatScales;
            StructuredBuffer<float4> _SplatRotations;
            StructuredBuffer<float4> _SplatColors;
            StructuredBuffer<float> _SplatOpacities;
            float _OpacityMultiplier;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR0;
                float pointSize : PSIZE;
            };

            float3 RotateVector(float3 v, float4 q)
            {
                float3 t = 2.0 * cross(q.xyz, v);
                return v + q.w * t + cross(q.xyz, t);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 position = _SplatPositions[input.vertexID];
                float3 scale = _SplatScales[input.vertexID];
                float4 rotation = _SplatRotations[input.vertexID];
                float4 color = _SplatColors[input.vertexID];
                float opacity = saturate(_SplatOpacities[input.vertexID] * _OpacityMultiplier);

                float3 oriented = RotateVector(float3(0, 0, 1), rotation);
                float size = max(max(scale.x, scale.y), scale.z);
                float4 worldPos = mul(unity_ObjectToWorld, float4(position + oriented * 0.0, 1.0));

                output.positionCS = mul(UNITY_MATRIX_VP, worldPos);
                output.color = float4(color.rgb, opacity);
                output.pointSize = max(1.0, size * 300.0);
                return output;
            }

            float4 Frag(Varyings input, float2 pointCoord : POINTCOORD) : SV_Target
            {
                float2 uv = pointCoord * 2.0 - 1.0;
                float dist2 = dot(uv, uv);
                float alpha = exp(-dist2 * 4.0) * input.color.a;
                return float4(input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
