namespace Halls.Core;

public sealed partial class Game
{
    // ConTab: extra loops, doors, gaps for each floor (main:4786)
    static readonly int[] T0 = [0, 255, 200, 150, 127, 100, 60, 20];
    static readonly int[] T1 = [0, 150, 100, 40, 23, 13, 7, 4];
    static readonly int[] T2 = [0, 200, 100, 50, 30, 20, 10, 5];

    // WallDefs (main:4672): 0-3 vertical walls, 4-7 horizontal - full, door, gap, none
    static readonly byte[][] WallDefs =
    [
        [0x9C, 0x9C, 0x9C, 0x9C, 0x9C, 0x9C], [0x98, 0xDD, 0xDD, 0xDD, 0xDD, 0x94],
        [0x98, 0x20, 0x20, 0x20, 0x20, 0x94], [0x20, 0x20, 0x20, 0x20, 0x20, 0x20],
        [0x93, 0x93, 0x93, 0x93, 0x93, 0x93], [0x92, 0xDC, 0xDC, 0xDC, 0xDC, 0x91],
        [0x92, 0x20, 0x20, 0x20, 0x20, 0x91], [0x20, 0x20, 0x20, 0x20, 0x20, 0x20],
    ];

    /// <summary>Maze: 16x16 rooms, bits 0-1 right, 2-3 left, 4-5 down, 6-7 up (0 wall, 1 door, 2 gap, 3 open).</summary>
    readonly byte[] rooms = new byte[256];
    readonly int[] moveList = new int[4];
    int moveCount;

    /// <summary>Init: rows and columns 0-112 of the map become spaces (not the sanctuary).</summary>
    void ClearFloor()
    {
        for (int r = 0; r < 113; r++)
            Array.Fill(Map, Codes.Space, r * 128, 113);
    }

    /// <summary>MAIN / Main: hunt and kill on the 16x16 rooms, with extra loops.</summary>
    void GenerateMaze(int floor)
    {
        Array.Clear(rooms);
        ClearFloor();
        int cur = rand.Next();
        while (true)
        {
            // the random walk
            while (true)
            {
                FindMoves(cur);
                if (moveCount == 0) break;
                cur = Join(cur, floor);
            }
            if (rand.Next() < T0[floor])
            {
                FindLinks(cur);
                if (moveCount != 0) Join(cur, floor);
            }
            // the next room not visited, joined to one that is
            while (true)
            {
                int n = 256;
                do
                {
                    cur = (cur + 1) & 255;
                    if (rooms[cur] == 0) break;
                } while (--n != 0);
                if (n == 0) return;
                FindLinks(cur);
                if (moveCount != 0) break;
            }
            Join(cur, floor);
        }
    }

    /// <summary>FindMoves: neighbours not visited, in the order up, down, left, right.</summary>
    void FindMoves(int cur)
    {
        moveCount = 0;
        if ((cur & 0xF0) != 0 && rooms[cur - 16] == 0) moveList[moveCount++] = 1;
        if ((cur & 0xF0) != 0xF0 && rooms[cur + 16] == 0) moveList[moveCount++] = 2;
        if ((cur & 15) != 0 && rooms[cur - 1] == 0) moveList[moveCount++] = 3;
        if ((cur & 15) != 15 && rooms[cur + 1] == 0) moveList[moveCount++] = 4;
    }

    /// <summary>FindLinks: visited neighbours not joined to this room.</summary>
    void FindLinks(int cur)
    {
        moveCount = 0;
        if ((cur & 0xF0) != 0 && rooms[cur - 16] != 0 && (rooms[cur - 16] & 0x30) == 0) moveList[moveCount++] = 1;
        if ((cur & 0xF0) != 0xF0 && rooms[cur + 16] != 0 && (rooms[cur + 16] & 0xC0) == 0) moveList[moveCount++] = 2;
        if ((cur & 15) != 0 && rooms[cur - 1] != 0 && (rooms[cur - 1] & 3) == 0) moveList[moveCount++] = 3;
        if ((cur & 15) != 15 && rooms[cur + 1] != 0 && (rooms[cur + 1] & 0x0C) == 0) moveList[moveCount++] = 4;
    }

    /// <summary>Move: Random for the direction, then Rnd6 for the kind of passage. Returns the room joined.</summary>
    int Join(int cur, int floor)
    {
        int i;
        if (moveCount == 1) i = 0;
        else
        {
            int r = rand.Next() & 3;
            if (moveCount == 2) i = r & 1;
            else if (moveCount == 4) i = r;
            else
            {
                while (r > 2) r = rand.Next() & 3;
                i = r;
            }
        }
        int k = rand.Next();
        int kind = k < T1[floor] ? 1 : k < T2[floor] ? 2 : 3;
        switch (moveList[i])
        {
            case 1: rooms[cur] |= (byte)(kind << 6); cur -= 16; rooms[cur] |= (byte)(kind << 4); break;
            case 2: rooms[cur] |= (byte)(kind << 4); cur += 16; rooms[cur] |= (byte)(kind << 6); break;
            case 3: rooms[cur] |= (byte)(kind << 2); cur -= 1; rooms[cur] |= (byte)kind; break;
            default: rooms[cur] |= (byte)kind; cur += 1; rooms[cur] |= (byte)(kind << 2); break;
        }
        return cur;
    }

    /// <summary>Page / PAGE + PageC22 + SideWall: the rooms as characters in the map.</summary>
    void PageMaze(int floor)
    {
        if (floor == 0)
        {
            // floor 0: one open hall
            ClearFloor();
            Map[0] = 0x95;
            for (int i = 1; i < 112; i++) { Map[i] = 0x93; Map[i * 128] = 0x9C; }
        }
        else
        {
            for (int l = 0; l < 256; l++)
            {
                int x = l & 15, y = l >> 4;
                int p = 7 * x + 7 * y * 128;
                int v = rooms[l];
                // Corner: U D L R walls leaving the corner
                int c = 0x90;
                if (y > 0 && (rooms[l - 16] & 0x0C) != 0x0C) c |= 8;
                if ((v & 0x0C) != 0x0C) c |= 4;
                if (x > 0 && (rooms[l - 1] & 0xC0) != 0xC0) c |= 2;
                if ((v & 0xC0) != 0xC0) c |= 1;
                Map[p] = (byte)(c == 0x90 ? 0x20 : c);
                var down = WallDefs[(v >> 2) & 3];
                for (int k = 0; k < 6; k++) Map[p + 128 * (k + 1)] = down[k];
                var left = WallDefs[((v >> 6) & 3) + 4];
                for (int k = 0; k < 6; k++) Map[p + k + 1] = left[k];
            }
        }
        // PageC22: the bottom wall
        const int bottom = 112 * 128;
        Map[bottom] = 0x99;
        for (int c = 1; c < 112; c++)
        {
            int above = Map[bottom - 128 + c];
            Map[bottom + c] = (byte)(above is 0x9C or 0x94 ? 0x9B : 0x93);
        }
        Map[bottom + 112] = 0x9A;
        // SideWall: the right wall, with the force field towards the sanctuary
        Map[112] = 0x96;
        for (int r = 1; r < 112; r++)
            Map[r * 128 + 112] = (byte)(Codes.IsWall(Map[r * 128 + 111]) ? 0x9E : 0x9C);
        Map[bottom + 112] = 0x9A;
        int d = (104 - 7 * floor) * 128 + 112;
        Map[d] = 0x94;
        for (int k = 1; k <= 4; k++) Map[d - 128 * k] = Codes.ForceField;
        Map[d - 640] = 0x98;
    }

    /// <summary>InitSanctuary: columns 113-127, drawn once per game.</summary>
    void InitSanctuary()
    {
        Array.Fill(Map, Codes.Space);
        Array.Clear(Overlay);
        Map[113] = 0x95;
        for (int k = 1; k <= 13; k++) Map[113 + k] = 0x93;
        Map[127] = 0x96;
        for (int k = 1; k <= 111; k++) Map[k * 128 + 127] = 0x9C;
        Map[112 * 128 + 127] = 0x9A;
        for (int k = 114; k <= 126; k++) Map[112 * 128 + k] = 0x93;
        Map[112 * 128 + 113] = 0x99;
        for (int k = 99; k <= 111; k++) Map[k * 128 + 113] = 0x9C;
        // the doors of floors 1-7 in the left wall; the one of floor 0 is a wall until GoldKey
        int r = 99;
        for (int j = 1; j <= 7; j++)
        {
            Map[(r - 1) * 128 + 113] = 0x9C;
            Map[(r - 2) * 128 + 113] = 0x94;
            for (int k = 3; k <= 6; k++) Map[(r - k) * 128 + 113] = Codes.ForceField;
            Map[(r - 7) * 128 + 113] = 0x98;
            r -= 7;
        }
        for (int k = 1; k <= 49; k++) Map[k * 128 + 113] = 0x9C;
        Map[50 * 128 + 113] = 0x99;
        Map[22 * 128 + 113] = 0x9D;
        for (int k = 114; k <= 126; k++) Map[22 * 128 + k] = 0x93;
        Map[22 * 128 + 127] = 0x9E;
        for (int k = 114; k <= 118; k++) Map[50 * 128 + k] = 0x93;
        Map[50 * 128 + 119] = 0x96;
        for (int k = 51; k <= 105; k++) Map[k * 128 + 119] = 0x9C;
        Map[106 * 128 + 119] = 0x98;
        // the stairs: a landing, three rows of the floor marker, three steps
        r = 51;
        for (int c = 0x87; c >= 0x80; c--)
        {
            Rung(r++, Codes.Landing);
            for (int k = 0; k < 3; k++) Rung(r++, c);
            for (int k = 0; k < 3; k++) Rung(r++, Codes.Steps);
        }
        for (int k = 0; k < 5; k++) Rung(r++, Codes.Landing);
        // SMess: the title written in the map
        string[] mess =
        [
            "  Halls", "  Of", "  The", "  Things", "", "Things", "trained by", "",
            "Martin", "Horsley", "", "Neil", "Mottershead", "", "Simon", "Brattel",
        ];
        for (int k = 0; k < mess.Length; k++)
        {
            string m = mess[k].PadRight(11);
            for (int j = 0; j < 11; j++) Map[(k + 2) * 128 + 115 + j] = (byte)m[j];
        }
    }

    void Rung(int row, int c)
    {
        for (int k = 114; k <= 118; k++) Map[row * 128 + k] = (byte)c;
    }
}
