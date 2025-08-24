using ASA_Save_Inspector.ObjectModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASA_Save_Inspector
{
    public class CustomList<T> : IList<T>
    {
        public List<T> _list = new List<T>();

        public T this[int index]
        {
            get { return _list[index]; }
            set { _list[index] = value; }
        }

        public int Count => _list.Count;

        public bool IsReadOnly => false;

        public void Add(T item)
        {
            _list.Add(item);
        }

        public void Clear()
        {
            _list.Clear();
        }

        public bool Contains(T item)
        {
            return _list.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _list.CopyTo(array, arrayIndex);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        public int IndexOf(T item)
        {
            return _list.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            _list.Insert(index, item);
        }

        public bool Remove(T item)
        {
            return _list.Remove(item);
        }

        public void RemoveAt(int index)
        {
            _list.RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        public override string ToString()
        {
            Type template = typeof(T);
            if (template == typeof(int?))
            {
                string ret = "";
                foreach (var val in _list)
                {
                    if (ret.Length > 0)
                        ret += ", ";
                    int? tmp = val as int?;
                    if (tmp != null && tmp.HasValue)
                        ret += tmp.Value.ToString(CultureInfo.InvariantCulture);
                    else
                        ret += "null";
                }
                return ret;
            }
            else if (template == typeof(long?))
            {
                string ret = "";
                foreach (var val in _list)
                {
                    if (ret.Length > 0)
                        ret += ", ";
                    long? tmp = val as long?;
                    if (tmp != null && tmp.HasValue)
                        ret += tmp.Value.ToString(CultureInfo.InvariantCulture);
                    else
                        ret += "null";
                }
                return ret;
            }
            else if (template == typeof(double?))
            {
                string ret = "";
                foreach (var val in _list)
                {
                    if (ret.Length > 0)
                        ret += ", ";
                    double? tmp = val as double?;
                    if (tmp != null && tmp.HasValue)
                        ret += tmp.Value.ToString(CultureInfo.InvariantCulture);
                    else
                        ret += "null";
                }
                return ret;
            }
            else
            {
                string ret = "";
                foreach (var val in _list)
                    if (val != null)
                    {
                        if (ret.Length > 0)
                            ret += ", ";
                        ret += val.ToString();
                    }
                return ret;
            }
        }
    }
}
