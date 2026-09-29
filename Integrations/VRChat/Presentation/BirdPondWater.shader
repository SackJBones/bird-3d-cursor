Shader "Bird/Shallow Pond Water"
{
    Properties { _Color("Water",Color)=(.1,.43,.5,.32) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            float4 _Color;
            struct appdata {float4 vertex:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f {float4 vertex:SV_POSITION;float2 surface:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            v2f vert(appdata v){UNITY_SETUP_INSTANCE_ID(v);v2f o;UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.vertex=UnityObjectToClipPos(v.vertex);o.surface=mul(unity_ObjectToWorld,v.vertex).xz;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                float ripple=sin(i.surface.x*3+_Time.y*.6)*sin(i.surface.y*4-_Time.y*.4);
                return float4(_Color.rgb+ripple*.016,_Color.a);
            }
            ENDCG
        }
    }
}
