Shader "CoffeeGame/TimeStopWorldEffect"
{
    Properties
    {
        [PerRendererData] _MainTex ("World", 2D) = "white" {}
        _ActorTex ("Upright actors", 2D) = "black" {}
        _Progress ("Turn progress", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend One Zero

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_ActorTex);
            SAMPLER(sampler_ActorTex);
            float _Progress;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float p = smoothstep(0.0, 1.0, _Progress);
                float scale = cos(3.14159265 * p);
                float safeScale = abs(scale) < .006 ? (scale < 0 ? -.006 : .006) : scale;
                float2 flipped = float2(input.uv.x, (input.uv.y - .5) / safeScale + .5);
                half3 underneath = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb * .22h;
                half3 turned = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(flipped)).rgb;
                float inside = step(0, flipped.y) * step(flipped.y, 1);
                half3 background = lerp(underneath, turned, inside);
                half4 actor = SAMPLE_TEXTURE2D(_ActorTex, sampler_ActorTex, input.uv);
                half4 source = half4(background * (1 - actor.a) + actor.rgb, 1);
                half luminance = dot(source.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                half3 gray = lerp(source.rgb, luminance.xxx, .92h * p);
                return half4(gray, 1.0h);
            }
            ENDHLSL
        }
    }
}
