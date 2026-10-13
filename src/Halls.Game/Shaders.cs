namespace Halls.Game;

/// <summary>GLSL 330 for the 3D view.</summary>
static class Shaders
{
    public const string BasicVs = """
        #version 330
        in vec3 vertexPosition;
        in vec2 vertexTexCoord;
        in vec4 vertexColor;
        uniform mat4 mvp;
        out vec2 fragTexCoord;
        out vec4 fragColor;
        void main()
        {
            fragTexCoord = vertexTexCoord;
            fragColor = vertexColor;
            gl_Position = mvp * vec4(vertexPosition, 1.0);
        }
        """;

    /// <summary>The cards: transparent pixels are cut out, so they need no sorting.</summary>
    public const string CutoutFs = """
        #version 330
        in vec2 fragTexCoord;
        in vec4 fragColor;
        uniform sampler2D texture0;
        uniform vec4 colDiffuse;
        out vec4 finalColor;
        void main()
        {
            vec4 c = texture(texture0, fragTexCoord) * colDiffuse * fragColor;
            if (c.a < 0.5) discard;
            finalColor = c;
        }
        """;

    /// <summary>The models: one light from above and some ambient.</summary>
    public const string LitVs = """
        #version 330
        in vec3 vertexPosition;
        in vec2 vertexTexCoord;
        in vec3 vertexNormal;
        in vec4 vertexColor;
        uniform mat4 mvp;
        uniform mat4 matNormal;
        out vec2 fragTexCoord;
        out vec4 fragColor;
        out vec3 fragNormal;
        void main()
        {
            fragTexCoord = vertexTexCoord;
            fragColor = vertexColor;
            fragNormal = vec3(matNormal * vec4(vertexNormal, 0.0));
            gl_Position = mvp * vec4(vertexPosition, 1.0);
        }
        """;

    public const string LitFs = """
        #version 330
        in vec2 fragTexCoord;
        in vec4 fragColor;
        in vec3 fragNormal;
        uniform sampler2D texture0;
        uniform vec4 colDiffuse;
        uniform vec3 lightDir;
        out vec4 finalColor;
        void main()
        {
            vec4 c = texture(texture0, fragTexCoord) * colDiffuse * fragColor;
            if (c.a < 0.1) discard;
            // a model without normals is lit evenly
            float d = length(fragNormal) < 1e-4 ? 0.6 : abs(dot(normalize(fragNormal), -normalize(lightDir)));
            finalColor = vec4(c.rgb * (0.45 + 0.7 * d), c.a);
        }
        """;
}
