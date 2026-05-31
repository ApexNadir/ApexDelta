using System.Reflection;

namespace ApexDelta.Serialiser.SyncTypes
{
    public static class SyncUtil
    {
        public static void ListenToMySyncVariables(ISyncVariableListener obj)
        {
            if (obj == null) return;

            Type type = obj.GetType();
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var field in fields)
            {
                object? fieldValue = field.GetValue(obj);
                if (fieldValue is ISyncVariable syncVar)
                {
                    syncVar.AddListener(obj);
                }
            }
        }
    }
    public class SyncVariable<T> : ISyncVariable
    {
        private T _value;
        private string _fieldName;
        private HashSet<ISyncVariableListener> _syncVariableListeners;

        public event Action<ISyncVariable> OnValueChanged;

        public IEnumerable<object> TraversalSet
        {
            get
            {
                yield return _value;
            }
        }

        public SyncVariable(bool init)
        {
            _syncVariableListeners = new();
            _value = default;
        }
        private SyncVariable() { }

        public SyncVariable(T initialValue)
        {
            _syncVariableListeners = new();
            _value = initialValue;
        }
        public SyncVariable(T initialValue, ISyncVariableListener listener)
        {
            _syncVariableListeners = new();
            AddListener(listener);
            _value = initialValue;
        }
        public T Value
        {
            get => _value;
            set
            {
                if (_value == null || !_value.Equals(value))
                {
                    _value = value;
                    OnValueChanged?.Invoke(this);
                }
            }
        }

        public void InvokeSyncListeners()
        {
            foreach (var listener in _syncVariableListeners)
                listener.HandleSyncVariableChanged(FieldName);
        }

        public void AddListener(ISyncVariableListener listener)
        {
            if (!_syncVariableListeners.Contains(listener))
            {
                _syncVariableListeners.Add(listener);
            }
        }
        public string FieldName
        {
            get => _fieldName;
            set
            {
                if (_fieldName == null)
                {
                    _fieldName = value;
                }
            }
        }
        public object ValueBoxed => _value;
    }
    public interface ISyncVariable
    {
        event Action<ISyncVariable> OnValueChanged;
        IEnumerable<object> TraversalSet { get; }


        string FieldName { get; set; }
        public void InvokeSyncListeners();
        public void AddListener(ISyncVariableListener listener);
    }

    public interface ISyncVariableListener
    {
        public void HandleSyncVariableChanged(string fieldName);
    }

    public interface ISyncChangeHandler : ISyncVariable
    {
        public bool Resend { get; }
        public object GetChangeData();
        public void ApplyChangeData(object ChangeData);
        public void ClearChangeData();
        public object EmptyChangeData { get; }
    }


    public class SyncHashSet<T> : HashSet<T>, ISyncChangeHandler
    {
        /*
        enum OperationType : byte
        {
            Clear = 0,
            Add = 1,
            Remove = 2
        };
        */
        private interface IOperation
        {
            void Apply(SyncHashSet<T> set);
        }

        private struct AddOperation : IOperation
        {
            public T value;

            public AddOperation(T value)
            {
                this.value = value;
            }

            public void Apply(SyncHashSet<T> set)
            {
                set.Add(value);
            }
        }
        private struct RemoveOperation : IOperation
        {
            public T value;

            public RemoveOperation(T value)
            {
                this.value = value;
            }

            public void Apply(SyncHashSet<T> set)
            {
                set.Remove(value);
            }
        }

        private struct ClearOperation : IOperation
        {
            public void Apply(SyncHashSet<T> set)
            {
                set.Clear();
            }
        }

        public object GetChangeData()
        {
            return ChangeDataList;
        }
        public void ApplyChangeData(object ChangeData)
        {
            var receivedChangeDataList = (List<IOperation>)ChangeData;

            foreach (var changeData in receivedChangeDataList)
            {
                changeData.Apply(this);
            }
        }
        public void ClearChangeData()
        {
            ChangeDataList.Clear();
        }



        [NonSerialized]
        private List<IOperation> ChangeDataList = new();

        public object EmptyChangeData => new List<IOperation>();
        public bool Resend => ChangeDataList.Count >= Count;

        private string _fieldName;
        public string FieldName
        {
            get => _fieldName;
            set
            {
                if (_fieldName == null)
                {
                    _fieldName = value;
                }
            }
        }

        [NonSerialized]
        private HashSet<object> traversalSet = new();
        public IEnumerable<object> TraversalSet
        {
            get
            {
                foreach (var item in traversalSet)
                {
                    yield return item;
                }
            }
        }

        private HashSet<ISyncVariableListener> _syncVariableListeners = new();
        public event Action<ISyncVariable> OnValueChanged;

        public void InvokeSyncListeners()
        {
            foreach (var listener in _syncVariableListeners)
                listener.HandleSyncVariableChanged(FieldName);
        }

        public void AddListener(ISyncVariableListener listener)
        {
            if (!_syncVariableListeners.Contains(listener))
                _syncVariableListeners.Add(listener);
        }



        public new bool Add(T item)
        {
            bool added = base.Add(item);
            if (added)
            {
                ChangeDataList.Add(new AddOperation(item));
                traversalSet.Clear();
                traversalSet.Add(item);
                OnValueChanged?.Invoke(this);
            }

            traversalSet.Clear();
            return added;
        }


        public new void UnionWith(IEnumerable<T> other)
        {
            traversalSet.Clear();
            foreach (T item in other)
            {
                if (base.Add(item))
                {
                    ChangeDataList.Add(new AddOperation(item));
                    traversalSet.Add(item);
                }
            }
            OnValueChanged?.Invoke(this);
            traversalSet.Clear();
        }
        public new bool Remove(T item)
        {
            traversalSet.Clear();
            bool removed = base.Remove(item);
            if (removed)
            {
                ChangeDataList.Add(new RemoveOperation(item));
                OnValueChanged?.Invoke(this);
            }
            return removed;
        }


        public new void Clear()
        {
            traversalSet.Clear();
            if (this.Count > 0)
            {
                base.Clear();
                ChangeDataList.Add(new ClearOperation());
                OnValueChanged?.Invoke(this);
            }
        }
    }

    public class SyncList<T> : List<T>, ISyncChangeHandler
    {

        private string _fieldName;
        public string FieldName
        {
            get => _fieldName;
            set
            {
                if (_fieldName == null)
                {
                    _fieldName = value;
                }
            }
        }

        //public object ValueBoxed => this;


        public event Action<ISyncVariable> OnValueChanged;
        private HashSet<ISyncVariableListener> _syncVariableListeners = new();

        public void InvokeSyncListeners()
        {
            foreach (var listener in _syncVariableListeners)
                listener.HandleSyncVariableChanged(FieldName);
        }
        public void AddListener(ISyncVariableListener listener)
        {
            if (!_syncVariableListeners.Contains(listener))
                _syncVariableListeners.Add(listener);
        }


        public new void Add(T item)
        {
            traverselSet.Clear();
            base.Add(item);
            ChangeDataList.Add(new AddOperation(item));
            traverselSet.Add(item);
            OnValueChanged?.Invoke(this);
            traverselSet.Clear();
        }

        public new void AddRange(IEnumerable<T> collection)
        {
            traverselSet.Clear();
            base.AddRange(collection);
            foreach (var item in collection)
            {
                ChangeDataList.Add(new AddOperation(item));
                traverselSet.Add(item);
            }
            OnValueChanged?.Invoke(this);
            traverselSet.Clear();
        }

        public new bool Remove(T item)
        {
            traverselSet.Clear();
            bool removed = base.Remove(item);
            if (removed)
            {
                ChangeDataList.Add(new RemoveOperation(item));
                OnValueChanged?.Invoke(this);
            }
            return removed;
        }
        public new void RemoveAt(int index)
        {
            traverselSet.Clear();
            base.RemoveAt(index);
            ChangeDataList.Add(new RemoveAtOperation(index));
            OnValueChanged?.Invoke(this);
        }

        public new void Clear()
        {
            traverselSet.Clear();
            if (Count > 0)
            {
                base.Clear();
                ChangeDataList.Add(new ClearOperation());
                OnValueChanged?.Invoke(this);
            }
        }

        public new T this[int index]
        {
            get => base[index];
            set
            {
                traverselSet.Clear();
                var old = base[index];
                base[index] = value;
                ChangeDataList.Add(new InsertAtOperation(index, value));
                traverselSet.Add(value);
                OnValueChanged?.Invoke(this);
                traverselSet.Clear();
            }
        }

        public new void Insert(int index, T item)
        {
            traverselSet.Clear();
            base.Insert(index, item);
            ChangeDataList.Add(new InsertAtOperation(index, item));
            traverselSet.Add(item);
            OnValueChanged?.Invoke(this);
            traverselSet.Clear();
        }


        internal interface IOperation
        {
            void Apply(SyncList<T> list);
        }

        private struct AddOperation : IOperation
        {
            public T value;

            public AddOperation(T value)
            {
                this.value = value;
            }

            public void Apply(SyncList<T> list)
            {
                list.Add(value);
            }
        }

        private struct InsertAtOperation : IOperation
        {
            public int index;
            public T value;

            public InsertAtOperation(int index, T value)
            {
                this.index = index;
                this.value = value;
            }

            public void Apply(SyncList<T> list)
            {
                list.Insert(index, value);
            }
        }
        private struct RemoveOperation : IOperation
        {
            public T value;

            public RemoveOperation(T value)
            {
                this.value = value;
            }

            public void Apply(SyncList<T> list)
            {
                list.Remove(value);
            }
        }
        private struct ClearOperation : IOperation
        {
            public void Apply(SyncList<T> list)
            {
                list.Clear();
            }
        }
        private struct RemoveAtOperation : IOperation
        {
            public int index;

            public RemoveAtOperation(int index)
            {
                this.index = index;
            }

            public void Apply(SyncList<T> list)
            {
                list.RemoveAt(index);
            }
        }
        [NonSerialized]
        private HashSet<T> traverselSet = new();
        public IEnumerable<object> TraversalSet
        {
            get
            {
                foreach (var item in traverselSet)
                {
                    yield return item;
                }
            }
        }

        [NonSerialized]
        private List<IOperation> ChangeDataList = new();
        public bool Resend => ChangeDataList.Count > Count;
        public object EmptyChangeData => new List<IOperation>();
        public object GetChangeData()
        {
            return ChangeDataList;
        }

        public void ApplyChangeData(object ChangeData)
        {
            foreach (var changeData in (List<IOperation>)ChangeData)
            {
                changeData.Apply(this);
            }
        }

        public void ClearChangeData()
        {
            ChangeDataList.Clear();
        }
    }


    public class SyncDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISyncChangeHandler where TKey : notnull
    {
        private string _fieldName;
        public string FieldName
        {
            get => _fieldName;
            set
            {
                if (_fieldName == null)
                    _fieldName = value;
            }
        }

        public event Action<ISyncVariable> OnValueChanged;

        private HashSet<ISyncVariableListener> _syncVariableListeners = new();

        public void AddListener(ISyncVariableListener listener)
        {
            if (!_syncVariableListeners.Contains(listener))
                _syncVariableListeners.Add(listener);
        }

        public void InvokeSyncListeners()
        {
            foreach (var listener in _syncVariableListeners)
                listener.HandleSyncVariableChanged(FieldName);
        }

        public new void Add(TKey key, TValue value)
        {
            traverselSet.Clear();
            base.Add(key, value);
            ChangeDataList.Add(new AddOperation(key, value));
            traverselSet.Add(key);
            traverselSet.Add(value);
            OnValueChanged?.Invoke(this);
            traverselSet.Clear();
        }

        public new bool Remove(TKey key)
        {
            traverselSet.Clear();
            bool removed = base.Remove(key);
            if (removed)
            {
                ChangeDataList.Add(new RemoveOperation(key));
                OnValueChanged?.Invoke(this);
            }
            return removed;
        }

        public new void Clear()
        {
            traverselSet.Clear();
            if (Count > 0)
            {
                base.Clear();
                ChangeDataList.Add(new ClearOperation());
                OnValueChanged?.Invoke(this);
            }
        }

        public new TValue this[TKey key]
        {
            get => base[key];
            set
            {
                traverselSet.Clear();
                base[key] = value;
                ChangeDataList.Add(new AddOperation(key, value)); // treat like overwrite
                traverselSet.Add(key);
                traverselSet.Add(value);
                OnValueChanged?.Invoke(this);
                traverselSet.Clear();
            }
        }

        internal interface IOperation
        {
            void Apply(SyncDictionary<TKey, TValue> dict);
        }

        private struct AddOperation : IOperation
        {
            public TKey Key;
            public TValue Value;

            public AddOperation(TKey key, TValue value)
            {
                Key = key;
                Value = value;
            }

            public void Apply(SyncDictionary<TKey, TValue> dict)
            {
                dict[Key] = Value;
            }
        }

        private struct RemoveOperation : IOperation
        {
            public TKey Key;

            public RemoveOperation(TKey key)
            {
                Key = key;
            }

            public void Apply(SyncDictionary<TKey, TValue> dict)
            {
                dict.Remove(Key);
            }
        }

        private struct ClearOperation : IOperation
        {
            public void Apply(SyncDictionary<TKey, TValue> dict)
            {
                dict.Clear();
            }
        }

        [NonSerialized]
        private HashSet<object> traverselSet = new();
        public IEnumerable<object> TraversalSet
        {
            get
            {
                foreach (var obj in traverselSet)
                    yield return obj;
            }
        }

        [NonSerialized]
        private List<IOperation> ChangeDataList = new();
        public bool Resend => ChangeDataList.Count > Count;
        public object EmptyChangeData => new List<IOperation>();
        public object GetChangeData() => ChangeDataList;

        public void ApplyChangeData(object ChangeData)
        {
            foreach (var op in (List<IOperation>)ChangeData)
                op.Apply(this);
        }

        public void ClearChangeData()
        {
            ChangeDataList.Clear();
        }
    }
}
