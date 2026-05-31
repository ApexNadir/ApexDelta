using System.Collections;
using System.Reflection;
using ApexDelta.Serialiser;

namespace ApexDelta.Serialiser.Test
{
    public static class SyncSerialiser_Test
    {
        public static void Run()
        {
            SyncObjectGraphSerialiser sendSyncSerialiser = new SyncObjectGraphSerialiser();

            SyncTestClass syncTestClass = new SyncTestClass();
            byte[] data = sendSyncSerialiser.Serialise(syncTestClass);

            SyncObjectGraphSerialiser receiveSyncSerialiser = new SyncObjectGraphSerialiser();
            SyncTestClass syncTestClass2;
            syncTestClass2 = (SyncTestClass)receiveSyncSerialiser.Deserialise(data);





            syncTestClass.Change();
            data = sendSyncSerialiser.SerialiseChange();
            sendSyncSerialiser.ClearChange();




            receiveSyncSerialiser.DeserialiseChange(data);

            if(DeepEquals(syncTestClass, syncTestClass2))
            {
                Console.WriteLine("SyncSerialiser_Test: SyncTestClass objects are equal after deserialisation.");
            }
            else
            {
                Console.WriteLine("SyncSerialiser_Test: SyncTestClass objects are NOT equal after deserialisation.");
                throw new Exception("SyncSerialiser_Test: SyncTestClass objects are NOT equal after deserialisation.");
            }



            sendSyncSerialiser.Clear();
            receiveSyncSerialiser.Clear();

            SyncListChangeMidway slcm = new();
            byte[] data2 = sendSyncSerialiser.Serialise(slcm);
            SyncListChangeMidway slcm2 = receiveSyncSerialiser.Deserialise(data2) as SyncListChangeMidway;

            slcm.Change();
            data2 = sendSyncSerialiser.SerialiseChange();
            sendSyncSerialiser.ClearChange();
            receiveSyncSerialiser.DeserialiseChange(data2);

            if (DeepEquals(slcm, slcm2))
            {
                Console.WriteLine("SyncSerialiser_Test: SyncListChangeMidway objects are equal after deserialisation.");
            }
            else
            {
                Console.WriteLine("SyncSerialiser_Test: SyncListChangeMidway objects are NOT equal after deserialisation.");
                throw new Exception("SyncSerialiser_Test: SyncListChangeMidway objects are NOT equal after deserialisation.");
            }
        }

        public static bool DeepEquals(object? a, object? b, HashSet<(object, object)>? visited = null)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) { 
                Console.WriteLine("one is null", a, b);
                return false;
            }
            if (a.GetType() != b.GetType()) { Console.WriteLine("type mismatch", a, b); return false; }

            visited ??= new();
            if (!a.GetType().IsValueType && visited.Contains((a, b))) return true;
            if (!a.GetType().IsValueType) visited.Add((a, b));

            if (a is IComparable compA)
                return compA.CompareTo(b) == 0;

            if (a is IEnumerable enumA && b is IEnumerable enumB)
            {
                var enumeratorA = enumA.GetEnumerator();
                var enumeratorB = enumB.GetEnumerator();
                while (true)
                {
                    bool hasNextA = enumeratorA.MoveNext();
                    bool hasNextB = enumeratorB.MoveNext();
                    if (hasNextA != hasNextB) { Console.WriteLine("enumerator element count different", a, b); return false; };
                    if (!hasNextA) break;

                    if (!DeepEquals(enumeratorA.Current, enumeratorB.Current, visited)) { return false; }
                }
                return true;
            }

            var fields = a.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                if (field.IsNotSerialized || field.MemberType == MemberTypes.Event || typeof(Delegate).IsAssignableFrom(field.FieldType)) continue;
                var valA = field.GetValue(a);
                var valB = field.GetValue(b);
                if (!DeepEquals(valA, valB, visited)) return false;
            }

            return true;
        }
    }


}
