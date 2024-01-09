using System;
using System.Xml;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using static System.Net.Mime.MediaTypeNames;
using System.Text;
using System.Linq;

namespace PatchSequenceUnpack
{
    internal class Program
    {
        static XmlWriter result;

        static TextWriter exportLog;

        static string dir = System.IO.Directory.GetCurrentDirectory();

        static string outputDir;

        static int i;

        static int j;

        static bool makeLoadFolders;

        static List<string> firstFolders = new List<string>();

        static void Main(string[] args)
        {
            Console.WriteLine("Input the patch folder's path");
            dir = Console.ReadLine();

            Console.WriteLine("Input the output folder's path");
            outputDir = Path.Combine(Console.ReadLine(), "Output");

            Console.WriteLine("Create pesudo loadfolder file? Y/N");
            makeLoadFolders = Console.ReadLine().ToLower()[0] == 'y';
            if (makeLoadFolders)
            {
                Console.WriteLine("A pesudo loadfolder file will be created");
            }

            DirectoryInfo directoryInfo = new DirectoryInfo(dir);
            Console.WriteLine("Output " + outputDir);
            Directory.CreateDirectory(outputDir);

            exportLog = File.AppendText(Path.Combine(outputDir, "log.txt"));

            foreach (FileInfo file in directoryInfo.GetFiles("*.xml", SearchOption.AllDirectories))
            {
                Read(file);
            }

            if (makeLoadFolders)
            {
                GenLoadFolders();
            }


            Log($"Converted patches stored at {outputDir}");
            Log($"De-Sequencing of {i} files and {j} patches Complete, Press any key to exit.");
            exportLog.Close();
            Console.ReadKey();
        }

        public static void Log(string logMessage)
        {
            Console.WriteLine(logMessage);
            exportLog.WriteLine(logMessage);
        }

        static void Read(FileInfo file)
        {
            Log("Reading " + file.FullName);
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
                    Log(file.FullName + " is not a patch");
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

                if (!firstFolders.Contains(firstFolder))
                {
                    firstFolders.Add(firstFolder);
                }

                s.Replace(file.Name, "");
                string directirory = s.ToString();
                Directory.CreateDirectory(Path.Combine(outputDir, firstFolder, "Patches", directirory));
                result = XmlWriter.Create(Path.Combine(outputDir, firstFolder, "Patches", filepath), writterSetting);
                Log("Writing to " + Path.Combine(outputDir, firstFolder, "Patches", filepath));
                result.WriteStartElement("Patch");
                UnPackPatch(xmlDoc);
                i++;

                result.WriteEndElement();
                result.Close();
            }
            catch (Exception ex)
            {
                Log("Exception reading " + file.Name + ": " + ex);
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

                XmlAttribute[] attributes = new XmlAttribute[childNode.Attributes.Count];
                childNode.Attributes.CopyTo(attributes, 0);

                foreach (XmlAttribute attribute in attributes)
                {
                    newNode.Attributes.Append(attribute);
                }

                newNode.InnerXml = childNode.InnerXml;

                newNode.WriteTo(result);
                j++;
            }
        }

        static void GenLoadFolders()
        {
            XmlWriterSettings writterSetting = new XmlWriterSettings();
            writterSetting.Indent = true;
            writterSetting.IndentChars = "\t";
            Directory.CreateDirectory(outputDir);
            result = XmlWriter.Create(Path.Combine(outputDir, "LoadFolders.xml"), writterSetting);
            result.WriteStartElement("loadFolders");

            result.WriteComment("This file is a pesudo-LoadFolders, auto created by the unpacker.\n You will need to replace ifModActives to corresponding packageID, and replace REPLACEMENT-CTRL-Hs to some other path like 1.4/mods/ to work.");

            result.WriteStartElement("v1.4");

            result.WriteStartElement("li");
            result.WriteString("/");
            result.WriteEndElement();

            foreach (string s in firstFolders)
            {
                result.WriteStartElement("li");
                result.WriteAttributeString("IfModActive", s);
                result.WriteString("REPLACEMENT-CTRL-H/" + s);
                result.WriteEndElement();
            }

            result.WriteEndElement();
            result.Close();
        }
    }
}
