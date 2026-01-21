Shader "UnitySplatter/GaussianSplat"
{
    Properties
    {
        _OpacityScale("Opacity Scale", Float) = 1
        _SizeScale("Size Scale", Float) = 1
        _MinOpacity("Min Opacity", Float) = 0.01
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct GaussianPoint
            {
                float3 Position;
                float3 Scale;
                float4 Rotation;
                float4 Color;
                float Opacity;
                float SH0;
                float SH1;
                float SH2;
                float SH3;
                float SH4;
                float SH5;
                float SH6;
                float SH7;
                float SH8;
            };

            StructuredBuffer<GaussianPoint> _Points;
            int _PointCount;
            float _OpacityScale;
            float _SizeScale;
            float _MinOpacity;
            float _EnableSH;

            struct appdata
            {
                uint vertexID : SV_VertexID;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float4 color : COLOR0;
                float pointSize : PSIZE;
            };

            v2f vert(appdata v)
            {
                v2f o;
                GaussianPoint p = _Points[v.vertexID];
                float4 worldPos = float4(p.Position, 1.0);
                o.position = UnityObjectToClipPos(worldPos);
                float opacity = saturate(p.Opacity * _OpacityScale);
                o.color = float4(p.Color.rgb, opacity);
                float size = max(0.001, (p.Scale.x + p.Scale.y + p.Scale.z) / 3.0);
                o.pointSize = size * _SizeScale * 1000.0;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                if (i.color.a < _MinOpacity)
                {
                    discard;
                }

                return i.color;
            }
            ENDHLSL
        }
    }
}
