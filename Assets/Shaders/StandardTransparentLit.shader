Shader "Custom/StandardTransparentLit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Gloss ("Gloss", Range(0,1)) = 0
        
        [Enum(Off, 0, Front, 1, Back, 2)]
        _Face ("Face Culling", Float) = 2
        [Enum(Off, 0, On, 1)]
        _ZWrite ("Z Write", Float) = 1
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalRenderPipeline"
        }
        LOD 100

        Pass
        {
            Cull [_Face]
            ZWrite [_ZWrite]
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
                float3 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _Gloss;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.normal = v.normal;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPosition = mul(unity_ObjectToWorld, v.vertex);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // Texture sampling and color
                half4 textureColor = tex2D(_MainTex, i.uv);
                half4 surfaceColor = textureColor * _Color;

                // Ambient
                half4 ambientColor = half4(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w, 1);
                ambientColor = ambientColor * surfaceColor;
                
                // Diffuse (Lambertian)
                half3 lightDirection = _MainLightPosition.xyz;
                half4 lightColor = half4(_MainLightColor.xyz, 1);
                half4 lambertian = half4(saturate(dot(lightDirection, i.normal)).xxx,1);
                half4 diffuseColor = surfaceColor * lightColor * lambertian;

                // Specular (Phong)
                // half3 viewVector = normalize(_WorldSpaceCameraPos - i.worldPosition);
                // half3 reflectVector = reflect(-lightDirection, normalize(i.normal));
                // half4 specularColor = half4(saturate(dot(reflectVector, viewVector)).xxx, 1);

                // Specular (Blinn-Phong)
                half3 viewVector = normalize(_WorldSpaceCameraPos - i.worldPosition);
                half4 specularColor = half4(saturate(dot(normalize(lightDirection + viewVector), normalize(i.normal))).xxx, 1);
                float specularExponent = exp2(_Gloss * 11) + 2;
                specularColor *= lambertian > 0;
                specularColor = pow(specularColor, specularExponent);
                specularColor *= lightColor;
                specularColor *= _Gloss;

                // Combine
                return saturate(ambientColor + diffuseColor + specularColor);
            }
            ENDHLSL
        }
    }
}
