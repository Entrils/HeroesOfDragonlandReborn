Shader "Heroes of Dragonland/Sky/Ashen Borderwood"
{
    Properties
    {
        [NoScaleOffset] _NightSky ("Night sky radiance", 2D) = "black" {}
        _Exposure ("Sky exposure (stops)", Range(-2, 2)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NightSky);
            SAMPLER(sampler_NightSky);
            CBUFFER_START(UnityPerMaterial)
                float _Exposure;
            CBUFFER_END

            struct Varyings { float4 position : SV_POSITION; float3 direction : TEXCOORD0; };

            Varyings vert(float4 vertex : POSITION)
            {
                Varyings output;
                output.position = TransformObjectToHClip(vertex.xyz);
                output.direction = vertex.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 ray = normalize(input.direction);
                float longitude = atan2(ray.x, ray.z);
                float2 uv = float2(0.5 + longitude / TWO_PI, 0.5 + asin(clamp(ray.y, -1, 1)) / PI);
                // Keep the native mip footprint when a pixel quad crosses the longitude seam.
                float2 dx = ddx(uv), dy = ddy(uv);
                dx.x -= round(dx.x);
                dy.x -= round(dy.x);
                half3 radiance = SAMPLE_TEXTURE2D_GRAD(_NightSky, sampler_NightSky, uv, dx, dy).rgb;
                return half4(radiance * exp2(_Exposure), 1);
            }
            ENDHLSL
        }
    }
}
