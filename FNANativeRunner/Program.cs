using System;
using Microsoft.Xna.Framework;
using GameCore;
static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        using (GameMain g = new GameMain())
        {
            g.Run();
        }
    }
}