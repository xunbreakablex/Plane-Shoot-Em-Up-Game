// Basit gradyan gokyuzu: ust renk, ufuk rengi, alt renk ve isteğe bagli gunes parlamasi.
// Asama atmosferi (StageAtmosphere) renkleri calisma zamaninda ayarlar.
Shader "PlaneShootEmUp/GradientSky"
{
    Properties
    {
        _Top ("Top", Color) = (0.28, 0.52, 0.9, 1)
        _Horizon ("Horizon", Color) = (0.78, 0.88, 0.97, 1)
        _Bottom ("Bottom", Color) = (0.45, 0.5, 0.45, 1)
        _Power ("Horizon falloff", Float) = 0.55
        _SunDir ("Sun direction (toward sun)", Vector) = (0, 0.7, 0.7, 0)
        _SunColor ("Sun color", Color) = (1, 0.95, 0.8, 1)
        _SunGlow ("Sun glow", Float) = 0.35
        _SunDisc ("Sun disc", Float) = 1.5
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Top, _Horizon, _Bottom, _SunColor;
            float _Power, _SunGlow, _SunDisc;
            float4 _SunDir;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;

                fixed3 col;
                if (h >= 0.0)
                    col = lerp(_Horizon.rgb, _Top.rgb, pow(saturate(h), _Power));
                else
                    col = lerp(_Horizon.rgb, _Bottom.rgb, saturate(-h * 4.0));

                float s = saturate(dot(d, normalize(_SunDir.xyz)));
                col += _SunColor.rgb * (pow(s, 10.0) * _SunGlow + pow(s, 900.0) * _SunDisc);
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
}
