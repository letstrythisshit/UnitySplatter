Shader "UnitySplatter/GaussianSplatMobile"
{
    Properties
    {
        _OpacityMultiplier("Opacity Multiplier", Range(0, 1)) = 1.0
        _ScaleMultiplier("Scale Multiplier", Range(0.1, 5.0)) = 1.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            Name "GaussianSplatMobilePass"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            // Structured buffers for splat data (mobile GPUs support these on ES 3.1+)
            StructuredBuffer<float3> _Positions;
            StructuredBuffer<float3> _Scales;
            StructuredBuffer<float4> _Rotations;
            StructuredBuffer<float4> _Colors;
            StructuredBuffer<float> _Opacities;

            // Shader properties
            float _OpacityMultiplier;
            float _ScaleMultiplier;

            struct appdata
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                half4 color : COLOR;
                half opacity : TEXCOORD0;
                half pointSize : PSIZE;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Simplified quaternion rotation (only Z-axis approximation for mobile)
            float3 ApplyRotation(float3 v, float4 q)
            {
                // Simplified rotation for mobile - just use quaternion W component
                // This is an approximation but much faster
                float angle = 2.0 * acos(q.w);
                float s = sin(angle * 0.5);
                float c = cos(angle * 0.5);

                // Rotate around approximate axis
                float2 rotated = float2(
                    v.x * c - v.y * s,
                    v.x * s + v.y * c
                );

                return float3(rotated.xy, v.z);
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                uint splatIndex = v.vertexID;

                // Fetch splat data with reduced precision
                half3 position = (half3)_Positions[splatIndex];
                half3 scale = (half3)_Scales[splatIndex] * _ScaleMultiplier;
                half4 color = (half4)_Colors[splatIndex];
                half opacity = (half)_Opacities[splatIndex];

                // Transform to world space (simplified)
                float4 worldPos = mul(unity_ObjectToWorld, float4(position, 1.0));

                // Calculate view space position
                float4 viewPos = mul(UNITY_MATRIX_V, worldPos);

                // Calculate clip space position
                o.pos = mul(UNITY_MATRIX_P, viewPos);

                // Calculate screen-space point size (simplified formula for mobile)
                half maxScale = max(max(scale.x, scale.y), scale.z);
                half distance = length(viewPos.xyz);

                // Simpler point size calculation
                o.pointSize = (maxScale * 200.0) / max(distance, 1.0);
                o.pointSize = clamp(o.pointSize, 1.0, 256.0); // Lower max for mobile

                // Pass color and opacity
                o.color = color;
                o.opacity = opacity * _OpacityMultiplier;

                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                // Get point coordinate (0 to 1)
                half2 pointCoord = i.pos.xy / i.pointSize;

                // Center and calculate distance
                half2 centered = pointCoord * 2.0 - 1.0;
                half dist2 = dot(centered, centered);

                // Early discard for fragments outside the splat
                clip(1.0 - dist2);

                // Simplified Gaussian falloff (faster on mobile)
                half gaussian = 1.0 - dist2; // Linear falloff instead of exp
                gaussian = gaussian * gaussian; // Square for smoother falloff

                // Apply opacity
                half finalOpacity = i.opacity * gaussian;

                // Simple color output (no lighting for mobile)
                return half4(i.color.rgb, finalOpacity);
            }
            ENDCG
        }
    }

    // Fallback to even simpler shader
    Fallback "Mobile/Particles/Alpha Blended"
}
