using AssetsTools.NET.Texture.TextureDecoders.CrnUnity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResourceModLoader
{
    internal static class Log
    {
        public static void Wait()
        {
            Console.WriteLine("按任意键退出程序");
            Console.ReadKey();
        }
        public static void Info(string t)
        {
            Console.WriteLine($"[I] {t}");
        }

        public static void Error(string t)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[E] {t}");
            Console.ResetColor();
        }
        public static void Warn(string t)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[W] {t}");
            Console.ResetColor();
        }

        public static void Debug(string t)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"[Debug] {t}");
            Console.ResetColor();
        }

        public static void SuccessAll(string t)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[SA] {t}");
            Console.ResetColor();
        }

        public static void SuccessPartial(string t)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"[SP] {t}");
            Console.ResetColor();
        }
    }
}