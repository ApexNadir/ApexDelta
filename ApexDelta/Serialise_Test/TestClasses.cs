using ApexDelta.Serialiser.SyncTypes;

namespace ApexDelta.Serialiser.Test
{

    internal class StructClassTest
    {
        MyStruct structTest;
        MyReferenceContainingStruct structTest2;
        public StructClassTest()
        {
            structTest = new MyStruct(10, 20);
            structTest2 = new MyReferenceContainingStruct();
        }
    }

    internal struct MyStruct
    {
        public int x;
        public int y;

        public MyStruct(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }


    internal struct MyReferenceContainingStruct
    {

        public Foo foo;

        public MyReferenceContainingStruct()
        {
            this.foo = new();
        }
    }

    internal class ArrayHolder
    {
        int[] foos;
        public ArrayHolder()
        {
            foos = new int[3];
            foos[0] = 1;
            foos[1] = 42;
            foos[2] = 100;
        }
    }
    internal class ArrayHolder2
    {
        List<int> foos;
        public ArrayHolder2()
        {
            foos = new(3);
            foos.Add(1);
            foos.Add(42);
            foos.Add(100);
        }
    }

    internal class Bar
    {
        Dictionary<int, Foo> fooDict;

        List<Foo> fooList;

        public int[] guh;
        Bar[] guh2;


        decimal test;

        MyStruct structTest;

        public Bar()
        {
            fooDict = new();
            fooDict.Add(1, new Foo());

            fooDict.Add(2, new Foo());
            fooDict.Add(3, new Foo());
            fooList = new();
            fooList.Add(fooDict[2]);
            fooList.Add(fooDict[1]);


            guh = new int[3];
            guh[0] = 4;
            guh[1] = 2;
            guh[2] = 3;

            guh2 = new Bar[1];
            guh2[0] = this;


            test = 1000M;

            structTest = new MyStruct(10, 20);
        }
    }

    internal class Foo
    {
        int x, y;
        public float f;
        string str;

        public Foo()
        {
            x = 1;
            y = 2;
            f = 3.4f;
            str = "Hello, World!";
        }
    }

    interface IExample
    {
        void DoSomething();
    }

    internal class Example : IExample
    {
        public void DoSomething()
        {
            Console.WriteLine("Doing something in Example class.");
        }
    }

    internal class SyncTestClass : ISyncVariableListener
    {
        public SyncVariable<int> syncInt;
        public SyncVariable<string> syncString;
        public SyncVariable<SyncFooTest> syncFoo;
        public SyncList<float> syncList;
        public SyncList<(int x, int y)> syncListStruct;
        public SyncList<SyncFooTest> syncListClass;
        public SyncList<IExample> exampleList;

        public SyncHashSet<double> syncHashSet;

        public SyncTestClass()
        {
            syncInt = new SyncVariable<int>(0, this);
            syncString = new SyncVariable<string>("Initial");
            syncFoo = new(new SyncFooTest());
            syncFoo.Value.Change();

            syncList = new();


            syncListStruct = new();
            syncListStruct.Add((1, 2));

            syncListClass = new();
            syncListClass.Add(syncFoo.Value);


            syncHashSet = new();
            syncHashSet.Add(1.1);

            exampleList = new();
            exampleList.Add(new Example());
        }

        public void Change()
        {
            syncInt.Value = 42;
            syncString.Value = "Changed";
            syncFoo.Value = new();

            syncList.Add(3.4f);

            syncListStruct.Add((3, 4));

            syncListClass.Add(new());
            syncHashSet.Add(1.3);
        }

        public void HandleSyncVariableChanged(string fieldName)
        {
            switch (fieldName)
            {
                case "syncInt":
                    Console.WriteLine($"syncInt changed to {syncInt.Value}");
                    break;
            }
        }
    }

    internal class SyncFooTest
    {
        public SyncVariable<int> syncInt;
        public SyncVariable<string> syncString;

        public SyncFooTest()
        {
            syncInt = new SyncVariable<int>(0);
            syncString = new SyncVariable<string>("Initial");
        }

        public void Change()
        {
            syncInt.Value = 42;
            syncString.Value = "Changed";
        }
    }


    internal class SyncListChangeMidway
    {
        SyncHashSet<Foo> syncSet;

        public SyncListChangeMidway()
        {
            syncSet = new();
            syncSet.Add(new Foo());
            syncSet.Add(new Foo());
            syncSet.Add(new Foo());
        }


        public void Change()
        {
            Foo newFoo = new Foo();
            syncSet.Add(newFoo);
            syncSet.Remove(newFoo);
        }
    }
}
