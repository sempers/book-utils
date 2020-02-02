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
        public int Id { get; set; }
        public int PostId { get; set; }
        public string Title { get; set; }
        public int Year { get; set; }
        public string Path { get; set; }
        public string Authors { get; set; }
        public string Summary { get; set; }
        public string Category { get; set; }


        public override string ToString()
        {
            return $"[{Year}] {Authors}. {Title}";
        }

        public string ClearAuthors
        {
            get
            {
                var src = Authors.Trim().Replace(":", " ").Replace("\"", "'").Replace("|", " ").Replace(@"\", "").Replace("/", "").Replace("*", "");
                var split = src.Split(new string[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
                if (split.Length > 2)
                    return $"{split[0]}, {split[1]} и др.";
                else
                    return src;
            }
        }

        public string ClearTitle
        {
            get
            {
                var src = Title.Trim().Replace(":", "").Replace("\"", "'").Replace("|", "").Replace(@"\", "").Replace("/", "").Replace("*", "").Replace("«", "'").Replace("»", "'").Limit(100);
                src = src.;
                return src;
            }
        }

        public string ProperFileName
        {
            get { return $"{ClearTitle} - {ClearAuthors}{System.IO.Path.GetExtension(Path)}"; }
        }

        public string FirstCategory
        {
            get
            {
                if (Category == null)
                    return "";
                return Category.Split(';')[0];
            }
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

