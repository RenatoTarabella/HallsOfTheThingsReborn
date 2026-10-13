using Halls.Game;

// Halls of the Things - Crystal Computing / Design Design, 1983,
// by Simon Brattel, Martin Horsley and Neil Mottershead.
//
//   Halls [--3d|--2d] [--seed N] [--screenshot file.png [--frames N] [--floor F] [--zoom 1-2] [--map]]

string? shot = null;
int frames = 60;
bool? view3D = null;
int? floor = null;
uint? seed = null;
float zoom = 1;
bool map = false, suspend = false, resume = false;
int wounds = -1;
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--3d": view3D = true; break;
        case "--2d": view3D = false; break;
        case "--screenshot" when i + 1 < args.Length: shot = args[++i]; break;
        case "--frames" when i + 1 < args.Length: frames = int.Parse(args[++i]); break;
        case "--floor" when i + 1 < args.Length: floor = int.Parse(args[++i]); break;
        case "--seed" when i + 1 < args.Length: seed = uint.Parse(args[++i]); break;
        case "--zoom" when i + 1 < args.Length: zoom = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
        case "--map": map = true; break;
        case "--suspend": suspend = true; break;
        case "--resume": resume = true; break;
        case "--wounds" when i + 1 < args.Length: wounds = int.Parse(args[++i]); break;
    }
}

using var app = new App(new App.Options(shot, frames, view3D, floor, seed, zoom, map, suspend, resume, wounds));
app.Run();
