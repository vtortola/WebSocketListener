using System;
using System.Diagnostics;

// ReSharper disable once CheckNamespace
namespace vtortola.WebSockets
{
    public static class PerformanceCounters
    {
        public static PerformanceCounter MessagesIn, MessagesOut, Connected, Delay, DelayBase;
        private static readonly string pflabel_msgIn = "Messages In /sec";
        private static readonly string pflabel_msgOut = "Messages Out /sec";
        private static readonly string pflabel_connected = "Connected";
        private static readonly string pflabel_delay = "Delay ms";

        public static bool CreatePerformanceCounters()
        {
            var categoryName = "WebSocketListener_Test";

            if (!PerformanceCounterCategory.Exists(categoryName))
            {
                var ccdc = new CounterCreationDataCollection();

                ccdc.Add(new CounterCreationData {
                    CounterType = PerformanceCounterType.RateOfCountsPerSecond64,
                    CounterName = pflabel_msgIn
                });

                ccdc.Add(new CounterCreationData {
                    CounterType = PerformanceCounterType.RateOfCountsPerSecond64,
                    CounterName = pflabel_msgOut
                });

                ccdc.Add(new CounterCreationData {
                    CounterType = PerformanceCounterType.NumberOfItems64,
                    CounterName = pflabel_connected
                });

                ccdc.Add(new CounterCreationData {
                    CounterType = PerformanceCounterType.AverageTimer32,
                    CounterName = pflabel_delay
                });

                ccdc.Add(new CounterCreationData {
                    CounterType = PerformanceCounterType.AverageBase,
                    CounterName = pflabel_delay + " base"
                });

                PerformanceCounterCategory.Create(categoryName, "", PerformanceCounterCategoryType.SingleInstance, ccdc);

                Console.WriteLine("Performance counters have been created, please re-run the app");
                return true;
            }

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
