using System.Collections;
using System.Reflection;
using ApexDelta.Serialiser.SyncTypes;

namespace ApexDelta.Serialiser
{

    public class SyncObjectGraphSerialiser : ObjectGraphSerialiser
    {
        /*

                bool isSyncVariable = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(SyncVariable<>);
                if (isSyncVariable)
                {

                }
         * */

        /*
        public struct Diff
        {
            public int objId;
            public bool isProperty;
            public string memberName;
            public object value;

            public void Serialise()
            {

            }
        }
        */

        private HashSet<int> objectsChangedSet;
        private HashSet<int> objectsCreatedSet;

        private List<Action> syncNotifyActionList;

        public SyncObjectGraphSerialiser() : base()
        {
            objectsCreatedSet = new();
            objectsChangedSet = new();
            syncNotifyActionList = new();
        }

        private void HandleSyncVariableChanged(ISyncVariable syncVar)
        {
            int startingId = _nextId;
            foreach(var item in syncVar.TraversalSet)
            {
                TraverseValue(item);
            }

            if(startingId != _nextId)
            {
                for(int i=startingId;i < _nextId; i++)
                {
                    objectsCreatedSet.Add(i);
                }
                //we need to serialise the new objects
            }

            int id = _objectToId[syncVar];
            objectsChangedSet.Add(id);
            /*
            T newValue = syncVar.Value;
            object newValueObj = newValue is null ? null : (object)newValue;
            if (!objectDiffs.ContainsKey(id)){ objectDiffs.Add(id, new()); }
            objectDiffs[id].Add("Value", (true, newValue));
            */
        }

        public byte[] SerialiseChange()
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            //new objects create instances
            writer.Write(objectsCreatedSet.Count);
            foreach (int id in objectsCreatedSet)
            {

                object obj = _idToObject[id];
                writer.Write(id);
                WriteType(writer, obj.GetType());
                if (obj is Array array)
                {
                    writer.Write(array.Length);
                }

                /*
                writer.Write(id);
                SerialiseObject(writer, _idToObject[id]);
                */
            }
            foreach (int id in objectsCreatedSet)
            {

                object obj = _idToObject[id];
                writer.Write(id);
                SerialiseObject(writer, obj);
                /*
                writer.Write(id);
                SerialiseObject(writer, _idToObject[id]);
                */
            }


            //changed objects
            writer.Write(objectsChangedSet.Count);
            foreach (int id in objectsChangedSet)
            {
                writer.Write(id);
                var obj = _idToObject[id];
                SerialiseObject(writer, obj);

            }

            return ms.ToArray();
        }

        public void DeserialiseChange(byte[] data)
        {
            syncNotifyActionList.Clear();

            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                /*
                int id = reader.ReadInt32();
                object obj = _idToObject[id];
                DeserialiseObject(reader, obj);
                */

                int id = reader.ReadInt32();
                Type type = ReadType(reader);
                object obj;

                if (type.IsArray)
                {
                    var elementType = type.GetElementType();
                    int length = reader.ReadInt32();
                    obj = Array.CreateInstance(elementType, length);
                }
                else
                {
                    obj = Activator.CreateInstance(type, true);
                }

                _idToObject[id] = obj;


            }

            for (int i = 0; i < count; i++)
            {
                int id = reader.ReadInt32();
                object obj = _idToObject[id];
                DeserialiseObject(reader, obj);
                if(obj is ISyncVariable syncVar)
                {
                    syncNotifyActionList.Add(syncVar.InvokeSyncListeners);
                }
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                int id = reader.ReadInt32();
                object obj = _idToObject[id];
                DeserialiseObject(reader, obj);
            }

            foreach(var action in syncNotifyActionList)
            {
                action?.Invoke();
            }

            syncNotifyActionList.Clear();
        }
        public override void Clear()
        {
            base.Clear();
            ClearChange();
        }
        public void ClearChange()
        {
            objectsCreatedSet.Clear();
            objectsChangedSet.Clear();
            syncNotifyActionList.Clear();
        }
        protected override void SerialiseObject(BinaryWriter writer, object obj)
        {
            if(obj is ISyncChangeHandler syncChangeHandler)
            {
                writer.Write(syncChangeHandler.Resend);
                if (!syncChangeHandler.Resend)
                {
                    SerialiseObject(writer, syncChangeHandler.GetChangeData());
                    syncChangeHandler.ClearChangeData();
                    return;
                }
                syncChangeHandler.ClearChangeData();
            }
            Type type = obj.GetType();
            Type genericType = type.IsGenericType ? type.GetGenericTypeDefinition() : null;

            if (obj is Array array)
            {
                foreach (var item in array)
                    WriteValue(writer, item);
            }
            else if (genericType == typeof(HashSet<>) || genericType == typeof(SyncHashSet<>))
            {
                int count = (int)obj.GetType().GetProperty("Count")!.GetValue(obj)!;
                writer.Write(count);
                IEnumerable enumerable = (IEnumerable)obj;
                foreach (var item in enumerable)
                    WriteValue(writer, item);
            }
            else if (obj is IList list)
            {
                writer.Write(list.Count);
                foreach (var item in list)
                    WriteValue(writer, item);
            }
            /*else if (type.IsGenericType && genericType == typeof(HashSet<>)){
                HashSet<> values = (HashSet<>)obj;
            }
            */
            else if (obj is IDictionary dict)
            {
                writer.Write(dict.Count);
                foreach (DictionaryEntry entry in dict)
                {
                    WriteValue(writer, entry.Key);
                    WriteValue(writer, entry.Value);
                }
            }
            else
            {

                //generic
                //var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                var fields = getFieldsFor(type);
                writer.Write(fields.Count);

                foreach (var field in fields.Values)
                {
                    writer.Write(field.Name);
                    WriteValue(writer, field.GetValue(obj));
                }
            }
        }

        protected override void DeserialiseObject(BinaryReader reader, object obj)
        {
            if (obj is ISyncChangeHandler syncChangeHandler)
            {
                bool Resend = reader.ReadBoolean(); // read the resend flag
                if (!Resend)
                {
                    object ChangeData = syncChangeHandler.EmptyChangeData;
                    DeserialiseObject(reader, ChangeData);
                    syncChangeHandler.ApplyChangeData(ChangeData);
                    return;
                }
            }


            Type type = obj.GetType();
            Type genericType = type.IsGenericType ? type.GetGenericTypeDefinition() : null;
            if (obj is Array array)
            {
                Type elementType = obj.GetType().GetElementType();
                //array
                for (int i = 0; i < array.Length; i++)
                {
                    array.SetValue(ReadValue(reader, elementType), i);
                }
            }
            else if (genericType == typeof(HashSet<>) || genericType == typeof(SyncHashSet<>))
            {
                Type argType = type.GetGenericArguments()[0];

                var addMethod = type.GetMethod("Add"); // Look up the Add method
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    addMethod?.Invoke(obj, new object[] { ReadValue(reader, argType) }); // Call Add(item)
                }
            }
            else if (obj is IList list)
            {
                list.Clear();
                int count = reader.ReadInt32();
                Type argType = type.GetGenericArguments()[0];
                for (int i = 0; i < count; i++)
                {
                    list.Add(ReadValue(reader, argType));
                }
            }
            else if (obj is IDictionary dict)
            {
                dict.Clear();
                int count = reader.ReadInt32();

                Type keyType = type.GetGenericArguments()[0];
                Type valueType = type.GetGenericArguments()[1];

                for (int i = 0; i < count; i++)
                {
                    object key = ReadValue(reader, keyType);
                    object value = ReadValue(reader, valueType);
                    dict.Add(key, value);
                }
            }
            else
            {
                //generic
                var fields = getFieldsFor(type);

                int fieldCount = reader.ReadInt32();
                for (int i = 0; i < fieldCount; i++)
                {
                    string name = reader.ReadString();
                    if (fields.TryGetValue(name, out var field))
                    {
                        object value = ReadValue(reader, field.FieldType);
                        field.SetValue(obj, value);
                    }
                    else
                    {
                        // Field not found, skip value
                        ReadValue(reader, typeof(object));
                    }
                }
            }
        }
        protected override void TraverseValue(object value)
        {
            if (value == null)
                return;

            Type type = value.GetType();
            if (IsPrimitive(type))
            {
                return;
            }

            if (_objectToId.ContainsKey(value)) return;




            int id = _nextId++;
            _objectToId[value] = id;
            _idToObject[id] = value;

            /*
            Type genericType = type.GetGenericTypeDefinition();
            Type[] genericArgs = type.GetGenericArguments();
            if (genericType == typeof(SyncVariable<>))
            {
                Type t = genericArgs[0];
                SyncVariable<t> syncVar = (SyncVariable<dynamic>)value;
                syncVar.OnValueChanged += HandleSyncVariableChanged;
                //var fieldValue = type.GetProperty("Value")?.GetValue(value);
            }
            */
            if (value is IDictionary dict)
            {
                foreach (DictionaryEntry entry in dict)
                {
                    TraverseValue(entry.Key);
                    TraverseValue(entry.Value);
                }
                return;
            }
            if (value is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                    TraverseValue(item);
                return;
            }

            var fields = getFieldsFor(type);
            foreach (var field in fields.Values)
            {
                var fieldValue = field.GetValue(value);
                if (fieldValue is ISyncVariable syncVar)
                {
                    syncVar.OnValueChanged += HandleSyncVariableChanged;
                    syncVar.FieldName = field.Name;
                }
                TraverseValue(fieldValue);
            }
        }
        /*
        protected override void SerialiseObject(BinaryWriter writer, object obj)
        {
            if (obj is Array array)
            {
                foreach (var item in array)
                    WriteValue(writer, item);
            }
            else if (obj is IEnumerable enumerable)
            {
                int count;
                if (obj is ICollection rawColl)
                    count = rawColl.Count;
                else
                    count = enumerable.Cast<object>().Count(); // fallback

                writer.Write(count);
                foreach (var item in enumerable)
                    WriteValue(writer, item);
            }
            else if (obj is IDictionary dict)
            {
                writer.Write(dict.Count);
                foreach (DictionaryEntry entry in dict)
                {
                    WriteValue(writer, entry.Key);
                    WriteValue(writer, entry.Value);
                }
            }
            else
            {

                //generic
                var type = obj.GetType();
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                writer.Write(fields.Length);

                foreach (var field in fields)
                {
                    if (field.IsNotSerialized || field.MemberType == MemberTypes.Event || typeof(Delegate).IsAssignableFrom(field.FieldType))
                    {
                        writer.Write(false); // field is skipped
                        continue;
                    }

                    writer.Write(true); // field is included
                    writer.Write(field.Name);
                    WriteValue(writer, field.GetValue(obj));
                }
            }
        }
        */
        protected override void WriteValue(BinaryWriter writer, object value)
        {
            if (value == null)
            {
                writer.Write((byte)FieldType.Null);
                return;
            }

            Type type = value.GetType();
            if (IsPrimitive(type))
            {
                writer.Write((byte)FieldType.Primitive);
                WritePrimitive(writer, value);
            }
            else if (type.IsValueType)
            {
                writer.Write((byte)FieldType.Struct);
                WriteType(writer, type);
                SerialiseObject(writer, value); // Serialise inline — no ID
            }
            else if (_objectToId.ContainsKey(value))
            {
                writer.Write((byte)FieldType.Reference);
                writer.Write(_objectToId[value]);
            }
            else
            {
                //must be an object that is not in scope anymore
                writer.Write((byte)FieldType.Null);
                return;
            }
        }


        protected object ReadValue(BinaryReader reader, Type expectedType)
        {
            byte tag = reader.ReadByte();
            switch ((FieldType)tag)
            {
                case FieldType.Null:
                    return null;
                case FieldType.Primitive:
                    return ReadPrimitive(reader, expectedType);
                case FieldType.Struct:
                    Type structType = ReadType(reader);
                    var structInstance = Activator.CreateInstance(structType);
                    DeserialiseObject(reader, structInstance);
                    return structInstance;
                case FieldType.Reference:
                    int refId = reader.ReadInt32();
                    return _idToObject[refId];
                default:
                    throw new Exception($"Unknown FieldType tag: {tag}");
            }
        }


        public void AddObject(object obj)
        {
            if (obj == null || IsPrimitive(obj.GetType())) return;

            if (_objectToId.ContainsKey(obj)) return;

            int id = _nextId++;
            _objectToId[obj] = id;
        }
    }

}
