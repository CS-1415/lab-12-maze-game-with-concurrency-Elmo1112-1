// Elmo 
// 19/04/2026
// lab 12


using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        Console.CursorVisible = false;
        Console.Clear();

        Game game = new Game();
        await game.Run();
    }
}

class Entity
{
    public int Row { get; set; }
    public int Col { get; set; }
    public char Symbol { get; set; }

    public Entity(int row, int col, char symbol)
    {
        Row = row;
        Col = col;
        Symbol = symbol;
    }
}

class Player : Entity
{
    public Player(int r, int c) : base(r, c, '@') { }
}

class Guard : Entity
{
    private static Random rand = new Random();

    public Guard(int r, int c) : base(r, c, '%') { }

    public async Task MoveAsync(Game game, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(rand.Next(150, 300));

            int newR = Row;
            int newC = Col;

            switch (rand.Next(4))
            {
                case 0: newR--; break;
                case 1: newR++; break;
                case 2: newC--; break;
                case 3: newC++; break;
            }

            game.MoveGuard(this, newR, newC);
        }
    }
}

class Game
{
    private string[] map;
    private Player player;
    private List<Guard> guards = new List<Guard>();
    private int score = 0;
    private int coins = 0;
    private bool running = true;
    private object lockObj = new object();
    private DateTime startTime;
    private bool gateOpened = false;

    public async Task Run()
    {
        Console.Clear();
        Console.WriteLine("maze escape game");
        Console.WriteLine("Collect coins (^), unlock gate, grab gems ($), reach # to win");
        Console.WriteLine("avoid the guards (%)");
        Console.ReadKey(true);

        map = File.ReadAllLines("map.txt");

        LoadEntities();
        startTime = DateTime.Now;

        CancellationTokenSource cts = new CancellationTokenSource();
        List<Task> guardTasks = new List<Task>();

        foreach (var g in guards)
            guardTasks.Add(g.MoveAsync(this, cts.Token));

        GameLoop();

        cts.Cancel();
        await Task.WhenAll(guardTasks);

        Console.ReadKey(true);
    }

    private void LoadEntities()
    {
        for (int r = 0; r < map.Length; r++)
        {
            for (int c = 0; c < map[r].Length; c++)
            {
                char ch = map[r][c];

                if (ch == '^') coins++;

                if (ch == '%')
                {
                    guards.Add(new Guard(r, c));
                    SetTile(r, c, ' ');
                }

                if (ch == ' ' && player == null)
                    player = new Player(r, c);
            }
        }
    }

    private void GameLoop()
    {
        do
        {
            Render();

            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true).Key;

                if (key == ConsoleKey.Escape)
                {
                    running = false;
                    break;
                }

                int newR = player.Row;
                int newC = player.Col;

                switch (key)
                {
                    case ConsoleKey.UpArrow: newR--; break;
                    case ConsoleKey.DownArrow: newR++; break;
                    case ConsoleKey.LeftArrow: newC--; break;
                    case ConsoleKey.RightArrow: newC++; break;
                }

                TryMovePlayer(newR, newC);
            }

            Thread.Sleep(50);

        } while (running);
    }

    private void TryMovePlayer(int r, int c)
    {
        lock (lockObj)
        {
            if (!IsInside(r, c)) return;

            char tile = map[r][c];

            if (tile == '*') return;
            if (tile == '|') return;

            if (tile == '^')
            {
                score += 100;
                coins--;
                SetTile(r, c, ' ');
            }

            if (tile == '$')
            {
                score += 200;
                SetTile(r, c, ' ');
            }

            if (!gateOpened && coins == 0)
            {
                OpenGate();
                gateOpened = true;
            }

            if (tile == '#')
                Win();

            player.Row = r;
            player.Col = c;

            CheckGuardCollision();
        }
    }

    public void MoveGuard(Guard g, int newR, int newC)
    {
        lock (lockObj)
        {
            if (!IsInside(newR, newC)) return;
            if (map[newR][newC] == '*') return;

            g.Row = newR;
            g.Col = newC;

            if (g.Row == player.Row && g.Col == player.Col)
                Lose();
        }
    }

    private void CheckGuardCollision()
    {
        foreach (var g in guards)
        {
            if (g.Row == player.Row && g.Col == player.Col)
                Lose();
        }
    }

    private void OpenGate()
    {
        for (int r = 0; r < map.Length; r++)
            map[r] = map[r].Replace("|", " ");
    }

    private bool IsInside(int r, int c)
    {
        return r >= 0 && r < map.Length && c >= 0 && c < map[r].Length;
    }

    private void SetTile(int r, int c, char ch)
    {
        char[] row = map[r].ToCharArray();
        row[c] = ch;
        map[r] = new string(row);
    }

    private void Render()
    {
        if (!running) return;

        lock (lockObj)
        {
            Console.SetCursorPosition(0, 0);

            for (int r = 0; r < map.Length; r++)
            {
                for (int c = 0; c < map[r].Length; c++)
                {
                    if (player.Row == r && player.Col == c)
                        Console.Write('@');
                    else if (guards.Exists(g => g.Row == r && g.Col == c))
                        Console.Write('%');
                    else
                        Console.Write(map[r][c]);
                }
                Console.WriteLine();
            }

            var elapsed = DateTime.Now - startTime;

            Console.WriteLine($"Score: {score}  Coins: {coins}  Time: {elapsed.Seconds}");
        }
    }

    private void Win()
    {
        running = false;
        Console.Clear();
        Console.WriteLine("you win!");
        Console.WriteLine($"Score {score}");
        Console.WriteLine($"Time {(DateTime.Now - startTime).Seconds}s");
    }

    private void Lose()
    {
        running = false;
        Console.Clear();
        Console.WriteLine("you lost");
        Console.WriteLine($"score {score}");
        Console.WriteLine($"time: {(DateTime.Now - startTime).Seconds}s");
    }
}