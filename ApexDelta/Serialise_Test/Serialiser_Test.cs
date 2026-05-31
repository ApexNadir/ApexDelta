using ApexDelta.Serialiser;

namespace ApexDelta.Serialiser.Test
{
    
    public static class Serialiser_Test
    {
        public static void Run()
        {

            ObjectGraphSerialiser serialiser = new();

            Foo foo = new();
            byte[] dataFoo = serialiser.Serialise(foo);
            foo = null;
            foo = serialiser.Deserialise(dataFoo) as Foo;


            ArrayHolder2 ah2 = new();
            byte[] dataAh2 = serialiser.Serialise(ah2);
            ah2 = null;
            ah2 = serialiser.Deserialise(dataAh2) as ArrayHolder2;

            ArrayHolder ah = new();
            byte[] dataAh = serialiser.Serialise(ah);
            ah = null;
            ah = serialiser.Deserialise(dataAh) as ArrayHolder;


            StructClassTest sct = new();
            byte[] dataSct = serialiser.Serialise(sct);
            sct = null;
            sct = serialiser.Deserialise(dataSct) as StructClassTest;


            Bar bar = new();



            byte[] data = serialiser.Serialise(bar);

            Bar bar2 = (Bar)serialiser.Deserialise(data);


            bar.guh = new int[0];

            //Console.WriteLine($"Serialised {data.Length} bytes");
        }
    }



}
