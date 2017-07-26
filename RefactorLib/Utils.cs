using System;

namespace Refactor
{
	public class Utils
	{
		public static void WL(ConsoleColor col, string message)
		{
			var prev = Console.ForegroundColor;
			Console.ForegroundColor = col;
			Console.WriteLine(message);
			Console.ForegroundColor = prev;
		}
	}
}
