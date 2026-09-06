Shader "CoffeeGame/ForestSurface"
{
    Properties
    {
        [MainTexture] _BaseMap ("Ground texture",2D) = "white" {}
        _Ground ("Ground blend",Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float _Ground;
        CBUFFER_END
        float4 _ForestFocus;
        float _CoffeeGameWorldTime;
        float _CoffeeGameCombatClockReady;
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; float4 occluder:TEXCOORD0; };
        struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; half4 color:COLOR; half fog:TEXCOORD2; float4 occluder:TEXCOORD3; };
        float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
        float Noise(float2 p)
        {
            float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
            return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
        }
        float3 Wind(float3 world,float amount)
        {
            float worldTime=lerp(_Time.y,_CoffeeGameWorldTime,saturate(_CoffeeGameCombatClockReady));
            float sway=sin(worldTime*1.4+world.x*0.8+world.z*0.5)*0.047+sin(worldTime*2.1+world.z*1.7)*0.018;
            world.xz+=float2(sway,sway*0.45)*amount*(1-_Ground);
            return world;
        }
        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionWS=Wind(TransformObjectToWorld(input.positionOS.xyz),input.color.a);
            output.positionCS=TransformWorldToHClip(output.positionWS);
            output.normalWS=TransformObjectToWorldNormal(input.normalOS);
            output.color=input.color;
            output.occluder=float4(TransformObjectToWorld(input.occluder.xyz),input.occluder.w);
            output.fog=ComputeFogFactor(output.positionCS.z);
            return output;
        }
        void ClipOccluder(Varyings input)
        {
            if(_Ground<0.5 && input.occluder.w>0 && _ForestFocus.w>0.5)
            {
                // Remove a whole foreground tree/shrub, so the playable space
                // stays clear without a noisy circular hole in its foliage.
                float2 ray=normalize(_WorldSpaceCameraPos.xz-_ForestFocus.xz);
                float2 delta=input.occluder.xz-_ForestFocus.xz;
                float along=dot(delta,ray);
                float width=abs(delta.x*ray.y-delta.y*ray.x);
                if((along>0.4 && width<input.occluder.w+1.8) || length(delta)<1.5)
                    clip(-1);
            }
        }
        ENDHLSL
        Pass
        {
            Name "ForestForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            half4 Frag(Varyings input,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
            {
                float3 world=input.positionWS;
                // Hide scenery between the camera and heroine. The
                // shadow pass keeps the canopy shadows; gameplay is untouched.
                ClipOccluder(input);
                half3 albedo=input.color.rgb;
                half3 normal=normalize(input.normalWS)*IS_FRONT_VFACE(face,1,-1);
                if(_Ground<0.5 && input.color.a<0.01)
                {
                    float ridges=Noise(float2((world.x+world.z)*32,world.y*1.7));
                    albedo*=0.82+ridges*0.34;
                }
                if(_Ground>0.5)
                {
                    float broad=Noise(world.xz*0.29),grain=Noise(world.xz*6.2);
                    float clearing=length(float2((world.x+1.6)/4.1,world.z/3.1));
                    float path=abs(world.x+1.6-sin(world.z*0.27)*0.85);
                    float dirt=max(1-smoothstep(0.7,1.3,clearing+(broad-0.5)*0.35),1-smoothstep(0.75,1.65,path+(broad-0.5)*0.7));
                    half3 moss=lerp(half3(0.047,0.09,0.022),half3(0.13,0.17,0.052),broad);
                    half3 soil=lerp(half3(0.15,0.115,0.064),half3(0.24,0.19,0.107),grain*0.55+broad*0.45);
                    half textureDetail=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,world.xz*0.26).g;
                    albedo=lerp(moss*(0.78+textureDetail*0.8),soil,dirt*0.92);
                    normal=normalize(normal+half3((grain-0.5)*0.12,0,(Noise(world.xz*7.1)-0.5)*0.12));
                }
                Light sun=GetMainLight(TransformWorldToShadowCoord(world));
                half ndl=saturate(dot(normal,sun.direction));
                half leaf=input.color.a*(1-_Ground);
                half back=saturate(dot(-normal,sun.direction))*leaf*0.28;
                half shade=lerp(0.48,1,sun.shadowAttenuation);
                half3 color=albedo*(half3(0.38,0.43,0.32)+sun.color*(ndl*0.82+back)*shade);
                color=MixFog(color,input.fog);
                return half4(color,1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection;
            float3 _LightPosition;
            float4 ShadowVert(Attributes input):SV_POSITION
            {
                float3 world=Wind(TransformObjectToWorld(input.positionOS.xyz),input.color.a);
                float3 normal=TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 direction=normalize(_LightPosition-world);
                #else
                    float3 direction=_LightDirection;
                #endif
                float4 clipPosition=TransformWorldToHClip(ApplyShadowBias(world,normal,direction));
                #if UNITY_REVERSED_Z
                    clipPosition.z=min(clipPosition.z,UNITY_NEAR_CLIP_VALUE*clipPosition.w);
                #else
                    clipPosition.z=max(clipPosition.z,UNITY_NEAR_CLIP_VALUE*clipPosition.w);
                #endif
                return clipPosition;
            }
            half4 ShadowFrag():SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthFrag
            half4 DepthFrag(Varyings input):SV_Target { ClipOccluder(input); return input.positionCS.z; }
            ENDHLSL
        }
    }
}
