using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Services
{
    public class ServiceData
    {
        public bool IsInitialized;
    }
    public class ServiceContainer
    {
        private readonly Dictionary<IService, ServiceData> _services = new();
        private readonly List<ITickableService> _tickableServices = new();
        private bool _initialized;

        public ServiceContainer Register<T>(T service) where T : class, IService
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }
            
            _initialized = false;
            _services[service] = new ServiceData { IsInitialized = false };
            return this;
        }

        public T Get<T>() where T : class, IService
        {
            foreach (var kvp in _services)
            {
                if (kvp.Key is T service)
                {
                    return service;
                }
            }

            throw new InvalidOperationException(
                $"[Services] Not found: {typeof(T).Name}. Register it in Bootstrap.");
        }

        /// <summary>
        /// Initialize services that haven't been initialized after registration
        /// </summary>
        public void InitDanglingServices()
        {
            foreach (var kvp in _services)
            {
                IService service = kvp.Key;
                ServiceData data = kvp.Value;

                if (!data.IsInitialized)
                {
                    try
                    {
                        service.Initialize(this);
                        data.IsInitialized = true;
                        if (service is ITickableService tickableService)
                        {
                            _tickableServices.Add(tickableService);
                        }
                        Debug.Log($"[Services] Initialized: {service.GetType().Name}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[Services] Failed to initialize {service.GetType().Name}: {e}");
                    }
                }
            }

            _initialized = true;
        }

        public void TickAll()
        {
            foreach (var service in _tickableServices)
            {
                service.Tick();
            }
        }

        public void TickLateAll()
        {
            foreach (var service in _tickableServices)
            {
                service.TickLate();
            }
        }
        
        public void DisposeAll()
        {
            foreach (var kvp in _services)
            {
                try
                {
                    kvp.Key.Dispose();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Services] Failed to dispose {kvp.Key.GetType().Name}: {e}");
                }
            }

            _services.Clear();
            _tickableServices.Clear();
            _initialized = false;
        }
    }
}
