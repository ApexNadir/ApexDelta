using ApexDelta.Serialiser.SyncTypes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApexDelta.Serialiser.Test
{
    public class SerialiseSpeed_Test
    {
        public static void Run()
        {
            //test 1M doubles
            {
                var swcreate = Stopwatch.StartNew();
                SpeedyFoo foo = new();
                swcreate.Stop();
                Console.WriteLine($"Created speedyfoo with {foo.doubleArray.Length} doubles array in {swcreate.ElapsedMilliseconds} ms");

                var sw = Stopwatch.StartNew();
                SyncObjectGraphSerialiser serialiser = new(), deserialiser = new();
                byte[] data = serialiser.Serialise(foo);
                Console.WriteLine($"Serialised {foo.doubleArray.Length} doubles from array in {data.Length} bytes in {sw.ElapsedMilliseconds} ms");
                var sw2 = Stopwatch.StartNew();
                SpeedyFoo foo2 = deserialiser.Deserialise(data) as SpeedyFoo;
                sw2.Stop();
                sw.Stop();
                Console.WriteLine($"Deserialised {foo.doubleArray.Length} doubles from array in {data.Length} bytes in {sw2.ElapsedMilliseconds} ms");
                Console.WriteLine($"Serialised and Deserialised {foo.doubleArray.Length} doubles from array in {data.Length} bytes in {sw.ElapsedMilliseconds} ms");
            }
            

            //test 1M doubles
            {
                var swcreate = Stopwatch.StartNew();
                Foo foo = new();
                swcreate.Stop();
                Console.WriteLine($"Created foo with {foo.hugeDoubleList.Count} doubles in {swcreate.ElapsedMilliseconds} ms");

                var sw = Stopwatch.StartNew();
                SyncObjectGraphSerialiser serialiser = new(), deserialiser = new();
                byte[] data = serialiser.Serialise(foo);
                Console.WriteLine($"Serialised {foo.hugeDoubleList.Count} doubles from list in {data.Length} bytes in {sw.ElapsedMilliseconds} ms");
                var sw2 = Stopwatch.StartNew(); 
                Foo foo2 = deserialiser.Deserialise(data) as Foo;
                sw2.Stop();
                sw.Stop();
                Console.WriteLine($"Deserialised {foo.hugeDoubleList.Count} doubles from list in {data.Length} bytes in {sw2.ElapsedMilliseconds} ms");
                Console.WriteLine($"Serialised and Deserialised {foo.hugeDoubleList.Count} doubles from list in {data.Length} bytes in {sw.ElapsedMilliseconds} ms");
            }

            //test objects and change
            {
                var swcreate = Stopwatch.StartNew();
                Foo2 foo = new();
                swcreate.Stop();
                Console.WriteLine($"Created foo2  with {foo.hugeObjectList.Count} doubles in {swcreate.ElapsedMilliseconds} ms");
                var sw = Stopwatch.StartNew();
                SyncObjectGraphSerialiser serialiser = new(), deserialiser = new();
                byte[] data = serialiser.Serialise(foo);
                Console.WriteLine($"Serialised {foo.hugeObjectList.Count} objects from list in {data.Length} bytes in {sw.ElapsedMilliseconds} ms");
                var sw2 = Stopwatch.StartNew();
                Foo2 foo2 = deserialiser.Deserialise(data) as Foo2;
                sw2.Stop();
                sw.Stop();
                Console.WriteLine($"Deserialised {foo.hugeObjectList.Count} objects from list in {data.Length} bytes in {sw2.ElapsedMilliseconds} ms");
                Console.WriteLine($"Serialised and Deserialised {foo.hugeObjectList.Count} doubles from list in {data.Length} bytes in {sw.ElapsedMilliseconds} ms");
            

                foo.hugeObjectList[30] = new Bar2();
                sw.Restart();

            }
            
            
        }
        private class SpeedyFoo
        {
            public double[] doubleArray;

            public SpeedyFoo()
            {
                doubleArray = new double[1000000];
            }
        }
        private class Foo
        {
            public SyncList<double> hugeDoubleList;
            public Foo()
            {
                hugeDoubleList = new();
                for (int i = 0; i < 1000000; i++)
                {
                    hugeDoubleList.Add(i * 1.1);
                }
            }
        }
        private class Foo2
        {
            public SyncList<Bar2> hugeObjectList;
            public Foo2()
            {
                hugeObjectList = new();
                for (int i = 0; i < 1000000; i++)
                {
                    hugeObjectList.Add(new());
                }
            }
        }
        private class Bar2
        {
            double x, y;
            string guh;
            public Bar2()
            {
                x = 1;
                y = 2;
                guh = "Hello, World!";
            }
        }
    }
}
