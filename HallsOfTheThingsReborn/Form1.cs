namespace HallsOfTheThingsReborn
{
    public partial class Form1 : Form
    {
        int width = 40;
        int height = 20;
        Cell[,] maze = new Cell[40, 20];
        int cellSize = 80; // Dimensione di ogni cella: 20x20 

        public Form1()
        {
            InitializeComponent();
            this.DoubleBuffered = true;

            this.FormBorderStyle = FormBorderStyle.None; // Rimuove i bordi
            this.WindowState = FormWindowState.Maximized; // Massimizza il form
            this.TopMost = true; // Mantiene il form in primo piano
            this.Paint += new PaintEventHandler(MainForm_Paint);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    maze[x, y] = new Cell();
                }
            }
            GenerateMaze(0, 0); // Avvia da una cella iniziale, ad esempio (0, 0)
        }

        void GenerateMaze(int x, int y)
        {
            maze[x, y].Visited = true;

            // Direzioni: up, right, down, left
            int[][] directions = new int[][]
            {
        new int[] {0, -1}, // up
        new int[] {1, 0},  // right
        new int[] {0, 1},  // down
        new int[] {-1, 0}  // left
            };

            // Mescola le direzioni per la casualità
            Random rng = new Random();
            directions = directions.OrderBy(a => rng.Next()).ToArray();

            foreach (var dir in directions)
            {
                int newX = x + dir[0];
                int newY = y + dir[1];

                if (IsInBounds(newX, newY) && !maze[newX, newY].Visited)
                {
                    // Rimuovi il muro tra la cella corrente (x, y) e la cella nuova (newX, newY)
                    RemoveWalls(x, y, newX, newY);

                    GenerateMaze(newX, newY);
                }
            }
        }

        void RemoveWalls(int x, int y, int newX, int newY)
        {
            // Rimuovi il muro tra (x, y) e (newX, newY)
            if (newX > x) // moving right
            {
                maze[x, y].RightWall = false;
                maze[newX, newY].LeftWall = false;
            }
            else if (newX < x) // moving left
            {
                maze[x, y].LeftWall = false;
                maze[newX, newY].RightWall = false;
            }
            else if (newY > y) // moving down
            {
                maze[x, y].BottomWall = false;
                maze[newX, newY].TopWall = false;
            }
            else if (newY < y) // moving up
            {
                maze[x, y].TopWall = false;
                maze[newX, newY].BottomWall = false;
            }
        }


        bool IsInBounds(int x, int y)
        {
            return x >= 0 && y >= 0 && x < width && y < height;
        }

        private void MainForm_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    int cellX = x * cellSize;
                    int cellY = y * cellSize;

                    // Scegli il colore per il muro o per lo sfondo
                    Brush wallBrush = Brushes.Blue; // Per i muri
                    Brush backgroundBrush = Brushes.Black; // Per lo sfondo

                    // Disegna lo sfondo della cella
                    g.FillRectangle(backgroundBrush, cellX, cellY, cellSize, cellSize);

                    // Disegna i muri come rettangoli o linee
                    if (maze[x, y].TopWall)
                    {
                        g.DrawLine(Pens.White, cellX, cellY, cellX + cellSize, cellY);
                    }
                    if (maze[x, y].LeftWall)
                    {
                        g.DrawLine(Pens.White, cellX, cellY, cellX, cellY + cellSize);
                    }
                    if (maze[x, y].BottomWall)
                    {
                        g.DrawLine(Pens.White, cellX, cellY + cellSize, cellX + cellSize, cellY + cellSize);
                    }
                    if (maze[x, y].RightWall)
                    {
                        g.DrawLine(Pens.White, cellX + cellSize, cellY, cellX + cellSize, cellY + cellSize);
                    }

                    // Aggiungi qui ulteriori elementi grafici se necessario
                }
            }
        }


    }
    public class Cell
    {
        public bool TopWall = true;
        public bool LeftWall = true;
        public bool BottomWall = true;
        public bool RightWall = true;
        public bool Visited = false;
        // Altri attributi se necessario
    }

}
