using System;
using System.IO;
class FakeEngine
{
    static void Main(string[] args)
    {
        if(args.Length==0) { System.Threading.Thread.Sleep(60000); return; }
        File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"args.txt"),args);
    }
}
