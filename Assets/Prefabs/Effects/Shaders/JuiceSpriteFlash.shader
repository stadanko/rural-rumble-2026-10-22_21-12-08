// D-046 red flash (Juice). Draws a sprite as a solid silhouette in _FlashColor, using only the sprite's alpha.
// Used by the runtime "JuiceFlash" overlay renderer that CombatTheatrics adds under each wrestler's sprite, so the
// flash wins over any SpriteRenderer colour the animation clips key. Unlit (same pass setup as URP's
// Sprite-Unlit-Default, so the 2D renderer draws it); _FlashColor is HDR so it can push through the bloom.
Shader "RuralRumble/Juice/SpriteFlash"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _FlashColor ("Flash Color", Color) = (1.5, 0.1, 0.08, 1)
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex FlashVertex
            #pragma fragment FlashFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _FlashColor;
            CBUFFER_END

            Varyings FlashVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * unity_SpriteColor;
                return o;
            }

            half4 FlashFragment(Varyings input) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * input.color.a * _FlashColor.a;
                return half4(_FlashColor.rgb, a);
            }
            ENDHLSL
        }
    }
}
