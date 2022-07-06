using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Scaffold.Core.Editor.Modules
{
    /// <summary>
    /// Writes a <c>Module</c> object to disk
    /// </summary>
    public class ModuleWriter
    {
        public ModuleWriter(string path)
        {
            _modulePath = path;
        }

        private string _modulePath;

        public void Save(Module module)
        {
            string rawModule = JsonConvert.SerializeObject(module, Formatting.Indented);
            File.WriteAllText(_modulePath, rawModule);
        }
    }
}
