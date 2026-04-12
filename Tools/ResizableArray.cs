using System;
using System.Collections;
using System.Collections.Generic;

namespace TrueParallax.Tools;

/// <summary>
/// Acts as a sort of fake Dictionary<int, T> where the key is >= 0 and keys are close together.
/// </summary>
/// <typeparam name="T"></typeparam>
public class ResizableArray<T> : IEnumerable<T>
{
    public T[] array = new T[0];
    public bool[] declared = new bool[0];

    public ResizableArray(int initialCapacity)
    {
        array = new T[initialCapacity];
        declared = new bool[initialCapacity];
    }

    public IEnumerator<T> GetEnumerator() => array.GetEnumerator() as IEnumerator<T>;

    IEnumerator IEnumerable.GetEnumerator() => array.GetEnumerator();

    public void Resize(int length)
    {
        Array.Resize(ref array, length);
        Array.Resize(ref declared, length);
    }

    public int Length => array.Length;

    public T this[int idx] {
        get => array[idx];
        set {
            if (idx < 0) throw new IndexOutOfRangeException("Index cannot be negative");
            if (idx >= array.Length) Resize(idx + 1); //increase size if it is needed
            array[idx] = value;
            declared[idx] = true;
        }
    }

    public void Add(int idx, T val) => this[idx] = val;
    public void Remove(int idx)
    {
        array[idx] = default;
        declared[idx] = false;
    }
    public bool ContainsKey(int idx) => idx >= 0 && idx < array.Length && declared[idx];

    public bool TryGetValue(int idx, out T val)
    {
        if (ContainsKey(idx))
        {
            val = this[idx];
            return true;
        }
        val = default;
        return false;
    }
}
