using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;
using ApexDelta.Serialiser;
public static class DotNetBenchmarks
{
    public static void Run()
    {
        Order order = Order.Create();

        for (int i = 0; i < 10; i++)
        {
            Stopwatch sw = Stopwatch.StartNew();
            ObjectGraphSerialiser serialiser = new(), deserialiser = new();

            byte[] data = serialiser.Serialise(order);
            sw.Stop();

            double nanosecondsPerTick = (1_000_000_000.0) / Stopwatch.Frequency;
            double nanoseconds = sw.ElapsedTicks * nanosecondsPerTick;

            Console.WriteLine($"DOTNETBENCHMARK-ApexDelta Serialised {data.Length} bytes in {nanoseconds} ns");
        }
        for(int i=0;i<10;i++)
        {
            Stopwatch sw = Stopwatch.StartNew();

            string json = JsonConvert.SerializeObject(order);
            sw.Stop();

            double nanosecondsPerTick = (1_000_000_000.0) / Stopwatch.Frequency;
            double nanoseconds = sw.ElapsedTicks * nanosecondsPerTick;

            byte[] bytes = Encoding.ASCII.GetBytes(json);
            Console.WriteLine($"DOTNETBENCHMARK-NewtonsoftJson Serialised {bytes.Length} bytes in {nanoseconds} ns");
        }
    }

    public class Order
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string Name { get; set; }

        public string Category { get; set; }

        public long TotalAmount { get; set; }

        public string User { get; set; }

        public static Order Create()
        {
            return new Order
            {
                Name = "Book Order",
                Category = "Books",
                TotalAmount = 100,
                User = Guid.NewGuid().ToString()
            };
        }
    }
}
