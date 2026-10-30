using System;
using System.Collections.Generic;
using System.Reflection;

namespace AtariGo.Client.Tests.Services
{
    public class RecordingNavigationProxy : DispatchProxy
    {
        private readonly List<string> _calls = new List<string>();

        public IReadOnlyList<string> Calls
        {
            get
            {
                return _calls;
            }
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            _calls.Add(targetMethod.Name);
            return null;
        }
    }
}
