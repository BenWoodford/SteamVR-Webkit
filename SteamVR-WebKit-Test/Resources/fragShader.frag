#version 450 core

uniform sampler2D _MainTex;
uniform sampler2D _AlphaMap;
uniform vec2 _OutputSize;
uniform vec2 _MousePosition;
out vec4 color;

void main(void)
{
	vec2 position = gl_FragCoord.xy / _OutputSize.xy;
	color = vec4(texelFetch(_MainTex, ivec2(gl_FragCoord.xy), 0).xyz, texelFetch(_AlphaMap, ivec2(gl_FragCoord.xy), 0).w);

	//color.w = 1 - clamp(length(position - (_MousePosition / _OutputSize)) / 0.2, 0.0, 1.0);
}