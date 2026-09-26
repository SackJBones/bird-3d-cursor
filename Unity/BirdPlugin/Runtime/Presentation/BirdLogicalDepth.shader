Shader "Bird/LogicalDepth"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _BirdDepthScale ("Logical / rendered distance", Float) = 1
        _UseVertexDepthScale ("Depth scale from UV2", Float) = 0
        _UseVertexColor ("Vertex color", Float) = 0
        [Toggle] _ZWrite ("Write depth", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZTest LEqual
            ZWrite [_ZWrite]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            float4 _Color;
            float _BirdDepthScale, _UseVertexDepthScale, _UseVertexColor;
            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 depthScale : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 position : SV_POSITION;
                float4 color : COLOR;
                float2 depthPair : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.position = UnityObjectToClipPos(v.vertex);
                float scale = max(1, lerp(_BirdDepthScale, v.depthScale.x, _UseVertexDepthScale));
                float viewDepth = -UnityObjectToViewPos(v.vertex).z;
                // The ratio of these perspective-correct interpolants is
                // screen-linear inverse logical depth, including trail spans
                // whose endpoints have different projection scales.
                o.depthPair = float2(1 / scale, viewDepth);
                o.color = _Color * lerp(float4(1,1,1,1), v.color, _UseVertexColor);
                return o;
            }
            struct output { float4 color : SV_Target; float depth : SV_Depth; };
            output frag(v2f i)
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                output o;
                float inverseLogicalDepth = i.depthPair.x / max(i.depthPair.y, 0.000001);
                // Inverse of Unity's LinearEyeDepth. Works with conventional
                // GLES depth and reversed D3D depth. Beyond the world clip,
                // the marker remains at far depth: sky-visible, world-occluded.
                o.depth = saturate((inverseLogicalDepth - _ZBufferParams.w) / _ZBufferParams.z);
                o.color = i.color;
                return o;
            }
            ENDCG
        }
    }
}
