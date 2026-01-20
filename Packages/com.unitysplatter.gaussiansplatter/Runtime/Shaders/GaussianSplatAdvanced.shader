Shader "UnitySplatter/GaussianSplatAdvanced"
{
    Properties
    {
        _OpacityMultiplier("Opacity Multiplier", Range(0, 1)) = 1.0
        _ScaleMultiplier("Scale Multiplier", Range(0.1, 10.0)) = 1.0
        _DepthOffset("Depth Offset", Float) = 0.0
        _AnisotropyStrength("Anisotropy Strength", Range(0, 1)) = 1.0

        [Toggle(USE_ADVANCED_SHADING)] _UseAdvancedShading("Use Advanced Shading", Float) = 1
        [Toggle(USE_SPECULAR)] _UseSpecular("Use Specular Highlights", Float) = 0
        _Shininess("Shininess", Range(1, 100)) = 20
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 200

        Pass
        {
            Name "GaussianSplatPass"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma multi_compile _ USE_ADVANCED_SHADING
            #pragma multi_compile _ USE_SPECULAR
            #pragma multi_compile _ UNITY_COLORSPACE_GAMMA

            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"

            // Structured buffers for splat data
            StructuredBuffer<float3> _Positions;
            StructuredBuffer<float3> _Scales;
            StructuredBuffer<float4> _Rotations;
            StructuredBuffer<float4> _Colors;
            StructuredBuffer<float> _Opacities;

            // Shader properties
            float _OpacityMultiplier;
            float _ScaleMultiplier;
            float _DepthOffset;
            float _AnisotropyStrength;
            float _Shininess;

            struct appdata
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float opacity : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float3 ellipsoidAxes : TEXCOORD3;
                float pointSize : PSIZE;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Quaternion to rotation matrix
            float3x3 QuaternionToMatrix(float4 q)
            {
                float3x3 m;

                float xx = q.x * q.x;
                float yy = q.y * q.y;
                float zz = q.z * q.z;
                float xy = q.x * q.y;
                float xz = q.x * q.z;
                float yz = q.y * q.z;
                float wx = q.w * q.x;
                float wy = q.w * q.y;
                float wz = q.w * q.z;

                m[0][0] = 1.0 - 2.0 * (yy + zz);
                m[0][1] = 2.0 * (xy - wz);
                m[0][2] = 2.0 * (xz + wy);

                m[1][0] = 2.0 * (xy + wz);
                m[1][1] = 1.0 - 2.0 * (xx + zz);
                m[1][2] = 2.0 * (yz - wx);

                m[2][0] = 2.0 * (xz - wy);
                m[2][1] = 2.0 * (yz + wx);
                m[2][2] = 1.0 - 2.0 * (xx + yy);

                return m;
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                uint splatIndex = v.vertexID;

                // Fetch splat data
                float3 position = _Positions[splatIndex];
                float3 scale = _Scales[splatIndex] * _ScaleMultiplier;
                float4 rotation = _Rotations[splatIndex];
                float4 color = _Colors[splatIndex];
                float opacity = _Opacities[splatIndex];

                // Transform to world space
                float4 worldPos = mul(unity_ObjectToWorld, float4(position, 1.0));
                o.worldPos = worldPos.xyz;

                // Calculate view space position
                float4 viewPos = mul(UNITY_MATRIX_V, worldPos);
                viewPos.z += _DepthOffset;

                // Calculate clip space position
                o.pos = mul(UNITY_MATRIX_P, viewPos);

                // Calculate screen-space point size based on scale
                float maxScale = max(max(scale.x, scale.y), scale.z);
                float distance = length(viewPos.xyz);

                // Adaptive point size with perspective correction
                float screenHeight = _ScreenParams.y;
                o.pointSize = (maxScale * screenHeight * 300.0) / (distance + 1.0);
                o.pointSize = clamp(o.pointSize, 1.0, 512.0);

                // Calculate world normal from rotation
                float3x3 rotMatrix = QuaternionToMatrix(rotation);
                o.worldNormal = normalize(mul(rotMatrix, float3(0, 0, 1)));

                // Store ellipsoid axes for anisotropic rendering
                o.ellipsoidAxes = scale;

                // Pass color and opacity
                o.color = color;
                o.opacity = opacity * _OpacityMultiplier;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Get point coordinate (0 to 1 in each dimension)
                float2 pointCoord = i.pos.xy / i.pointSize;

                // Center the coordinate (-1 to 1)
                float2 centered = pointCoord * 2.0 - 1.0;

                // Calculate distance from center with anisotropy
                float2 anisotropicCoord = centered;
                anisotropicCoord.x /= lerp(1.0, i.ellipsoidAxes.x / max(i.ellipsoidAxes.y, 0.001), _AnisotropyStrength);
                anisotropicCoord.y /= lerp(1.0, i.ellipsoidAxes.y / max(i.ellipsoidAxes.z, 0.001), _AnisotropyStrength);

                float dist2 = dot(anisotropicCoord, anisotropicCoord);

                // Discard fragments outside the splat
                if (dist2 > 1.0)
                    discard;

                // Calculate Gaussian falloff
                float gaussian = exp(-dist2 * 4.0);

                #ifdef USE_ADVANCED_SHADING
                    // Calculate normal for the sphere
                    float3 sphereNormal = normalize(float3(centered, sqrt(max(0.0, 1.0 - dist2))));

                    // Transform to world space
                    float3 worldNormal = normalize(i.worldNormal + sphereNormal * 0.3);

                    // Calculate lighting
                    float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                    float ndotl = max(0.0, dot(worldNormal, lightDir));

                    // Ambient + Diffuse
                    float3 ambient = UNITY_LIGHTMODEL_AMBIENT.xyz;
                    float3 diffuse = _LightColor0.rgb * ndotl;

                    float3 lighting = ambient + diffuse;

                    #ifdef USE_SPECULAR
                        // Specular highlights
                        float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                        float3 halfDir = normalize(lightDir + viewDir);
                        float spec = pow(max(0.0, dot(worldNormal, halfDir)), _Shininess);
                        lighting += _LightColor0.rgb * spec * 0.5;
                    #endif

                    float3 finalColor = i.color.rgb * lighting;
                #else
                    float3 finalColor = i.color.rgb;
                #endif

                // Apply Gaussian falloff to opacity
                float finalOpacity = i.opacity * gaussian;

                // Gamma correction if needed
                #ifdef UNITY_COLORSPACE_GAMMA
                    finalColor = pow(finalColor, 2.2);
                #endif

                return fixed4(finalColor, finalOpacity);
            }
            ENDCG
        }
    }

    // Fallback for older hardware
    Fallback "Unlit/Transparent"
}
