Shader "Bird/Pond Fish"
{
    Properties { _Color("Pigment",Color)=(1,.7,.25,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            float4 _Color;
            struct appdata { float4 vertex:POSITION;float3 normal:NORMAL;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION;float4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);v2f o;UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float phase=unity_ObjectToWorld._m03*1.7+unity_ObjectToWorld._m23;
                v.vertex.x+=sin(_Time.y*6+phase+v.vertex.z*8)*saturate(-v.vertex.z*2.5)*.055;
                o.vertex=UnityObjectToClipPos(v.vertex);
                float shade=.66+.34*saturate(UnityObjectToWorldNormal(v.normal).y);
                o.color=float4(v.color.rgb*_Color.rgb*shade,1);return o;
            }
            fixed4 frag(v2f i):SV_Target{return i.color;}
            ENDCG
        }
    }
}
