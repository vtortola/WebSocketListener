using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace DataFlowTest
{
    class Program
    {
        static void Main(string[] args)
        {
            test();
            Console.ReadKey();
        }

        private static async void test()
        {
            Func<Int32, Task<String>> transformer = async i => { await Task.Yield(); throw new ArgumentException("whatever error"); };
            TransformBlock<Int32, String> transform = new TransformBlock<int, string>(transformer);
            transform.Post(1);
            transform.Post(2);

            try
            {
                await transform.Completion;
            }
            catch (Exception ex)
            {
                // catch
                Console.WriteLine(ex);
            }
        }
    }
}
