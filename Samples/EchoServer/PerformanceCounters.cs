using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebSocketListenerTests.Echo
{
    public static class PerformanceCounters
    {
        public static PerformanceCounter MessagesIn, MessagesOut, Connected, Delay, DelayBase;
        static String pflabel_msgIn = "Messages In /sec", pflabel_msgOut = "Messages Out /sec", pflabel_connected = "Connected", pflabel_delay = "Delay ms";

        public static bool CreatePerformanceCounters()
        {
            var categoryName = "WebSocketListener_Test";

            if (!PerformanceCounterCategory.Exists(categoryName))
            {
                var ccdc = new CounterCreationDataCollection();

                ccdc.Add(new CounterCreationData
                {
                    CounterType = PerformanceCounterType.RateOfCountsPerSecond64,
                    CounterName = pflabel_msgIn
                });

                ccdc.Add(new CounterCreationData
                {
                    CounterType = PerformanceCounterType.RateOfCountsPerSecond64,
                    CounterName = pflabel_msgOut
                });

                ccdc.Add(new CounterCreationData
                {
                    CounterType = PerformanceCounterType.NumberOfItems64,
                    CounterName = pflabel_connected
                });

                ccdc.Add(new CounterCreationData
                {
                    CounterType = PerformanceCounterType.AverageTimer32,
                    CounterName = pflabel_delay
                });

                ccdc.Add(new CounterCreationData
                {
                    CounterType = PerformanceCounterType.AverageBase,
                    CounterName = pflabel_delay + " base"
                });

                PerformanceCounterCategory.Create(categoryName, "", PerformanceCounterCategoryType.SingleInstance, ccdc);

                Console.WriteLine("Performance counters have been created, please re-run the app");
                return true;
            }
            else
            {
                //PerformanceCounterCategory.Delete(categoryName);
                //Console.WriteLine("Delete");
                //return true;

                MessagesIn = new PerformanceCounter(categoryName, pflabel_msgIn, false);
                MessagesOut = new PerformanceCounter(categoryName, pflabel_msgOut, false);
                Connected = new PerformanceCounter(categoryName, pflabel_connected, false);
                Delay = new PerformanceCounter(categoryName, pflabel_delay, false);
                DelayBase = new PerformanceCounter(categoryName, pflabel_delay + " base", false);
                Connected.RawValue = 0;

                return false;
            }
        }


    }
}
