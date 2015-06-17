using System;
using System.Diagnostics;
using SvnBridge.Infrastructure.Statistics;

namespace SvnBridge.PerfCounter.Installer
{
    class Program
    {
        static void Main()
        {
            try
            {
                //if (PerformanceCounterCategory.Exists("SvnBridge"))
                //    PerformanceCounterCategory.Delete("SvnBridge");
                ActionTrackingViaPerfCounter.CreatePerfCounters();
                Console.WriteLine("Succesfully create SvnBridge performance counters");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
