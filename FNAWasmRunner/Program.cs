using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.InteropServices;
using GameCore;
using Microsoft.Xna.Framework;

[assembly: System.Runtime.Versioning.SupportedOSPlatform("browser")]

partial class Program
{
    private static void Main()
    {
        Console.WriteLine("Hi!");
    }

    static GameMain game;
    static FieldInfo RunApplication;
    public static bool firstLaunch = true;

    [JSExport]
    internal static Task PreInit()
    {
        return Task.Run(() =>
        {
            Environment.SetEnvironmentVariable("FNA_PLATFORM_BACKEND", "SDL3");
        });
    }

    [JSExport]
    internal static Task Init()
    {
        // Any init for the Game - usually before game.Run() in the decompilation
        game = new GameMain();
		RunApplication = game.GetType().GetField("RunApplication", BindingFlags.NonPublic | BindingFlags.Instance);
        return Task.Delay(0);
    }

    [JSExport]
    internal static Task<bool> Cleanup()
    {
        // Any cleanup for the Game - usually after game.Run() in the decompilation
        return Task.FromResult(true);
    }

    [JSExport]
    internal static Task<bool> MainLoop()
    {
        try
        {
            game.RunOneFrame();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Error in MainLoop()!");
            Console.Error.WriteLine(e);
            return (Task<bool>)Task.FromException(e);
        }
        return Task.FromResult((bool)RunApplication.GetValue(game));
    }
}
