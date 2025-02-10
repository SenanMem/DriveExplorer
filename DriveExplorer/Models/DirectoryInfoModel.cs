using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DriveExplorer.Models
{
    public class DirectoryInfoModel
    {
        public string Directory { get; set; }
        public int FileCount { get; set; }
        public double TotalSize { get; set; }
    }
}
