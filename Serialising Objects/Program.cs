using ApexDelta.Serialiser.Test;


//TODO change all references to weakreferences so GC can do its thang

public static class Program
{
    public static void Main()
    {
        //Console.ReadKey();
        
        Console.WriteLine("Object Graph Serialiser Example");

        Console.WriteLine("Running Serialiser Tests...");
        Serialiser_Test.Run();
        Console.WriteLine("Running Sync Serialiser Tests...");
        SyncSerialiser_Test.Run();
        

        Console.WriteLine("Running DotNet Benchmarks...");
        DotNetBenchmarks.Run();

        Console.WriteLine("Running Serialisation Speed Tests...");
        SerialiseSpeed_Test.Run();

        /*
        try
        {
            Console.WriteLine("Running Serialiser Tests...");
            Serialiser_Test.Run();
            Console.WriteLine("Running Sync Serialiser Tests...");
            SyncSerialiser_Test.Run();
        }
        catch (Exception e)
        {
            Console.WriteLine("An error occurred during serialization tests:");
            Console.WriteLine(e.Message);
            Console.WriteLine(e.StackTrace);
            return;
        }
        */

        Console.WriteLine("Tests completed successfully. Press any key to exit.");
    }
}

