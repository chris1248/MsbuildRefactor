using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
