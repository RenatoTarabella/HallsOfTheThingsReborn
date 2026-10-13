using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Raylib_cs;

namespace Halls.Game;

/// <summary>One line of Assets/Models/models.json.</summary>
public sealed class ModelEntry
{
    /// <summary>glTF / GLB / OBJ / IQM / VOX / M3D, relative to Assets/Models.</summary>
    public string File { get; set; } = "";

    /// <summary>Scale after fitting (1 = one cell wide when Fit is on).</summary>
    public float Scale { get; set; } = 1;

    /// <summary>Scale the model so that its larger horizontal side is one cell.</summary>
    public bool Fit { get; set; } = true;

    /// <summary>Degrees around the vertical axis, added to the direction (0 = the model looks north, -Z).</summary>
    public float RotationY { get; set; }

    /// <summary>Raised by this much (cells) above the floor.</summary>
    public float OffsetY { get; set; }

    /// <summary>Multiply the colours of the model by the Spectrum colour of the code.</summary>
    public bool Tint { get; set; }
}

/// <summary>
/// The models that replace the characters in the 3D view. Every key is optional:
/// what has no model is drawn with boxes (walls, doors, stairs) or as a card with
/// the character of 1983. See Assets/Models/README.md for the keys.
/// </summary>
public sealed unsafe class ModelLibrary : IDisposable
{
    public sealed record Loaded(Model Model, ModelEntry Entry, Matrix4x4 Base);

    readonly Dictionary<string, Loaded> models = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<string> Keys => models.Keys;
    public List<string> Errors { get; } = new();

    public static string Folder => Path.Combine(AppContext.BaseDirectory, "Assets", "Models");

    public ModelLibrary(Shader lit)
    {
        Load(lit);
        // the problems go to models.log, the front end says how many
        string log = Path.Combine(Folder, "models.log");
        try
        {
            if (Errors.Count > 0) File.WriteAllLines(log, Errors);
            else if (File.Exists(log)) File.Delete(log);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    void Load(Shader lit)
    {
        string map = Path.Combine(Folder, "models.json");
        if (!File.Exists(map)) return;
        Dictionary<string, ModelEntry>? entries;
        try
        {
            entries = JsonSerializer.Deserialize<Dictionary<string, ModelEntry>>(File.ReadAllText(map),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException e)
        {
            Errors.Add("models.json: " + e.Message);
            return;
        }
        foreach (var (key, e) in entries ?? new())
        {
            if (key.StartsWith('_') || string.IsNullOrWhiteSpace(e.File)) continue;
            string file = Path.Combine(Folder, e.File);
            if (!File.Exists(file)) { Errors.Add($"{key}: {e.File} not found"); continue; }
            var m = Raylib.LoadModel(file);
            if (m.MeshCount == 0) { Errors.Add($"{key}: {e.File} has no meshes"); continue; }
            for (int i = 0; i < m.MaterialCount; i++) m.Materials[i].Shader = lit;
            models[key] = new Loaded(m, e, BaseTransform(m, e));
        }
    }

    /// <summary>Centre on the base, fit to one cell, scale.</summary>
    static Matrix4x4 BaseTransform(Model m, ModelEntry e)
    {
        var b = Raylib.GetModelBoundingBox(m);
        var size = b.Max - b.Min;
        float s = e.Scale;
        if (e.Fit)
        {
            float side = MathF.Max(size.X, size.Z);
            if (side > 1e-5f) s /= side;
        }
        var centre = new Vector3((b.Min.X + b.Max.X) / 2, b.Min.Y, (b.Min.Z + b.Max.Z) / 2);
        return Matrix4x4.CreateTranslation(-centre) * Matrix4x4.CreateScale(s)
               * Matrix4x4.CreateRotationY(-e.RotationY * MathF.PI / 180)
               * Matrix4x4.CreateTranslation(0, e.OffsetY, 0);
    }

    public bool TryGet(string key, out Loaded m) => models.TryGetValue(key, out m!);

    /// <summary>The first of the keys that has a model.</summary>
    public Loaded? Find(params ReadOnlySpan<string> keys)
    {
        foreach (var k in keys)
            if (models.TryGetValue(k, out var m)) return m;
        return null;
    }

    /// <summary>Draws a model at the centre of a cell (floor level), turned clockwise by degrees.</summary>
    public static void Draw(Loaded m, Vector3 at, float degrees, Color tint, bool force = false)
    {
        var world = m.Base * Matrix4x4.CreateRotationY(-degrees * MathF.PI / 180) * Matrix4x4.CreateTranslation(at);
        var model = m.Model;
        // raylib keeps column vectors: transpose the System.Numerics matrix
        model.Transform = Matrix4x4.Transpose(world);
        // two-sided: models made by hand often have some faces turned the wrong way
        Rlgl.DisableBackfaceCulling();
        Raylib.DrawModel(model, Vector3.Zero, 1, m.Entry.Tint || force ? tint : Color.White);
        Rlgl.EnableBackfaceCulling();
    }

    public void Dispose()
    {
        foreach (var m in models.Values) Raylib.UnloadModel(m.Model);
        models.Clear();
    }
}
