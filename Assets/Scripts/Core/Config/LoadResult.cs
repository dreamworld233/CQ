using System;
using System.Collections.Generic;

namespace CQ.Core.Config
{
    [Serializable]
    public class LoadResult<T>
    {
        public List<T> items = new List<T>();
        public List<string> errors = new List<string>();

        public bool Ok => errors.Count == 0;
    }
}