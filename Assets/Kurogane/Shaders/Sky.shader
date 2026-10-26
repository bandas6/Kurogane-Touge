// Cielo de Kurogane Touge (SkyDome): color por vértice por la textura, sin niebla ni luces.
// La cúpula se dibuja después de lo opaco (cola 2460) sin escribir profundidad y con prueba de profundidad:
// solo rellena los píxeles que nada tapa.
// La ciudad media del valle lo usa en la cola Geometry y con _ZWrite = 1 (sin niebla, con profundidad).
// Con _Gradient = 1 (la cúpula) el color sale de la elevación: suelo, horizonte, franja y cenit. El clima
// (WeatherSystem) cambia esos colores y _Tint con MaterialPropertyBlock, sin tocar los materiales horneados.
// Sin LightMode, URP lo dibuja como SRPDefaultUnlit; también funciona con el pipeline integrado.
Shader "Kurogane/Sky"
{
    Properties
    {
        _MainTex ("Textura", 2D) = "white" {}
        _Tint ("Tinte", Color) = (1, 1, 1, 1)
        [Toggle] _Gradient ("Degradado por elevación (cúpula)", Float) = 0
        _Horizon ("Horizonte", Color) = (0.07, 0.1, 0.23, 1)
        _Zenith ("Cenit", Color) = (0.008, 0.01, 0.03, 1)
        _Ground ("Suelo", Color) = (0.03, 0.04, 0.09, 1)
        _Band ("Franja del horizonte (alfa = fuerza)", Color) = (0, 0, 0, 0)
        _FogMix ("Fundido con el horizonte (cordilleras en la niebla)", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Mezcla origen", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Mezcla destino", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("Escribir profundidad", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Tint, _Horizon, _Zenith, _Ground, _Band;
            float _Gradient, _FogMix;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 dir : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float3 SkyGradient(float3 dir)
            {
                float e = degrees(asin(clamp(normalize(dir).y, -1, 1)));
                if (e < 0) return lerp(_Horizon.rgb, _Ground.rgb, saturate(-e / 7));
                float3 c = lerp(_Horizon.rgb, _Zenith.rgb, pow(saturate(e / 90), 0.45));
                return c + _Band.rgb * _Band.a * saturate(1 - e / 14);
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 c = _Gradient > 0.5 ? float4(SkyGradient(i.dir), 1) : tex2D(_MainTex, i.uv) * i.color;
                c *= _Tint;
                c.rgb = lerp(c.rgb, _Horizon.rgb, _FogMix);
                return c;
            }
            ENDCG
        }
    }
}
