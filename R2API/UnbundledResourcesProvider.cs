using System;
using System.Collections.Generic;
using R2API.Utils;
using System.Linq;
using System.Collections;
using UnityObject = UnityEngine.Object;

namespace R2API {
    /// <summary>
    /// This class allows loading of resources not stored in an assetbundle with Resources.Load. Primary use would be for generating an item icon in code.
    /// </summary>
    public sealed class UnbundledResourcesProvider : IResourceProvider {
        private readonly Dictionary<string, UnityObject> resources = new Dictionary<string, UnityObject>();

        public string ModPrefix { get; }

        public UnbundledResourcesProvider(string modPrefix) {
            ModPrefix = modPrefix;
        }

        public UnbundledResourcesProvider(string modPrefix, params (string key, UnityObject resource)[] resources) {
            ModPrefix = modPrefix;
            for (var i = 0; i < resources.Length; i++) {
                var (key, resource) = resources[i];
                _ = Store(key, resource, resource.GetType());
            }
        }

        public UnityObject Load(string path, Type type) {
            var key = ConvertToKey(path);
            return resources[key];
        }

        public string Store<TResource>(string path, TResource resource) where TResource : UnityObject {
            return Store(path, resource, typeof(TResource));
        }

        public string Store(string path, UnityObject resource, Type type) {
            string key;
            string fullPath;

            if (IsValidKey(path)) {
                key = path;
                fullPath = ConvertToFullpath(key);
            } else {
                key = ConvertToKey(path);
                fullPath = path;
            }

            if (!type.IsSubclassOf(typeof(UnityObject))) {
                throw new ArgumentException($"Must be a subclass of {typeof(UnityObject).FullName}", nameof(type));
            }

            if (resources.ContainsKey(key)) {
                R2API.Logger.LogWarning($"Resource key: {key} is already present in provider. This will overwrite the existing resource");
            }

            resources[key] = resource;
            return fullPath;
        }

        public void Remove(string path) {
            string key;
            if (IsValidKey(path)) {
                key = path;
            } else if (IsValidFullPath(path)) {
                key = ConvertToKey(path);
            } else {
                throw new KeyNotFoundException($"key: {path} was not found");
            }

            resources.Remove(key);
        }

        public void Remove(UnityObject resource) {
            var fullPath = GetPathForResource(resource);
            resources.Remove(ConvertToKey(fullPath));
        }

        public string GetPathForResource(UnityObject resource) {
            return $"{ModPrefix}:{resources.First((kvp) => kvp.Value == resource).Key}";
        }

        public UnityEngine.ResourceRequest LoadAsync(string path, Type type) {
            var req = new UnityEngine.ResourceRequest();
            var asset = Load(path, type);

            req.SetFieldValue("asset", asset);
            req.SetFieldValue("isDone", true);
            req.SetFieldValue("progress", 1f);
            var call = req.GetFieldValue<Action<UnityEngine.AsyncOperation>>("m_completeCallback");
            var obj = new UnityEngine.GameObject().AddComponent<UnityEngine.MonoBehaviour>();
            obj.StartCoroutine(CallbackRoutine(req, call, obj));
            return req;
        }

        private IEnumerator CallbackRoutine(UnityEngine.ResourceRequest request, Action<UnityEngine.AsyncOperation> callback, UnityEngine.MonoBehaviour self) {
            yield return new UnityEngine.WaitForEndOfFrame();
            callback.Invoke(request);
            UnityObject.Destroy(self.gameObject);
        }

        public UnityObject[] LoadAll(Type type) {
            return resources.Values.Where((obj) => obj.GetType() == type || obj.GetType().IsSubclassOf(type)).ToArray();
        }

        private string ConvertToKey(string fullPath) {
            var split = fullPath.Split(':');
            if (split.Length < 2 || !fullPath.StartsWith("@")) {
                throw new ArgumentException($"Full path was not a valid path. Must be of format: @{ModPrefix}:[key]", nameof(fullPath));
            } else if (split.Length > 2) {
                throw new ArgumentException("Cannot have multiple ':'s", nameof(fullPath));
            } else {
                return split[1];
            }
        }

        private string ConvertToFullpath(string key) {
            if (!IsValidKey(key)) throw new ArgumentException("Must not contain an @ or :", nameof(key));
            return $"@{ModPrefix}:{key}";
        }

        private bool IsValidFullPath(string fullPath) {
            var split = fullPath.Split(':');
            return split.Length == 2 && fullPath.StartsWith("@");
        }

        private bool IsValidKey(string key) {
            return !(key.Contains(":") && key.Contains("@"));
        }
    }
}
