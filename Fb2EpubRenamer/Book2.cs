using CommonUtils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BookUtils
{
    public class Book2
    {
        public string Title { get; set; }
        public int Year { get; set; }
        public string Path { get; set; }
        public string Authors { get; set; }

        public override string ToString()
        {
            return $"[{Year}] {Authors}. {Title}";
        }

        public string ClearAuthors
        {
            get
            {
                var src = Authors.Trim().Replace(":", " ").Replace("\"", "'").Replace("|", " ").Replace(@"\", "").Replace("/", "").Replace("*", "").Replace("«", "'").Replace("»", "'");
                var split = src.Split(new string[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
                return split.Length > 2 ? $"{split[0]}, {split[1]} и др." : src;
            }
        }

        public string ClearTitle
        {
            get
            {
                return Title.Trim().Replace(":", "").Replace("\"", "'").Replace("|", "").Replace(@"\", "").Replace("/", "").Replace("*", "").Replace("«", "'").Replace("»", "'").Limit(100);
			}
        }

        public string ProperFileName
        {
            get { return $"{ClearTitle} - {ClearAuthors}{System.IO.Path.GetExtension(Path)}"; }
        }

        public string ProperPath
        {
            get
            {
                return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), ProperFileName);
            }
        }
    }
}

