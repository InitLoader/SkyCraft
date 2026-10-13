Shader "SulfurCraft/World"
{
    Properties
    {
        _MainTex ("Minecraft texture", 2D) = "white" {}
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.1
        _SrcBlend ("Source blend", Float) = 1
        _DstBlend ("Destination blend", Float) = 0
        _ZWrite ("Depth write", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Cutoff;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; UNITY_FOG_COORDS(1) };
            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color;
                UNITY_TRANSFER_FOG(output, output.position);
                return output;
            }
            float4 frag(v2f input) : SV_Target
            {
                float4 color = tex2D(_MainTex, input.uv) * input.color;
                clip(color.a - _Cutoff);
                UNITY_APPLY_FOG(input.fogCoord, color);
                return color;
            }
            ENDHLSL
        }
    }
}
