Shader "LeiTing/ProceduralLaser"
{
    Properties
    {
        _CoreColor ("Core Color", Color) = (1, 1, 1, 1)
        _BeamColor ("Beam Color", Color) = (0.25, 0.9, 1, 0.95)
        _GlowColor ("Glow Color", Color) = (0.1, 0.55, 1, 0.45)
        _PulseSpeed ("Pulse Speed", Float) = 18
        _GlowWidth ("Glow Width", Float) = 0.95
        _BodyWidth ("Body Width", Float) = 0.45
        _CoreWidth ("Core Width", Float) = 0.16
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _CoreColor;
            fixed4 _BeamColor;
            fixed4 _GlowColor;
            float _PulseSpeed;
            float _GlowWidth;
            float _BodyWidth;
            float _CoreWidth;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord;
                output.color = input.color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float horizontal = abs(input.uv.x * 2.0 - 1.0);
                float core = 1.0 - smoothstep(_CoreWidth, _CoreWidth + 0.08, horizontal);
                float body = 1.0 - smoothstep(_BodyWidth, _BodyWidth + 0.18, horizontal);
                float glow = 1.0 - smoothstep(_GlowWidth, 1.0, horizontal);
                float pulse = 0.72 + 0.28 * sin((input.uv.y * 8.0 - _Time.y * _PulseSpeed) * 6.28318);

                fixed4 color = _GlowColor * glow;
                color = lerp(color, _BeamColor, body * pulse);
                color = lerp(color, _CoreColor, core);
                color.a *= saturate(max(glow * 0.65, max(body * 0.9, core)) * input.color.a);
                return color;
            }
            ENDCG
        }
    }
}
