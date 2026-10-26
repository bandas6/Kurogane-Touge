// Filtro PS2 de Kurogane Touge: estela entre cuadros, brillo, tramado ordenado y color de 16 bits,
// más gotas de lluvia en pantalla (_Rain) en el mismo pase final.
// Lo usa PS2Filter.cs con Graphics.Blit, así que funciona igual con URP que con el pipeline integrado.
Shader "Hidden/Kurogane/PS2Filter"
{
    Properties
    {
        _MainTex ("Imagen", 2D) = "black" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _History;
    sampler2D _Bloom;
    float _Trail;
    float _Threshold;
    float _BloomIntensity;
    float _Dither;
    float2 _BlurDir;
    sampler2D _Drops;
    float _Rain;

    // Descarta NaN, infinitos y negativos que a veces deja la escena en HDR; si no, la estela los
    // arrastra de cuadro en cuadro y el desenfoque los convierte en manchas negras permanentes
    bool Invalid(float3 c) { return any(isnan(c)) || any(isinf(c)); }
    float3 Clean(float3 c, float3 fallback) { return Invalid(c) ? fallback : clamp(c, 0, 16); }

    // Pase 0: se queda solo con lo que supera el umbral de brillo
    float4 FragBright(v2f_img i) : SV_Target
    {
        float3 c = Clean(tex2D(_MainTex, i.uv).rgb, 0);
        float l = max(c.r, max(c.g, c.b));
        return float4(c * saturate((l - _Threshold) / max(1e-4, 1 - _Threshold)), 1);
    }

    // Pase 1: desenfoque gaussiano separable de 9 muestras en la dirección _BlurDir
    float4 FragBlur(v2f_img i) : SV_Target
    {
        float2 d = _BlurDir * _MainTex_TexelSize.xy;
        float3 c = tex2D(_MainTex, i.uv).rgb * 0.227;
        c += (tex2D(_MainTex, i.uv + d * 1.385).rgb + tex2D(_MainTex, i.uv - d * 1.385).rgb) * 0.316;
        c += (tex2D(_MainTex, i.uv + d * 3.231).rgb + tex2D(_MainTex, i.uv - d * 3.231).rgb) * 0.070;
        return float4(c, 1);
    }

    // Pase 2: estela, mezcla el cuadro nuevo con el anterior (sin cuantizar, para que no se quede pegada)
    float4 FragTrail(v2f_img i) : SV_Target
    {
        float3 now = Clean(tex2D(_MainTex, i.uv).rgb, 0);
        float3 before = Clean(tex2D(_History, i.uv).rgb, now);
        return float4(lerp(now, before, _Trail), 1);
    }

    // Pase 3: brillo encima, tramado Bayer 4x4 y cuantización a 16 bits (5-6-5)
    static const float Bayer[16] =
    {
         0.0 / 16,  8.0 / 16,  2.0 / 16, 10.0 / 16,
        12.0 / 16,  4.0 / 16, 14.0 / 16,  6.0 / 16,
         3.0 / 16, 11.0 / 16,  1.0 / 16,  9.0 / 16,
        15.0 / 16,  7.0 / 16, 13.0 / 16,  5.0 / 16
    };

    float4 FragFinal(v2f_img i) : SV_Target
    {
        // Gotas en pantalla con lluvia: dos capas del mapa de gotas que se deslizan hacia abajo y desvían la imagen
        float2 uv = i.uv;
        if (_Rain > 0.001)
        {
            float2 duv = uv * float2(_MainTex_TexelSize.z / _MainTex_TexelSize.w, 1) * 2.5;
            float4 d1 = tex2D(_Drops, duv + float2(0, _Time.y * 0.05));
            float4 d2 = tex2D(_Drops, duv * 1.6 + float2(0.37, _Time.y * 0.11));
            uv += ((d1.rg - 0.5) * d1.a + (d2.rg - 0.5) * d2.a) * 0.03 * _Rain;
        }
        float3 c = Clean(tex2D(_MainTex, uv).rgb, 0) + Clean(tex2D(_Bloom, uv).rgb, 0) * _BloomIntensity;
        c = saturate(c);
        int2 p = int2(fmod(floor(i.uv * _MainTex_TexelSize.zw), 4));
        float b = lerp(0.5, Bayer[p.y * 4 + p.x], _Dither);
        const float3 levels = float3(31, 63, 31);
        // Se cuantiza en espacio gamma, como el color de 16 bits de la PS2. En espacio lineal los escalones de
        // rojo y azul (5 bits) son enormes en las sombras y el tramado tiñe de magenta la noche.
    #if !defined(UNITY_COLORSPACE_GAMMA)
        c = LinearToGammaSpace(c);
    #endif
        c = saturate(floor(c * levels + b) / levels);
    #if !defined(UNITY_COLORSPACE_GAMMA)
        c = GammaToLinearSpace(c);
    #endif
        return float4(c, 1);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragBright
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragBlur
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragTrail
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragFinal
            ENDCG
        }
    }
}
