using System;
using System.Xml;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using static System.Net.Mime.MediaTypeNames;
using System.Text;

namespace PatchSequenceUnpack
{
    internal class Program
    {
        static XmlWriter result;

        static string dir = System.IO.Directory.GetCurrentDirectory();

        static string outputDir;

        static int i;

        static int j;

        static void Main(string[] args)
        {
            Console.WriteLine("Input the patch folder's path");
            dir = Console.ReadLine();

            Console.WriteLine("Input the output folder's path");
            outputDir = Path.Combine(Console.ReadLine(), "Output");

            DirectoryInfo directoryInfo = new DirectoryInfo(dir);
            Console.WriteLine("Output " + outputDir);
            Directory.CreateDirectory(outputDir);

            foreach (FileInfo file in directoryInfo.GetFiles("*.xml", SearchOption.AllDirectories))
            {
                Read(file);
            }
            Console.WriteLine($"Converted patches stored at {outputDir}");
            Console.WriteLine($"De-Sequencing of {i} files and {j} patches Complete, Press any key to exit.");
            Console.ReadKey();
        }

        static void Read(FileInfo file)
        {
            Console.WriteLine("Reading " + file.Name);
            try
            {
                XmlDocument xmlDoc;
                XmlReaderSettings settings = new XmlReaderSettings
                {
                    IgnoreComments = true,
                    IgnoreWhitespace = true,
                    CheckCharacters = false
                };

                StringReader input = new StringReader(File.ReadAllText(file.FullName));
                XmlReader xmlReader = XmlReader.Create(input, settings);
                xmlDoc = new XmlDocument();
                xmlDoc.Load(xmlReader);

                if (!IsPatch(xmlDoc))
                {
                    Console.WriteLine(file.FullName + " is not a patch");
                    return;
                }
                XmlWriterSettings writterSetting = new XmlWriterSettings();
                writterSetting.Indent = true;
                writterSetting.IndentChars = "\t";

                StringBuilder s = new StringBuilder();
                s.Append(file.FullName);
                s.Replace(dir, "");
                s.Remove(0, 1);
                string filepath = s.ToString();
                string firstFolder = filepath.Split(@"\".ToCharArray())[0];
                s.Replace(file.Name, "");
                string directirory = s.ToString();
                Directory.CreateDirectory(Path.Combine(outputDir, firstFolder, "Patches", directirory));
                result = XmlWriter.Create(Path.Combine(outputDir, firstFolder, "Patches", filepath), writterSetting);
                result.WriteStartElement("Patch");
                UnPackPatch(xmlDoc);
                i++;

                result.WriteEndElement();
                result.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception reading " + file.Name + ": " + ex);
            }
        }

        static bool IsPatch(XmlDocument xmlDoc)
        {
            return xmlDoc.DocumentElement.Name == "Patch";
        }

        static bool UnPackPatch(XmlDocument xmlDoc)
        {
            XmlElement documentElement = xmlDoc.DocumentElement;
            if (documentElement.Name != "Patch")
            {
                return false;
            }
            foreach (XmlNode childNode in documentElement.ChildNodes)
            {
                if (childNode.NodeType == XmlNodeType.Element)
                {
                    if (childNode.Name != "Operation")
                    {
                        continue;
                    }
                    FindPatchOps(childNode);
                }
            }

            return true;
        }

        static void FindPatchOps(XmlNode childNode)
        {
            foreach (XmlAttribute attr in childNode.Attributes)
            {
                if (attr.InnerText == "PatchOperationSequence")
                {
                    UnPackSequence(childNode);
                    continue;
                }
                else if (attr.InnerText == "PatchOperationFindMod")
                {
                    UnPackFindMod(childNode);
                    continue;
                }
                else
                {
                    childNode.WriteTo(result);
                }
            }
        }

        static void UnPackFindMod(XmlNode sequence)
        {
            foreach (XmlNode childNode in sequence.ChildNodes)
            {
                if (childNode.NodeType == XmlNodeType.Element)
                {
                    if (childNode.Name == "match")
                    {
                        FindPatchOps(childNode);
                    }
                }
            }
        }

        static void UnPackSequence(XmlNode sequence)
        {
            foreach (XmlNode childNode in sequence.ChildNodes)
            {
                if (childNode.NodeType == XmlNodeType.Element)
                {
                    if (childNode.Name == "operations")
                    {
                        Console.WriteLine("unpacking sequence");
                        UnPackSequenceInner(childNode);
                    }
                }
            }
        }

        static void UnPackSequenceInner(XmlNode sequence)
        {
            foreach (XmlNode childNode in sequence.ChildNodes)
            {
                XmlNode newNode = childNode.OwnerDocument.CreateElement("Operation");
                foreach (XmlAttribute att in childNode.Attributes) newNode.Attributes.Append(att);
                newNode.InnerXml = childNode.InnerXml;

                newNode.WriteTo(result);
                j++;
            }
        }
    }
}
