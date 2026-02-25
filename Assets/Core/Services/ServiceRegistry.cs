using System;
using System.Collections.Generic;

namespace DVBARPG.Core.Services
{
    public sealed class ServiceRegistry
    {
        private readonly Dictionary<Type, object> _services = new();

        public void Register<T>(T instance) where T : class
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            _services[typeof(T)] = instance;
        }

        public bool TryGet<T>(out T instance) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var obj))
            {
                instance = (T)obj;
                return true;
            }

            instance = null;
            return false;
        }

        public T Get<T>() where T : class
        {
            if (TryGet<T>(out var instance)) return instance;
            throw new InvalidOperationException($"Service not registered: {typeof(T).Name}");
        }
    }
}
