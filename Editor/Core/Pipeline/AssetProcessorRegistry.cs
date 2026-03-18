using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SemanticSearch.Editor.Core.LLM;

namespace SemanticSearch.Editor.Core.Pipeline
{
    public class AssetProcessorRegistry
    {
        readonly List<IAssetProcessor> _processors = new List<IAssetProcessor>();
        string[] _allExtensionsCache;

        public AssetProcessorRegistry(IVisionClient vlClient, IEmbeddingClient embeddingClient)
        {
            Register(new ImageAssetProcessor(vlClient, embeddingClient));
            Register(new PrefabAssetProcessor(vlClient, embeddingClient));
        }

        public void Register(IAssetProcessor processor)
        {
            _processors.Add(processor);
            _allExtensionsCache = null;
        }

        public IAssetProcessor GetProcessor(string assetPath)
        {
            for (int i = 0; i < _processors.Count; i++)
            {
                if (_processors[i].CanProcess(assetPath))
                    return _processors[i];
            }
            return null;
        }

        public string[] GetAllSupportedExtensions()
        {
            if (_allExtensionsCache != null)
                return _allExtensionsCache;

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in _processors)
            {
                foreach (var ext in p.SupportedExtensions)
                    set.Add(ext);
            }
            _allExtensionsCache = set.ToArray();
            return _allExtensionsCache;
        }

        public bool IsSupported(string assetPath)
        {
            var ext = Path.GetExtension(assetPath).ToLowerInvariant();
            var all = GetAllSupportedExtensions();
            return Array.IndexOf(all, ext) >= 0;
        }
    }
}
