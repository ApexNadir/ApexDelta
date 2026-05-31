using ApexDelta.Serialiser.SyncTypes;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using static System.Net.Mime.MediaTypeNames;
using System.Text;

namespace ApexDelta.Serialiser
{
    public class ObjectGraphSerialiser
    {
        protected Dictionary<object, int> _objectToId;
        protected Dictionary<int, object> _idToObject;
        protected int _nextId;
        protected Dictionary<string, Type> seenTypes;
        protected Dictionary<Type, Dictionary<string, FieldInfo>> seenTypeFields;

        protected Dictionary<Type, int> declaredTypes;
        protected Dictionary<int, Type> readTypes;

        public ObjectGraphSerialiser()
        {
            _objectToId = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
            _idToObject = new();
            seenTypes = new();
            seenTypeFields = new();
            declaredTypes = new();
            readTypes = new();
        }

        public virtual void Clear()
        {
            _idToObject.Clear();
            _objectToId.Clear();
            readTypes.Clear();
            declaredTypes.Clear();
            _nextId = 1;
        }
        
        protected void WriteType(BinaryWriter writer, Type type)
        {
            if (declaredTypes.TryGetValue(type, out int typeId))
            {
                writer.Write((ushort)typeId);
            }
            else
            {
                int newId = declaredTypes.Count;
                declaredTypes[type] = newId;
                readTypes[newId] = type;
                writer.Write((ushort)newId);
                //WriteString(writer, type.AssemblyQualifiedName);
                writer.Write(type.AssemblyQualifiedName); // Write type name for later retrieval
            }
        }

        protected Type ReadType(BinaryReader reader)
        {
            Type type;
            int typeId = reader.ReadUInt16();
            bool isDeclared = readTypes.ContainsKey(typeId);
            if (isDeclared)
            {
                if (!readTypes.TryGetValue(typeId, out type))
                {
                    throw new InvalidOperationException($"Type ID {typeId} not found in declared types.");
                }
            }
            else
            {
                //string typeName = ReadString(reader);
                string typeName = reader.ReadString();
                type = getTypeOf(typeName);
                declaredTypes[type] = typeId;
                readTypes[typeId] = type;
            }
            return type;
        }

        public virtual byte[] Serialise(object root)
        {
            Clear();

            TraverseValue(root);

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            writer.Write(seenTypes.Count == 0);

            writer.Write(_objectToId.Count);
            foreach (var pair in _objectToId)
            {
                object obj = pair.Key;
                int id = pair.Value;
                writer.Write(id);
                Type type = obj.GetType();

                WriteType(writer, type);
                if (obj is Array array)
                {
                    writer.Write(array.Length);
                }
            }

            foreach (var pair in _objectToId)
            {
                object obj = pair.Key;
                int id = pair.Value;
                writer.Write(id);
                SerialiseObject(writer, obj);
            }

            writer.Write(_objectToId[root]); // root object ID
            return ms.ToArray();
        }
        


        public virtual byte[] SerialiseParallelSlow(object root)
        {
            Clear();

            TraverseValue(root);

            var objectList = _objectToId.ToList(); // Make a snapshot for thread-safe parallel processing

            // Prepare buffers for parallel sections
            var metaBlocks = new ConcurrentBag<byte[]>();
            var dataBlocks = new ConcurrentBag<byte[]>();

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            writer.Write(seenTypes.Count == 0);
            metaBlocks.Add(ms.ToArray());

            // Serialize metadata in parallel
            Parallel.ForEach(objectList, pair =>
            {
                using var ms = new MemoryStream();
                using var writer = new BinaryWriter(ms);

                object obj = pair.Key;
                int id = pair.Value;

                writer.Write(id);
                writer.Write(obj.GetType().AssemblyQualifiedName);
                if (obj is Array array)
                {
                    writer.Write(array.Length);
                }

                metaBlocks.Add(ms.ToArray());
            });

            // Serialize actual object data in parallel
            Parallel.ForEach(objectList, pair =>
            {
                using var ms = new MemoryStream();
                using var writer = new BinaryWriter(ms);

                int id = pair.Value;
                object obj = pair.Key;

                writer.Write(id);
                lock (obj) // Ensure thread safety if SerialiseObject has side effects
                {
                    SerialiseObject(writer, obj);
                }

                dataBlocks.Add(ms.ToArray());
            });

            // Aggregate results into final stream
            using var finalStream = new MemoryStream();
            using var finalWriter = new BinaryWriter(finalStream);

            finalWriter.Write(_objectToId.Count);

            foreach (var block in metaBlocks.OrderBy(b => BitConverter.ToInt32(b, 0))) // Ensure consistent order
            {
                finalWriter.Write(block);
            }

            foreach (var block in dataBlocks.OrderBy(b => BitConverter.ToInt32(b, 0))) // Ensure consistent order
            {
                finalWriter.Write(block);
            }

            finalWriter.Write(_objectToId[root]); // Write root object ID

            return finalStream.ToArray();
        }
        protected Type getTypeOf(string typeName)
        {
            Type type;
            if (!seenTypes.ContainsKey(typeName))
            {
                type = Type.GetType(typeName);
                seenTypes[typeName] = type;
            }
            else
            {
                type = seenTypes[typeName];
            }
            return type;
        }
        protected Dictionary<string, FieldInfo> getFieldsFor(Type type)
        {
            
            if (!seenTypeFields.ContainsKey(type))
            {
                var fields = GetAllFields(type)
                    .Where(f => !f.IsNotSerialized && f.MemberType != MemberTypes.Event && !typeof(Delegate).IsAssignableFrom(f.FieldType))
                    .ToDictionary(f => f.Name);
                seenTypeFields.Add(type, fields);
            }
            return seenTypeFields[type];
        }

        public static IEnumerable<FieldInfo> GetAllFields(Type type)
        {
            while (type != null)
            {
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                    yield return field;

                type = type.BaseType;
            }
        }


        public virtual object Deserialise(byte[] data)
        {
            Clear();

            _idToObject = new Dictionary<int, object>();

            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);


            bool clearSeenTypes = reader.ReadBoolean();
            if (clearSeenTypes)
            {
                seenTypes.Clear();
            }

            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
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
                DeserialiseObject(reader, _idToObject[id]);
            }




            int rootId = reader.ReadInt32();
            return _idToObject[rootId];
        }


        public int getId(object obj)
        {
            return _objectToId[obj];
        }

        public object getObject(int id)
        {
            return _idToObject[id];
        }

        public T getObject<T>(int id)
        {
            return (T)_idToObject[id];
        }


        public void ClearSeenTypes()
        {
            seenTypes.Clear();
        }



        protected virtual void TraverseValue(object value)
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
                Type elementType = type.IsArray? type.GetElementType() : 
                    (type.IsGenericType ? type.GetGenericArguments()[0] : null);
                if (!elementType.IsPrimitive)
                {
                    foreach (var item in enumerable)
                        TraverseValue(item);
                }
                return;
            }
            
            foreach (var field in getFieldsFor(type).Values)
            {
                var fieldValue = field.GetValue(value);
                TraverseValue(fieldValue);
            }
        }

        protected virtual void SerialiseObject(BinaryWriter writer, object obj)
        {

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

        protected virtual void DeserialiseObject(BinaryReader reader, object obj)
        {
            Type type = obj.GetType();
            Type genericType = type.IsGenericType ? type.GetGenericTypeDefinition() : null;
            if (obj is Array array)
            {
                Type elementType = type.GetElementType();
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

        protected virtual void WriteValue(BinaryWriter writer, object value)
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
                /*
            case FieldType.List:
                var list = (IList)Activator.CreateInstance(expectedType);
                Type itemType = expectedType.GetGenericArguments()[0];
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                    list.Add(ReadValue(reader, itemType));
                return list;
            case FieldType.Array:
                Type arrayType = expectedType.GetElementType();
                int arrayLength = reader.ReadInt32();
                Array array = Array.CreateInstance(arrayType, arrayLength);
                for (int i = 0; i < arrayLength; i++)
                    array.SetValue(ReadValue(reader, arrayType), i);
                return array;

            case FieldType.Dictionary:
                var dict = (IDictionary)Activator.CreateInstance(expectedType);
                Type[] args = expectedType.GetGenericArguments();
                int dictCount = reader.ReadInt32();
                for (int i = 0; i < dictCount; i++)
                {
                    var key = ReadValue(reader, args[0]);
                    var val = ReadValue(reader, args[1]);
                    dict.Add(key, val);
                }
                return dict;
                */
                default:
                    throw new Exception($"Unknown FieldType tag: {tag}");
            }
        }

        protected enum FieldType : byte
        {
            Null = 0,
            Primitive = 1,
            Reference = 2,
            Struct = 3
        }

        protected static bool IsPrimitive(Type t)
        {
            return t.IsPrimitive || t == typeof(string) || t == typeof(decimal)
                   || t == typeof(DateTime) || t == typeof(Guid) || t == typeof(Type) || t.IsSubclassOf(typeof(Type));
        }

        protected void WritePrimitive(BinaryWriter writer, object value)
        {
            switch (value)
            {
                case int i: writer.Write(i); break;
                case float f: writer.Write(f); break;
                case bool b: writer.Write(b); break;
                case string s: 


                    writer.Write(s ?? "");
                    break;
                case double d: writer.Write(d); break;
                case long l: writer.Write(l); break;
                case short s: writer.Write(s); break;
                case byte b: writer.Write(b); break;
                case decimal d: writer.Write(d); break;
                case ulong ul: writer.Write(ul); break;
                case uint ui: writer.Write(ui); break;
                case ushort us: writer.Write(us); break;
                case DateTime dt: writer.Write(dt.ToBinary()); break;
                case Type type: WriteType(writer, type); break;
                default: throw new NotSupportedException("Unsupported primitive: " + value.GetType());
            }
        }

        protected void WriteString(BinaryWriter writer, string str)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(str);
            writer.Write(bytes.Length);  // Or use int for longer strings
            writer.Write(bytes);
        }
        protected string ReadString(BinaryReader reader)
        {
            int length = reader.ReadByte();  // Must match how you wrote it
            byte[] bytes = reader.ReadBytes(length);
            string result = Encoding.ASCII.GetString(bytes);
            return result;
        }

        protected object ReadPrimitive(BinaryReader reader, Type t)
        {
            if (t == typeof(int)) return reader.ReadInt32();
            if (t == typeof(float)) return reader.ReadSingle();
            if (t == typeof(bool)) return reader.ReadBoolean();
            if (t == typeof(string)) return reader.ReadString();
            if (t == typeof(double)) return reader.ReadDouble();
            if (t == typeof(long)) return reader.ReadInt64();
            if (t == typeof(short)) return reader.ReadInt16();
            if (t == typeof(byte)) return reader.ReadByte();
            if (t == typeof(decimal)) return reader.ReadDecimal();
            if (t == typeof(ulong)) return reader.ReadUInt64();
            if (t == typeof(uint)) return reader.ReadUInt32();
            if (t == typeof(ushort)) return reader.ReadUInt16();
            if (t == typeof(DateTime)) return DateTime.FromBinary(reader.ReadInt64());
            if (t == typeof(Type))
            {
                return ReadType(reader);
            }
            throw new NotSupportedException("Unsupported primitive type: " + t);
        }

    }

}