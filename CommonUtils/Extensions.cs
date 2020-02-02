using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace CommonUtils
{
    public static class Extensions
    {
        public static object _lock = new object();

        public static string Mend(this string str)
        {
            var str1 = str.Replace('`', '\'').Replace('"', '\'').Replace('“', '\'').Replace('”', '\'').Replace('‘', '\'').Replace('’', '\'').Replace('«', '\'').Replace('»', '\'');
            var bytes = Encoding.UTF8.GetBytes(str1);
            var convertedBytes = Encoding.Convert(Encoding.UTF8, Encoding.GetEncoding("windows-1251"), bytes);
            var strToTrim = Encoding.GetEncoding("windows-1251").GetString(convertedBytes);
            if (strToTrim.Length > 4000)
            {
                strToTrim = strToTrim.Substring(0, 4000);
                strToTrim = strToTrim.Substring(0, strToTrim.LastIndexOf("|"));
            }
            return strToTrim;
        }

        public static string DBToString(this object dbobj)
        {
            if (dbobj is DBNull)
                return "";
            else
                return dbobj.ToString();
        }

        public static string JoinDistinct(this XmlNodeList list, string separator)
        {
            var strList = new List<string>();
            foreach (XmlNode node in list)
            {
                var str = node.InnerText;
                if (str != "" && !strList.Contains(str))
                    strList.Add(str);
            }
            return string.Join(separator, strList.ToArray());
        }

        public static string JoinDistinct(this IEnumerable<XElement> list, string separator)
        {
            var strList = new List<string>();
            foreach (var node in list)
            {
                var str = node.Value;
                if (str != "" && !strList.Contains(str))
                    strList.Add(str);
            }
            return string.Join(separator, strList.ToArray());
        }

        public static string JoinDistinctAttribute(this XmlNodeList list, string attr, string separator)
        {
            var strList = new List<string>();
            foreach (XmlNode node in list)
            {
                var str = node.Attributes[attr].Value.ToString();
                if (!string.IsNullOrEmpty(str) && !strList.Contains(str))
                    strList.Add(str);
            }
            return string.Join(separator, strList.ToArray());
        }

        public static string JoinDistinctSanction(this XmlNodeList list, string separator)
        {
            var strList = new List<string>();
            foreach (XmlNode node in list)
            {
                var sanlist = node.Attributes["sanlist"] != null ? node.Attributes["sanlist"].Value : node.Attributes["authority"].Value;
                if (sanlist.Contains("OFAC"))
                {
                    if (node.Attributes["type_name"].Value == "Блокирующие")
                        sanlist += " [SDN]";
                    if (node.Attributes["type_name"].Value == "Неблокирующие")
                    {
                        sanlist += " [SSI]";
                        var extra = node.Attributes["extra_informations"].Value;
                        if (extra.Contains("Subject to Directive 1"))
                            sanlist += "[Dir1]";
                        if (extra.Contains("Subject to Directive 2"))
                            sanlist += "[Dir2]";
                        if (extra.Contains("Subject to Directive 3"))
                            sanlist += "[Dir3]";
                        if (extra.Contains("Subject to Directive 4"))
                            sanlist += "[Dir4]";
                    }
                }
                if (sanlist.Contains("The Restrictive measures (sanctions) in force (EU)"))
                {
                    if (node.Attributes["type_name"].Value == "Блокирующие")
                        sanlist = "EU [Блок.]";
                    if (node.Attributes["type_name"].Value == "Неблокирующие")
                        sanlist = "EU [Неблок.]";
                }
                sanlist = sanlist.Replace("Special Economic and Other Restrictive Measures (Sanctions)", "S.E.O.R.M. (Sanctions)");
                if (!string.IsNullOrEmpty(node.InnerText.Trim()))
                    sanlist += " ((" + node.InnerText.Trim() + "))";
                strList.Add(sanlist);
            }
            return string.Join(separator, strList.ToArray());
        }

        public static string LimitTo(this string s, int limit)
        {
            if (s == null)
                return "";
            if (s.Length < limit)
                return s;
            return s.Substring(0, limit) + "...";
        }

        public static string JoinDistinctAttributes(this XmlNodeList list, string[] attrs, string separator)
        {
            var strList = new List<string>();
            foreach (XmlNode node in list)
            {
                var str = "";
                foreach (var attr in attrs)
                {
                    str += node.NodeAttribute(attr) + ", ";
                }
                str = str.Trim();
                if (!string.IsNullOrEmpty(str) && !strList.Contains(str))
                    strList.Add(str);
            }
            return string.Join(separator, strList.ToArray());
        }

        public static string JoinDistinct(this List<string> list, string separator)
        {
            var strList = new List<string>();
            foreach (var str in list)
            {
                if (str != "" && !strList.Contains(str))
                    strList.Add(str);
            }
            return string.Join(separator, strList.ToArray());
        }

        public static string NodeValue(this XmlNode node, string subnode)
        {
            if (node.SelectSingleNode(subnode) != null)
            {
                return node.SelectSingleNode(subnode).InnerText;
            }
            else
                return "";
        }

        public static string NodeValue(this XElement node, string subnode)
        {
            if (node.Element(subnode) != null)
            {
                return node.Element(subnode).Value;
            }
            else
                return "";
        }

        public static string NodeAttribute(this XmlNode node, string attribute)
        {
            var result = "";
            if (node.Attributes[attribute] != null)
            {
                result = node.Attributes[attribute].Value;
            }
            return result;
        }

        public static string SubNodeAttribute(this XmlNode node, string subnode, string attribute)
        {
            var result = "";
            if (node.SelectSingleNode(subnode) != null)
            {
                var _sub = node.SelectSingleNode(subnode);
                if (_sub.Attributes[attribute] != null)
                {
                    result = _sub.Attributes[attribute].Value;
                }
            }
            return result;
        }

        public static string NodeValue(this XmlNode node, string subnode, XmlNamespaceManager mgr)
        {

            if (node.SelectSingleNode("ns:" + subnode, mgr) != null)
            {
                return node.SelectSingleNode("ns:" + subnode, mgr).InnerText;
            }
            else
                return "";
        }

        public static bool ContainsOneOf(this string str, string[] arr)
        {
            foreach (var s in arr)
            {
                if (str.Contains(s)) return true;
            }
            return false;
        }

        public static IEnumerable<XElement> SelectNodes(this XElement element, string path)
        {
            return element.Elements(path);
        }

        public static string OuterXml(this XElement element)
        {
            var reader = element.CreateReader();
            reader.MoveToContent();
            return reader.ReadOuterXml();
        }

        public static string Limit(this string s, int limit)
        {
            if (s.Length > limit - 3)
            {
                s = s.Substring(0, limit - 3);
                s = s.Substring(0, s.LastIndexOf(" ")) + "...";
            }
            return s;
        }
    }
}
