using System.Diagnostics;
using ldvs.Core;
internal class Program
{
    /// <summary>
    /// The main entry point for the application. 
    /// This creates an instance of your game and calls the Run() method 
    /// </summary>
    /// <param name="args">Command-line arguments passed to the application.</param>
    private static void Main(string[] args)
    {

        Trace.Listeners.Add(new ConsoleTraceListener());
        using var game = new ldvsGame("do some silly things like v/s did here",2560,1440,false);
        game.Run();

    }
}