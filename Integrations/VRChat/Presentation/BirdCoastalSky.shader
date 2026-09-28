Shader "Bird/Coastal daylight sky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (.29,.52,.75,1)
        _Horizon ("Horizon haze", Color) = (.76,.84,.88,1)
        _Ground ("Below horizon", Color) = (.53,.69,.75,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            fixed4 _Zenith, _Horizon, _Ground;
            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; float3 direction : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_OUTPUT(v2f,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex); o.direction = mul((float3x3)unity_ObjectToWorld, v.vertex.xyz); return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float height = normalize(i.direction).y;
                fixed3 sky = lerp(_Horizon.rgb, _Zenith.rgb, smoothstep(0, .65, max(0,height)));
                sky = lerp(sky, _Ground.rgb, smoothstep(0, .14, max(0,-height)));
                return fixed4(sky,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
