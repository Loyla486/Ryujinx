using System.Collections.Concurrent;

namespace Ryujinx.Core.OsHle
{
    class GlobalStateTable
    {
        private ConcurrentDictionary<Process, IdDictionary> DictByProcess;

        public GlobalStateTable()
        {
            DictByProcess = new ConcurrentDictionary<Process, IdDictionary>();
        }

        public int Add(Process Process, object Obj)
        {
            IdDictionary Dict = DictByProcess.GetOrAdd(Process, (Key) => new IdDictionary());

            return Dict.Add(Obj);
        }

        public object GetData(Process Process, int Id)
        {
            if (DictByProcess.TryGetValue(Process, out IdDictionary Dict))
            {
                return Dict.GetData(Id);
            }

            return null;
        }

        public T GetData<T>(Process Process, int Id)
        {
            if (DictByProcess.TryGetValue(Process, out IdDictionary Dict))
            {
                return Dict.GetData<T>(Id);
            }

            return default(T);
        }

        public bool Delete(Process Process, int Id)
        {
            if (DictByProcess.TryGetValue(Process, out IdDictionary Dict))
            {
                return Dict.Delete(Id);
            }

            return false;
        }
    }
}