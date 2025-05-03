using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JinReporter
{
    public class TemplateInfo
    {
        public string TemplateName { get; set; }  // 如"UK"、"DE"
        public string SheetName { get; set; }    // 完整的Sheet名如"模板_UK"
    }

    public class DataSourceConfig
    {
        public string TemplateName { get; set; }
        public string CountryTablePath { get; set; }
        public string ProductTablePath { get; set; }
    }
}
