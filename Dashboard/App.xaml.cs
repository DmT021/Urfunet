using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using YFSDashBoard;
using YnetFS;

namespace Dashboard
{
    /// <summary>
    /// Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {

            var v = new MainWindow();
                v.ShowDialog();
        }
        void foo()
        {

            var g = new Stack<int>();
            var c = Enumerable.Range(1, 100).ToList();
            for (int i = 101; i > 2; i--)
            {
                g.Push(i);
            }



            var paisr = new List<clc>();
            int h = 0;
            while (g.Count > 1)
            {
                paisr.Add(new clc() { x = c[h], p = new Pair() { x1 = g.Pop(), x2 = g.Pop() } });
                h++;
                if (g.Count == 1)
                {
                    paisr.Add(new clc() { x = c[h], p = new Pair() { x1 = g.Pop() } });
                    break;
                }
            }

            h = paisr.Count;
            while (paisr.Count > 2)
            {
                var p = paisr.Last();
                var top = paisr.FirstOrDefault(x => x.p.Has(p.x));
                if (top != null)
                {
                    top.p.Addto(p.x, p.p);
                    paisr.Remove(paisr.Last());
                }
            }
            return;
        }
    }

 class clc
 {
 	public int x {get;set;}
	public Pair p{get;set;}
 }
 
class Pair
{
	public int x1{get;set;}
	public int x2{get;set;}
	
	public List<Pair> Trans1{get;set;}
	public List<Pair> Trans2{get;set;}
 
 public Pair()
	{
		Trans1 = new List<Pair>();
		Trans2 = new List<Pair>();
	}
 
 public bool Has(int q)
	{
	return (x1==q||x2==q);
	}
 public void Addto(int q, Pair p)
	{
		if (q==x1)
			Trans1.Add(p);
			else 
			Trans2.Add(p);
	}
}
}
