using System;

namespace Capetown.Models
{
    public class InstallEsDataSettings
    {
        public string[] Cluster { get; set; }

        public EsIndex[] Indices { get; set; }

    }

    public class EsIndex
    {
        public string Key { get; set; }
        public string Name { get; set; }

    }



}
