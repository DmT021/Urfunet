using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using YnetFS;

namespace YFSDashBoard
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
        
            new Thread(MailLoop).Start();
        }

        private static void MailLoop(object obj)
        {
            while (true)
            {
                Console.Write("Command> ");
                var cmd = Console.ReadLine();
                switch (cmd.ToUpper())
                {
                    case "Q":
                    case "QUIT":
                        {
                            return;
                            break;
                        }
                    default:
                        break;
                }
            }
        }
    }


}
